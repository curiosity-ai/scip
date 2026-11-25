namespace ScipSharp.Indexer.Dart;

/// <summary>Maps UTF-16 offsets of a document to zero-based line/character pairs.</summary>
internal sealed class LineIndex
{
    private readonly List<int> _starts = [0];

    public LineIndex(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _starts.Add(i + 1);
            }
            else if (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n'))
            {
                _starts.Add(i + 1);
            }
        }
    }

    public readonly record struct Range(int StartLine, int StartCharacter, int EndLine, int EndCharacter);

    public (int Line, int Character) Locate(int offset)
    {
        var idx = _starts.BinarySearch(offset);
        if (idx < 0)
        {
            idx = ~idx - 1;
        }

        return (idx, offset - _starts[idx]);
    }

    public Range RangeOf(int offset, int length)
    {
        var (sl, sc) = Locate(offset);
        var (el, ec) = Locate(offset + length);
        return new Range(sl, sc, el, ec);
    }
}
