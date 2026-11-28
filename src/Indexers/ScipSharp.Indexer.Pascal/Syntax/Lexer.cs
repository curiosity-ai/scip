namespace ScipSharp.Indexer.Pascal.Syntax;

/// <summary>
/// Raw Pascal lexer: identifiers (case-insensitive, <c>&amp;</c>-escaped), numbers (<c>$hex</c>, <c>&amp;octal</c>,
/// <c>%binary</c>, reals), strings with embedded <c>#nn</c> char codes, symbols (incl. <c>(. .)</c> digraphs) and
/// compiler directives (<c>{$...}</c>, <c>(*$...*)</c>). Comments (<c>{ }</c>, <c>(* *)</c>, <c>//</c>) are recorded
/// on the <see cref="SourceText"/>. Keyword classification is left to the <see cref="Preprocessor"/> (it is mode dependent).
/// </summary>
internal sealed class Lexer(SourceText source)
{
    private readonly string _text = source.Text;
    private int _pos;

    public SourceText Source { get; } = source;

    /// <summary>FPC and OBJFPC modes allow nested <c>{ }</c> comments.</summary>
    public bool NestedComments { get; set; }

    public Token Next()
    {
        while (true)
        {
            SkipWhitespace();
            if (_pos >= _text.Length)
            {
                return new Token(TokenKind.Eof, "", Source, _text.Length, _text.Length);
            }

            var c = _text[_pos];
            var start = _pos;
            if (c == '{')
            {
                if (Peek(1) == '$')
                {
                    var end = _text.IndexOf('}', _pos + 2);
                    end = end < 0 ? _text.Length : end;
                    _pos = Math.Min(_text.Length, end + 1);
                    return new Token(TokenKind.Directive, _text[(start + 2)..end].Trim(), Source, start, _pos);
                }

                SkipBraceComment();
                AddComment(start);
                continue;
            }

            if (c == '(' && Peek(1) == '*')
            {
                var end = _text.IndexOf("*)", _pos + 2, StringComparison.Ordinal);
                var close = end < 0 ? _text.Length : end;
                _pos = end < 0 ? _text.Length : end + 2;
                if (start + 2 < _text.Length && _text[start + 2] == '$')
                {
                    return new Token(TokenKind.Directive, _text[(start + 3)..Math.Max(start + 3, close)].Trim(), Source, start, _pos);
                }

                AddComment(start);
                continue;
            }

            if (c == '/' && Peek(1) == '/')
            {
                while (_pos < _text.Length && _text[_pos] != '\n' && _text[_pos] != '\r')
                {
                    _pos++;
                }

                AddComment(start);
                continue;
            }

            if (c == '#' && IsIdentStart(Peek(1)) && IsAtLineStart(start))
            {
                // C preprocessor line (#if/#define/#include), used by e.g. Pascal-P5 sources: mapped onto the equivalent
                // compiler directive, anything else is skipped like a comment.
                while (_pos < _text.Length && _text[_pos] != '\n')
                {
                    _pos++;
                }

                if (CppDirective(_text[(start + 1).._pos].TrimEnd('\r')) is { } directive)
                {
                    return new Token(TokenKind.Directive, directive, Source, start, _pos);
                }

                continue;
            }

            if (IsIdentStart(c))
            {
                _pos++;
                while (_pos < _text.Length && IsIdentPart(_text[_pos]))
                {
                    _pos++;
                }

                return new Token(TokenKind.Identifier, _text[start.._pos], Source, start, _pos);
            }

            if (c == '&')
            {
                var n = Peek(1);
                if (IsIdentStart(n))
                {
                    _pos++;
                    while (_pos < _text.Length && IsIdentPart(_text[_pos]))
                    {
                        _pos++;
                    }

                    return new Token(TokenKind.Identifier, _text[(start + 1).._pos], Source, start, _pos) { Escaped = true };
                }

                if (n is >= '0' and <= '7')
                {
                    _pos++;
                    while (_pos < _text.Length && _text[_pos] is >= '0' and <= '7' or '_')
                    {
                        _pos++;
                    }

                    return new Token(TokenKind.Number, _text[start.._pos], Source, start, _pos);
                }
            }

            if (char.IsAsciiDigit(c))
            {
                return LexNumber(start);
            }

            if (c == '$' && char.IsAsciiHexDigit(Peek(1)))
            {
                _pos++;
                while (_pos < _text.Length && (char.IsAsciiHexDigit(_text[_pos]) || _text[_pos] == '_'))
                {
                    _pos++;
                }

                return new Token(TokenKind.Number, _text[start.._pos], Source, start, _pos);
            }

            if (c == '%' && Peek(1) is '0' or '1')
            {
                _pos++;
                while (_pos < _text.Length && _text[_pos] is '0' or '1' or '_')
                {
                    _pos++;
                }

                return new Token(TokenKind.Number, _text[start.._pos], Source, start, _pos);
            }

            if (c == '\'' || (c == '#' && (char.IsAsciiDigit(Peek(1)) || Peek(1) == '$')))
            {
                return LexString(start);
            }

            return LexSymbol(start);
        }
    }

