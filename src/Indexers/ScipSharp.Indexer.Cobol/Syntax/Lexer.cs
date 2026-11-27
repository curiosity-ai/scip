using System.Text;

namespace ScipSharp.Indexer.Cobol.Syntax;

/// <summary>
/// Reference-format aware COBOL lexer. Handles fixed (sequence/indicator/area A+B/identification columns), free and
/// variable format, comment and debug lines, <c>*&gt;</c> floating comments, continuation of alphanumeric literals and
/// words, <c>&gt;&gt;SOURCE</c> / <c>$SET SOURCEFORMAT</c> switches, <c>CBL</c>/<c>PROCESS</c> option lines and picture strings.
/// Columns are UTF-16 code unit offsets into the original line.
/// </summary>
internal sealed class Lexer
{
    private const int FixedCodeStart = 7;
    private const int FixedCodeEnd = 72;

    private readonly int _fileId;
    private readonly bool _debugLines;
    private readonly bool _withLocations;
    private readonly List<Token> _tokens = [];
    private SourceFormat _format;
    private bool _expectPicture;
    private bool _mergeNext;
    private OpenLiteral? _open;

    private Lexer(int fileId, SourceFormat format, bool debugLines, bool withLocations)
    {
        _fileId = fileId;
        _format = format;
        _debugLines = debugLines;
        _withLocations = withLocations;
    }

    private sealed class OpenLiteral(StringBuilder text, char quote, SourceLocation location, bool spaceBefore, bool firstOnLine, bool areaA)
    {
        public StringBuilder Text { get; } = text;

        public char Quote { get; } = quote;

        public SourceLocation Location { get; } = location;

        public bool SpaceBefore { get; } = spaceBefore;

        public bool FirstOnLine { get; } = firstOnLine;

        public bool AreaA { get; } = areaA;
    }

    /// <summary>Splits text into lines (LF, CRLF or CR), keeping line numbering identical to SCIP's.</summary>
    public static string[] SplitLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
        .Select(l => l.TrimEnd('\r')).ToArray();

    /// <summary>Lexes a whole source file.</summary>
    public static List<Token> Lex(string text, int fileId, SourceFormat format = SourceFormat.Auto, bool debugLines = false) =>
        Lex(SplitLines(text), fileId, format, debugLines);

    public static List<Token> Lex(string[] lines, int fileId, SourceFormat format = SourceFormat.Auto, bool debugLines = false)
    {
        var lexer = new Lexer(fileId, format == SourceFormat.Auto ? Detect(lines) : format, debugLines, withLocations: true);
        for (var i = 0; i < lines.Length; i++)
        {
            lexer.LexLine(lines[i], i);
        }

        lexer.CloseLiteral();
        return lexer._tokens;
    }

    /// <summary>
    /// Lexes free-format replacement text (produced by COPY ... REPLACING). Locations use file id -1 and are offsets
    /// into <paramref name="text"/>; callers map them back to the source.
    /// </summary>
    public static List<Token> LexFragment(string text)
    {
        var lexer = new Lexer(-1, SourceFormat.Free, debugLines: true, withLocations: true);
        var lines = SplitLines(text);
        for (var i = 0; i < lines.Length; i++)
        {
            lexer.LexCode(lines[i], i, 0, lines[i].Length);
            lexer.CloseLiteral();
        }

        return lexer._tokens;
    }

    /// <summary>
    /// Guesses the reference format from the layout: free when program text starts in the sequence area (and that area
    /// does not look like a six-character sequence/tag field followed by an indicator).
    /// </summary>
    public static SourceFormat Detect(IReadOnlyList<string> lines)
    {
        int free = 0, fixedVotes = 0, seen = 0;
        foreach (var line in lines)
        {
            if (seen > 400)
            {
                break;
            }

            var idx = FirstNonSpace(line);
            if (idx < 0)
            {
                continue;
            }

            seen++;
            var rest = line.AsSpan(idx);
            if (rest.StartsWith(">>SOURCE", StringComparison.OrdinalIgnoreCase) || rest.StartsWith("$SET", StringComparison.OrdinalIgnoreCase))
            {
                continue; // handled while lexing
            }

            if (idx < 6)
            {
                if (line.Length >= 7 && !line.AsSpan(0, 6).ContainsAny(' ', '\t') && IsIndicator(line[6]))
                {
                    fixedVotes++;
                }
                else if (char.IsLetter(line[idx]) || rest.StartsWith("*>", StringComparison.Ordinal) || rest.StartsWith(">>", StringComparison.Ordinal))
                {
                    free++;
                }
                else
                {
                    fixedVotes++;
                }
            }
            else if (idx == 6 && char.IsLetter(line[6]) && line[6] is not ('D' or 'd'))
            {
                free++;
            }
            else
            {
                fixedVotes++;
            }
        }

        return free > 0 && free * 4 >= fixedVotes ? SourceFormat.Free : SourceFormat.Fixed;
    }

