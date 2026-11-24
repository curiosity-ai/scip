using System.Text;

namespace Scip.Indexer.Clang;

public enum TokKind { Ident, Number, StringLiteral, CharLiteral, Punct }

/// <summary>A C/C++ token. <see cref="Line"/> is 0-based, <see cref="Col"/> and <see cref="Len"/> are UTF-8 byte counts.</summary>
public readonly record struct Tok(TokKind Kind, string Text, int File, int Line, int Col, int Len, bool Bol, bool Space, bool Macro = false)
{
    public bool Is(string text) => Kind is TokKind.Punct or TokKind.Ident && Text == text;

    public bool IsIdent => Kind == TokKind.Ident;
}

/// <summary>Native C/C++ lexer over UTF-8 bytes (positions are therefore already in SCIP's UTF-8 code-unit encoding).</summary>
public static class Lexer
{
    private static readonly string[] Multi =
    [
        "...", "->*", "::", "->", "++", "--", "&&", "||", "==", "!=", "<=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<", "##", ".*",
    ];

    private static readonly HashSet<string> StringPrefixes = ["L", "u", "U", "u8", "R", "LR", "uR", "UR", "u8R"];

    private static bool IsIdStart(byte b) => b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or (byte)'_' or (byte)'$' or >= 0x80;

    private static bool IsIdPart(byte b) => IsIdStart(b) || b is >= (byte)'0' and <= (byte)'9';

    public static List<Tok> Lex(byte[] s, int file)
    {
        var toks = new List<Tok>();
        int i = 0, line = 0, lineStart = 0;
        var bol = true;
        var space = false;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '\n')
            {
                i++;
                line++;
                lineStart = i;
                bol = true;
                space = true;
                continue;
            }

            if (c is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\f' or (byte)'\v')
            {
                i++;
                space = true;
                continue;
            }

            if (c == '\\' && i + 1 < s.Length && (s[i + 1] == '\n' || (s[i + 1] == '\r' && i + 2 < s.Length && s[i + 2] == '\n')))
            {
                i += s[i + 1] == '\n' ? 2 : 3;
                line++;
                lineStart = i;
                space = true;
                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n')
                {
                    if (s[i] == '\\' && i + 1 < s.Length && s[i + 1] == '\n')
                    {
                        i++;
                        line++;
                        lineStart = i + 1;
                    }

                    i++;
                }

                space = true;
                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/'))
                {
                    if (s[i] == '\n')
                    {
                        line++;
                        lineStart = i + 1;
                    }

                    i++;
                }

                i = Math.Min(s.Length, i + 2);
                space = true;
                continue;
            }

            var start = i;
            var startLine = line;
            var col = i - lineStart;
            TokKind kind;
            string text;
            if (IsIdStart(c))
            {
                while (i < s.Length && IsIdPart(s[i]))
                {
                    i++;
                }

                text = Encoding.UTF8.GetString(s, start, i - start);
                if (i < s.Length && (s[i] == '"' || s[i] == '\'') && StringPrefixes.Contains(text))
                {
                    var raw = text.EndsWith('R') && s[i] == '"';
                    i = raw ? SkipRaw(s, i, ref line, ref lineStart) : SkipQuoted(s, i, s[i]);
                    kind = s[start + text.Length] == '"' ? TokKind.StringLiteral : TokKind.CharLiteral;
                    text = Encoding.UTF8.GetString(s, start, i - start);
                }
                else
                {
                    kind = TokKind.Ident;
                }
            }
            else if (c is >= (byte)'0' and <= (byte)'9' || (c == '.' && i + 1 < s.Length && s[i + 1] is >= (byte)'0' and <= (byte)'9'))
            {
                i++;
                while (i < s.Length && (IsIdPart(s[i]) || s[i] == '.' || s[i] == '\''
                    || ((s[i] == '+' || s[i] == '-') && s[i - 1] is (byte)'e' or (byte)'E' or (byte)'p' or (byte)'P')))
                {
                    i++;
                }

                kind = TokKind.Number;
                text = Encoding.UTF8.GetString(s, start, i - start);
            }
            else if (c == '"' || c == '\'')
            {
                i = SkipQuoted(s, i, c);
                kind = c == '"' ? TokKind.StringLiteral : TokKind.CharLiteral;
                text = Encoding.UTF8.GetString(s, start, i - start);
            }
            else
            {
                kind = TokKind.Punct;
                text = ((char)c).ToString();
                foreach (var m in Multi)
                {
                    if (Matches(s, i, m))
                    {
                        text = m;
                        break;
                    }
                }

                i += text.Length;
            }

            toks.Add(new Tok(kind, text, file, startLine, col, i - start, bol, space));
            bol = false;
            space = false;
        }

        return toks;
    }

    private static bool Matches(byte[] s, int i, string m)
    {
        if (i + m.Length > s.Length)
        {
            return false;
        }

        for (var k = 0; k < m.Length; k++)
        {
            if (s[i + k] != m[k])
            {
                return false;
            }
        }

        return true;
    }

    private static int SkipQuoted(byte[] s, int i, byte quote)
    {
        i++;
        while (i < s.Length && s[i] != quote && s[i] != '\n')
        {
            i += s[i] == '\\' && i + 1 < s.Length ? 2 : 1;
        }

        return Math.Min(s.Length, i + 1);
    }

    private static int SkipRaw(byte[] s, int i, ref int line, ref int lineStart)
    {
        var d = i + 1;
        var open = Array.IndexOf(s, (byte)'(', d);
        if (open < 0)
        {
            return s.Length;
        }

        var closer = ")" + Encoding.UTF8.GetString(s, d, open - d) + "\"";
        for (var k = open + 1; k < s.Length; k++)
        {
            if (s[k] == '\n')
            {
                line++;
                lineStart = k + 1;
            }

            if (s[k] == ')' && Matches(s, k, closer))
            {
                return k + closer.Length;
            }
        }

        return s.Length;
    }
}
