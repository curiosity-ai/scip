namespace Scip.Indexer.Ruby;

internal enum Tk { Ident, Const, IVar, CVar, GVar, Keyword, Symbol, Label, Str, Num, Punct, Newline, Eof }

/// <summary>A lexical token. <c>Col</c>/<c>EndCol</c> are UTF-16 offsets from the start of <c>Line</c> (0-based).</summary>
internal readonly record struct Token(Tk Kind, string Text, int Line, int Col, int EndCol, bool SpaceBefore);

/// <summary>
/// A pragmatic Ruby tokenizer: it understands comments, heredocs, string/regex/percent literals (including
/// <c>#{}</c> interpolation, whose tokens are emitted inline) and enough context to tell values from operators.
/// It is not a full Ruby lexer; see README.md for the known gaps.
/// </summary>
internal sealed class RubyLexer
{
    private static readonly HashSet<string> Keywords =
    [
        "alias", "and", "begin", "break", "case", "class", "def", "defined?", "do", "else", "elsif", "end", "ensure",
        "false", "for", "if", "in", "module", "next", "nil", "not", "or", "redo", "rescue", "retry", "return", "self",
        "super", "then", "true", "undef", "unless", "until", "when", "while", "yield", "__FILE__", "__LINE__", "__method__",
    ];

    private static readonly string[] Puncts =
    [
        "**=", "<=>", "===", "...", "<<=", ">>=", "&&=", "||=", "&.", "::", "**", "==", "!=", ">=", "<=", "&&", "||", "<<", ">>",
        "+=", "-=", "*=", "/=", "%=", "|=", "&=", "^=", "=~", "!~", "=>", "->", "..",
    ];

    private static readonly string[] SymbolOperators =
        ["[]=", "[]", "<=>", "===", "==", "=~", "!=", "!~", "<<", ">>", "<=", ">=", "**", "+@", "-@", "+", "-", "*", "/", "%", "<", ">", "!", "&", "|", "^", "~"];

    private readonly string _s;
    private readonly List<Token> _toks = [];
    private readonly List<(string Id, bool Indented, bool Interp)> _heredocs = [];
    private int _pos;
    private int _line;
    private int _lineStart;

    private RubyLexer(string source) => _s = source;

    public static List<Token> Lex(string source)
    {
        var lx = new RubyLexer(source);
        lx.Run(false);
        lx.Add(Tk.Eof, "", lx._pos, lx._pos, false);
        return lx._toks;
    }

    private char Cur => _pos < _s.Length ? _s[_pos] : '\0';

    private char At(int off) => _pos + off < _s.Length ? _s[_pos + off] : '\0';

    private Token? Last => _toks.Count > 0 ? _toks[^1] : null;

    private void Add(Tk kind, string text, int start, int end, bool space) =>
        _toks.Add(new Token(kind, text, _line, start - _lineStart, end - _lineStart, space));

    private void AddAt(Tk kind, string text, int line, int lineStart, int start, int end, bool space) =>
        _toks.Add(new Token(kind, text, line, start - lineStart, end - lineStart, space));

    private static bool IsIdStart(char c) => c == '_' || char.IsLetter(c) || c > 127;

    private static bool IsIdChar(char c) => c == '_' || char.IsLetterOrDigit(c) || c > 127;

    private bool PrevIsValue()
    {
        if (Last is not { } t)
        {
            return false;
        }

        return t.Kind switch
        {
            Tk.Ident or Tk.Const or Tk.IVar or Tk.CVar or Tk.GVar or Tk.Num or Tk.Str or Tk.Symbol => true,
            Tk.Punct => t.Text is ")" or "]" or "}",
            Tk.Keyword => t.Text is "end" or "self" or "true" or "false" or "nil" or "__FILE__" or "__LINE__" or "__method__",
            _ => false,
        };
    }

    private void NewLine()
    {
        _line++;
        _lineStart = _pos;
    }

