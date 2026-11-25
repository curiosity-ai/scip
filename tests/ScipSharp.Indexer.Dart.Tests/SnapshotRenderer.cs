using System.Text;

namespace ScipSharp.Indexer.Dart.Tests;

/// <summary>Renders a document in the `scip snapshot` text format (comment lines under each source line).</summary>
internal static class SnapshotRenderer
{
    public static string Render(Document doc, string source, Func<string, bool>? includeSymbol = null)
    {
        var lines = source.Split('\n');
        var byLine = doc.Occurrences
            .Select(o => (Occ: o, R: RangeOf(o)))
            .GroupBy(x => x.R.Line)
            .ToDictionary(g => g.Key, g => g.ToList());
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            sb.Append("  ").AppendLine(lines[i].TrimEnd('\r'));
            if (!byLine.TryGetValue(i, out var occs))
            {
                continue;
            }

            foreach (var (occ, r) in occs.OrderBy(x => x.R.Start))
            {
                if (includeSymbol is not null && !includeSymbol(occ.Symbol))
                {
                    continue;
                }

                var role = (occ.SymbolRoles & (int)SymbolRole.Definition) != 0 ? "definition" : "reference";
                if (r.End == r.Start)
                {
                    sb.Append("// ").Append(role).Append(' ').AppendLine(occ.Symbol);
                    continue;
                }

                sb.Append("//").Append(' ', r.Start).Append('^', r.End - r.Start).Append(' ').Append(role).Append(' ')
                    .AppendLine(occ.Symbol);
            }
        }

        return sb.ToString();
    }

    public static (int Line, int Start, int End) RangeOf(Occurrence o) => o.TypedRangeCase switch
    {
        Occurrence.TypedRangeOneofCase.SingleLineRange => (o.SingleLineRange.Line, o.SingleLineRange.StartCharacter, o.SingleLineRange.EndCharacter),
        Occurrence.TypedRangeOneofCase.MultiLineRange => (o.MultiLineRange.StartLine, o.MultiLineRange.StartCharacter, o.MultiLineRange.StartCharacter + 1),
        _ => (0, 0, 0),
    };
}
