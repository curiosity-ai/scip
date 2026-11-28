using System.Text;

namespace ScipSharp.Indexer.Php.Syntax;

internal enum TK { Eof, InlineHtml, OpenTagEcho, CloseTag, Variable, Name, Number, String, Template, Cast, Op }

internal enum NameKind { Unqualified, Qualified, FullyQualified, Relative }

/// <summary>A <c>/** ... */</c> comment. Offsets are byte offsets (the source is decoded as Latin-1).</summary>
internal sealed record DocComment(int Start, int End, string Text);

internal abstract record TplPart(int Start, int End);

internal sealed record TplLiteral(int Start, int End) : TplPart(Start, End);

/// <summary><c>$name</c>, optionally followed by <c>[key]</c> or <c>-&gt;prop</c>.</summary>
internal sealed record TplSimple(int Start, int End, string Var, int VarEnd, TplSuffix? Suffix) : TplPart(Start, End);

internal abstract record TplSuffix(int Start, int End);

/// <summary>Key of <c>$a[key]</c>: bare word, number or variable.</summary>
internal sealed record TplDim(int Start, int End, string Text, bool IsVariable, bool IsNumber) : TplSuffix(Start, End);

internal sealed record TplProp(int Start, int End, string Name) : TplSuffix(Start, End);

/// <summary><c>{$expr}</c> or <c>${expr}</c>; holds the tokens of the embedded expression.</summary>
internal sealed record TplExpr(int Start, int End, List<Token> Tokens) : TplPart(Start, End);

internal sealed class Token
{
    public TK Kind;
    /// <summary>Source text (identifiers are re-decoded as UTF-8). Variables include the leading <c>$</c>.</summary>
    public string Text = "";
    public int Start;
    public int End;
    public DocComment? Doc;
    /// <summary>Index of this token in PHP's own token stream (whitespace and comments included).</summary>
    public int Pos;
    public NameKind NameKind;
    public List<TplPart>? Parts;

    public bool IsOp(string op) => Kind == TK.Op && Text == op;

    public bool IsKeyword(string kw) =>
        Kind == TK.Name && NameKind == NameKind.Unqualified && string.Equals(Text, kw, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{Kind}:{Text}@{Start}";
}

internal sealed class ParseException(string message, int position) : Exception(message)
{
    public int Position { get; } = position;
}

/// <summary>
/// PHP lexer. The source is a Latin-1 decoded string so that string indices are UTF-8 byte offsets,
/// which is what SCIP positions need. Token indices mimic PHP's tokenizer (whitespace and comments count).
/// </summary>
internal sealed class Lexer(string src)
{
    private static readonly string[] Ops3 = ["===", "!==", "<=>", "**=", "...", "<<=", ">>=", "??=", "?->"];
    private static readonly string[] Ops2 =
        ["==", "!=", "<>", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=", ".=", "%=", "&=", "|=", "^=", "->", "=>", "::", "<<", ">>", "??", "**"];

    private static readonly HashSet<string> CastNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "integer", "bool", "boolean", "float", "double", "real", "string", "binary", "array", "object", "unset",
    };

    private int _i;
    private int _pos;
    private DocComment? _pendingDoc;

    // Interpolations (`"{$a["{$b}"]}"`) are lexed recursively; malformed input such as many unclosed `"{$x` nests one
    // level per occurrence, so the depth is capped (a parse error: the file is skipped) instead of overflowing the stack.
    private const int MaxNesting = 256;
    private int _nesting;

    // Heredoc labels for which no closing marker exists at or after the given offset (so a file with many unterminated
    // `<<<X` does not rescan the rest of the file for each).
    private readonly Dictionary<string, int> _noHeredocEnd = new(StringComparer.Ordinal);

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        _i = 0;
        _pos = 0;
        LexHtml(tokens);
        LexPhpInto(tokens, stopAtBrace: false);
        tokens.Add(new Token { Kind = TK.Eof, Start = src.Length, End = src.Length, Pos = _pos });
        return tokens;
    }