    private void Run(bool untilBrace)
    {
        var braceDepth = 0;
        while (_pos < _s.Length)
        {
            var c = Cur;
            var space = false;
            if (c is ' ' or '\t' or '\r')
            {
                while (Cur is ' ' or '\t' or '\r')
                {
                    _pos++;
                }

                continue;
            }

            space = _pos > 0 && _s[_pos - 1] is ' ' or '\t' or '\n' or '\r';
            if (c == '\\' && (At(1) == '\n' || (At(1) == '\r' && At(2) == '\n')))
            {
                _pos += At(1) == '\n' ? 2 : 3;
                NewLine();
                continue;
            }

            if (c == '\n')
            {
                var start = _pos;
                _pos++;
                if (!SuppressNewline())
                {
                    Add(Tk.Newline, "\n", start, _pos, false);
                }

                NewLine();
                ReadHeredocBodies();
                if (string.CompareOrdinal(_s, _pos, "=begin", 0, 6) == 0 && !untilBrace)
                {
                    SkipBlockComment();
                }
                else if (string.CompareOrdinal(_s, _pos, "__END__", 0, 7) == 0 && (_pos + 7 >= _s.Length || _s[_pos + 7] is '\n' or '\r'))
                {
                    _pos = _s.Length;
                }

                continue;
            }

            if (_pos == 0 && _s.StartsWith("=begin", StringComparison.Ordinal))
            {
                SkipBlockComment();
                continue;
            }

            if (c == '#')
            {
                while (_pos < _s.Length && Cur != '\n')
                {
                    _pos++;
                }

                continue;
            }

            if (untilBrace)
            {
                if (c == '{')
                {
                    braceDepth++;
                }
                else if (c == '}')
                {
                    if (braceDepth == 0)
                    {
                        _pos++;
                        return;
                    }

                    braceDepth--;
                }
            }

            LexOne(c, space);
        }
    }

    private void SkipBlockComment()
    {
        while (_pos < _s.Length)
        {
            var eol = _s.IndexOf('\n', _pos);
            var lineText = eol < 0 ? _s[_pos..] : _s[_pos..eol];
            if (eol < 0)
            {
                _pos = _s.Length;
                return;
            }

            _pos = eol + 1;
            NewLine();
            if (lineText.StartsWith("=end", StringComparison.Ordinal))
            {
                return;
            }
        }
    }

    private bool SuppressNewline()
    {
        if (Last is not { } t || t.Kind == Tk.Newline)
        {
            return true;
        }

        if (t.Kind == Tk.Punct && t.Text is "," or "." or "&." or "(" or "[" or "=" or "+" or "-" or "*" or "/" or "&&" or "||" or "?" or "=>" or "&" or "<<" or "==" or "!=" or "<" or ">" or "+=" or "-=" or "||=" or "&&=" or "::" or "|=" or "<=>")
        {
            return true;
        }

        if (t.Kind == Tk.Keyword && t.Text is "and" or "or" or "not")
        {
            return true;
        }

        // Leading-dot method chain on the next non-blank, non-comment line.
        var p = _pos;
        while (p < _s.Length)
        {
            while (p < _s.Length && _s[p] is ' ' or '\t' or '\r')
            {
                p++;
            }

            if (p < _s.Length && _s[p] == '#')
            {
                while (p < _s.Length && _s[p] != '\n')
                {
                    p++;
                }
            }

            if (p < _s.Length && _s[p] == '\n')
            {
                p++;
                continue;
            }

            break;
        }

        return p + 1 < _s.Length && ((_s[p] == '.' && _s[p + 1] != '.') || (_s[p] == '&' && _s[p + 1] == '.'));
    }

