using System.Globalization;

namespace ScipSharp.Indexer.Python.Syntax;

public enum TokenKind { Name, Number, String, Op, Newline, Indent, Dedent, End }

/// <summary>A lexical token. <see cref="Start"/>/<see cref="End"/> are UTF-16 offsets into the source text.</summary>
public readonly record struct Token(TokenKind Kind, int Start, int End, string Text)
{
    public bool IsOp(string text) => Kind == TokenKind.Op && Text == text;

    public bool IsKw(string text) => Kind == TokenKind.Name && Text == text;
}

/// <summary>
/// A Python 3 tokenizer (indentation aware). f-string replacement fields are not tokenized here; the parser re-lexes them
/// with <see cref="Tokenizer(string, int, int)"/> so that offsets stay absolute.
/// </summary>
public sealed class Tokenizer
{
    private static readonly string[] Ops3 = ["**=", "//=", ">>=", "<<=", "..."];
    private static readonly string[] Ops2 =
        ["**", "//", ">>", "<<", "<=", ">=", "==", "!=", "->", ":=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "@=", "<>"];

    private readonly string _s;
    private readonly int _end;
    private readonly List<Token> _tokens = [];
    private readonly Stack<int> _indents = new([0]);
    private readonly bool _implicitParens;
    private int _pos;
    private int _depth;

    public Tokenizer(string text) : this(text, 0, text.Length, false) { }

    /// <summary>Tokenizes <c>text[start..end]</c> as an expression (newlines are insignificant).</summary>
    public Tokenizer(string text, int start, int end) : this(text, start, end, true) { }

    private Tokenizer(string text, int start, int end, bool implicitParens)
    {
        _s = text;
        _pos = start;
        _end = end;
        _implicitParens = implicitParens;
        _depth = implicitParens ? 1 : 0;
    }

    public List<Token> Tokenize()
    {
        var atLineStart = !_implicitParens;
        var lineHasTokens = false;
        while (_pos < _end)
        {
            if (atLineStart && _depth == 0)
            {
                atLineStart = false;
                var col = 0;
                var p = _pos;
                while (p < _end && _s[p] is ' ' or '\t' or '\f')
                {
                    col = _s[p] == '\t' ? (col / 8 + 1) * 8 : _s[p] == '\f' ? 0 : col + 1;
                    p++;
                }

                if (p >= _end)
                {
                    _pos = p;
                    break;
                }

                if (_s[p] is '\n' or '\r' or '#')
                {
                    // blank or comment-only line: produces no tokens
                    _pos = p;
                    SkipComment();
                    SkipNewline();
                    atLineStart = true;
                    continue;
                }

                _pos = p;
                if (col > _indents.Peek())
                {
                    _indents.Push(col);
                    _tokens.Add(new Token(TokenKind.Indent, p, p, ""));
                }
                else
                {
                    while (col < _indents.Peek())
                    {
                        _indents.Pop();
                        _tokens.Add(new Token(TokenKind.Dedent, p, p, ""));
                    }
                }
            }

            var c = _s[_pos];
            if (c is ' ' or '\t' or '\f')
            {
                _pos++;
                continue;
            }

            if (c == '#')
            {
                SkipComment();
                continue;
            }

            if (c == '\\' && _pos + 1 < _end && _s[_pos + 1] is '\n' or '\r')
            {
                _pos++;
                SkipNewline();
                continue;
            }

            if (c is '\n' or '\r')
            {
                if (_depth == 0)
                {
                    if (lineHasTokens)
                    {
                        _tokens.Add(new Token(TokenKind.Newline, _pos, _pos + 1, "\n"));
                    }

                    lineHasTokens = false;
                    atLineStart = true;
                }

                SkipNewline();
                continue;
            }

            lineHasTokens = true;
            LexToken();
        }

        if (!_implicitParens)
        {
            if (lineHasTokens)
            {
                _tokens.Add(new Token(TokenKind.Newline, _end, _end, ""));
            }

            while (_indents.Count > 1)
            {
                _indents.Pop();
                _tokens.Add(new Token(TokenKind.Dedent, _end, _end, ""));
            }
        }

        _tokens.Add(new Token(TokenKind.End, _end, _end, ""));
        return _tokens;
    }

    private void SkipComment()
    {
        if (_pos < _end && _s[_pos] == '#')
        {
            while (_pos < _end && _s[_pos] is not ('\n' or '\r'))
            {
                _pos++;
            }
        }
    }

    private void SkipNewline()
    {
        if (_pos < _end && _s[_pos] == '\r')
        {
            _pos++;
        }

        if (_pos < _end && _s[_pos] == '\n')
        {
            _pos++;
        }
    }

    private static bool IsIdStart(char c) =>
        c == '_' || char.IsLetter(c) || c > 127 && !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && !char.IsSymbol(c) && !char.IsControl(c);

    private static bool IsIdPart(char c) =>
        c == '_' || char.IsLetterOrDigit(c)
        || c > 127 && CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;

    private void LexToken()
    {
        var start = _pos;
        var c = _s[_pos];
        if (IsIdStart(c))
        {
            while (_pos < _end && IsIdPart(_s[_pos]))
            {
                _pos++;
            }

            if (_pos < _end && _s[_pos] is '"' or '\'' && _pos - start <= 2 && IsStringPrefix(_s.AsSpan(start, _pos - start)))
            {
                LexString(start, _pos - start);
                return;
            }

            _tokens.Add(new Token(TokenKind.Name, start, _pos, _s[start.._pos]));
            return;
        }

        if (c is '"' or '\'')
        {
            LexString(start, 0);
            return;
        }

        if (char.IsAsciiDigit(c) || c == '.' && _pos + 1 < _end && char.IsAsciiDigit(_s[_pos + 1]))
        {
            _pos++;
            while (_pos < _end)
            {
                var d = _s[_pos];
                if (char.IsAsciiLetterOrDigit(d) || d is '_' or '.')
                {
                    _pos++;
                }
                else if (d is '+' or '-' && _s[_pos - 1] is 'e' or 'E'
                    && !_s.AsSpan(start, _pos - start).StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    _pos++;
                }
                else
                {
                    break;
                }
            }

            _tokens.Add(new Token(TokenKind.Number, start, _pos, _s[start.._pos]));
            return;
        }

        foreach (var op in Ops3)
        {
            if (_pos + 3 <= _end && string.CompareOrdinal(_s, _pos, op, 0, 3) == 0)
            {
                _pos += 3;
                _tokens.Add(new Token(TokenKind.Op, start, _pos, op));
                return;
            }
        }

        foreach (var op in Ops2)
        {
            if (_pos + 2 <= _end && string.CompareOrdinal(_s, _pos, op, 0, 2) == 0)
            {
                _pos += 2;
                _tokens.Add(new Token(TokenKind.Op, start, _pos, op));
                return;
            }
        }

        _pos++;
        if (c is '(' or '[' or '{')
        {
            _depth++;
        }
        else if (c is ')' or ']' or '}' && _depth > (_implicitParens ? 1 : 0))
        {
            _depth--;
        }

        _tokens.Add(new Token(TokenKind.Op, start, _pos, c.ToString()));
    }

    private static bool IsStringPrefix(ReadOnlySpan<char> p)
    {
        if (p.Length == 0)
        {
            return true;
        }

        Span<char> lower = stackalloc char[p.Length];
        p.ToLowerInvariant(lower);
        return lower is "r" or "u" or "b" or "f" or "br" or "rb" or "fr" or "rf" or "t" or "tr" or "rt";
    }

    private void LexString(int tokenStart, int prefixLen)
    {
        var isF = false;
        for (var i = 0; i < prefixLen; i++)
        {
            if (_s[tokenStart + i] is 'f' or 'F' or 't' or 'T')
            {
                isF = true;
            }
        }

        var q = _s[_pos];
        var triple = _pos + 2 < _end && _s[_pos + 1] == q && _s[_pos + 2] == q;
        _pos += triple ? 3 : 1;
        ScanStringBody(q, triple, isF);
        _tokens.Add(new Token(TokenKind.String, tokenStart, _pos, _s[tokenStart.._pos]));
    }

    /// <summary>Advances past the closing quote. For f-strings, quotes inside <c>{...}</c> do not terminate the string.</summary>
    private void ScanStringBody(char q, bool triple, bool isF)
    {
        var braces = 0;
        while (_pos < _end)
        {
            var c = _s[_pos];
            if (c == '\\')
            {
                _pos += 2;
                continue;
            }

            if (isF)
            {
                if (c == '{')
                {
                    if (braces == 0 && _pos + 1 < _end && _s[_pos + 1] == '{')
                    {
                        _pos += 2;
                        continue;
                    }

                    braces++;
                }
                else if (c == '}' && braces > 0)
                {
                    braces--;
                }
                else if (braces > 0 && c is '"' or '\'')
                {
                    // a nested string literal inside a replacement field
                    var nested = _pos + 2 < _end && _s[_pos + 1] == c && _s[_pos + 2] == c;
                    _pos += nested ? 3 : 1;
                    ScanStringBody(c, nested, false);
                    continue;
                }
            }

            if (c == q && braces == 0)
            {
                if (!triple)
                {
                    _pos++;
                    return;
                }

                if (_pos + 2 < _end && _s[_pos + 1] == q && _s[_pos + 2] == q)
                {
                    _pos += 3;
                    return;
                }
            }
            else if (c is '\n' or '\r' && !triple && braces == 0)
            {
                return; // unterminated single-line string
            }

            _pos++;
        }

        _pos = Math.Min(_pos, _end);
    }
}