    private static bool IsIdentStart(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or >= '\x80';

    private static bool IsIdentChar(char c) => IsIdentStart(c) || c is >= '0' and <= '9';

    private static bool IsWs(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

    /// <summary>
    /// Decodes a byte-per-char (Latin-1) slice of source as UTF-8. Bytes that do not form valid UTF-8 (identifiers in
    /// Latin-1 files) are kept one-to-one as U+0080..U+00FF instead of becoming U+FFFD, so distinct names stay distinct.
    /// </summary>
    internal static string Decode(string latin1)
    {
        var i = 0;
        while (i < latin1.Length && latin1[i] < '\x80')
        {
            i++;
        }

        if (i == latin1.Length)
        {
            return latin1;
        }

        var bytes = new byte[latin1.Length];
        for (var k = 0; k < latin1.Length; k++)
        {
            bytes[k] = (byte)latin1[k];
        }

        var sb = new StringBuilder(latin1.Length);
        sb.Append(latin1, 0, i);
        while (i < bytes.Length)
        {
            if (bytes[i] < 0x80)
            {
                sb.Append((char)bytes[i++]);
                continue;
            }

            if (System.Text.Rune.DecodeFromUtf8(bytes.AsSpan(i), out var rune, out var used) == System.Buffers.OperationStatus.Done)
            {
                sb.Append(rune);
                i += used;
            }
            else
            {
                sb.Append((char)bytes[i++]);
            }
        }

        return sb.ToString();
    }

    /// <summary>Consumes inline HTML up to and including the next open tag.</summary>
    private void LexHtml(List<Token> tokens)
    {
        var start = _i;
        while (_i < src.Length)
        {
            if (src[_i] == '<' && _i + 1 < src.Length && src[_i + 1] == '?')
            {
                if (StartsWithIgnoreCase(_i, "<?php") && (_i + 5 >= src.Length || IsWs(src[_i + 5])))
                {
                    EmitHtml(tokens, start, _i);
                    _i += 5;
                    if (_i < src.Length)
                    {
                        if (src[_i] == '\r' && _i + 1 < src.Length && src[_i + 1] == '\n')
                        {
                            _i += 2;
                        }
                        else
                        {
                            _i++;
                        }
                    }

                    _pos++;
                    return;
                }

                if (_i + 2 < src.Length && src[_i + 2] == '=')
                {
                    EmitHtml(tokens, start, _i);
                    tokens.Add(Make(TK.OpenTagEcho, _i, _i + 3, "<?="));
                    _i += 3;
                    return;
                }
            }

            _i++;
        }

        EmitHtml(tokens, start, _i);
    }

    private void EmitHtml(List<Token> tokens, int start, int end)
    {
        if (end > start)
        {
            tokens.Add(Make(TK.InlineHtml, start, end, src[start..end]));
        }
    }

    private void EnterNesting(int at)
    {
        if (_nesting >= MaxNesting || !System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new ParseException($"string interpolation nested too deeply at offset {at}", at);
        }

        _nesting++;
    }

    private bool StartsWithIgnoreCase(int at, string s) =>
        at + s.Length <= src.Length && string.Compare(src, at, s, 0, s.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private Token Make(TK kind, int start, int end, string text)
    {
        var t = new Token { Kind = kind, Start = start, End = end, Text = text, Pos = _pos++, Doc = _pendingDoc };
        _pendingDoc = null;
        return t;
    }

    /// <summary>
    /// Lexes PHP code until end of input (or the unbalanced closing brace when <paramref name="stopAtBrace"/>), but not
    /// past <paramref name="limit"/> (the end of the enclosing string for an interpolation).
    /// </summary>
    private void LexPhpInto(List<Token> tokens, bool stopAtBrace, int limit = int.MaxValue)
    {
        var depth = 0;
        var end = Math.Min(limit, src.Length);
        while (_i < end)
        {
            var c = src[_i];
            if (IsWs(c))
            {
                while (_i < src.Length && IsWs(src[_i]))
                {
                    _i++;
                }

                _pos++;
                continue;
            }

            if (c == '#' && _i + 1 < src.Length && src[_i + 1] == '[')
            {
                tokens.Add(Make(TK.Op, _i, _i + 2, "#["));
                _i += 2;
                continue;
            }

            if (c == '#' || (c == '/' && _i + 1 < src.Length && src[_i + 1] == '/'))
            {
                while (_i < src.Length && src[_i] != '\n' && src[_i] != '\r' && !(src[_i] == '?' && _i + 1 < src.Length && src[_i + 1] == '>'))
                {
                    _i++;
                }

                _pos++;
                continue;
            }

            if (c == '/' && _i + 1 < src.Length && src[_i + 1] == '*')
            {
                var s = _i;
                var e = src.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                _i = e < 0 ? src.Length : e + 2;
                if (_i - s >= 5 && src[s + 2] == '*' && IsWs(src[s + 3]))
                {
                    _pendingDoc = new DocComment(s, _i, src[s.._i]);
                }

                _pos++;
                continue;
            }

            if (c == '?' && _i + 1 < src.Length && src[_i + 1] == '>')
            {
                var s = _i;
                _i += 2;
                if (_i < src.Length && src[_i] == '\n')
                {
                    _i++;
                }
                else if (_i + 1 < src.Length && src[_i] == '\r' && src[_i + 1] == '\n')
                {
                    _i += 2;
                }

                tokens.Add(Make(TK.CloseTag, s, _i, "?>"));
                LexHtml(tokens);
                continue;
            }

            if (stopAtBrace)
            {
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    if (depth == 0)
                    {
                        return;
                    }

                    depth--;
                }
            }

            LexToken(tokens);
        }
    }

    private void LexToken(List<Token> tokens)
    {
        var c = src[_i];
        var s = _i;
        if (c == '$')
        {
            if (_i + 1 < src.Length && IsIdentStart(src[_i + 1]))
            {
                _i++;
                while (_i < src.Length && IsIdentChar(src[_i]))
                {
                    _i++;
                }

                tokens.Add(Make(TK.Variable, s, _i, Decode(src[s.._i])));
                return;
            }

            _i++;
            tokens.Add(Make(TK.Op, s, _i, "$"));
            return;
        }

        if (IsIdentStart(c) || (c == '\\' && _i + 1 < src.Length && IsIdentStart(src[_i + 1])))
        {
            LexName(tokens);
            return;
        }

        if (c is >= '0' and <= '9' || (c == '.' && _i + 1 < src.Length && src[_i + 1] is >= '0' and <= '9'))
        {
            LexNumber(tokens);
            return;
        }

        if (c == '\'')
        {
            _i++;
            while (_i < src.Length && src[_i] != '\'')
            {
                _i += src[_i] == '\\' ? 2 : 1;
            }

            _i = Math.Min(_i + 1, src.Length);
            tokens.Add(Make(TK.String, s, _i, src[s.._i]));
            return;
        }

        if (c == '"')
        {
            LexDoubleQuoted(tokens, '"');
            return;
        }

        if (c == '`')
        {
            LexDoubleQuoted(tokens, '`');
            return;
        }

        if (c == '<' && string.CompareOrdinal(src, _i, "<<<", 0, 3) == 0 && TryLexHeredoc(tokens))
        {
            return;
        }

        if (c == '(' && TryLexCast(tokens))
        {
            return;
        }

        foreach (var op in Ops3)
        {
            if (string.CompareOrdinal(src, _i, op, 0, 3) == 0)
            {
                _i += 3;
                tokens.Add(Make(TK.Op, s, _i, op));
                return;
            }
        }

        foreach (var op in Ops2)
        {
            if (string.CompareOrdinal(src, _i, op, 0, 2) == 0)
            {
                _i += 2;
                tokens.Add(Make(TK.Op, s, _i, op));
                return;
            }
        }

        _i++;
        tokens.Add(Make(TK.Op, s, _i, c.ToString()));
    }

    private void LexName(List<Token> tokens)
    {
        var s = _i;
        var kind = NameKind.Unqualified;
        if (src[_i] == '\\')
        {
            kind = NameKind.FullyQualified;
            _i++;
        }

        ScanIdent();
        var first = src[(kind == NameKind.FullyQualified ? s + 1 : s).._i];
        if (kind == NameKind.Unqualified && _i < src.Length && src[_i] == '\\' && _i + 1 < src.Length && IsIdentStart(src[_i + 1]))
        {
            kind = string.Equals(first, "namespace", StringComparison.OrdinalIgnoreCase) ? NameKind.Relative : NameKind.Qualified;
        }

        while (_i + 1 < src.Length && src[_i] == '\\' && IsIdentStart(src[_i + 1]))
        {
            _i++;
            ScanIdent();
            if (kind == NameKind.Unqualified)
            {
                kind = NameKind.Qualified;
            }
        }

        var t = Make(TK.Name, s, _i, Decode(src[s.._i]));
        t.NameKind = kind;
        tokens.Add(t);
    }

    private void ScanIdent()
    {
        while (_i < src.Length && IsIdentChar(src[_i]))
        {
            _i++;
        }
    }

    private void LexNumber(List<Token> tokens)
    {
        var s = _i;
        if (src[_i] == '0' && _i + 1 < src.Length && src[_i + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            _i += 2;
            while (_i < src.Length && (Uri.IsHexDigit(src[_i]) || src[_i] == '_'))
            {
                _i++;
            }
        }
        else
        {
            while (_i < src.Length && (char.IsAsciiDigit(src[_i]) || src[_i] == '_'))
            {
                _i++;
            }

            if (_i < src.Length && src[_i] == '.' && _i + 1 < src.Length && char.IsAsciiDigit(src[_i + 1]))
            {
                _i++;
                while (_i < src.Length && (char.IsAsciiDigit(src[_i]) || src[_i] == '_'))
                {
                    _i++;
                }
            }
            else if (_i < src.Length && src[_i] == '.' && !(_i + 1 < src.Length && (src[_i + 1] == '.' || IsIdentStart(src[_i + 1]))))
            {
                _i++;
            }

            if (_i < src.Length && src[_i] is 'e' or 'E')
            {
                var j = _i + 1;
                if (j < src.Length && src[j] is '+' or '-')
                {
                    j++;
                }

                if (j < src.Length && char.IsAsciiDigit(src[j]))
                {
                    _i = j;
                    while (_i < src.Length && char.IsAsciiDigit(src[_i]))
                    {
                        _i++;
                    }
                }
            }
        }

        tokens.Add(Make(TK.Number, s, _i, src[s.._i]));
    }

    private bool TryLexCast(List<Token> tokens)
    {
        var j = _i + 1;
        while (j < src.Length && (src[j] is ' ' or '\t'))
        {
            j++;
        }

        var ws = j;
        while (j < src.Length && char.IsAsciiLetter(src[j]))
        {
            j++;
        }

        if (j == ws || !CastNames.Contains(src[ws..j]))
        {
            return false;
        }

        var name = src[ws..j];
        while (j < src.Length && (src[j] is ' ' or '\t'))
        {
            j++;
        }

        if (j >= src.Length || src[j] != ')')
        {
            return false;
        }

        var s = _i;
        _i = j + 1;
        tokens.Add(Make(TK.Cast, s, _i, name.ToLowerInvariant()));
        return true;
    }

    /// <summary>Skips a <c>{$expr}</c> interpolation starting at the brace, including nested string literals; returns the index after the closing brace.</summary>
    private int SkipBraceExpr(int j)
    {
        EnterNesting(j);
        try
        {
            return SkipBraceExprCore(j);
        }
        finally
        {
            _nesting--;
        }
    }

    private int SkipBraceExprCore(int j)
    {
        var depth = 0;
        while (j < src.Length)
        {
            var c = src[j];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return j + 1;
                }
            }
            else if (c == '\'')
            {
                j++;
                while (j < src.Length && src[j] != '\'')
                {
                    j += src[j] == '\\' ? 2 : 1;
                }
            }
            else if (c == '"')
            {
                j++;
                while (j < src.Length && src[j] != '"')
                {
                    if (src[j] == '\\')
                    {
                        j += 2;
                    }
                    else if (src[j] == '{' && j + 1 < src.Length && src[j + 1] == '$')
                    {
                        j = SkipBraceExpr(j);
                    }
                    else
                    {
                        j++;
                    }
                }
            }

            j++;
        }

        return j;
    }

    private void LexDoubleQuoted(List<Token> tokens, char quote)
    {
        var s = _i;
        var j = _i + 1;
        var interpolated = false;
        while (j < src.Length && src[j] != quote)
        {
            if (src[j] == '\\')
            {
                j += 2;
                continue;
            }

            if (src[j] == '$' && j + 1 < src.Length && (IsIdentStart(src[j + 1]) || src[j + 1] == '{'))
            {
                interpolated = true;
            }
            else if (src[j] == '{' && j + 1 < src.Length && src[j + 1] == '$')
            {
                interpolated = true;
                j = SkipBraceExpr(j);
                continue;
            }

            j++;
        }

        var end = Math.Min(j + 1, src.Length);
        if (!interpolated)
        {
            _i = end;
            tokens.Add(Make(TK.String, s, _i, src[s.._i]));
            return;
        }

        var t = Make(TK.Template, s, end, src[s..end]);
        // The template token's own index stands for the opening quote; the closing quote is counted below.
        t.Parts = ScanTemplate(s + 1, end - 1, heredoc: false);
        _pos++;
        _i = end;
        tokens.Add(t);
    }

    private bool TryLexHeredoc(List<Token> tokens)
    {
        var j = _i + 3;
        while (j < src.Length && (src[j] is ' ' or '\t'))
        {
            j++;
        }

        var quote = '\0';
        if (j < src.Length && src[j] is '\'' or '"')
        {
            quote = src[j];
            j++;
        }

        var idStart = j;
        while (j < src.Length && IsIdentChar(src[j]))
        {
            j++;
        }

        if (j == idStart)
        {
            return false;
        }

        var id = src[idStart..j];
        if (quote != '\0')
        {
            if (j >= src.Length || src[j] != quote)
            {
                return false;
            }

            j++;
        }

        if (j < src.Length && src[j] == '\r')
        {
            j++;
        }

        if (j >= src.Length || src[j] != '\n')
        {
            return false;
        }

        j++;
        var bodyStart = j;

        if (_noHeredocEnd.TryGetValue(id, out var noEndFrom) && bodyStart >= noEndFrom)
        {
            return false;
        }

        // Find the closing marker: at the start of a line, optionally indented, not followed by an identifier char.
        var lineStart = bodyStart;
        var bodyEnd = -1;
        var end = -1;
        while (lineStart <= src.Length)
        {
            var k = lineStart;
            while (k < src.Length && (src[k] is ' ' or '\t'))
            {
                k++;
            }

            if (string.CompareOrdinal(src, k, id, 0, id.Length) == 0 && (k + id.Length >= src.Length || !IsIdentChar(src[k + id.Length])))
            {
                bodyEnd = lineStart;
                end = k + id.Length;
                break;
            }

            var nl = src.IndexOf('\n', lineStart);
            if (nl < 0)
            {
                break;
            }

            lineStart = nl + 1;
        }

        if (end < 0)
        {
            _noHeredocEnd[id] = bodyStart;
            return false;
        }

        var s = _i;
        // The newline before the closing marker belongs to the heredoc terminator, not the body.
        var contentEnd = bodyEnd;
        if (contentEnd > bodyStart)
        {
            contentEnd--;
            if (contentEnd > bodyStart && src[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }
        }

        if (quote == '\'')
        {
            var t = Make(TK.String, s, end, src[s..end]);
            _pos += (contentEnd > bodyStart ? 1 : 0) + 1;
            tokens.Add(t);
        }
        else
        {
            var t = Make(TK.Template, s, end, src[s..end]);
            // The template token's own index stands for T_START_HEREDOC.
            t.Parts = ScanTemplate(bodyStart, contentEnd, heredoc: true);
            _pos++; // end heredoc
            tokens.Add(t);
        }

        _i = end;
        return true;
    }

    /// <summary>Splits the body of an interpolated string into literal and expression parts.</summary>
    private List<TplPart> ScanTemplate(int from, int to, bool heredoc)
    {
        EnterNesting(from);
        try
        {
            return ScanTemplateCore(from, to, heredoc);
        }
        finally
        {
            _nesting--;
        }
    }

    private List<TplPart> ScanTemplateCore(int from, int to, bool heredoc)
    {
        _ = heredoc;
        var parts = new List<TplPart>();
        var litStart = from;
        var j = from;
        void FlushLiteral(int upTo)
        {
            if (upTo > litStart)
            {
                parts.Add(new TplLiteral(litStart, upTo));
                _pos++;
            }
        }

        while (j < to)
        {
            var c = src[j];
            if (c == '\\')
            {
                j += 2;
                continue;
            }

            if (c == '$' && j + 1 < to && IsIdentStart(src[j + 1]))
            {
                FlushLiteral(j);
                var vs = j;
                j++;
                var ns = j;
                while (j < to && IsIdentChar(src[j]))
                {
                    j++;
                }

                var name = "$" + Decode(src[ns..j]);
                var varEnd = j;
                TplSuffix? suffix = null;
                var count = 1;
                if (j < to && src[j] == '[')
                {
                    var ks = j + 1;
                    var ke = ks;
                    if (ke < to && src[ke] == '-')
                    {
                        ke++;
                    }

                    var isVar = ke < to && src[ke] == '$';
                    if (isVar)
                    {
                        ke++;
                    }

                    while (ke < to && IsIdentChar(src[ke]))
                    {
                        ke++;
                    }

                    if (ke < to && src[ke] == ']' && ke > ks)
                    {
                        var text = src[ks..ke];
                        suffix = new TplDim(ks, ke, Decode(text), isVar, !isVar && (char.IsAsciiDigit(text[^1]) || text[0] == '-') && char.IsAsciiDigit(text.TrimStart('-')[0]));
                        j = ke + 1;
                        count = 4;
                    }
                }
                else if (j + 2 < to && src[j] == '-' && src[j + 1] == '>' && IsIdentStart(src[j + 2]))
                {
                    var ps = j + 2;
                    var pe = ps;
                    while (pe < to && IsIdentChar(src[pe]))
                    {
                        pe++;
                    }

                    suffix = new TplProp(ps, pe, Decode(src[ps..pe]));
                    j = pe;
                    count = 3;
                }
                else if (j + 3 < to && src[j] == '?' && src[j + 1] == '-' && src[j + 2] == '>' && IsIdentStart(src[j + 3]))
                {
                    var ps = j + 3;
                    var pe = ps;
                    while (pe < to && IsIdentChar(src[pe]))
                    {
                        pe++;
                    }

                    suffix = new TplProp(ps, pe, Decode(src[ps..pe]));
                    j = pe;
                    count = 3;
                }

                _pos += count;
                parts.Add(new TplSimple(vs, j, name, varEnd, suffix));
                litStart = j;
                continue;
            }

            if ((c == '{' && j + 1 < to && src[j + 1] == '$') || (c == '$' && j + 1 < to && src[j + 1] == '{'))
            {
                FlushLiteral(j);
                var es = j;
                var inner = c == '{' ? j + 1 : j + 2;
                _pos++; // T_CURLY_OPEN / T_DOLLAR_OPEN_CURLY_BRACES
                var saveI = _i;
                var tokens = new List<Token>();
                _i = inner;
                var savedDoc = _pendingDoc;
                LexPhpInto(tokens, stopAtBrace: true, limit: to);
                _pendingDoc = savedDoc;
                var close = _i;
                _i = saveI;
                if (c == '$')
                {
                    // ${name} / ${expr}: treat a bare identifier as a variable name.
                    if (tokens.Count == 1 && tokens[0].Kind == TK.Name)
                    {
                        var nt = tokens[0];
                        tokens[0] = new Token { Kind = TK.Variable, Text = "$" + nt.Text, Start = nt.Start - 1, End = nt.End, Pos = nt.Pos };
                    }
                    else if (tokens.Count > 0 && tokens[0].Kind == TK.Name && tokens.Count > 1 && tokens[1].IsOp("["))
                    {
                        var nt = tokens[0];
                        tokens[0] = new Token { Kind = TK.Variable, Text = "$" + nt.Text, Start = nt.Start, End = nt.End, Pos = nt.Pos };
                    }
                }

                _pos++; // closing brace
                parts.Add(new TplExpr(es, Math.Min(close + 1, to), tokens));
                j = Math.Min(close + 1, to);
                litStart = j;
                continue;
            }

            j++;
        }

        FlushLiteral(to);
        return parts;
    }
}
