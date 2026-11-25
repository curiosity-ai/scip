using System.Runtime.InteropServices;

namespace ScipSharp;

/// <summary>Deterministic sorting and lookup helpers (port of <c>sort.go</c>). All sorts are in place and return their argument.</summary>
public static class Sorting
{
    /// <summary>Linear search. Prefer <see cref="FindSymbolBinarySearch"/> on canonicalized documents.</summary>
    public static SymbolInformation? FindSymbol(Document document, string symbolName)
    {
        foreach (var symbol in document.Symbols)
        {
            if (symbol.Symbol == symbolName)
            {
                return symbol;
            }
        }

        return null;
    }

    /// <summary>
    /// Pre-condition: symbols are sorted by name and merged, as guaranteed by <see cref="Canonicalizer.CanonicalizeDocument"/>.
    /// </summary>
    public static SymbolInformation? FindSymbolBinarySearch(Document canonicalizedDocument, string symbolName)
    {
        var symbols = canonicalizedDocument.Symbols;
        int lo = 0, hi = symbols.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            var c = SymbolOrder.Compare(symbols[mid].Symbol, symbolName);
            if (c == 0)
            {
                return symbols[mid];
            }

            if (c < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return null;
    }

    /// <summary>Sorts ascending by relative path.</summary>
    public static IList<Document> SortDocuments(IList<Document> documents) =>
        SortInPlace(documents, static (a, b) => SymbolOrder.Compare(a.RelativePath, b.RelativePath));

    /// <summary>
    /// Returns the occurrences containing the position, ordered "inside-out": enclosed occurrences come before their enclosers.
    /// </summary>
    public static List<Occurrence> FindOccurrences(IEnumerable<Occurrence> occurrences, int targetLine, int targetCharacter)
    {
        var pos = new Position(targetLine, targetCharacter);
        var filtered = occurrences.Where(o => o.Contains(pos)).ToList();
        SortInPlace(filtered, static (a, b) =>
        {
            a.TryGetSourceRange(out var ra);
            b.TryGetSourceRange(out var rb);
            return rb.CompareStrict(ra);
        });
        return filtered;
    }

    /// <summary>Ascending by range start (enclosing before enclosed); ties broken by symbol name.</summary>
    public static IList<Occurrence> SortOccurrences(IList<Occurrence> occurrences) =>
        SortInPlace(occurrences, static (a, b) => a.CompareOccurrence(b));

    /// <summary>Ascending by start position, enclosing ranges before enclosed.</summary>
    public static IList<Range> SortRanges(IList<Range> ranges) =>
        SortInPlace(ranges, static (a, b) => a.CompareStrict(b));

    /// <summary>Ascending by symbol name.</summary>
    public static IList<SymbolInformation> SortSymbols(IList<SymbolInformation> symbols) =>
        SortInPlace(symbols, static (a, b) => SymbolOrder.Compare(a.Symbol, b.Symbol));

    /// <summary>More severe first (lower enum value), then by message.</summary>
    public static IList<Diagnostic> SortDiagnostics(IList<Diagnostic> diagnostics) =>
        SortInPlace(diagnostics, static (a, b) =>
        {
            var c = ((int)a.Severity).CompareTo((int)b.Severity);
            return c != 0 ? c : SymbolOrder.Compare(a.Message, b.Message);
        });

    public static IList<DiagnosticTag> SortDiagnosticTags(IList<DiagnosticTag> tags) =>
        SortInPlace(tags, static (a, b) => ((int)a).CompareTo((int)b));

    /// <summary>Ascending by symbol name.</summary>
    public static IList<Relationship> SortRelationships(IList<Relationship> relationships) =>
        SortInPlace(relationships, static (a, b) => SymbolOrder.Compare(a.Symbol, b.Symbol));

    /// <summary>
    /// Compares a target position against a four-element range bound: -1 if the position is after the range,
    /// +1 if before it, 0 if enclosed (half-open). Note the sign convention mirrors the Go implementation.
    /// </summary>
    public static int ComparePositionToRange(
        int startLine, int startCharacter, int endLine, int endCharacter, int targetLine, int targetCharacter)
    {
        if (targetLine < startLine)
        {
            return 1;
        }

        if (targetLine > endLine)
        {
            return -1;
        }

        if (targetLine == startLine && targetCharacter < startCharacter)
        {
            return 1;
        }

        if (targetLine == endLine && targetCharacter >= endCharacter)
        {
            return -1;
        }

        return 0;
    }

    internal static IList<T> SortInPlace<T>(IList<T> items, Comparison<T> comparison)
    {
        // Stable (ties keep input order) so results are deterministic regardless of input container.
        var n = items.Count;
        if (n < 2)
        {
            return items;
        }

        var keyed = new (T Item, int Index)[n];
        for (var i = 0; i < n; i++)
        {
            keyed[i] = (items[i], i);
        }

        keyed.AsSpan().Sort((a, b) =>
        {
            var c = comparison(a.Item, b.Item);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });

        if (items is List<T> list)
        {
            var span = CollectionsMarshal.AsSpan(list);
            for (var i = 0; i < n; i++)
            {
                span[i] = keyed[i].Item;
            }
        }
        else
        {
            for (var i = 0; i < n; i++)
            {
                items[i] = keyed[i].Item;
            }
        }

        return items;
    }
}