    private void AddComment(int start)
    {
        // A file included twice is lexed twice; record each comment once.
        if (Source.Comments.Count == 0 || Source.Comments[^1].Start < start)
        {
            Source.Comments.Add(new Comment(start, _pos, _text[start.._pos]));
        }
    }

    /// <summary>Translates a C preprocessor line into Pascal directive text (or null to ignore it).</summary>
    internal static string? CppDirective(string line)
    {
        var trimmed = line.Trim();
        var space = trimmed.IndexOfAny([' ', '\t']);
        var word = space < 0 ? trimmed : trimmed[..space];
        var rest = space < 0 ? "" : trimmed[(space + 1)..].Trim();
        var comment = rest.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0)
        {
            rest = rest[..comment].Trim();
        }

        return word switch
        {
            "include" => "I " + rest.Trim('"', '<', '>'),
            "ifdef" => "IFDEF " + rest,
            "ifndef" => "IFNDEF " + rest,
            "if" => "IF " + CppExpression(rest),
            "elif" => "ELSEIF " + CppExpression(rest),
            "else" => "ELSE",
            "endif" => "ENDIF",
            "define" => "DEFINE " + (rest.IndexOfAny([' ', '\t']) is var sp and > 0 ? rest[..sp] + " := " + rest[(sp + 1)..].Trim() : rest),
            "undef" => "UNDEF " + rest,
            _ => null,
        };
    }

    private static string CppExpression(string e) => e
        .Replace("&&", " and ", StringComparison.Ordinal)
        .Replace("||", " or ", StringComparison.Ordinal)
        .Replace("!=", "<>", StringComparison.Ordinal)
        .Replace("==", "=", StringComparison.Ordinal)
        .Replace("!", " not ", StringComparison.Ordinal);

    private bool IsAtLineStart(int offset)
    {
        for (var i = offset - 1; i >= 0; i--)
        {
            var ch = _text[i];
            if (ch == '\n')
            {
                return true;
            }

            if (ch is not (' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    private char Peek(int n) => _pos + n < _text.Length ? _text[_pos + n] : '\0';

    private static bool IsIdentStart(char c) => c == '_' || char.IsAsciiLetter(c) || (c >= 0x80 && char.IsLetter(c));

    private static bool IsIdentPart(char c) => c == '_' || char.IsAsciiLetterOrDigit(c) || (c >= 0x80 && char.IsLetterOrDigit(c));

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && (_text[_pos] <= ' ' || _text[_pos] == '﻿' || _text[_pos] == ' '))
        {
            _pos++;
        }
    }

    private void SkipBraceComment()
    {
        var start = _pos;
        if (!SkipBraceComment(NestedComments && !NestingFails(start)))
        {
            // Unbalanced nesting would swallow the rest of the file: treat this comment as non-nesting.
            _pos = start;
            _braceDepth ??= BraceDepths(out _braceSuffixMin);
            SkipBraceComment(false);
        }
    }

    /// <summary>Brace depth before each offset (count of '{' minus '}'), and its suffix minimum.</summary>
    private int[]? _braceDepth;

    private int[]? _braceSuffixMin;

    /// <summary>
    /// After one nested scan failed (ran to EOF), predicts in O(1) whether a nested scan from <paramref name="start"/>
    /// would fail too: it does iff the brace depth never drops back to its value at <paramref name="start"/>. Without
    /// this, every later comment containing a stray '{' re-scans to the end of the file (quadratic).
    /// </summary>
    private bool NestingFails(int start) => _braceDepth is not null && _braceSuffixMin![start + 1] > _braceDepth[start];

    private int[] BraceDepths(out int[] suffixMin)
    {
        var depth = new int[_text.Length + 1];
        for (var i = 0; i < _text.Length; i++)
        {
            depth[i + 1] = depth[i] + (_text[i] == '{' ? 1 : _text[i] == '}' ? -1 : 0);
        }

        suffixMin = new int[_text.Length + 1];
        suffixMin[_text.Length] = depth[_text.Length];
        for (var i = _text.Length - 1; i >= 0; i--)
        {
            suffixMin[i] = Math.Min(depth[i], suffixMin[i + 1]);
        }

        return depth;
    }

    private bool SkipBraceComment(bool nested)
    {
        var depth = 0;
        while (_pos < _text.Length)
        {
            var ch = _text[_pos++];
            if (ch == '{')
            {
                if (depth == 0 || nested)
                {
                    depth++;
                }
            }
            else if (ch == '}')
            {
                depth--;
                if (depth <= 0)
                {
                    return true;
                }
            }
        }

        return depth <= 0;
    }

    private Token LexNumber(int start)
    {
        while (_pos < _text.Length && (char.IsAsciiDigit(_text[_pos]) || _text[_pos] == '_'))
        {
            _pos++;
        }

        if (Peek(0) == '.' && char.IsAsciiDigit(Peek(1)))
        {
            _pos++;
            while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
            {
                _pos++;
            }
        }

        if (Peek(0) is 'e' or 'E' && (char.IsAsciiDigit(Peek(1)) || (Peek(1) is '+' or '-' && char.IsAsciiDigit(Peek(2)))))
        {
            _pos += 2;
            while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
            {
                _pos++;
            }
        }

        return new Token(TokenKind.Number, _text[start.._pos], Source, start, _pos);
    }

    private Token LexString(int start)
    {
        while (_pos < _text.Length)
        {
            var c = _text[_pos];
            if (c == '\'')
            {
                _pos++;
                while (_pos < _text.Length)
                {
                    if (_text[_pos] == '\'')
                    {
                        if (Peek(1) == '\'')
                        {
                            _pos += 2;
                            continue;
                        }

                        _pos++;
                        break;
                    }

                    if (_text[_pos] is '\n' or '\r')
                    {
                        break; // unterminated: stop at end of line
                    }

                    _pos++;
                }
            }
            else if (c == '#' && (char.IsAsciiDigit(Peek(1)) || (Peek(1) == '$' && char.IsAsciiHexDigit(Peek(2)))))
            {
                _pos++;
                if (_text[_pos] == '$')
                {
                    _pos++;
                    while (_pos < _text.Length && char.IsAsciiHexDigit(_text[_pos]))
                    {
                        _pos++;
                    }
                }
                else
                {
                    while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
                    {
                        _pos++;
                    }
                }
            }
            else
            {
                break;
            }
        }

        return new Token(TokenKind.String, _text[start.._pos], Source, start, _pos);
    }

    private static readonly string[] TwoCharSymbols = [":=", "<=", ">=", "<>", "..", "**", "><", "+=", "-=", "*=", "/=", "@@"];

    private Token LexSymbol(int start)
    {
        if (_pos + 1 < _text.Length)
        {
            var two = _text.AsSpan(_pos, 2);
            if (two is "(." && Peek(2) != '.')
            {
                _pos += 2;
                return new Token(TokenKind.Symbol, "[", Source, start, _pos);
            }

            if (two is ".)")
            {
                _pos += 2;
                return new Token(TokenKind.Symbol, "]", Source, start, _pos);
            }

            foreach (var s in TwoCharSymbols)
            {
                if (two.SequenceEqual(s))
                {
                    if (s == "@@" && !IsIdentStart(Peek(2)))
                    {
                        break;
                    }

                    _pos += 2;
                    return new Token(TokenKind.Symbol, s, Source, start, _pos);
                }
            }
        }

        _pos++;
        return new Token(TokenKind.Symbol, _text[start.._pos], Source, start, _pos);
    }
}