    private void LexOne(char c, bool space)
    {
        var start = _pos;
        var afterDot = Last is { Kind: Tk.Punct, Text: "." or "&." };
        var afterDef = Last is { Kind: Tk.Keyword, Text: "def" };

        if (char.IsAsciiDigit(c))
        {
            _pos++;
            while (IsIdChar(Cur) || (Cur == '.' && char.IsAsciiDigit(At(1))))
            {
                _pos++;
            }

            Add(Tk.Num, _s[start.._pos], start, _pos, space);
            return;
        }

        if (IsIdStart(c))
        {
            _pos++;
            while (IsIdChar(Cur))
            {
                _pos++;
            }

            if (Cur is '?' or '!' && !(At(1) == '=' && At(2) is not ('=' or '~' or '>')) && At(1) != ':' || (Cur == '?' && At(1) == ':' && At(2) == ':'))
            {
                _pos++;
            }

            var text = _s[start.._pos];
            if (Cur == ':' && At(1) != ':' && !afterDot && !(Last is { Kind: Tk.Punct, Text: "?" }) && !afterDef)
            {
                _pos++;
                Add(Tk.Label, text, start, start + text.Length, space);
                return;
            }

            if (text == "defined" && Cur == '?')
            {
                _pos++;
                text = "defined?";
            }

            var kind = Keywords.Contains(text) && !afterDot && !(afterDef && text != "self") ? Tk.Keyword
                : char.IsUpper(text[0]) ? Tk.Const : Tk.Ident;
            Add(kind, text, start, _pos, space);
            return;
        }

        switch (c)
        {
            case '@':
                _pos++;
                var cvar = Cur == '@';
                if (cvar)
                {
                    _pos++;
                }

                while (IsIdChar(Cur))
                {
                    _pos++;
                }

                Add(cvar ? Tk.CVar : Tk.IVar, _s[start.._pos], start, _pos, space);
                return;
            case '$':
                _pos++;
                if (IsIdStart(Cur))
                {
                    while (IsIdChar(Cur))
                    {
                        _pos++;
                    }
                }
                else
                {
                    _pos++;
                }

                Add(Tk.GVar, _s[start.._pos], start, _pos, space);
                return;
            case '"' or '`':
                _pos++;
                ReadString(start, c, c, true, space);
                return;
            case '\'':
                _pos++;
                ReadString(start, '\'', '\'', false, space);
                return;
            case ':':
                if (At(1) == ':')
                {
                    _pos += 2;
                    Add(Tk.Punct, "::", start, _pos, space);
                    return;
                }

                if (At(1) == '"' && !(PrevIsValue() && !space))
                {
                    _pos += 2;
                    ReadString(start, '"', '"', true, space);
                    return;
                }

                if ((IsIdStart(At(1)) || At(1) is '@' or '$') && !(PrevIsValue() && !space))
                {
                    _pos++;
                    var nameStart = _pos;
                    while (Cur is '@' or '$')
                    {
                        _pos++;
                    }

                    while (IsIdChar(Cur))
                    {
                        _pos++;
                    }

                    if (Cur is '?' or '!' or '=' && At(1) is not ('=' or '~' or '>'))
                    {
                        _pos++;
                    }

                    // Symbol tokens exclude the colon so that attr_* definitions can be ranged on the name.
                    Add(Tk.Symbol, _s[nameStart.._pos], nameStart, _pos, space);
                    return;
                }

                if (!(PrevIsValue() && !space) && At(1) != ':')
                {
                    foreach (var op in SymbolOperators)
                    {
                        if (string.CompareOrdinal(_s, _pos + 1, op, 0, op.Length) == 0)
                        {
                            _pos += 1 + op.Length;
                            Add(Tk.Symbol, op, start + 1, _pos, space);
                            return;
                        }
                    }
                }

                _pos++;
                Add(Tk.Punct, ":", start, _pos, space);
                return;
            case '/' when !PrevIsValue() || (Last is { Kind: Tk.Ident } && space && At(1) is not (' ' or '=')):
                _pos++;
                ReadString(start, '/', '/', true, space);
                while (Cur is 'i' or 'm' or 'x' or 'o' or 'u' or 'n')
                {
                    _pos++;
                }

                return;
            case '%' when (!PrevIsValue() || (Last is { Kind: Tk.Ident } && space && At(1) is not (' ' or '='))) && TryPercentLiteral(space):
                return;
            case '<' when At(1) == '<' && TryHeredoc(space):
                return;
            case '?' when !PrevIsValue() && (char.IsLetterOrDigit(At(1)) || "/.,;:!@#$%^&*()-_=+[]{}<>|\"'`~".Contains(At(1))) && !IsIdChar(At(2)):
                _pos += 2;
                Add(Tk.Str, _s[start.._pos], start, _pos, space);
                return;
        }

        foreach (var p in Puncts)
        {
            if (string.CompareOrdinal(_s, _pos, p, 0, p.Length) == 0)
            {
                _pos += p.Length;
                Add(Tk.Punct, p, start, _pos, space);
                return;
            }
        }

        _pos++;
        Add(Tk.Punct, c.ToString(), start, _pos, space);
    }

    private static char Closer(char open) => open switch { '(' => ')', '[' => ']', '{' => '}', '<' => '>', _ => open };

