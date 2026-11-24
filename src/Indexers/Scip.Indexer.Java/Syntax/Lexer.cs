using System.Text;

namespace Scip.Indexer.Java.Syntax;

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

    public static List<Token> Lex(string s)
    {
        var tokens = new List<Token>();
        var i = 0;
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
                i = end < 0 ? s.Length : end + 2;
                continue;
            }
            var start = i;
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
        if (s[i] == '0' && i + 1 < s.Length && (s[i + 1] is 'x' or 'X' or 'b' or 'B'))
        {
            i += 2;
            while (i < s.Length && (char.IsAsciiHexDigit(s[i]) || s[i] is '_' or 'L' or 'l')) i++;
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

    /// <summary>Finds the javadoc comment that ends right before <paramref name="declStart"/> (whitespace only in between).</summary>
    public static string? DocBefore(string s, int declStart)
    {
        var i = declStart - 1;
        while (i >= 0 && char.IsWhiteSpace(s[i])) i--;
        if (i < 1 || s[i] != '/' || s[i - 1] != '*') return null;
        var start = s.LastIndexOf("/**", i - 1, StringComparison.Ordinal);
        if (start < 0 || start + 2 > i - 1 && start + 3 > i) return null;
        var body = s[(start + 3)..(i - 1)];
        return CleanDoc(body);
    }

    /// <summary>Strips leading <c>*</c> decoration and surrounding whitespace the way javac's doc comment API does.</summary>
    public static string CleanDoc(string body)
    {
        var sb = new StringBuilder();
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var n = 0; n < lines.Length; n++)
        {
            var line = lines[n];
            if (n > 0)
            {
                var t = line.TrimStart();
                line = t.StartsWith('*') ? t[1..] : line;
            }
            sb.Append(line.TrimEnd());
            if (n < lines.Length - 1) sb.Append('\n');
        }
        return sb.ToString().Trim(' ', '\n');
    }
}
