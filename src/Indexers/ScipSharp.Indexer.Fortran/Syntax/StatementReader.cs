using System.Text;
using ScipSharp.Indexer.Fortran.Semantics;

namespace ScipSharp.Indexer.Fortran.Syntax;

/// <summary>A Fortran statement: the tokens of one logical line (or one <c>;</c>-separated part of it).</summary>
internal sealed class Stmt(SourceDoc file, List<Token> tokens)
{
    public SourceDoc File { get; } = file;

    public List<Token> Tokens { get; } = tokens;

    /// <summary>Statement label (columns 1-5 in fixed form, a leading integer in free form).</summary>
    public Token? Label { get; set; }

    /// <summary>Doc comments: <c>!&gt;</c> lines before the statement and <c>!!</c>/<c>!&lt;</c> lines after it.</summary>
    public List<string> Docs { get; } = [];

    /// <summary>Set for a <c>#include</c> directive.</summary>
    public string? CppInclude { get; init; }

    /// <summary>Deferred pass-2 work recorded by the collector (definitions, references, expressions to walk).</summary>
    public List<Action<Resolver>> Actions { get; } = [];

    public override string ToString() => string.Join(' ', Tokens.Select(static t => t.Text));
}

/// <summary>
/// Splits a document into statements: handles fixed form (comment characters in column 1, labels in columns 1-5,
/// continuation in column 6, text beyond the line length ignored, DEC tab format) and free form (<c>!</c> comments,
/// <c>&amp;</c> continuation including leading <c>&amp;</c> and continued character context), <c>;</c> separators,
/// C preprocessor directives and FORD/Doxygen doc comments.
/// </summary>
internal sealed class StatementReader
{
    private readonly SourceDoc _doc;
    private readonly int _fixedLineLength;
    private readonly Preprocessor _pp;
    private readonly List<Stmt> _result = [];
    private readonly List<(int Line, int Start, int End, bool Space)> _segments = [];
    private List<string> _leadingDocs = [];
    private List<string> _pendingDocs = [];
    private readonly List<string> _pendingTrailing = [];
    private Token? _pendingLabel;
    private Stmt? _last;
    private bool _trailingOpen;
    private bool _cont;
    private char _quote;

    private StatementReader(SourceDoc doc, int fixedLineLength, Preprocessor pp)
    {
        _doc = doc;
        _fixedLineLength = fixedLineLength;
        _pp = pp;
    }

    public static List<Stmt> Read(SourceDoc doc, int fixedLineLength, Preprocessor pp)
    {
        var reader = new StatementReader(doc, fixedLineLength, pp);
        reader.Run();
        return reader._result;
    }

    private void Run()
    {
        var lines = _doc.Lines;
        for (var li = 0; li < lines.Length; li++)
        {
            var raw = lines[li];
            if (Preprocessor.IsDirective(raw) && _quote == '\0')
            {
                var sbText = new StringBuilder(raw.TrimStart()[1..]);
                while (sbText.Length > 0 && sbText[^1] == '\\' && li + 1 < lines.Length)
                {
                    li++;
                    sbText.Length--;
                    sbText.Append(lines[li]);
                }

                var text = sbText.ToString();

                var include = _pp.Directive(text, _doc);
                if (include is not null)
                {
                    if (!_cont)
                    {
                        Flush();
                    }

                    _result.Add(new Stmt(_doc, []) { CppInclude = include });
                }

                _trailingOpen = false;
                continue;
            }

            if (!_pp.Active)
            {
                continue;
            }

            if (_doc.Form == SourceForm.Free)
            {
                FreeLine(li, raw);
            }
            else
            {
                FixedLine(li, raw);
            }
        }

        Flush();
    }

    private void FreeLine(int li, string raw)
    {
        var pos = 0;
        var space = false;
        if (_cont)
        {
            while (pos < raw.Length && raw[pos] is ' ' or '\t')
            {
                pos++;
            }

            if (pos < raw.Length && raw[pos] == '&')
            {
                pos++;
            }
            else if (_quote == '\0')
            {
                space = true;
            }
            else
            {
                pos = 0;
            }
        }

        var commentStart = ScanCode(raw, pos, raw.Length);
        var codeEnd = commentStart >= 0 ? commentStart : raw.Length;
        var k = codeEnd - 1;
        while (k >= pos && raw[k] is ' ' or '\t' or '\r')
        {
            k--;
        }

        var blank = k < pos;
        var contNext = !blank && raw[k] == '&';
        if (blank)
        {
            if (!_cont)
            {
                _quote = '\0';
                Comment(commentStart >= 0 ? raw[commentStart..] : null);
            }

            return;
        }

        if (!contNext)
        {
            _quote = '\0';
        }

        AddSegment(li, pos, contNext ? k : codeEnd, space);
        TrailingComment(raw, commentStart);
        _cont = contNext;
        if (!_cont)
        {
            Flush();
        }
    }

    private void FixedLine(int li, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            Comment(null);
            return;
        }

        if (raw[0] is 'c' or 'C' or '*' or '!' or 'd' or 'D')
        {
            Comment(raw);
            return;
        }

        var tab = raw.IndexOf('\t', StringComparison.Ordinal);
        int labelEnd, codeStart, limit;
        bool isCont;
        if (tab is >= 0 and < 6)
        {
            labelEnd = tab;
            isCont = tab + 1 < raw.Length && raw[tab + 1] is >= '1' and <= '9';
            codeStart = isCont ? tab + 2 : tab + 1;
            limit = raw.Length;
        }
        else
        {
            labelEnd = Math.Min(5, raw.Length);
            var c6 = raw.Length > 5 ? raw[5] : ' ';
            isCont = c6 is not (' ' or '0');
            codeStart = Math.Min(6, raw.Length);
            limit = _fixedLineLength > 0 ? Math.Min(raw.Length, _fixedLineLength) : raw.Length;
        }

