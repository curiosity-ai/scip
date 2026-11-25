#pragma warning disable CS0612 // deprecated range fields are intentionally supported

namespace ScipSharp;

/// <summary>Deterministic sorting and merging of document contents (port of <c>canonicalize.go</c>).</summary>
public static class Canonicalizer
{
    /// <summary>
    /// Deterministically sorts and merges fields of <paramref name="document"/> (mutated and returned).
    /// Post-conditions: occurrences all have well-formed ranges and are sorted by strict range order;
    /// symbols are sorted by name with same-named entries merged; all strings are valid Unicode.
    /// </summary>
    public static Document CanonicalizeDocument(Document document)
    {
        Replace(document.Occurrences, CanonicalizeOccurrences(document.Occurrences));
        Replace(document.Symbols, CanonicalizeSymbols(document.Symbols));
        return Sanitizer.SanitizeDocument(document);
    }

    public static List<Occurrence> CanonicalizeOccurrences(IEnumerable<Occurrence> occurrences)
    {
        var flattened = Flattener.FlattenOccurrences(RemoveIllegalOccurrences(occurrences));
        var canonicalized = new List<Occurrence>(flattened.Count);
        foreach (var occurrence in flattened)
        {
            canonicalized.Add(CanonicalizeOccurrence(occurrence));
        }

        Sorting.SortOccurrences(canonicalized);
        return canonicalized;
    }

    /// <summary>Returns the occurrences that carry a range; the others are dropped.</summary>
    public static List<Occurrence> RemoveIllegalOccurrences(IEnumerable<Occurrence> occurrences) =>
        occurrences.Where(static o => o.TryGetSourceRange(out _)).ToList();

    /// <summary>
    /// Re-emits occurrences carrying only the deprecated <c>range</c> field in the compact three-element form.
    /// Occurrences with a typed range are left unchanged.
    /// </summary>
    public static Occurrence CanonicalizeOccurrence(Occurrence occurrence)
    {
        if (occurrence.TypedRangeCase == Occurrence.TypedRangeOneofCase.None && occurrence.Range.Count > 0)
        {
            occurrence.TryGetSourceRange(out var range);
            Replace(occurrence.Range, range.ToScipRange());
        }

        Replace(occurrence.Diagnostics, CanonicalizeDiagnostics(occurrence.Diagnostics));
        return occurrence;
    }

    public static List<Diagnostic> CanonicalizeDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        var canonicalized = diagnostics.Select(CanonicalizeDiagnostic).ToList();
        Sorting.SortDiagnostics(canonicalized);
        return canonicalized;
    }

    public static Diagnostic CanonicalizeDiagnostic(Diagnostic diagnostic)
    {
        var tags = diagnostic.Tags.ToList();
        Sorting.SortDiagnosticTags(tags);
        Replace(diagnostic.Tags, tags);
        return diagnostic;
    }

    public static List<SymbolInformation> CanonicalizeSymbols(IEnumerable<SymbolInformation> symbols)
    {
        var canonicalized = Flattener.FlattenSymbols(symbols).Select(CanonicalizeSymbol).ToList();
        Sorting.SortSymbols(canonicalized);
        return canonicalized;
    }

    public static SymbolInformation CanonicalizeSymbol(SymbolInformation symbol)
    {
        Replace(symbol.Relationships, CanonicalizeRelationships(symbol.Relationships));
        return symbol;
    }

    public static List<Relationship> CanonicalizeRelationships(IEnumerable<Relationship> relationships)
    {
        var flattened = Flattener.FlattenRelationships(relationships);
        Sorting.SortRelationships(flattened);
        return flattened;
    }

    private static void Replace<T>(Google.Protobuf.Collections.RepeatedField<T> field, IEnumerable<T> items)
    {
        var copy = items.ToList(); // `items` may be derived from `field`
        field.Clear();
        field.AddRange(copy);
    }
}
