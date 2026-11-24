#pragma warning disable CS0612 // deprecated range fields are intentionally supported

using System.Text;
using Google.Protobuf.Reflection;

namespace Scip;

/// <summary>Result of rendering a document: the text plus any symbol-formatting errors that were swallowed along the way.</summary>
public sealed record FormattedSnapshot(string Text, IReadOnlyList<Exception> Errors);

/// <summary>
/// Renders SCIP documents into the pretty-printed text format used for snapshot testing
/// (port of <c>testutil/format.go</c>). Symbols that fail to parse are emitted verbatim and reported in <see cref="FormattedSnapshot.Errors"/>.
/// </summary>
public static class SnapshotFormatter
{
    /// <summary>
    /// Renders every document of <paramref name="index"/> (plus external symbols) as a <see cref="SourceFile"/>.
    /// Source texts are read from the index's project root (or <paramref name="customProjectRoot"/>).
    /// Errors are returned unless <paramref name="formatter"/> is lenient.
    /// </summary>
    public static (List<SourceFile> Files, List<Exception> Errors) FormatSnapshots(
        Index index, string commentSyntax, SymbolFormatter formatter, string customProjectRoot = "")
    {
        var result = new List<SourceFile>();
        var errors = new List<Exception>();
        var localSourcesRoot = "";
        if (Uri.TryCreate(index.Metadata?.ProjectRoot ?? "", UriKind.Absolute, out var uri) && uri.IsFile)
        {
            localSourcesRoot = uri.LocalPath;
        }

        if (customProjectRoot.Length > 0)
        {
            localSourcesRoot = customProjectRoot;
        }
        else if (!Directory.Exists(localSourcesRoot) && !File.Exists(localSourcesRoot))
        {
            var cwd = Directory.GetCurrentDirectory();
            Console.Error.WriteLine(
                $"Project root [{localSourcesRoot}] doesn't exist, using current working directory [{cwd}] instead. " +
                "To override this behaviour, use --project-root=<folder> option directly");
            localSourcesRoot = cwd;
        }

        foreach (var document in index.Documents)
        {
            var path = Path.Combine(localSourcesRoot, document.RelativePath);
            string text;
            try
            {
                var snapshot = FormatSnapshot(document, commentSyntax, formatter, path);
                text = snapshot.Text;
                AddErrors(snapshot.Errors, document.RelativePath);
            }
            catch (IOException e)
            {
                text = "";
                AddErrors([e], document.RelativePath);
            }

            result.Add(new SourceFile(path, document.RelativePath, text));
        }

        if (index.ExternalSymbols.Count > 0)
        {
            var formatted = FormatExternalSymbols(index.ExternalSymbols, formatter);
            AddErrors(formatted.Errors, "external_symbols");
            result.Add(new SourceFile(Path.Combine(localSourcesRoot, "external_symbols.txt"), "external_symbols.txt", formatted.Text));
        }

        return (result, errors);

        void AddErrors(IEnumerable<Exception> found, string where)
        {
            if (!formatter.Lenient)
            {
                errors.AddRange(found.Select(e => new InvalidOperationException($"{where}: {e.Message}", e)));
            }
        }
    }

