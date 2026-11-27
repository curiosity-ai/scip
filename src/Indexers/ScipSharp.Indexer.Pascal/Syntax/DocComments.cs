using System.Text;

namespace ScipSharp.Indexer.Pascal.Syntax;

/// <summary>Finds the comment block documenting a declaration (adjacent comments above it, or a trailing same-line comment).</summary>
internal static class DocComments
{
    /// <summary>
    /// Comments between <paramref name="previousEnd"/> and <paramref name="declStart"/> that form a block directly above the
    /// declaration (each on its own line, separated by at most one line break).
    /// </summary>
    public static string? Above(SourceText src, int previousEnd, int declStart)
    {
        var comments = src.Comments;
        var hi = LastEndingBefore(comments, declStart);
        if (hi < 0)
        {
            return null;
        }

        var block = new List<Comment>();
        var nextStart = declStart;
        for (var i = hi; i >= 0; i--)
        {
            var c = comments[i];
            if (c.Start < previousEnd)
            {
                break;
            }

            var gap = src.Text.AsSpan(c.End, nextStart - c.End);
            if (!IsWhitespace(gap) || Count(gap, '\n') > 1)
            {
                break;
            }

            if (!IsWhitespace(src.LinePrefix(c.Start)))
            {
                break; // trailing comment of the previous line
            }

            block.Add(c);
            nextStart = c.Start;
        }

        if (block.Count == 0)
        {
            return null;
        }

        block.Reverse();
        return Clean(block.Select(static c => c.Text));
    }

    /// <summary>A comment that starts on the line of <paramref name="declEnd"/> after it (and before <paramref name="nextStart"/>).</summary>
    public static string? Trailing(SourceText src, int declEnd, int nextStart)
    {
        var comments = src.Comments;
        var idx = FirstStartingAtOrAfter(comments, declEnd);
        if (idx >= comments.Count)
        {
            return null;
        }

        var c = comments[idx];
        if (c.Start >= nextStart && nextStart > declEnd)
        {
            return null;
        }

        var gap = src.Text.AsSpan(declEnd, c.Start - declEnd);
        if (!IsWhitespace(gap) || gap.Contains('\n'))
        {
            return null;
        }

        return Clean([c.Text]);
    }

    private static int LastEndingBefore(List<Comment> comments, int pos)
    {
        int lo = 0, hi = comments.Count - 1, result = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (comments[mid].End <= pos)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    private static int FirstStartingAtOrAfter(List<Comment> comments, int pos)
    {
        int lo = 0, hi = comments.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (comments[mid].Start < pos)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private static bool IsWhitespace(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    private static int Count(ReadOnlySpan<char> s, char ch)
    {
        var n = 0;
        foreach (var c in s)
        {
            if (c == ch)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Strips comment delimiters and decoration; returns null for empty or separator-only comments.</summary>
    public static string? Clean(IEnumerable<string> comments)
    {
        var lines = new List<string>();
        foreach (var raw in comments)
        {
            var text = raw;
            if (text.StartsWith("//", StringComparison.Ordinal))
            {
                text = text.TrimStart('/');
                if (text.StartsWith('<'))
                {
                    text = text[1..];
                }
            }
            else if (text.StartsWith("(*", StringComparison.Ordinal))
            {
                text = text[2..];
                text = text.EndsWith("*)", StringComparison.Ordinal) ? text[..^2] : text;
            }
            else if (text.StartsWith('{'))
            {
                text = text[1..];
                text = text.EndsWith('}') ? text[..^1] : text;
            }

            foreach (var line in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
            {
                var l = line.Trim();
                l = l.TrimStart('*').TrimEnd('*').Trim();
                if (l.Length > 0 && l.All(static c => c is '-' or '=' or '*' or '~' or '#' or '_' or '/' or '+'))
                {
                    continue;
                }

                lines.Add(l);
            }
        }

        while (lines.Count > 0 && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var b = new StringBuilder();
        foreach (var l in lines)
        {
            if (b.Length > 0)
            {
                b.Append('\n');
            }

            b.Append(l);
        }

        return b.ToString();
    }
}