        // Label field: digits and blanks; '!' starts a comment; anything else means the code starts early (tolerated).
        var labelDigits = new StringBuilder();
        var labelCol = -1;
        var labelLast = -1;
        for (var i = 0; i < labelEnd; i++)
        {
            var ch = raw[i];
            if (ch == ' ')
            {
                continue;
            }

            if (ch == '!')
            {
                Comment(raw[i..]);
                return;
            }

            if (char.IsDigit(ch))
            {
                labelCol = labelCol < 0 ? i : labelCol;
                labelLast = i;
                labelDigits.Append(ch);
                continue;
            }

            codeStart = i;
            isCont = false;
            labelDigits.Clear();
            labelCol = -1;
            break;
        }

        if (codeStart > limit)
        {
            codeStart = limit;
        }

        if (!isCont)
        {
            _quote = '\0';
        }

        var quoteBefore = _quote;
        var commentStart = ScanCode(raw, codeStart, limit);
        var codeEnd = commentStart >= 0 ? commentStart : limit;
        var blank = string.IsNullOrWhiteSpace(raw[codeStart..codeEnd]);
        if (blank && !isCont && labelDigits.Length == 0)
        {
            _quote = quoteBefore;
            Comment(commentStart >= 0 ? raw[commentStart..] : null);
            return;
        }

        if (!isCont)
        {
            Flush();
            if (labelDigits.Length > 0)
            {
                _pendingLabel = new Token(TokenKind.Int, labelDigits.ToString(), _doc, li, labelCol, labelLast + 1);
            }
        }

        AddSegment(li, codeStart, codeEnd, false);
        TrailingComment(raw, commentStart);
    }

    /// <summary>Scans code from <paramref name="start"/> tracking character context; returns the index of a '!' comment or -1.</summary>
    private int ScanCode(string raw, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            var ch = raw[i];
            if (_quote != '\0')
            {
                if (ch == _quote)
                {
                    if (i + 1 < end && raw[i + 1] == _quote)
                    {
                        i++;
                        continue;
                    }

                    _quote = '\0';
                }

                continue;
            }

            if (ch is '\'' or '"')
            {
                _quote = ch;
            }
            else if (ch == '!')
            {
                return i;
            }
        }

        return -1;
    }

    private void AddSegment(int line, int start, int end, bool space)
    {
        if (_segments.Count == 0)
        {
            _pendingDocs = _leadingDocs;
            _leadingDocs = [];
        }

        _segments.Add((line, start, Math.Max(start, end), space));
    }

    private void TrailingComment(string raw, int commentStart)
    {
        if (commentStart >= 0 && commentStart + 1 < raw.Length && raw[commentStart + 1] is '!' or '<')
        {
            _pendingTrailing.Add(StripDoc(raw[(commentStart + 2)..]));
        }
    }

    /// <summary>A comment or blank line (null). Collects <c>!&gt;</c> (before) and <c>!!</c>/<c>!&lt;</c> (after) doc comments.</summary>
    private void Comment(string? text)
    {
        if (text is null || text.Length < 2)
        {
            _trailingOpen = false;
            return;
        }

        var body = text[1..];
        if (body[0] == '>')
        {
            _leadingDocs.Add(StripDoc(body[1..]));
            _trailingOpen = false;
        }
        else if (body[0] is '!' or '<' && text[0] == '!')
        {
            var doc = StripDoc(body[1..]);
            if (_segments.Count > 0)
            {
                _pendingTrailing.Add(doc);
            }
            else if (_trailingOpen && _last is not null)
            {
                _last.Docs.Add(doc);
            }
            else
            {
                _leadingDocs.Add(doc);
            }
        }
        else
        {
            _trailingOpen = false;
        }
    }

    private static string StripDoc(string s)
    {
        s = s.TrimEnd();
        return s.StartsWith(' ') ? s[1..] : s;
    }

    private void Flush()
    {
        if (_segments.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        var lines = new List<int>();
        var cols = new List<int>();
        foreach (var (line, start, end, space) in _segments)
        {
            var raw = _doc.Lines[line];
            if (space)
            {
                sb.Append(' ');
                lines.Add(line);
                cols.Add(start);
            }

            for (var i = start; i < end && i < raw.Length; i++)
            {
                sb.Append(raw[i]);
                lines.Add(line);
                cols.Add(i);
            }
        }

        var tokens = Lexer.Tokenize(sb.ToString(), [.. lines], [.. cols], _doc);
        var first = true;
        var current = new List<Token>();
        Stmt? firstStmt = null;
        for (var i = 0; i <= tokens.Count; i++)
        {
            if (i < tokens.Count && !tokens[i].Is(";"))
            {
                current.Add(tokens[i]);
                continue;
            }

            if (current.Count == 0 && !(first && _pendingLabel is not null))
            {
                continue;
            }

            var st = new Stmt(_doc, current);
            if (first && _pendingLabel is not null)
            {
                st.Label = _pendingLabel;
            }
            else if (_doc.Form == SourceForm.Free && current.Count > 1 && current[0].Kind == TokenKind.Int)
            {
                st.Label = current[0];
                current.RemoveAt(0);
            }

            if (firstStmt is null)
            {
                firstStmt = st;
                st.Docs.AddRange(_pendingDocs);
                st.Docs.AddRange(_pendingTrailing);
            }

            first = false;
            _result.Add(st);
            _last = st;
            current = [];
        }

        if (firstStmt is not null)
        {
            _last = firstStmt;
        }

        _trailingOpen = true;
        _segments.Clear();
        _pendingDocs = [];
        _pendingTrailing.Clear();
        _pendingLabel = null;
    }
}
