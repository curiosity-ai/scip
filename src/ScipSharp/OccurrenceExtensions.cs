#pragma warning disable CS0612 // deprecated range fields are intentionally supported

using Google.Protobuf.Collections;

namespace ScipSharp;

/// <summary>Range, role and symbol-table helpers for protobuf messages (port of <c>occurrence_range.go</c>, <c>symbol_role.go</c>, <c>symbol_table.go</c>).</summary>
public static class OccurrenceExtensions
{
    /// <summary>
    /// Gets the source range; <c>typed_range</c> takes precedence over the deprecated <c>range</c> field.
    /// Malformed deprecated ranges (length not 3 or 4) are reported as missing. On failure <paramref name="range"/> is default.
    /// </summary>
    public static bool TryGetSourceRange(this Occurrence occurrence, out Range range)
    {
        var typed = occurrence.TypedRangeCase switch
        {
            Occurrence.TypedRangeOneofCase.SingleLineRange => occurrence.SingleLineRange?.ToRange(),
            Occurrence.TypedRangeOneofCase.MultiLineRange => occurrence.MultiLineRange?.ToRange(),
            _ => null,
        };
        return Resolve(typed, occurrence.Range, out range);
    }

    /// <summary>Counterpart of <see cref="TryGetSourceRange"/> for <c>typed_enclosing_range</c>/<c>enclosing_range</c>.</summary>
    public static bool TryGetEnclosingSourceRange(this Occurrence occurrence, out Range range)
    {
        var typed = occurrence.TypedEnclosingRangeCase switch
        {
            Occurrence.TypedEnclosingRangeOneofCase.SingleLineEnclosingRange => occurrence.SingleLineEnclosingRange?.ToRange(),
            Occurrence.TypedEnclosingRangeOneofCase.MultiLineEnclosingRange => occurrence.MultiLineEnclosingRange?.ToRange(),
            _ => null,
        };
        return Resolve(typed, occurrence.EnclosingRange, out range);
    }

    /// <summary>The source range, or null if none is set.</summary>
    public static Range? GetSourceRange(this Occurrence occurrence) =>
        occurrence.TryGetSourceRange(out var r) ? r : null;

    /// <summary>The enclosing source range, or null if none is set.</summary>
    public static Range? GetEnclosingSourceRange(this Occurrence occurrence) =>
        occurrence.TryGetEnclosingSourceRange(out var r) ? r : null;

    private static bool Resolve(Range? typed, RepeatedField<int> legacy, out Range range)
    {
        if (typed is { } t)
        {
            range = t;
            return true;
        }

        switch (legacy.Count)
        {
            case 3:
                range = Range.CreateUnchecked([legacy[0], legacy[1], legacy[2]]);
                return true;
            case 4:
                range = Range.CreateUnchecked([legacy[0], legacy[1], legacy[2], legacy[3]]);
                return true;
            default:
                range = default;
                return false;
        }
    }

    /// <summary>Sets <c>typed_range</c> and clears the deprecated <c>range</c> field.</summary>
    public static void SetSourceRange(this Occurrence occurrence, Range range)
    {
        occurrence.Range.Clear();
        if (range.IsSingleLine)
        {
            occurrence.SingleLineRange = range.ToSingleLineRange();
        }
        else
        {
            occurrence.MultiLineRange = range.ToMultiLineRange();
        }
    }

    /// <summary>Sets <c>typed_enclosing_range</c> and clears the deprecated <c>enclosing_range</c> field.</summary>
    public static void SetEnclosingSourceRange(this Occurrence occurrence, Range range)
    {
        occurrence.EnclosingRange.Clear();
        if (range.IsSingleLine)
        {
            occurrence.SingleLineEnclosingRange = range.ToSingleLineRange();
        }
        else
        {
            occurrence.MultiLineEnclosingRange = range.ToMultiLineRange();
        }
    }

    /// <summary>
    /// Canonical ordering: ascending source range (strict), symbol name as tiebreaker. Missing ranges compare as the zero range.
    /// </summary>
    public static int CompareOccurrence(this Occurrence occurrence, Occurrence other)
    {
        occurrence.TryGetSourceRange(out var r1);
        other.TryGetSourceRange(out var r2);
        var c = r1.CompareStrict(r2);
        return c != 0 ? c : SymbolOrder.Compare(occurrence.Symbol, other.Symbol);
    }

    /// <summary>True if the occurrence has a range containing <paramref name="position"/>.</summary>
    public static bool Contains(this Occurrence occurrence, Position position) =>
        occurrence.TryGetSourceRange(out var r) && r.Contains(position);

    public static Range ToRange(this SingleLineRange range) =>
        new(range.Line, range.StartCharacter, range.Line, range.EndCharacter);

    public static Range ToRange(this MultiLineRange range) =>
        new(range.StartLine, range.StartCharacter, range.EndLine, range.EndCharacter);

    /// <summary>True if the symbol role bit is set in <paramref name="occurrence"/>.</summary>
    public static bool Matches(this SymbolRole role, Occurrence occurrence) => (occurrence.SymbolRoles & (int)role) > 0;

    /// <summary>Map of symbol name to information for a document (last entry wins).</summary>
    public static Dictionary<string, SymbolInformation> SymbolTable(this Document document)
    {
        var table = new Dictionary<string, SymbolInformation>(document.Symbols.Count, StringComparer.Ordinal);
        foreach (var info in document.Symbols)
        {
            table[info.Symbol] = info;
        }

        return table;
    }

    /// <summary>Extension form of <see cref="SymbolParser.IsLocalSymbol"/>.</summary>
    public static bool IsLocalSymbol(this string symbol) => SymbolParser.IsLocalSymbol(symbol);

    /// <summary>Extension form of <see cref="SymbolParser.IsGlobalSymbol"/>.</summary>
    public static bool IsGlobalSymbol(this string symbol) => SymbolParser.IsGlobalSymbol(symbol);
}

/// <summary>String ordering identical to Go's byte-wise UTF-8 comparison (i.e. by Unicode code point).</summary>
public static class SymbolOrder
{
    public static int Compare(string? a, string? b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        a ??= "";
        b ??= "";
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            if (a[i] != b[i])
            {
                return Fix(a[i]) < Fix(b[i]) ? -1 : 1;
            }
        }

        return a.Length.CompareTo(b.Length);
    }

    // Reorders UTF-16 code units so that surrogates (supplementary code points) sort after all BMP chars.
    private static int Fix(char c) => c >= 0xE000 ? c - 0x800 : c >= 0xD800 ? c + 0x2000 : c;
}
