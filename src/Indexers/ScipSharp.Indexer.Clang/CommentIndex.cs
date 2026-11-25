using System.Text;

namespace ScipSharp.Indexer.Clang;

/// <summary>
/// Raw comments of one source file, used to attach documentation to definitions the way upstream's
/// <c>showDocs</c> does: the comment block directly above a declaration plus a trailing comment on its line.
/// </summary>
public sealed class CommentIndex
{
    private sealed record Comment(int StartLine, int EndLine, int StartCol, bool OwnLine, List<string> Lines);

    private readonly List<Comment> _comments = [];
    private readonly Dictionary<int, Comment> _byEndLine = [];

    public CommentIndex(string text)
    {
        var lines = text.Split('\n');
        var inBlock = false;
        List<string>? block = null;
        var blockStart = 0;
        var blockCol = 0;
        var blockOwn = false;
        for (var ln = 0; ln < lines.Length; ln++)
        {
            var line = lines[ln].TrimEnd('\r');
            var i = 0;
            while (i < line.Length)
            {
                if (inBlock)
                {
                    var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        block!.Add(StripBlockLine(line[i..]));
                        i = line.Length;
                    }
                    else
                    {
                        block!.Add(StripBlockLine(line[i..end]));
                        Add(new Comment(blockStart, ln, blockCol, blockOwn, Clean(block, block: true)));
                        inBlock = false;
                        i = end + 2;
                    }

                    continue;
                }

                var c = line[i];
                if (c == '"' || c == '\'')
                {
                    i = SkipQuoted(line, i);
                    continue;
                }

                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    var own = line[..i].Trim().Length == 0;
                    Add(new Comment(ln, ln, i, own, Clean([line[(i + 2)..]], block: false)));
                    i = line.Length;
                    continue;
                }

                if (c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                {
                    blockOwn = line[..i].Trim().Length == 0;
                    blockStart = ln;
                    blockCol = i;
                    block = [];
                    inBlock = true;
                    i += 2;
                    continue;
                }

                i++;
            }

            if (inBlock && i >= line.Length && block!.Count == 0)
            {
                block.Add("");
            }
        }
    }

    public static CommentIndex? TryLoad(string path)
    {
        try
        {
            return new CommentIndex(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Documentation for a declaration whose name token is at <paramref name="line"/> (0-based).</summary>
    public string? DocFor(int line, int col, int declStartLine)
    {
        var parts = new List<string>();
        var cur = declStartLine - 1;
        var lead = new List<List<string>>();
        while (cur >= 0 && _byEndLine.TryGetValue(cur, out var c) && c.OwnLine)
        {
            // a comment right below a line that has a trailing comment continues that comment, not ours
            if (c.StartLine - 1 >= 0 && _byEndLine.TryGetValue(c.StartLine - 1, out var above) && !above.OwnLine && c.StartLine == c.EndLine && _comments.Any(x => x.StartLine == c.EndLine + 1))
            {
                break;
            }

            lead.Add(c.Lines);
            cur = c.StartLine - 1;
        }

        lead.Reverse();
        foreach (var l in lead)
        {
            parts.AddRange(l);
        }

        foreach (var c in _comments)
        {
            if (c.StartLine == line && !c.OwnLine && c.StartCol > col)
            {
                parts.AddRange(c.Lines);
                if (_byEndLine.TryGetValue(c.EndLine + 1, out var next) && next.OwnLine && next.StartLine == next.EndLine && _comments.Any(x => x.StartLine == next.EndLine + 1))
                {
                    parts.AddRange(next.Lines);
                }

                break;
            }
        }

        parts = [.. parts.Where(static p => p.Length > 0)];
        return parts.Count == 0 ? null : string.Join('\n', parts);
    }

    private void Add(Comment c)
    {
        _comments.Add(c);
        _byEndLine[c.EndLine] = c;
    }

    private static int SkipQuoted(string line, int i)
    {
        var q = line[i];
        i++;
        while (i < line.Length && line[i] != q)
        {
            i += line[i] == '\\' ? 2 : 1;
        }

        return i + 1;
    }

    private static string StripBlockLine(string s)
    {
        var t = s.TrimStart();
        if (t.StartsWith('*'))
        {
            t = t[1..];
        }

        return t;
    }

    private static List<string> Clean(List<string> raw, bool block)
    {
        var res = new List<string>();
        for (var k = 0; k < raw.Count; k++)
        {
            var s = raw[k];
            if (!block || k == 0)
            {
                // doc markers: "///", "//!", "/**", "/*!", "///<", "//!<"
                if (s.StartsWith('/') || s.StartsWith('!') || (block && s.StartsWith('*')))
                {
                    s = s[1..];
                }

                if (s.StartsWith('<'))
                {
                    s = s[1..];
                }
            }

            s = s.Trim();
            res.Add(s);
        }

        return res;
    }
}
