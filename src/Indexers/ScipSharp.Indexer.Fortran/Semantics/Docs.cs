namespace ScipSharp.Indexer.Fortran.Semantics;

/// <summary>Normalises FORD (<c>!!</c>, <c>!&gt;</c>) and Doxygen (<c>!&gt;</c>, <c>*&gt;</c>, <c>!&lt;</c>) doc comment lines.</summary>
internal static class Docs
{
    private static readonly string[] DroppedCommands = [@"\verbatim", @"\endverbatim", @"\addtogroup", @"\ingroup", "@addtogroup", "@ingroup"];

    public static string Clean(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return "";
        }

        var kept = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (DroppedCommands.Any(c => trimmed.StartsWith(c, StringComparison.Ordinal)))
            {
                continue;
            }

            if (trimmed.StartsWith(@"\brief ", StringComparison.Ordinal) || trimmed.StartsWith("@brief ", StringComparison.Ordinal))
            {
                line = trimmed[7..];
            }

            kept.Add(line);
        }

        while (kept.Count > 0 && kept[0].Length == 0)
        {
            kept.RemoveAt(0);
        }

        while (kept.Count > 0 && kept[^1].Length == 0)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        var indent = kept.Where(static l => l.Length > 0).Select(static l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join('\n', kept.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
    }
}
