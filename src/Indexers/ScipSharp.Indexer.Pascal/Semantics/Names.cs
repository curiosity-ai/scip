using System.Text;

namespace ScipSharp.Indexer.Pascal.Semantics;

/// <summary>SCIP descriptor spelling helpers.</summary>
internal static class Names
{
    public static string Escape(string name)
    {
        if (name.Length > 0 && name.All(static c => c is '_' or '+' or '-' or '$' || char.IsAsciiLetterOrDigit(c)))
        {
            return name;
        }

        return "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`";
    }

    public static string Namespace(string name) => Escape(name) + "/";

    public static string Type(string name) => Escape(name) + "#";

    public static string Term(string name) => Escape(name) + ".";

    public static string Method(string name, int overload) =>
        Escape(name) + (overload == 0 ? "()." : $"(+{overload.ToString(System.Globalization.CultureInfo.InvariantCulture)}).");

    public static string TypeParameter(string name) => "[" + Escape(name) + "]";

    /// <summary>Collapses whitespace and strips comments from a source slice (for signatures).</summary>
    public static string CleanSignature(string text, int maxLength = 400)
    {
        var b = new StringBuilder(text.Length);
        var i = 0;
        var pendingSpace = false;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\'')
            {
                var end = text.IndexOf('\'', i + 1);
                end = end < 0 ? text.Length - 1 : end;
                Flush();
                b.Append(text, i, end - i + 1);
                i = end + 1;
                continue;
            }

            if (c == '{' && (i + 1 >= text.Length || text[i + 1] != '$'))
            {
                var end = text.IndexOf('}', i + 1);
                i = end < 0 ? text.Length : end + 1;
                pendingSpace = true;
                continue;
            }

            if (c == '{')
            {
                var end = text.IndexOf('}', i + 1);
                i = end < 0 ? text.Length : end + 1;
                continue;
            }

            if (c == '(' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*)", i + 2, StringComparison.Ordinal);
                i = end < 0 ? text.Length : end + 2;
                pendingSpace = true;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                var end = text.IndexOf('\n', i);
                i = end < 0 ? text.Length : end;
                pendingSpace = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                i++;
                continue;
            }

            Flush();
            b.Append(c);
            i++;
        }

        var s = b.ToString().Trim();
        return s.Length > maxLength ? s[..maxLength] + " …" : s;

        void Flush()
        {
            if (pendingSpace && b.Length > 0)
            {
                b.Append(' ');
            }

            pendingSpace = false;
        }
    }
}