    /// <summary>Renders <paramref name="document"/> (whose source is read from <paramref name="sourceFilePath"/>). Sorts the document's occurrences in place.</summary>
    public static FormattedSnapshot FormatSnapshot(Document document, string commentSyntax, SymbolFormatter formatter, string sourceFilePath)
    {
        var data = File.ReadAllText(sourceFilePath);
        var b = new StringBuilder();
        var errors = new List<Exception>();
        var symtab = document.SymbolTable();

        // Stable sort by range only (no symbol tiebreaker), in place like the Go implementation.
        Sorting.SortInPlace(document.Occurrences, static (a, c) =>
        {
            a.TryGetSourceRange(out var ra);
            c.TryGetSourceRange(out var rc);
            return ra.CompareStrict(rc);
        });
        var occurrences = document.Occurrences;

        var enclosing = EnclosingRanges(occurrences);
        var enclosingByStartLine = GroupBy(enclosing, static e => e.Range.Start.Line, static e => e.Range.Start.Character);
        var enclosingByEndLine = GroupBy(enclosing, static e => e.Range.End.Line, static e => e.Range.End.Character);
        var syntheticDefinitions = SyntheticDefinitionsByParent(document.Symbols);

        string FormatSymbol(string symbol) => FormatSymbolOrVerbatim(formatter, symbol, errors);

        void FormatOccurrence(Occurrence occ, SymbolInformation? synthetic, string renderedLine)
        {
            occ.TryGetSourceRange(out var pos);
            var isDefinition = SymbolRole.Definition.Matches(occ);
            var marker = synthetic is null ? '^' : '_';

            b.Append(commentSyntax);
            b.Append(' ', pos.Start.Character);

            var markerCount = MarkerLength(pos, renderedLine);
            b.Append(marker, markerCount);
            b.Append(' ');

            var role = synthetic is not null ? "synthetic_definition"
                : isDefinition ? "definition"
                : SymbolRole.ForwardDefinition.Matches(occ) ? "forward_definition"
                : "reference";
            b.Append(role).Append(' ');
            b.Append(FormatSymbol(synthetic?.Symbol ?? occ.Symbol));
            if (!pos.IsSingleLine)
            {
                b.Append($" {pos.End.Line - pos.Start.Line}:{pos.End.Character}");
            }

            var prefix = "\n" + commentSyntax + new string(' ', pos.Start.Character + markerCount + 1);

            if (synthetic is null && occ.OverrideDocumentation.Count > 0)
            {
                WriteDocumentation(b, occ.OverrideDocumentation[0], prefix, true);
            }

            var info = synthetic;
            if (info is null)
            {
                symtab.TryGetValue(occ.Symbol, out info);
            }

            if (info is not null && isDefinition)
            {
                if (info.Kind != SymbolInformation.Types.Kind.UnspecifiedKind)
                {
                    b.Append(prefix).Append("kind ").Append(OriginalName(info.Kind));
                }

                if (info.DisplayName.Length > 0)
                {
                    b.Append(prefix).Append("display_name ").Append(info.DisplayName);
                }

                if (info.SignatureDocumentation is { Text.Length: > 0 } sig)
                {
                    b.Append(prefix).Append("signature_documentation");
                    WriteMultiline(b, prefix, sig.Text);
                }

                if (info.EnclosingSymbol.Length > 0)
                {
                    b.Append(prefix).Append("enclosing_symbol ").Append(FormatSymbol(info.EnclosingSymbol));
                }

                foreach (var documentation in info.Documentation)
                {
                    WriteDocumentation(b, documentation.Trim(), prefix, false);
                }

                WriteRelationships(b, info.Relationships, prefix, FormatSymbol);
            }

            if (synthetic is null)
            {
                foreach (var diagnostic in occ.Diagnostics)
                {
                    b.Append(prefix).Append("diagnostic ").Append(OriginalName(diagnostic.Severity)).Append(':');
                    WriteMultiline(b, prefix, diagnostic.Message);
                }
            }

            b.Append('\n');
        }

        var i = 0;
        var lines = data.Split('\n');
        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            if (enclosingByStartLine.TryGetValue(lineNumber, out var starting))
            {
                foreach (var er in starting)
                {
                    b.Append(commentSyntax).Append(' ', er.Range.Start.Character);
                    b.Append("⌄ enclosing_range_start ").Append(FormatSymbol(er.Symbol)).Append('\n');
                }
            }

            var renderedLine = RenderLine(lines[lineNumber]);
            b.Append(' ', commentSyntax.Length).Append(renderedLine).Append('\n');
            while (i < occurrences.Count)
            {
                var occ = occurrences[i];
                occ.TryGetSourceRange(out var pos);
                if (pos.Start.Line != lineNumber)
                {
                    break;
                }

                FormatOccurrence(occ, null, renderedLine);
                if (SymbolRole.Definition.Matches(occ) && syntheticDefinitions.TryGetValue(occ.Symbol, out var synthetics))
                {
                    foreach (var synthetic in synthetics)
                    {
                        FormatOccurrence(occ, synthetic, renderedLine);
                    }
                }

                i++;
            }

            if (enclosingByEndLine.TryGetValue(lineNumber, out var ending))
            {
                foreach (var er in ending)
                {
                    b.Append(commentSyntax).Append(' ', Math.Max(0, er.Range.End.Character - 1));
                    b.Append("⌃ enclosing_range_end ").Append(FormatSymbol(er.Symbol)).Append('\n');
                }
            }
        }

