using System.Text;

namespace ScipSharp.Indexer.Java.Syntax;

public enum Tk { Ident, Number, String, Char, Op, Eof }

/// <summary>A lexical token. Offsets are UTF-16 code unit offsets into the source text.</summary>
public readonly record struct Token(Tk Kind, string Text, int Start, int End)
{
    public bool Is(string text) => Kind is Tk.Op or Tk.Ident && Text == text;
}

/// <summary>Maps offsets to zero-based (line, column) pairs in UTF-16 code units.</summary>
public sealed class LineMap
{
    private readonly List<int> _starts = [0];

    public LineMap(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') _starts.Add(i + 1);
            else if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                _starts.Add(i + 1);
            }
        }
    }

    public (int Line, int Col) Position(int offset)
    {
        var idx = _starts.BinarySearch(offset);
        if (idx < 0) idx = ~idx - 1;
        return (idx, offset - _starts[idx]);
    }
}

/// <summary>Hand written Java lexer (Java 21 incl. text blocks). Comments are skipped; '>' is always a single token.</summary>
public static class Lexer
{
    private static readonly string[] Ops3 = ["...", "<<=", "->", "::", "++", "--", "&&", "||", "==", "!=", "<=", "+=", "-=", "*=", "/=", "&=", "|=", "^=", "%=", "<<"];

    /// <param name="s">Java source text.</param>
    /// <param name="docs">When given, receives (token start, javadoc text) for every token preceded by a javadoc comment.</param>
    public static List<Token> Lex(string s, Dictionary<int, string>? docs = null)
    {
        var tokens = new List<Token>();
        var i = 0;
        string? pendingDoc = null;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n' && s[i] != '\r') i++;
                continue;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                var end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var commentEnd = end < 0 ? s.Length : end + 2;
                if (docs is not null && i + 2 < s.Length && s[i + 2] == '*') pendingDoc = ScanDocComment(s.AsSpan(i, commentEnd - i));
                i = commentEnd;
                continue;
            }
            var start = i;
            if (pendingDoc is not null) { docs![start] = pendingDoc; pendingDoc = null; }
            if (char.IsLetter(c) || c == '_' || c == '$' || char.IsSurrogate(c))
            {
                i++;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '$' || char.IsSurrogate(s[i]))) i++;
                tokens.Add(new Token(Tk.Ident, s[start..i], start, i));
            }
            else if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                i = LexNumber(s, i);
                tokens.Add(new Token(Tk.Number, s[start..i], start, i));
            }
            else if (c == '"')
            {
                if (i + 2 < s.Length && s[i + 1] == '"' && s[i + 2] == '"')
                {
                    i += 3;
                    while (i < s.Length && !(s[i] == '"' && i + 2 < s.Length && s[i + 1] == '"' && s[i + 2] == '"'))
                    {
                        if (s[i] == '\\') i++;
                        i++;
                    }
                    i = Math.Min(i + 3, s.Length);
                }
                else
                {
                    i++;
                    while (i < s.Length && s[i] != '"' && s[i] != '\n') { if (s[i] == '\\') i++; i++; }
                    i = Math.Min(i + 1, s.Length);
                }
                tokens.Add(new Token(Tk.String, s[start..i], start, i));
            }
            else if (c == '\'')
            {
                i++;
                while (i < s.Length && s[i] != '\'' && s[i] != '\n') { if (s[i] == '\\') i++; i++; }
                i = Math.Min(i + 1, s.Length);
                tokens.Add(new Token(Tk.Char, s[start..i], start, i));
            }
            else
            {
                var len = 1;
                foreach (var op in Ops3)
                {
                    if (string.CompareOrdinal(s, i, op, 0, op.Length) == 0) { len = op.Length; break; }
                }
                i += len;
                tokens.Add(new Token(Tk.Op, s[start..i], start, i));
            }
        }
        tokens.Add(new Token(Tk.Eof, "", s.Length, s.Length));
        return tokens;
    }

    private static int LexNumber(string s, int i)
    {
        var start = i;
        if (s[i] == '0' && i + 1 < s.Length && (s[i + 1] is 'x' or 'X' or 'b' or 'B'))
        {
            i += 2;
            while (i < s.Length && (char.IsAsciiHexDigit(s[i]) || s[i] is '_' or 'L' or 'l')) i++;
            if (s[start + 1] is 'x' or 'X')
            {
                // hexadecimal floating point literal: 0x1.8p3, 0x1p31, 0x.8p-2f
                var j = i;
                if (j < s.Length && s[j] == '.')
                {
                    j++;
                    while (j < s.Length && (char.IsAsciiHexDigit(s[j]) || s[j] == '_')) j++;
                }
                if (j < s.Length && s[j] is 'p' or 'P')
                {
                    j++;
                    if (j < s.Length && s[j] is '+' or '-') j++;
                    while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '_')) j++;
                    if (j < s.Length && s[j] is 'f' or 'F' or 'd' or 'D') j++;
                    return j;
                }
            }
            return i;
        }
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '_')) i++;
        if (i < s.Length && s[i] == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))
        {
            i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '_')) i++;
        }
        else if (i < s.Length && s[i] == '.' && !(i + 1 < s.Length && (char.IsLetter(s[i + 1]) || s[i + 1] == '.'))) i++;
        if (i < s.Length && s[i] is 'e' or 'E')
        {
            var j = i + 1;
            if (j < s.Length && s[j] is '+' or '-') j++;
            if (j < s.Length && char.IsDigit(s[j]))
            {
                i = j;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
        }
        if (i < s.Length && s[i] is 'f' or 'F' or 'd' or 'D' or 'l' or 'L') i++;
        return i;
    }

    /// <summary>
    /// The text of a javadoc comment exactly as javac's <c>JavaTokenizer</c> computes it for <c>getDocComment</c>: leading
    /// whitespace and <c>*</c> decoration are stripped from each line (the first line is stripped of all leading
    /// whitespace), line breaks are normalised to <c>\n</c> and trailing <c>*</c> characters are dropped.
    /// </summary>
    public static string ScanDocComment(ReadOnlySpan<char> c)
    {
        var n = c.Length;
        var p = 2;
        while (p < n && c[p] == '*') p++;
        if (p < n && c[p] == '/') return "";
        var first = true;
        if (p < n)
        {
            if (c[p] == '\n') { p++; first = false; }
            else if (c[p] == '\r') { p++; if (p < n && c[p] == '\n') p++; first = false; }
        }
        var sb = new StringBuilder();
        var done = false;
        while (p < n && !done)
        {
            var begin = p;
            while (p < n && c[p] is ' ' or '\t' or '\f') p++;
            if (p < n && c[p] == '*')
            {
                do p++; while (p < n && c[p] == '*');
                if (p < n && c[p] == '/') break;
            }
            else if (!first) p = begin;
            var eol = false;
            while (p < n && !eol)
            {
                var ch = c[p];
                switch (ch)
                {
                    case '*':
                        p++;
                        if (p < n && c[p] == '/') { done = true; eol = true; break; }
                        sb.Append('*');
                        break;
                    case '\f':
                        p++;
                        eol = true;
                        break;
                    case '\r':
                        p++;
                        if (p < n && c[p] == '\n') p++;
                        sb.Append('\n');
                        eol = true;
                        break;
                    case '\n':
                        p++;
                        sb.Append('\n');
                        eol = true;
                        break;
                    default:
                        sb.Append(ch);
                        p++;
                        break;
                }
            }
            first = false;
        }
        var len = sb.Length;
        while (len > 0 && sb[len - 1] == '*') len--;
        return sb.ToString(0, len);
    }
}
