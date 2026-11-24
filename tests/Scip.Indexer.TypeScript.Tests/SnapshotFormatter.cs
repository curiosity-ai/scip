using System.Text;

namespace Scip.Indexer.TypeScript.Tests;

/// <summary>Port of upstream <c>SnapshotTesting.ts</c> (<c>formatSnapshot</c>), the text format of the snapshot files.</summary>
public static class SnapshotFormatter
{
    private const string StripIndexerPrefix = "scip-typescript npm ";
    private const string CommentSyntax = "//";
    private const string FormatOptionsPrefix = "// format-options:";

    public readonly record struct R(int StartLine, int StartCharacter, int EndLine, int EndCharacter) : IComparable<R>
    {
        public bool IsSingleLine => StartLine == EndLine;

        public int CompareTo(R other)
        {
            if (StartLine != other.StartLine) { return StartLine - other.StartLine; }
            if (StartCharacter != other.StartCharacter) { return StartCharacter - other.StartCharacter; }
            if (EndLine != other.EndLine) { return EndLine - other.EndLine; }
            return EndCharacter - other.EndCharacter;
        }
    }

    public static R RangeOf(Occurrence o) => o.TypedRangeCase switch
    {
        Occurrence.TypedRangeOneofCase.SingleLineRange => new R(o.SingleLineRange.Line, o.SingleLineRange.StartCharacter, o.SingleLineRange.Line, o.SingleLineRange.EndCharacter),
        Occurrence.TypedRangeOneofCase.MultiLineRange => new R(o.MultiLineRange.StartLine, o.MultiLineRange.StartCharacter, o.MultiLineRange.EndLine, o.MultiLineRange.EndCharacter),
        _ => throw new InvalidOperationException($"occurrence of '{o.Symbol}' has no range"),
    };

    public static R? EnclosingRangeOf(Occurrence o) => o.TypedEnclosingRangeCase switch
    {
        Occurrence.TypedEnclosingRangeOneofCase.SingleLineEnclosingRange => new R(o.SingleLineEnclosingRange.Line, o.SingleLineEnclosingRange.StartCharacter, o.SingleLineEnclosingRange.Line, o.SingleLineEnclosingRange.EndCharacter),
        Occurrence.TypedEnclosingRangeOneofCase.MultiLineEnclosingRange => new R(o.MultiLineEnclosingRange.StartLine, o.MultiLineEnclosingRange.StartCharacter, o.MultiLineEnclosingRange.EndLine, o.MultiLineEnclosingRange.EndCharacter),
        _ => null,
    };

    private static string Strip(string name) => name.StartsWith(StripIndexerPrefix, StringComparison.Ordinal) ? name[StripIndexerPrefix.Length..] : name;