        return new FormattedSnapshot(b.ToString(), errors);
    }

    private static FormattedSnapshot FormatExternalSymbols(IEnumerable<SymbolInformation> symbols, SymbolFormatter formatter)
    {
        var b = new StringBuilder();
        var errors = new List<Exception>();
        var sorted = symbols.ToList();
        Sorting.SortSymbols(sorted);
        for (var i = 0; i < sorted.Count; i++)
        {
            if (i > 0)
            {
                b.Append('\n');
            }

            b.Append(FormatSymbolOrVerbatim(formatter, sorted[i].Symbol, errors));
            WriteRelationships(b, sorted[i].Relationships, "\n  ", s => FormatSymbolOrVerbatim(formatter, s, errors));
        }

        return new FormattedSnapshot(b.ToString(), errors);
    }

    private static string FormatSymbolOrVerbatim(SymbolFormatter formatter, string symbol, List<Exception> errors)
    {
        if (!SymbolParser.TryParse(symbol, out var parsed, out var error))
        {
            errors.Add(new SymbolParseException($"{symbol}: {error}"));
            return symbol;
        }

        return formatter.Format(parsed);
    }

    private static void WriteRelationships(StringBuilder b, IEnumerable<Relationship> relationships, string prefix, Func<string, string> formatSymbol)
    {
        var sorted = relationships.ToList();
        Sorting.SortRelationships(sorted);
        foreach (var relationship in sorted)
        {
            b.Append(prefix).Append("relationship ").Append(formatSymbol(relationship.Symbol));
            if (relationship.IsImplementation)
            {
                b.Append(" implementation");
            }

            if (relationship.IsReference)
            {
                b.Append(" reference");
            }

            if (relationship.IsTypeDefinition)
            {
                b.Append(" type_definition");
            }

            if (relationship.IsDefinition)
            {
                b.Append(" definition");
            }
        }
    }

    private static void WriteMultiline(StringBuilder b, string prefix, string paragraph)
    {
        foreach (var s in paragraph.Split('\n'))
        {
            b.Append(prefix).Append("> ").Append(s);
        }
    }

    private static void WriteDocumentation(StringBuilder b, string documentation, string prefix, bool isOverride)
    {
        b.Append(prefix);
        if (isOverride)
        {
            b.Append("override_");
        }

        b.Append("documentation");
        WriteMultiline(b, prefix, documentation);
    }

    private sealed record EnclosingRange(Range Range, string Symbol);

    private static List<EnclosingRange> EnclosingRanges(IEnumerable<Occurrence> occurrences)
    {
        var result = new List<EnclosingRange>();
        foreach (var occ in occurrences)
        {
            if (occ.TryGetEnclosingSourceRange(out var r))
            {
                result.Add(new EnclosingRange(r, occ.Symbol));
            }
        }

        return result;
    }

    private static Dictionary<int, List<EnclosingRange>> GroupBy(
        List<EnclosingRange> ranges, Func<EnclosingRange, int> line, Func<EnclosingRange, int> character)
    {
        var result = new Dictionary<int, List<EnclosingRange>>();
        foreach (var r in ranges)
        {
            if (!result.TryGetValue(line(r), out var list))
            {
                result[line(r)] = list = [];
            }

            list.Add(r);
        }

        foreach (var list in result.Values)
        {
            Sorting.SortInPlace(list, (a, b) => character(a).CompareTo(character(b)));
        }

        return result;
    }

    /// <summary>
    /// Maps a "parent" symbol to the symbols declaring a definition relationship on it (their definition is synthesized
    /// at the parent's definition site), sorted by symbol.
    /// </summary>
    internal static Dictionary<string, List<SymbolInformation>> SyntheticDefinitionsByParent(IEnumerable<SymbolInformation> symbols)
    {
        var result = new Dictionary<string, List<SymbolInformation>>(StringComparer.Ordinal);
        foreach (var info in symbols)
        {
            foreach (var rel in info.Relationships)
            {
                if (rel.IsDefinition)
                {
                    if (!result.TryGetValue(rel.Symbol, out var list))
                    {
                        result[rel.Symbol] = list = [];
                    }

                    list.Add(info);
                }
            }
        }

        foreach (var list in result.Values)
        {
            Sorting.SortSymbols(list);
        }

        return result;
    }

    /// <summary>Trims a trailing carriage return and expands tabs to single spaces.</summary>
    internal static string RenderLine(string line) => (line.EndsWith('\r') ? line[..^1] : line).Replace('\t', ' ');

    /// <summary>Marker count for an occurrence: exact for single-line ranges, to end of the start line otherwise; at least 1.</summary>
    internal static int MarkerLength(Range pos, string renderedStartLine)
    {
        var length = pos.End.Character - pos.Start.Character;
        if (!pos.IsSingleLine)
        {
            length = Encoding.UTF8.GetByteCount(renderedStartLine) - pos.Start.Character;
        }

        return Math.Max(length, 1);
    }

    private static string OriginalName<TEnum>(TEnum value) where TEnum : struct, Enum =>
        typeof(TEnum).GetField(value.ToString())?
            .GetCustomAttributes(typeof(OriginalNameAttribute), false)
            .OfType<OriginalNameAttribute>().FirstOrDefault()?.Name ?? value.ToString();
}