    private static bool IsIndicator(char c) => c is ' ' or '*' or '/' or '-' or 'D' or 'd' or '$';

    private static int FirstNonSpace(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (!char.IsWhiteSpace(line[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool StartsWithWord(ReadOnlySpan<char> s, string word) =>
        s.StartsWith(word, StringComparison.OrdinalIgnoreCase) && (s.Length == word.Length || char.IsWhiteSpace(s[word.Length]));

    private void LexLine(string raw, int lineNumber)
    {
        int codeStart, codeEnd;
        var indicator = ' ';
        var trimmed = raw.AsSpan().TrimStart();
        if (trimmed.IsEmpty)
        {
            return;
        }

        // Directives are recognised wherever they start (some shops put them in the sequence area).
        if (trimmed.StartsWith(">>", StringComparison.Ordinal))
        {
            Directive(trimmed[2..]);
            return;
        }

        if (StartsWithWord(trimmed, "CBL") || StartsWithWord(trimmed, "PROCESS"))
        {
            return; // IBM compiler option lines
        }

        if (_format == SourceFormat.Free)
        {
            codeStart = 0;
            codeEnd = raw.Length;
            if (trimmed.StartsWith("$SET", StringComparison.OrdinalIgnoreCase))
            {
                Directive(trimmed);
                return;
            }
        }
        else
        {
            if (raw.Length <= 6)
            {
                return; // only a sequence number
            }

            indicator = raw[6];
            codeStart = FixedCodeStart;
            codeEnd = _format == SourceFormat.Variable ? raw.Length : Math.Min(raw.Length, FixedCodeEnd);
            if (raw[0] == '\t')
            {
                // Micro Focus: a leading tab skips the sequence area and indicator.
                indicator = ' ';
                codeStart = 1;
            }

            switch (indicator)
            {
                case '*' or '/':
                    return;
                case '$':
                    Directive(raw.AsSpan(6));
                    return;
                case 'D' or 'd' when !_debugLines:
                    return;
            }

            var code = codeStart < codeEnd ? raw.AsSpan(codeStart, codeEnd - codeStart).TrimStart() : [];
            if (code.StartsWith(">>", StringComparison.Ordinal))
            {
                Directive(code[2..]);
                return;
            }

            if (code.IsEmpty && indicator != '-')
            {
                return;
            }
        }

        if (indicator == '-')
        {
            if (_open is not null)
            {
                var pos = codeStart;
                while (pos < codeEnd && char.IsWhiteSpace(raw[pos]))
                {
                    pos++;
                }

                if (pos < codeEnd && (raw[pos] == _open.Quote || raw[pos] is '\'' or '"'))
                {
                    var next = ContinueLiteral(raw, pos + 1, codeEnd);
                    LexCode(raw, lineNumber, next, codeEnd);
                    return;
                }

                CloseLiteral();
            }
            else
            {
                _mergeNext = true;
            }
        }
        else
        {
            CloseLiteral();
        }

        LexCode(raw, lineNumber, codeStart, codeEnd);
        _mergeNext = false;
    }

    private void Directive(ReadOnlySpan<char> text)
    {
        var upper = text.ToString().ToUpperInvariant();
        if (upper.StartsWith("SOURCE", StringComparison.Ordinal) || upper.Contains("SOURCEFORMAT", StringComparison.Ordinal))
        {
            var at = upper.IndexOf("SOURCEFORMAT", StringComparison.Ordinal);
            var tail = at >= 0 ? upper[(at + "SOURCEFORMAT".Length)..] : upper["SOURCE".Length..];
            if (tail.Contains("FREE", StringComparison.Ordinal))
            {
                _format = SourceFormat.Free;
            }
            else if (tail.Contains("VARIABLE", StringComparison.Ordinal))
            {
                _format = SourceFormat.Variable;
            }
            else if (tail.Contains("FIXED", StringComparison.Ordinal))
            {
                _format = SourceFormat.Fixed;
            }
        }
    }

    /// <summary>Continues an open literal on a continuation line; returns the index after the literal.</summary>
    private int ContinueLiteral(string raw, int from, int end)
    {
        var open = _open!;
        var k = from;
        while (k < end)
        {
            if (raw[k] == open.Quote)
            {
                if (k + 1 < end && raw[k + 1] == open.Quote)
                {
                    k += 2;
                    continue;
                }

                open.Text.Append(raw, from, k + 1 - from);
                CloseLiteral();
                return k + 1;
            }

            k++;
        }

        open.Text.Append(raw, from, end - from);
        return end;
    }

    private void CloseLiteral()
    {
        if (_open is null)
        {
            return;
        }

        var open = _open;
        _open = null;
        Add(new Token(TokenKind.Literal, open.Text.ToString(), _withLocations ? open.Location : null)
        {
            SpaceBefore = open.SpaceBefore,
            FirstOnLine = open.FirstOnLine,
            AreaA = open.AreaA,
        });
    }

    private void LexCode(string raw, int line, int start, int end)
    {
        var first = true;
        var i = start;
        while (i < end)
        {
            var c = raw[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '*' && i + 1 < end && raw[i + 1] == '>')
            {
                break; // floating comment
            }

            var spaceBefore = i == start || char.IsWhiteSpace(raw[i - 1]);
            var tokenStart = i;
            TokenKind kind;
            if (_expectPicture && c is not ('\'' or '"'))
            {
                var j = i;
                while (j < end && !char.IsWhiteSpace(raw[j]) && !(raw[j] == '=' && j + 1 < end && raw[j + 1] == '='))
                {
                    j++;
                }

                var pic = raw.AsSpan(i, j - i);
                if (pic.Equals("IS", StringComparison.OrdinalIgnoreCase))
                {
                    Emit(TokenKind.Word, raw, line, i, j, spaceBefore, ref first);
                    i = j;
                    continue;
                }

                _expectPicture = false;
                var picEnd = j;
                if (pic.Length > 1 && pic[^1] is '.' or ',' or ';')
                {
                    picEnd--;
                }

                Emit(TokenKind.Picture, raw, line, i, picEnd, spaceBefore, ref first);
                if (picEnd < j)
                {
                    Emit(raw[picEnd] == '.' ? TokenKind.Period : TokenKind.Comma, raw, line, picEnd, j, false, ref first);
                }

                i = j;
                continue;
            }

            if (c is '\'' or '"')
            {
                i = LexLiteral(raw, line, i, i, end, spaceBefore, ref first);
                continue;
            }

            if (c == '+' && raw.AsSpan(i).StartsWith("++INCLUDE", StringComparison.OrdinalIgnoreCase))
            {
                i += "++INCLUDE".Length;
                Emit(TokenKind.Word, raw, line, tokenStart, i, spaceBefore, ref first);
                continue;
            }

            if (IsWordStart(c) || (c == '.' && i + 1 < end && char.IsAsciiDigit(raw[i + 1]) && spaceBefore))
            {
                var j = i + 1;
                var numeric = char.IsAsciiDigit(c) || c == '.';
                while (j < end)
                {
                    var d = raw[j];
                    if (IsWordChar(d))
                    {
                        numeric &= char.IsAsciiDigit(d);
                        j++;
                    }
                    else if (d == '.' && numeric && j + 1 < end && char.IsAsciiDigit(raw[j + 1]))
                    {
                        j++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (j < end && raw[j] is '\'' or '"' && IsLiteralPrefix(raw.AsSpan(i, j - i)))
                {
                    i = LexLiteral(raw, line, i, j, end, spaceBefore, ref first);
                    continue;
                }

                var word = raw.AsSpan(i, j - i);
                if (_mergeNext && first && _tokens.Count > 0 && _tokens[^1].IsWord)
                {
                    var prev = _tokens[^1];
                    _tokens[^1] = new Token(TokenKind.Word, prev.Text + word.ToString(), prev.Location)
                    {
                        SpaceBefore = prev.SpaceBefore,
                        FirstOnLine = prev.FirstOnLine,
                        AreaA = prev.AreaA,
                    };
                    first = false;
                    _mergeNext = false;
                }
                else
                {
                    Emit(TokenKind.Word, raw, line, i, j, spaceBefore, ref first);
                }

                if (word.Equals("PIC", StringComparison.OrdinalIgnoreCase) || word.Equals("PICTURE", StringComparison.OrdinalIgnoreCase))
                {
                    _expectPicture = true;
                }

                i = j;
                continue;
            }

            var len = 1;
            switch (c)
            {
                case '.':
                    kind = i + 1 >= end || char.IsWhiteSpace(raw[i + 1]) ? TokenKind.Period : TokenKind.Other;
                    break;
                case ',' or ';':
                    kind = TokenKind.Comma;
                    break;
                case '(':
                    kind = TokenKind.LParen;
                    break;
                case ')':
                    kind = TokenKind.RParen;
                    break;
                case ':':
                    kind = TokenKind.Colon;
                    break;
                case '=':
                    if (i + 1 < end && raw[i + 1] == '=')
                    {
                        kind = TokenKind.PseudoDelimiter;
                        len = 2;
                    }
                    else
                    {
                        kind = TokenKind.Operator;
                    }

                    break;
                case '*':
                    kind = TokenKind.Operator;
                    len = i + 1 < end && raw[i + 1] == '*' ? 2 : 1;
                    break;
                case '<' or '>':
                    kind = TokenKind.Operator;
                    len = i + 1 < end && (raw[i + 1] == '=' || (c == '<' && raw[i + 1] == '>')) ? 2 : 1;
                    break;
                case '+' or '-' or '/' or '&':
                    kind = TokenKind.Operator;
                    break;
                default:
                    kind = TokenKind.Other;
                    break;
            }

            Emit(kind, raw, line, i, i + len, spaceBefore, ref first);
            i += len;
        }
    }

    private int LexLiteral(string raw, int line, int start, int quoteAt, int end, bool spaceBefore, ref bool first)
    {
        var quote = raw[quoteAt];
        var k = quoteAt + 1;
        while (k < end)
        {
            if (raw[k] == quote)
            {
                if (k + 1 < end && raw[k + 1] == quote)
                {
                    k += 2;
                    continue;
                }

                Emit(TokenKind.Literal, raw, line, start, k + 1, spaceBefore, ref first);
                return k + 1;
            }

            k++;
        }

        // Unterminated: may be continued on the next line.
        _open = new OpenLiteral(
            new StringBuilder(raw, start, end - start, end - start + 16),
            quote,
            new SourceLocation(_fileId, line, start, end),
            spaceBefore,
            first,
            IsAreaA(start));
        first = false;
        return end;
    }

    private void Emit(TokenKind kind, string raw, int line, int start, int end, bool spaceBefore, ref bool first)
    {
        Add(new Token(kind, raw[start..end], _withLocations ? new SourceLocation(_fileId, line, start, end) : null)
        {
            SpaceBefore = spaceBefore || first,
            FirstOnLine = first,
            AreaA = IsAreaA(start),
        });
        first = false;
    }

    private bool IsAreaA(int column) => _withLocations && _format != SourceFormat.Free && column is >= 7 and < 11;

    private void Add(Token token) => _tokens.Add(token);

    private static bool IsLiteralPrefix(ReadOnlySpan<char> s) =>
        s.Length <= 2 && s.ToString().ToUpperInvariant() is "X" or "N" or "G" or "Z" or "B" or "U" or "NX" or "BX" or "H";

    internal static bool IsWordStart(char c) => char.IsLetterOrDigit(c) || c == '_';

    internal static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '_';
}
