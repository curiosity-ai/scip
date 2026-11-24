namespace Scip.Indexer.Rust;

/// <summary>
/// Turns scanner results into SCIP documents and writes them. The write step (<see cref="Write"/>) ports the tail of
/// <c>cli/scip.rs</c>: documents without occurrences are dropped and non-local symbol duplicates are reported
/// (duplicate <c>SymbolInformation</c> of inherent impls is omitted, other duplicates are kept so they stay visible).
/// </summary>
public static class RustIndexAssembler
{
    public const string DuplicateSymbolsMessage = """

        Encountered duplicate scip symbols, indicating an internal rust-analyzer bug. These duplicates are
        included in the output, but this causes information lookup to be ambiguous and so information about
        these symbols presented by downstream tools may be incorrect.

        Known rust-analyzer bugs that can cause this:

          * Struct/enum/const/static/impl definitions nested in a function do not mention the function name.
            See #18771.

        Duplicate symbols encountered:

        """;

    /// <summary>Port of <c>text_range_to_scip_range</c>: single-line ranges use three numbers.</summary>
    public static int[] ToScipRange(int[] lineStarts, int start, int end)
    {
        var (sl, sc) = LineCol(lineStarts, start);
        var (el, ec) = LineCol(lineStarts, end);
        return sl == el ? [sl, sc, ec] : [sl, sc, el, ec];
    }

    private static (int Line, int Col) LineCol(int[] lineStarts, int offset)
    {
        var idx = Array.BinarySearch(lineStarts, offset);
        var line = idx >= 0 ? idx : ~idx - 1;
        return (line, offset - lineStarts[line]);
    }

    public static int[] LineStarts(byte[] bytes)
    {
        List<int> starts = [0];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return [.. starts];
    }

    /// <summary>Sets the typed (non-deprecated) range encoding.</summary>
    private static void SetRange(Occurrence occ, int[] r, bool enclosing)
    {
        if (r.Length == 3)
        {
            var single = new SingleLineRange { Line = r[0], StartCharacter = r[1], EndCharacter = r[2] };
            if (enclosing) { occ.SingleLineEnclosingRange = single; } else { occ.SingleLineRange = single; }
        }
        else
        {
            var multi = new MultiLineRange { StartLine = r[0], StartCharacter = r[1], EndLine = r[2], EndCharacter = r[3] };
            if (enclosing) { occ.MultiLineEnclosingRange = multi; } else { occ.MultiLineRange = multi; }
        }
    }

    private static bool IsImplSymbol(string symbol) =>
        SymbolParser.TryParse(symbol, out var parsed, out _)
        && parsed.Descriptors.Any(d => d is { Suffix: SymbolDescriptor.Types.Suffix.Type, Name: "impl" })
        && parsed.Descriptors[^1].Suffix == SymbolDescriptor.Types.Suffix.TypeParameter;

    public static Document ToDocument(SourceFileResult file, string relativePath)
    {
        var lines = LineStarts(file.Bytes);
        var doc = new Document
        {
            RelativePath = relativePath,
            Language = "rust",
            PositionEncoding = PositionEncoding.Utf8CodeUnitOffsetFromLineStart,
        };
        foreach (var o in file.Occurrences.OrderBy(o => o.Start).ThenByDescending(o => o.End))
        {
            var occ = new Occurrence { Symbol = o.Symbol };
            SetRange(occ, ToScipRange(lines, o.Start, o.End), enclosing: false);
            if (o.IsDefinition)
            {
                occ.SymbolRoles = (int)SymbolRole.Definition;
            }

            if (o.Enclosing is { } e)
            {
                SetRange(occ, ToScipRange(lines, e.Start, e.End), enclosing: true);
            }

            doc.Occurrences.Add(occ);
        }

        doc.Symbols.AddRange(file.Symbols);
        return doc;
    }

    /// <summary>
    /// Streams the index to <paramref name="output"/> through <see cref="IndexWriter"/>.
    /// Returns the number of duplicate-symbol errors reported to <paramref name="log"/>.
    /// </summary>
    public static int Write(Stream output, Metadata metadata, IEnumerable<Document> documents,
        IEnumerable<SymbolInformation> externalSymbols, TextWriter log)
    {
        HashSet<string> emitted = [];
        List<(string Location, string Symbol)> duplicates = [];

        bool ShouldEmit(string symbol, string location)
        {
            if (symbol.StartsWith("local ", StringComparison.Ordinal) || emitted.Add(symbol))
            {
                return true;
            }

            if (RustMonikers.IsInherentImplBlock(symbol))
            {
                return false; // upstream #18772: only the first inherent impl gets SymbolInformation
            }

            duplicates.Add((location, symbol));
            return false; // `scip lint` rejects repeated SymbolInformation; the duplicate is still reported to the log
        }

        using var writer = new IndexWriter(output, leaveOpen: true);
        writer.WriteMetadata(metadata);
        HashSet<string> paths = [];
        List<Document> kept = [];
        var docList = documents.ToList();
        // rust-analyzer only reports SymbolInformation for symbols that occur; impl blocks have no definition occurrence
        // of their own (only `Self` references), so an impl's information is kept only when it is referenced.
        HashSet<string> occurring = [.. docList.SelectMany(d => d.Occurrences).Select(o => o.Symbol)];
        foreach (var document in docList)
        {
            if (document.Occurrences.Count == 0 || !paths.Add(document.RelativePath))
            {
                continue;
            }

            var keep = document.Symbols
                .Where(s => (!(s.Kind == SymbolInformation.Types.Kind.TypeAlias && IsImplSymbol(s.Symbol) || s.Kind == SymbolInformation.Types.Kind.Field && s.DisplayName.Length > 0 && char.IsAsciiDigit(s.DisplayName[0])) || occurring.Contains(s.Symbol))
                    && ShouldEmit(s.Symbol, document.RelativePath))
                .ToList();
            document.Symbols.Clear();
            document.Symbols.AddRange(keep);
            kept.Add(document);
        }

        List<SymbolInformation> externals = [];
        foreach (var ext in externalSymbols)
        {
            if (ShouldEmit(ext.Symbol, "<external>"))
            {
                externals.Add(ext);
            }
        }

        // Every non-local occurrence symbol needs a SymbolInformation (e.g. `mod x;` declarations whose module file
        // is not itself a crate root): synthesize a bare one next to the first occurrence.
        foreach (var document in kept)
        {
            foreach (var occ in document.Occurrences)
            {
                if (occ.Symbol.Length > 0 && !occ.Symbol.StartsWith("local ", StringComparison.Ordinal) && emitted.Add(occ.Symbol))
                {
                    document.Symbols.Add(new SymbolInformation { Symbol = occ.Symbol });
                }
            }
        }

        foreach (var document in kept)
        {
            writer.WriteDocument(document);
        }

        foreach (var ext in externals)
        {
            writer.WriteExternalSymbol(ext);
        }

        if (duplicates.Count > 0)
        {
            log.WriteLine(DuplicateSymbolsMessage);
            foreach (var (location, symbol) in duplicates)
            {
                log.WriteLine(location);
                log.WriteLine($"  Duplicate symbol: {symbol}");
                log.WriteLine();
            }
        }

        return duplicates.Count;
    }
}
