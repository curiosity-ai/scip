using System.Text;

namespace ScipSharp;

/// <summary>
/// Converts <c>character</c> offsets within a single line between the SCIP <see cref="PositionEncoding"/>s
/// (UTF-8 bytes, UTF-16 code units, UTF-32 code points). Lone surrogates count as U+FFFD (3 UTF-8 bytes, 1 unit otherwise).
/// </summary>
public static class PositionEncodings
{
    /// <summary>Number of code units that <paramref name="rune"/> occupies in <paramref name="encoding"/>.</summary>
    public static int UnitLength(Rune rune, PositionEncoding encoding) => encoding switch
    {
        PositionEncoding.Utf8CodeUnitOffsetFromLineStart => rune.Utf8SequenceLength,
        PositionEncoding.Utf16CodeUnitOffsetFromLineStart => rune.Utf16SequenceLength,
        PositionEncoding.Utf32CodeUnitOffsetFromLineStart => 1,
        _ => throw new ArgumentException($"Cannot interpret offsets with encoding {encoding}", nameof(encoding)),
    };

    /// <summary>
    /// Converts <paramref name="offset"/> (in <paramref name="from"/> units from the start of <paramref name="line"/>)
    /// to <paramref name="to"/> units. Fails if the offset is negative, past the end of the line, or falls inside a character.
    /// </summary>
    public static bool TryConvert(ReadOnlySpan<char> line, int offset, PositionEncoding from, PositionEncoding to, out int converted)
    {
        converted = 0;
        if (offset < 0)
        {
            return false;
        }

        var consumed = 0;
        var i = 0;
        while (consumed < offset && i < line.Length)
        {
            Rune.DecodeFromUtf16(line[i..], out var rune, out var charsConsumed);
            consumed += UnitLength(rune, from);
            converted += UnitLength(rune, to);
            i += charsConsumed;
        }

        return consumed == offset;
    }

    /// <summary>Like <see cref="TryConvert"/> but throws <see cref="ArgumentOutOfRangeException"/> on failure.</summary>
    public static int Convert(ReadOnlySpan<char> line, int offset, PositionEncoding from, PositionEncoding to) =>
        TryConvert(line, offset, from, to, out var result)
            ? result
            : throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset is outside the line or inside a character.");

    /// <summary>Length of <paramref name="line"/> in the given encoding.</summary>
    public static int LineLength(ReadOnlySpan<char> line, PositionEncoding encoding)
    {
        var total = 0;
        var i = 0;
        while (i < line.Length)
        {
            Rune.DecodeFromUtf16(line[i..], out var rune, out var charsConsumed);
            total += UnitLength(rune, encoding);
            i += charsConsumed;
        }

        return total;
    }
}