    public static string Format(string inputText, Document doc, IReadOnlyList<SymbolInformation>? externalSymbols = null)
    {
        var lines = inputText.Split('\n');
        var output = new StringBuilder($"{CommentSyntax} language {doc.Language}\n");
        var symbolTable = new Dictionary<string, SymbolInformation>();
        foreach (var s in doc.Symbols) { symbolTable[s.Symbol] = s; }
        var externalTable = new Dictionary<string, SymbolInformation>();
        foreach (var s in externalSymbols ?? []) { externalTable[s.Symbol] = s; }

        var (showDocs, showKinds, showRanges) = ParseOptions(lines);
        var enclosing = new List<(R Range, string Symbol)>();
        var withDefinitions = new HashSet<string>();
        foreach (var o in doc.Occurrences)
        {
            if ((o.SymbolRoles & 1) > 0) { withDefinitions.Add(o.Symbol); }
            if (showRanges && EnclosingRangeOf(o) is { } er) { enclosing.Add((er, o.Symbol)); }
        }

        // Stable sort: by range, then (for equal ranges) the one that ends last first.
        var sortedEnclosing = enclosing
            .Select((e, i) => (e, i))
            .OrderBy(x => x.e.Range)
            .ThenByDescending(x => x.e.Range.EndLine)
            .ThenBy(x => x.i)
            .Select(x => x.e)
            .ToList();
        var starts = Enumerable.Range(0, lines.Length).Select(_ => new List<(R Range, string Symbol)>()).ToArray();
        var ends = Enumerable.Range(0, lines.Length).Select(_ => new List<(R Range, string Symbol)>()).ToArray();
        foreach (var e in sortedEnclosing)
        {
            starts[e.Range.StartLine].Add(e);
            ends[e.Range.EndLine].Insert(0, e);
        }

        var emittedDocstrings = new HashSet<string>();

        void PushDoc(R range, string symbol, bool isDefinition, bool isStartOfLine)
        {
            if (emittedDocstrings.Contains(symbol)) { output.Append('\n'); return; }
            if (!isDefinition && withDefinitions.Contains(symbol)) { output.Append('\n'); return; }
            emittedDocstrings.Add(symbol);
            var prefix = "\n" + CommentSyntax;
            if (!isStartOfLine) { prefix += new string(' ', Math.Max(0, range.StartCharacter - 2)); }

            void PushLines(string label, IEnumerable<string> docLines, bool external)
            {
                var idx = 0;
                foreach (var line in docLines)
                {
                    output.Append(prefix);
                    if (idx == 0)
                    {
                        if (external) { output.Append("external "); }
                        output.Append(label).Append(' ');
                    }
                    else
                    {
                        output.Append("            > ");
                    }

                    output.Append(line.Length > 40 ? line[..40] : line);
                    if (line.Length > 40) { output.Append("..."); }
                    idx++;
                }
            }

            void PushOneDoc(SymbolInformation info, bool external)
            {
                if (!showDocs) { return; }
                foreach (var documentation in info.Documentation)
                {
                    PushLines("documentation", documentation.Split('\n'), external);
                }

                if (info.SignatureDocumentation is { } sig)
                {
                    PushLines("signature_documentation", new[] { sig.Language }.Concat(sig.Text.Split('\n')), external);
                }
            }

            void PushRelationships(IEnumerable<Relationship> relationships)
            {
                foreach (var r in relationships.OrderBy(x => x.Symbol, StringComparer.Ordinal))
                {
                    output.Append(prefix).Append("relationship");
                    if (r.IsImplementation) { output.Append(" implementation"); }
                    if (r.IsReference) { output.Append(" reference"); }
                    if (r.IsTypeDefinition) { output.Append(" type_definition"); }
                    output.Append(' ').Append(Strip(r.Symbol));
                }
            }

            void PushKind(SymbolInformation.Types.Kind kind)
            {
                if (!showKinds) { return; }
                output.Append(prefix).Append("kind ").Append(kind);
            }

            if (externalTable.TryGetValue(symbol, out var ext))
            {
                PushKind(ext.Kind);
                PushOneDoc(ext, true);
                PushRelationships(ext.Relationships);
            }
            else if (symbolTable.TryGetValue(symbol, out var info))
            {
                PushKind(info.Kind);
                PushOneDoc(info, false);
                PushRelationships(info.Relationships);
            }

            output.Append('\n');
        }

        void PushEnclosing((R Range, string Symbol) e, bool end)
        {
            if (!showRanges) { return; }
            output.Append(CommentSyntax);
            output.Append(' ', Math.Max(1, e.Range.StartCharacter - 2));
            output.Append(e.Range.StartCharacter < 2 ? "<" : end ? "^" : "⌄");
            output.Append(end ? " end " : " start ");
            output.Append("enclosing_range ").Append(Strip(e.Symbol)).Append('\n');
        }

        var occurrences = doc.Occurrences.Select((o, i) => (o, i)).OrderBy(x => RangeOf(x.o)).ThenBy(x => x.i).Select(x => x.o).ToList();
        var occurrenceIndex = 0;
        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            if (occurrenceIndex == 0 && occurrences.Count > 0)
            {
                var occurrence = occurrences[0];
                var range = RangeOf(occurrence);
                if (range.StartCharacter == 0 && range.EndCharacter == 0)
                {
                    var isDefinition = (occurrence.SymbolRoles & 1) > 0;
                    output.Append(CommentSyntax).Append(" < ").Append(isDefinition ? "definition" : "reference").Append(' ').Append(Strip(occurrence.Symbol));
                    PushDoc(range, occurrence.Symbol, isDefinition, true);
                    output.Append('\n');
                    occurrenceIndex++;
                }
            }

            foreach (var e in starts[lineNumber]) { PushEnclosing(e, false); }
            output.Append(lines[lineNumber]).Append('\n');
            while (occurrenceIndex < occurrences.Count && RangeOf(occurrences[occurrenceIndex]).StartLine == lineNumber)
            {
                var occurrence = occurrences[occurrenceIndex++];
                var range = RangeOf(occurrence);
                if (!range.IsSingleLine) { throw new NotSupportedException("not yet implemented, multi-line ranges"); }
                output.Append(CommentSyntax);
                var isStartOfLine = range.StartCharacter == 0;
                if (!isStartOfLine && range.StartCharacter > 2) { output.Append(' ', range.StartCharacter - 2); }
                var modifier = isStartOfLine ? 1 : 0;
                var caretLength = range.EndCharacter - range.StartCharacter - modifier;
                if (caretLength < 0) { throw new InvalidOperationException("negative length occurrence at " + range); }
                output.Append('^', caretLength).Append(' ');
                var isDefinition = (occurrence.SymbolRoles & 1) > 0;
                output.Append(isDefinition ? "definition" : "reference").Append(' ');
                var symbol = Strip(occurrence.Symbol);
                var nl = symbol.IndexOf('\n', StringComparison.Ordinal);
                output.Append(nl >= 0 ? symbol[..nl] + "|" + symbol[(nl + 1)..] : symbol);
                PushDoc(range, occurrence.Symbol, isDefinition, isStartOfLine);
                foreach (var diagnostic in occurrence.Diagnostics)
                {
                    var indent = new string(' ', Math.Max(0, range.StartCharacter - 2));
                    output.Append(CommentSyntax).Append(indent).Append($"diagnostic {diagnostic.Severity}:\n");
                    if (diagnostic.Message.Length > 0)
                    {
                        foreach (var messageLine in diagnostic.Message.Split('\n'))
                        {
                            output.Append(CommentSyntax).Append(indent).Append("> ").Append(messageLine).Append('\n');
                        }
                    }
                }
            }

            foreach (var e in ends[lineNumber]) { PushEnclosing(e, true); }
        }

        return output.ToString();
    }

    private static (bool Docs, bool Kinds, bool Ranges) ParseOptions(string[] lines)
    {
        bool docs = false, kinds = false, ranges = false;
        foreach (var line in lines)
        {
            if (!line.StartsWith(FormatOptionsPrefix, StringComparison.Ordinal)) { continue; }
            foreach (var option in line[FormatOptionsPrefix.Length..].Trim().Split(','))
            {
                switch (option.Trim())
                {
                    case "showDocs": docs = true; break;
                    case "showKinds": kinds = true; break;
                    case "showRanges": ranges = true; break;
                    default: throw new InvalidOperationException($"Invalid format option: {option.Trim()}");
                }
            }

            break;
        }

        return (docs, kinds, ranges);
    }
}
