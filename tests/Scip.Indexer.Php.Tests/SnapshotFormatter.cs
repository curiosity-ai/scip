#pragma warning disable CS0612 // Occurrence.Range is deprecated but still decoded when present
using System.Text;

namespace Scip.Indexer.Php.Tests;

/// <summary>
/// Port of the document snapshot renderer of <c>scip snapshot</c> (<c>bindings/go/scip/testutil/format.go</c>), which the
/// upstream scip-php golden files were produced with. Only the parts scip-php exercises (occurrences, documentation) are
/// implemented. Sources are handled as Latin-1 so that string indices equal UTF-8 byte offsets.
/// </summary>
internal static class SnapshotFormatter
{
    private const string Comment = "//";

    public static string Format(Document document, string sourceText)
    {
        var occurrences = document.Occurrences
            .Select((o, i) => (Occ: o, Index: i, Range: RangeOf(o)))
            .OrderBy(x => x.Range.StartLine)
            .ThenBy(x => x.Range.StartChar)
            .ThenBy(x => x.Range.EndLine)
            .ThenBy(x => x.Range.EndChar)
            .ThenBy(x => x.Index)
            .ToList();
        var symtab = document.Symbols.ToDictionary(s => s.Symbol);
        var b = new StringBuilder();
        var next = 0;
        var lines = sourceText.Split('\n');
        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            var rendered = lines[lineNumber].TrimEnd('\r').Replace('\t', ' ');
            b.Append(' ', Comment.Length).Append(rendered).Append('\n');
            while (next < occurrences.Count && occurrences[next].Range.StartLine == lineNumber)
            {
                var (occ, _, range) = occurrences[next];
                FormatOccurrence(b, occ, range, rendered, symtab);
                next++;
            }
        }

        return b.ToString();
    }

    private static void FormatOccurrence(StringBuilder b, Occurrence occ, Rng r, string renderedLine, Dictionary<string, SymbolInformation> symtab)
    {
        var isDefinition = (occ.SymbolRoles & (int)SymbolRole.Definition) != 0;
        b.Append(Comment).Append(' ', r.StartChar);
        var singleLine = r.StartLine == r.EndLine;
        var markerCount = singleLine ? r.EndChar - r.StartChar : renderedLine.Length - r.StartChar;
        if (markerCount < 1)
        {
            markerCount = 1;
        }

        b.Append('^', markerCount).Append(' ');
        b.Append(isDefinition ? "definition" : "reference").Append(' ');
        b.Append(SymbolFormatter.LenientVerbose.Format(occ.Symbol));
        if (!singleLine)
        {
            b.Append(' ').Append(r.EndLine - r.StartLine).Append(':').Append(r.EndChar);
        }

        var prefix = "\n" + Comment + new string(' ', r.StartChar);
        if (isDefinition && symtab.TryGetValue(occ.Symbol, out var info))
        {
            foreach (var documentation in info.Documentation)
            {
                b.Append(prefix).Append("documentation");
                foreach (var line in documentation.Trim().Split('\n'))
                {
                    b.Append(prefix).Append("> ").Append(line);
                }
            }
        }

        b.Append('\n');
    }

    private readonly record struct Rng(int StartLine, int StartChar, int EndLine, int EndChar);

    private static Rng RangeOf(Occurrence o) => o.TypedRangeCase switch
    {
        Occurrence.TypedRangeOneofCase.SingleLineRange => new Rng(o.SingleLineRange.Line, o.SingleLineRange.StartCharacter, o.SingleLineRange.Line, o.SingleLineRange.EndCharacter),
        Occurrence.TypedRangeOneofCase.MultiLineRange => new Rng(o.MultiLineRange.StartLine, o.MultiLineRange.StartCharacter, o.MultiLineRange.EndLine, o.MultiLineRange.EndCharacter),
        _ => o.Range.Count == 3
            ? new Rng(o.Range[0], o.Range[1], o.Range[0], o.Range[2])
            : new Rng(o.Range[0], o.Range[1], o.Range[2], o.Range[3]),
    };
}
