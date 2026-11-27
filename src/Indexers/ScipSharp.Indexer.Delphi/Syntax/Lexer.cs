using System.Text;

namespace ScipSharp.Indexer.Delphi.Syntax;

/// <summary>
/// Tokenises Delphi source text: <c>{ }</c>, <c>(* *)</c> and <c>//</c> comments, compiler directives (returned as
/// <see cref="TokenKind.Directive"/> tokens for the <see cref="Preprocessor"/>), <c>&amp;</c>-escaped identifiers,
/// <c>#13</c>/<c>#$0D</c> character codes, Delphi 12 multi-line strings (<c>'''</c>), <c>$hex</c>, <c>%binary</c> and
/// digit separators. <c>///</c> XMLDoc comment lines are attached to the next token as <see cref="Token.Doc"/>.
/// </summary>
internal sealed class Lexer(string text, int fileId)
{
    private int _pos;
    private int _line;
    private int _lineStart;
    private readonly List<string> _docLines = [];

    public static List<Token> Tokenize(string text, int fileId) => new Lexer(text, fileId).Run();

    public List<Token> Run()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var t = Next();
            tokens.Add(t);
            if (t.IsEof)
            {
                return tokens;
            }
        }
    }

    private char Cur => _pos < text.Length ? text[_pos] : '\0';

    private char Peek(int n = 1) => _pos + n < text.Length ? text[_pos + n] : '\0';

    private void Advance()
    {
        if (_pos < text.Length && text[_pos] == '\n')
        {
            _line++;
            _lineStart = _pos + 1;
        }

        _pos++;
    }

    private Token Make(TokenKind kind, string value, int start, int startLine, int startCol, TokenFlags flags = TokenFlags.None, bool attachDoc = true)
    {
        string? doc = null;
        if (attachDoc && _docLines.Count > 0 && kind != TokenKind.Directive)
        {
            doc = string.Join("\n", _docLines);
            _docLines.Clear();
        }

        return new Token(kind, value, fileId, startLine, startCol, _line, _pos - _lineStart, start, _pos, flags, doc);
    }

    private Token Next()
    {
        while (true)
        {
            SkipWhitespace();
            if (_pos >= text.Length)
            {
                return Make(TokenKind.EndOfFile, "", _pos, _line, _pos - _lineStart, attachDoc: false);
            }

            var start = _pos;
            var line = _line;
            var col = _pos - _lineStart;
            var c = Cur;

            if (c == '/' && Peek() == '/')
            {
                var isDoc = Peek(2) == '/' && Peek(3) != '/';
                while (_pos < text.Length && Cur != '\n')
                {
                    _pos++;
                }

                var body = text.AsSpan(start, _pos - start).TrimEnd('\r');
                if (isDoc)
                {
                    _docLines.Add(body[3..].ToString());
                }
                else
                {
                    _docLines.Clear();
                }

                continue;
            }

            if (c == '{')
            {
                var isDirective = Peek() == '$';
                while (_pos < text.Length && Cur != '}')
                {
                    Advance();
                }

                var inner = text.Substring(start + 1, Math.Max(0, _pos - start - 1));
                if (_pos < text.Length)
                {
                    Advance();
                }

                if (isDirective)
                {
                    return Make(TokenKind.Directive, inner[1..], start, line, col);
                }

                _docLines.Clear();
                continue;
            }

            if (c == '(' && Peek() == '*')
            {
                var isDirective = Peek(2) == '$';
                Advance();
                Advance();
                while (_pos < text.Length && !(Cur == '*' && Peek() == ')'))
                {
                    Advance();
                }

                var inner = text.Substring(start + 2, Math.Max(0, Math.Min(_pos, text.Length) - start - 2));
                if (_pos < text.Length)
                {
                    Advance();
                    Advance();
                }

                if (isDirective)
                {
                    return Make(TokenKind.Directive, inner[1..], start, line, col);
                }

                _docLines.Clear();
                continue;
            }

            if (IsIdentStart(c) || (c == '&' && IsIdentStart(Peek())))
            {
                var escaped = c == '&';
                if (escaped)
                {
                    _pos++;
                }

                var nameStart = _pos;
                while (_pos < text.Length && IsIdentPart(Cur))
                {
                    _pos++;
                }

                var name = text[nameStart.._pos];
                var kind = !escaped && Keywords.Reserved.Contains(name) ? TokenKind.Keyword : TokenKind.Identifier;
                return Make(kind, name, start, line, col, escaped ? TokenFlags.Escaped : TokenFlags.None);
            }

            if (char.IsAsciiDigit(c) || (c == '$' && char.IsAsciiHexDigit(Peek())) || (c == '%' && Peek() is '0' or '1') ||
                (c == '&' && Peek() is >= '0' and <= '7'))
            {
                return LexNumber(start, line, col);
            }

            if (c == '\'' || c == '#')
            {
                return LexString(start, line, col);
            }

            return LexSymbol(start, line, col);
        }
    }

    private void SkipWhitespace()
    {
        while (_pos < text.Length && char.IsWhiteSpace(Cur))
        {
            Advance();
        }
    }

    internal static bool IsIdentStart(char c) => c == '_' || char.IsLetter(c);

    internal static bool IsIdentPart(char c) => c == '_' || char.IsLetterOrDigit(c);

    private Token LexNumber(int start, int line, int col)
    {
        var c = Cur;
        if (c is '$' or '%' or '&')
        {
            _pos++;
            while (_pos < text.Length && (char.IsAsciiHexDigit(Cur) || Cur == '_'))
            {
                _pos++;
            }

            return Make(TokenKind.Number, text[start.._pos], start, line, col);
        }

        while (_pos < text.Length && (char.IsAsciiDigit(Cur) || Cur == '_'))
        {
            _pos++;
        }

        // A fraction, but not the '..' of a subrange.
        if (Cur == '.' && char.IsAsciiDigit(Peek()))
        {
            _pos++;
            while (_pos < text.Length && (char.IsAsciiDigit(Cur) || Cur == '_'))
            {
                _pos++;
            }
        }

        if (Cur is 'e' or 'E' && (char.IsAsciiDigit(Peek()) || (Peek() is '+' or '-' && char.IsAsciiDigit(Peek(2)))))
        {
            _pos += 2;
            while (_pos < text.Length && char.IsAsciiDigit(Cur))
            {
                _pos++;
            }
        }

        return Make(TokenKind.Number, text[start.._pos], start, line, col);
    }

    private Token LexString(int start, int line, int col)
    {
        var sb = new StringBuilder();
        while (_pos < text.Length)
        {
            if (Cur == '#')
            {
                _pos++;
                var numStart = _pos;
                if (Cur == '$')
                {
                    _pos++;
                    while (_pos < text.Length && char.IsAsciiHexDigit(Cur))
                    {
                        _pos++;
                    }
                }
                else
                {
                    while (_pos < text.Length && char.IsAsciiDigit(Cur))
                    {
                        _pos++;
                    }
                }

                var digits = text[numStart.._pos];
                if (digits.Length > 0)
                {
                    var value = digits[0] == '$'
                        ? int.TryParse(digits.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var h) ? h : 0
                        : int.TryParse(digits, out var d) ? d : 0;
                    if (value is >= 0 and <= 0xFFFF)
                    {
                        sb.Append((char)value);
                    }
                }

                continue;
            }

            if (Cur == '\'')
            {
                if (TryLexMultiline(sb))
                {
                    continue;
                }

                _pos++;
                while (_pos < text.Length)
                {
                    if (Cur == '\'')
                    {
                        if (Peek() == '\'')
                        {
                            sb.Append('\'');
                            _pos += 2;
                            continue;
                        }

                        _pos++;
                        break;
                    }

                    if (Cur == '\n')
                    {
                        break; // unterminated string: stop at the end of the line
                    }

                    sb.Append(Cur);
                    _pos++;
                }

                continue;
            }

            break;
        }

        return Make(TokenKind.String, sb.ToString(), start, line, col);
    }

    /// <summary>Delphi 12 multi-line strings: an odd number (≥3) of quotes followed by a line break, closed by the same quotes.</summary>
    private bool TryLexMultiline(StringBuilder sb)
    {
        var n = 0;
        while (_pos + n < text.Length && text[_pos + n] == '\'')
        {
            n++;
        }

        if (n < 3 || n % 2 == 0)
        {
            return false;
        }

        var after = _pos + n;
        while (after < text.Length && text[after] is ' ' or '\t' or '\r')
        {
            after++;
        }

        if (after >= text.Length || text[after] != '\n')
        {
            return false;
        }

        var quotes = new string('\'', n);
        _pos = after;
        Advance(); // newline
        var contentStart = _pos;
        while (_pos < text.Length)
        {
            var lineStart = _pos;
            while (_pos < text.Length && text[_pos] is ' ' or '\t')
            {
                _pos++;
            }

            if (string.CompareOrdinal(text, _pos, quotes, 0, n) == 0 && (_pos + n >= text.Length || text[_pos + n] != '\''))
            {
                sb.Append(text.AsSpan(contentStart, Math.Max(0, lineStart - contentStart)).TrimEnd("\r\n"));
                _pos += n;
                return true;
            }

            while (_pos < text.Length && text[_pos] != '\n')
            {
                _pos++;
            }

            if (_pos < text.Length)
            {
                Advance();
            }
        }

        sb.Append(text.AsSpan(contentStart));
        return true;
    }

    private static readonly string[] TwoCharSymbols = [":=", "..", "<=", ">=", "<>", "(.", ".)", "@@", "+=", "-=", "*=", "/="];

    private Token LexSymbol(int start, int line, int col)
    {
        if (_pos + 1 < text.Length)
        {
            var two = text.AsSpan(_pos, 2);
            foreach (var s in TwoCharSymbols)
            {
                if (two.SequenceEqual(s))
                {
                    // `(.` / `.)` are the digraphs for `[` / `]`; `+=` etc. are FPC-only and lexed as two tokens here.
                    if (s is "+=" or "-=" or "*=" or "/=")
                    {
                        break;
                    }

                    _pos += 2;
                    var value = s switch { "(." => "[", ".)" => "]", _ => s };
                    return Make(TokenKind.Symbol, value, start, line, col);
                }
            }
        }

        _pos++;
        return Make(TokenKind.Symbol, text[start.._pos], start, line, col);
    }
}
