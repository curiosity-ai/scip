using System.Text;
using ScipRange = ScipSharp.Range;

namespace ScipSharp.Indexer.Pascal.Syntax;

/// <summary>How the bytes of a source file were decoded; decides the SCIP <c>position_encoding</c> of its document.</summary>
internal enum SourceEncoding
{
    /// <summary>Valid UTF-8 (with or without BOM): positions are UTF-8 byte offsets.</summary>
    Utf8,

    /// <summary>UTF-16 with BOM: positions are UTF-16 code unit offsets.</summary>
    Utf16,

    /// <summary>A legacy single-byte code page: every byte is one UTF-16 unit, so UTF-16 offsets equal byte offsets.</summary>
    Legacy,
}

/// <summary>A comment seen by the lexer (directives excluded), kept for documentation extraction.</summary>
internal readonly record struct Comment(int Start, int End, string Text);

/// <summary>A decoded Pascal source file with line/column mapping.</summary>
internal sealed class SourceText
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly int[] _lineStarts;

    public SourceText(string path, string text, SourceEncoding encoding)
    {
        Path = path;
        Text = text;
        Encoding = encoding;
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    /// <summary>Absolute path on disk.</summary>
    public string Path { get; }

    public string Text { get; }

    public SourceEncoding Encoding { get; }

    /// <summary>Comments in source order.</summary>
    public List<Comment> Comments { get; } = [];

    public PositionEncoding PositionEncoding => Encoding == SourceEncoding.Utf8
        ? PositionEncoding.Utf8CodeUnitOffsetFromLineStart
        : PositionEncoding.Utf16CodeUnitOffsetFromLineStart;

    /// <summary>Loads and decodes a file: UTF-8 (BOM optional), UTF-16 (BOM), otherwise <paramref name="legacy"/>.</summary>
    public static SourceText Load(string path, Encoding legacy)
    {
        var bytes = File.ReadAllBytes(path);
        return Decode(path, bytes, legacy);
    }

    public static SourceText Decode(string path, byte[] bytes, Encoding legacy)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new SourceText(path, System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), SourceEncoding.Utf8);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return new SourceText(path, System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), SourceEncoding.Utf16);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return new SourceText(path, System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), SourceEncoding.Utf16);
        }

        try
        {
            return new SourceText(path, StrictUtf8.GetString(bytes), SourceEncoding.Utf8);
        }
        catch (DecoderFallbackException)
        {
            return new SourceText(path, legacy.GetString(bytes), SourceEncoding.Legacy);
        }
    }

    public int LineOf(int offset)
    {
        var idx = Array.BinarySearch(_lineStarts, offset);
        return idx >= 0 ? idx : ~idx - 1;
    }

    public int LineStart(int line) => _lineStarts[line];

    public int LineCount => _lineStarts.Length;

    /// <summary>The SCIP position of <paramref name="offset"/> in this document's position encoding.</summary>
    public Position PositionOf(int offset)
    {
        var line = LineOf(offset);
        var start = _lineStarts[line];
        var col = offset - start;
        if (Encoding == SourceEncoding.Utf8)
        {
            var span = Text.AsSpan(start, col);
            var ascii = true;
            foreach (var c in span)
            {
                if (c >= 0x80)
                {
                    ascii = false;
                    break;
                }
            }

            if (!ascii)
            {
                col = System.Text.Encoding.UTF8.GetByteCount(span);
            }
        }

        return new Position(line, col);
    }

    public ScipRange RangeOf(int start, int end) => new(PositionOf(start), PositionOf(end));

    /// <summary>Text of the line containing <paramref name="offset"/> up to it.</summary>
    public ReadOnlySpan<char> LinePrefix(int offset)
    {
        var start = _lineStarts[LineOf(offset)];
        return Text.AsSpan(start, offset - start);
    }
}