    private bool TryPercentLiteral(bool space)
    {
        var start = _pos;
        var i = _pos + 1;
        var type = 'Q';
        if (i < _s.Length && _s[i] is 'w' or 'W' or 'i' or 'I' or 'q' or 'Q' or 'r' or 's' or 'x')
        {
            type = _s[i];
            i++;
        }

        if (i >= _s.Length || char.IsLetterOrDigit(_s[i]) || _s[i] is ' ' or '\n' or '=' && i == _pos + 1)
        {
            return false;
        }

        var open = _s[i];
        if (char.IsWhiteSpace(open))
        {
            return false;
        }

        _pos = i + 1;
        ReadString(start, open, Closer(open), type is 'Q' or 'W' or 'I' or 'r' or 'x', space);
        if (type == 'r')
        {
            while (Cur is 'i' or 'm' or 'x' or 'o' or 'u' or 'n')
            {
                _pos++;
            }
        }

        return true;
    }

    private bool TryHeredoc(bool space)
    {
        var i = _pos + 2;
        var indented = false;
        if (i < _s.Length && _s[i] is '~' or '-')
        {
            indented = true;
            i++;
        }

        var squiggly = indented;
        var quote = i < _s.Length && _s[i] is '"' or '\'' or '`' ? _s[i] : '\0';
        if (quote != '\0')
        {
            i++;
        }

        var idStart = i;
        while (i < _s.Length && IsIdChar(_s[i]))
        {
            i++;
        }

        if (i == idStart || (quote == '\0' && !char.IsUpper(_s[idStart]) && _s[idStart] != '_'))
        {
            return false;
        }

        if (!squiggly && quote == '\0' && !(PrevIsValue() ? Last is { Kind: Tk.Ident } && space : true))
        {
            return false;
        }

        if (!squiggly && quote == '\0' && PrevIsValue() && !(Last is { Kind: Tk.Ident } && space))
        {
            return false;
        }

        var id = _s[idStart..i];
        if (quote != '\0')
        {
            if (i >= _s.Length || _s[i] != quote)
            {
                return false;
            }

            i++;
        }

        var start = _pos;
        _pos = i;
        _heredocs.Add((id, indented, quote != '\''));
        Add(Tk.Str, _s[start..i], start, i, space);
        return true;
    }

    private void ReadHeredocBodies()
    {
        var pending = _heredocs.ToArray();
        _heredocs.Clear();
        foreach (var (id, indented, _) in pending)
        {
            while (_pos < _s.Length)
            {
                var eol = _s.IndexOf('\n', _pos);
                var lineEnd = eol < 0 ? _s.Length : eol;
                var text = _s[_pos..lineEnd].TrimEnd('\r');
                var isEnd = (indented ? text.Trim() : text) == id;
                _pos = eol < 0 ? _s.Length : eol + 1;
                NewLine();
                if (isEnd)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Reads a delimited literal whose opening delimiter has been consumed.</summary>
    private void ReadString(int start, char open, char close, bool interpolate, bool space)
    {
        var startLine = _line;
        var startLineStart = _lineStart;
        var tokenIndex = _toks.Count;
        var interpolated = false;
        var depth = 0;
        while (_pos < _s.Length)
        {
            var c = Cur;
            if (c == '\\')
            {
                _pos += 2;
                if (_pos - 1 < _s.Length && _s[_pos - 1] == '\n')
                {
                    NewLine();
                }

                continue;
            }

            if (c == '\n')
            {
                _pos++;
                NewLine();
                continue;
            }

            if (interpolate && c == '#' && At(1) == '{')
            {
                if (!interpolated)
                {
                    // Reserve the position of the string token: tokens inside the interpolation come after it.
                    _toks.Add(default);
                    interpolated = true;
                }

                _pos += 2;
                Run(true);
                continue;
            }

            if (c == close && depth == 0)
            {
                _pos++;
                break;
            }

            if (open != close && c == open)
            {
                depth++;
            }
            else if (open != close && c == close)
            {
                depth--;
            }

            _pos++;
        }

        var tok = new Token(Tk.Str, "", startLine, start - startLineStart, _pos - (_line == startLine ? startLineStart : _lineStart), space);
        if (interpolated)
        {
            _toks[tokenIndex] = tok;
            // Marks the end of the literal so a following operator sees a value.
            _toks.Add(new Token(Tk.Str, "", _line, _pos - _lineStart, _pos - _lineStart, false));
        }
        else
        {
            _toks.Add(tok);
        }
    }
}
