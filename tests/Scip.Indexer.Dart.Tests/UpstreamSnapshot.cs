namespace Scip.Indexer.Dart.Tests;

/// <summary>Reads definitions out of a `scip snapshot` text file as (source line, column, symbol) triples.</summary>
internal static class UpstreamSnapshot
{
    /// <summary>
    /// Definitions of non-local symbols declared in the indexed package. Local symbols are skipped because their
    /// numbering depends on local variables, which this indexer does not track.
    /// </summary>
    public static HashSet<string> Definitions(string snapshot, string role = "definition")
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var line = -1;
        foreach (var raw in snapshot.Split('\n'))
        {
            var l = raw.TrimEnd('\r');
            if (!l.StartsWith("//", StringComparison.Ordinal))
            {
                line++;
                continue;
            }

            var caret = l.IndexOf('^');
            if (caret < 0)
            {
                continue;
            }

            var rest = l[caret..].TrimStart('^').TrimStart();
            if (!rest.StartsWith(role + " ", StringComparison.Ordinal))
            {
                continue;
            }

            var symbol = rest[(role.Length + 1)..];
            if (symbol.StartsWith("local ", StringComparison.Ordinal) || symbol.EndsWith("`pubspec.yaml`/", StringComparison.Ordinal))
            {
                continue;
            }

            // file symbols (document-level) end with a bare namespace; keep all others
            result.Add($"{line}:{caret - 2} {symbol}");
        }

        return result;
    }
}
