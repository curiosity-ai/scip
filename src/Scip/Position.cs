namespace Scip;

/// <summary>A zero-based offset position (line, character). Port of Go <c>scip.Position</c>.</summary>
public readonly record struct Position(int Line, int Character) : IComparable<Position>
{
    /// <summary>Orders by line, then by character; returns -1, 0 or +1.</summary>
    public int CompareTo(Position other)
    {
        if (Line != other.Line)
        {
            return Line < other.Line ? -1 : 1;
        }

        if (Character != other.Character)
        {
            return Character < other.Character ? -1 : 1;
        }

        return 0;
    }

    public static bool operator <(Position a, Position b) => a.CompareTo(b) < 0;

    public static bool operator >(Position a, Position b) => a.CompareTo(b) > 0;

    public static bool operator <=(Position a, Position b) => a.CompareTo(b) <= 0;

    public static bool operator >=(Position a, Position b) => a.CompareTo(b) >= 0;

    /// <summary>Equivalent of Go <c>Position.Less</c>.</summary>
    public bool Less(Position other) => CompareTo(other) < 0;
}

/// <summary>Why a SCIP range was rejected.</summary>
public enum RangeError
{
    IncorrectLength,
    NegativeOffsets,
    EndBeforeStart,
}

/// <summary>Thrown by <see cref="Range.Create"/> for malformed SCIP ranges.</summary>
public sealed class RangeException(RangeError error) : Exception(Range.Describe(error))
{
    public RangeError Error { get; } = error;
}

/// <summary>A half-open <c>[Start, End)</c> range between two positions. Port of Go <c>scip.Range</c>.</summary>
public readonly record struct Range(Position Start, Position End)
{
    public Range(int startLine, int startCharacter, int endLine, int endCharacter)
        : this(new Position(startLine, startCharacter), new Position(endLine, endCharacter))
    {
    }

    /// <summary>Message text for a <see cref="RangeError"/> (matches the Go error strings).</summary>
    public static string Describe(RangeError error) => error switch
    {
        RangeError.IncorrectLength => "incorrect length",
        RangeError.NegativeOffsets => "negative offsets",
        RangeError.EndBeforeStart => "end before start",
        _ => throw new ArgumentOutOfRangeException(nameof(error)),
    };

    /// <summary>Validates and converts a SCIP-encoded range (3 or 4 elements).</summary>
    public static bool TryCreate(ReadOnlySpan<int> scipRange, out Range range, out RangeError error)
    {
        range = default;
        error = default;
        int startLine, startChar, endLine, endChar;
        switch (scipRange.Length)
        {
            case 3:
                startLine = endLine = scipRange[0];
                startChar = scipRange[1];
                endChar = scipRange[2];
                if (!(startLine >= 0 && startChar >= 0 && endChar >= startChar))
                {
                    error = Classify(startLine, endLine, startChar, endChar);
                    return false;
                }

                break;
            case 4:
                startLine = scipRange[0];
                startChar = scipRange[1];
                endLine = scipRange[2];
                endChar = scipRange[3];
                if (!(startLine >= 0 && startChar >= 0 &&
                      ((endLine > startLine && endChar >= 0) || (endLine == startLine && endChar >= startChar))))
                {
                    error = Classify(startLine, endLine, startChar, endChar);
                    return false;
                }

                break;
            default:
                error = RangeError.IncorrectLength;
                return false;
        }

        range = new Range(startLine, startChar, endLine, endChar);
        return true;
    }

    private static RangeError Classify(int startLine, int endLine, int startChar, int endChar) =>
        startLine < 0 || endLine < 0 || startChar < 0 || endChar < 0
            ? RangeError.NegativeOffsets
            : RangeError.EndBeforeStart;

    /// <summary>Like <see cref="TryCreate"/> but throws <see cref="RangeException"/>.</summary>
    public static Range Create(ReadOnlySpan<int> scipRange) =>
        TryCreate(scipRange, out var r, out var e) ? r : throw new RangeException(e);

    /// <summary>
    /// Converts a SCIP range without validation. Pre-condition: length is 3 or 4 (see <c>scip.proto</c>).
    /// </summary>
    public static Range CreateUnchecked(ReadOnlySpan<int> scipRange)
    {
        var endLine = scipRange[0];
        var endCharacter = scipRange[2];
        if (scipRange.Length == 4)
        {
            endLine = scipRange[2];
            endCharacter = scipRange[3];
        }

        return new Range(scipRange[0], scipRange[1], endLine, endCharacter);
    }

    /// <summary>Returns null if the range is well-formed, otherwise the reason it is not.</summary>
    public RangeError? Validate()
    {
        if (Start.Line < 0 || Start.Character < 0 || End.Line < 0 || End.Character < 0)
        {
            return RangeError.NegativeOffsets;
        }

        return Start.CompareTo(End) > 0 ? RangeError.EndBeforeStart : null;
    }

    public bool IsSingleLine => Start.Line == End.Line;

    /// <summary>The compact SCIP encoding: 3 elements if single-line, otherwise 4.</summary>
    public int[] ToScipRange() => IsSingleLine
        ? [Start.Line, Start.Character, End.Character]
        : [Start.Line, Start.Character, End.Line, End.Character];

    /// <summary>Pre-condition: <see cref="IsSingleLine"/>.</summary>
    public SingleLineRange ToSingleLineRange() => new()
    {
        Line = Start.Line,
        StartCharacter = Start.Character,
        EndCharacter = End.Character,
    };

    public MultiLineRange ToMultiLineRange() => new()
    {
        StartLine = Start.Line,
        StartCharacter = Start.Character,
        EndLine = End.Line,
        EndCharacter = End.Character,
    };

    /// <summary>True if <paramref name="position"/> lies within <c>[Start, End)</c>.</summary>
    public bool Contains(Position position) => !position.Less(Start) && position.Less(End);

    /// <summary>True if the two half-open ranges overlap.</summary>
    public bool Intersects(Range other) => Start.Less(other.End) && other.Start.Less(End);

    /// <summary>Returns 0 if the ranges intersect (not only if equal); otherwise compares start positions.</summary>
    public int Compare(Range other) => Intersects(other) ? 0 : Start.CompareTo(other.Start);

    /// <summary>Consistent with <see cref="Compare"/>: <c>Compare(o) &lt; 0</c> iff <c>Less(o)</c>.</summary>
    public bool Less(Range other) => !Intersects(other) && Start.Less(other.Start);

    /// <summary>Returns 0 iff the ranges are exactly equal; orders by start then end.</summary>
    public int CompareStrict(Range other)
    {
        var c = Start.CompareTo(other.Start);
        return c != 0 ? c : End.CompareTo(other.End);
    }

    /// <summary>Consistent with <see cref="CompareStrict"/>.</summary>
    public bool LessStrict(Range other) => CompareStrict(other) < 0;

    public override string ToString() => $"{Start.Line}:{Start.Character}-{End.Line}:{End.Character}";
}
