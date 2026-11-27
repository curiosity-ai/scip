using System.Text;

namespace ScipSharp.Indexer.Fortran.Syntax;

/// <summary>Helpers over a statement's token list.</summary>
internal static class Toks
{
    /// <summary>Index of the bracket closing the one at <paramref name="open"/> (or the list end when unbalanced).</summary>
    public static int Match(List<Token> t, int open)
    {
        var depth = 0;
        for (var i = open; i < t.Count; i++)
        {
            if (t[i].Is("(") || t[i].Is("["))
            {
                depth++;
            }
            else if (t[i].Is(")") || t[i].Is("]"))
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return t.Count;
    }

    /// <summary>Splits [<paramref name="a"/>, <paramref name="b"/>) at top-level commas.</summary>
    public static List<(int Start, int End)> SplitTop(List<Token> t, int a, int b)
    {
        var result = new List<(int, int)>();
        var depth = 0;
        var start = a;
        b = Math.Min(b, t.Count);
        for (var i = a; i < b; i++)
        {
            var tok = t[i];
            if (tok.Is("(") || tok.Is("["))
            {
                depth++;
            }
            else if (tok.Is(")") || tok.Is("]"))
            {
                depth--;
            }
            else if (depth == 0 && tok.Is(","))
            {
                result.Add((start, i));
                start = i + 1;
            }
        }

        if (start < b)
        {
            result.Add((start, b));
        }

        return result;
    }

    /// <summary>Index of the first top-level token equal to <paramref name="op"/> in [a, b), or -1.</summary>
    public static int FindTop(List<Token> t, int a, int b, string op)
    {
        var depth = 0;
        b = Math.Min(b, t.Count);
        for (var i = a; i < b; i++)
        {
            var tok = t[i];
            if (tok.Is("(") || tok.Is("["))
            {
                depth++;
            }
            else if (tok.Is(")") || tok.Is("]"))
            {
                depth--;
            }
            else if (depth == 0 && tok.Is(op))
            {
                return i;
            }
        }

        return -1;
    }

    public static bool IsKw(List<Token> t, int i, string kw) => i >= 0 && i < t.Count && t[i].IsKw(kw);

    public static bool IsOp(List<Token> t, int i, string op) => i >= 0 && i < t.Count && t[i].Is(op);

    public static bool IsName(List<Token> t, int i) => i >= 0 && i < t.Count && t[i].IsName;

    public static string Lower(List<Token> t, int i) => i >= 0 && i < t.Count && t[i].IsName ? t[i].Lower : "";

    /// <summary>
    /// True when the statement starting at <paramref name="i"/> is an assignment or pointer assignment: a designator
    /// (name, subscripts, <c>%</c> components, coindices) followed by <c>=</c> or <c>=&gt;</c>.
    /// </summary>
    public static bool IsAssignment(List<Token> t, int i)
    {
        if (!IsName(t, i))
        {
            return false;
        }

        var j = i + 1;
        while (j < t.Count)
        {
            if (t[j].Is("(") || t[j].Is("["))
            {
                j = Match(t, j) + 1;
            }
            else if (t[j].Is("%") && IsName(t, j + 1))
            {
                j += 2;
            }
            else
            {
                break;
            }
        }

        return j < t.Count && (t[j].Is("=") || t[j].Is("=>"));
    }

    /// <summary>Source-like text of tokens [a, b): a blank wherever the original had a gap or a line break.</summary>
    public static string Text(List<Token> t, int a, int b)
    {
        var sb = new StringBuilder();
        b = Math.Min(b, t.Count);
        for (var i = a; i < b; i++)
        {
            if (t[i].IsKindParam)
            {
                sb.Append('_');
            }
            else if (i > a && (t[i].Line != t[i - 1].Line || t[i].Col > t[i - 1].EndCol))
            {
                sb.Append(' ');
            }

            sb.Append(t[i].Text);
        }

        return sb.ToString();
    }

    /// <summary>Splits a single-line token into two at <paramref name="at"/> characters (fixed-form keyword gluing).</summary>
    public static (Token First, Token Second)? Split(Token tok, int at, TokenKind secondKind)
    {
        if (at <= 0 || at >= tok.Text.Length || tok.EndCol - tok.Col != tok.Text.Length)
        {
            return null;
        }

        var first = new Token(TokenKind.Name, tok.Text[..at], tok.File, tok.Line, tok.Col, tok.Col + at);
        var second = new Token(secondKind, tok.Text[at..], tok.File, tok.Line, tok.Col + at, tok.EndCol);
        return (first, second);
    }
}
