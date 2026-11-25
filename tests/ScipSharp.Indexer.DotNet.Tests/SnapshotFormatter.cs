using System.Text;
using Index = ScipSharp.Index;

namespace ScipSharp.Indexer.DotNet.Tests;

/// <summary>Renders a SCIP document as annotated source, identical to the upstream snapshot format.</summary>
internal static class SnapshotFormatter
{
    public static string FormatDocument(Index index, Document document, string projectDirectory)
    {
        _ = index;
        var commentChar = document.Language == "C#" ? "//" : "'";
        var sb = new StringBuilder();
        var inputPath = Path.Combine(projectDirectory, document.RelativePath);
        var occurrences = document.Occurrences.OrderBy(Range.FromOccurrence).ToList();
        var symtab = new Dictionary<string, SymbolInformation>();
        foreach (var info in document.Symbols)
        {
            // Conflicting SymbolInformation is a bug.
            if (!symtab.TryAdd(info.Symbol, info))
            {
                throw new InvalidOperationException($"duplicate SymbolInformation '{info.Symbol}'");
            }
        }

        var occurrenceIndex = 0;
        var lines = File.ReadAllLines(inputPath);
        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            var line = lines[lineNumber].Replace("\t", " ");
            sb.Append(line.Length > 0 ? "  " : "").AppendLine(line);
            while (occurrenceIndex < occurrences.Count)
            {
                var occurrence = occurrences[occurrenceIndex];
                var range = Range.FromOccurrence(occurrence);
                if (range.Start.Line != range.End.Line)
                {
                    occurrenceIndex++;
                    continue; // multi-line occurrences are not rendered
                }

                if (range.Start.Line != lineNumber)
                {
                    break;
                }

                var isDefinition = (occurrence.SymbolRoles & (int)SymbolRole.Definition) != 0;
                var length = range.End.Character - range.Start.Character;
                var indent = new string(' ', range.Start.Character);
                if (document.Language == "Visual Basic")
                {
                    indent += " ";
                }

                sb.Append(commentChar).Append(indent).Append('^', length)
                    .Append(' ').Append(isDefinition ? "definition" : "reference").Append(' ')
                    .AppendLine(occurrence.Symbol);

                if (isDefinition)
                {
                    var info = symtab.GetValueOrDefault(occurrence.Symbol, new SymbolInformation());
                    var prefix = commentChar + indent + new string(' ', length + 1);
                    foreach (var documentation in info.Documentation)
                    {
                        sb.Append(prefix).Append("documentation ").AppendLine(documentation.Replace("\n", "\\n"));
                    }

                    foreach (var relationship in info.Relationships)
                    {
                        sb.Append(prefix).Append("relationship ");
                        if (relationship.IsDefinition) sb.Append("definition ");
                        if (relationship.IsImplementation) sb.Append("implementation ");
                        if (relationship.IsReference) sb.Append("reference ");
                        if (relationship.IsTypeDefinition) sb.Append("type_definition ");
                        sb.AppendLine(relationship.Symbol);
                    }
                }

                occurrenceIndex++;
            }
        }

        return sb.ToString();
    }
}

internal readonly record struct Position(int Line, int Character) : IComparable<Position>
{
    public int CompareTo(Position other) => Line != other.Line ? Line - other.Line : Character - other.Character;
}

internal readonly record struct Range(Position Start, Position End) : IComparable<Range>
{
    public int CompareTo(Range other) => Start.CompareTo(other.Start) is var c and not 0 ? c : End.CompareTo(other.End);

    /// <summary>Reads the typed range encoding.</summary>
    public static Range FromOccurrence(Occurrence occ) => occ.TypedRangeCase switch
    {
        Occurrence.TypedRangeOneofCase.SingleLineRange => new Range(
            new Position(occ.SingleLineRange.Line, occ.SingleLineRange.StartCharacter),
            new Position(occ.SingleLineRange.Line, occ.SingleLineRange.EndCharacter)),
        Occurrence.TypedRangeOneofCase.MultiLineRange => new Range(
            new Position(occ.MultiLineRange.StartLine, occ.MultiLineRange.StartCharacter),
            new Position(occ.MultiLineRange.EndLine, occ.MultiLineRange.EndCharacter)),
        _ => throw new ArgumentException("occurrence has no typed range"),
    };
}
