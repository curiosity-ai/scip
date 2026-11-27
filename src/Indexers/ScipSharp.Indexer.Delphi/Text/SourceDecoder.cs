using System.Text;

namespace ScipSharp.Indexer.Delphi.Text;

/// <summary>How a source file was decoded.</summary>
internal enum SourceEncoding
{
    Utf8,
    Utf8Bom,
    Utf16LittleEndian,
    Utf16BigEndian,
    Windows1252,
}

/// <summary>
/// Decodes Delphi sources. Modern Delphi writes UTF-8 with a BOM (or UTF-16 with a BOM); older code bases (Delphi 7 and
/// earlier, or files saved as "ANSI") are Windows-1252. Files without a BOM are decoded as strict UTF-8 and fall back to
/// Windows-1252 when that fails. The BOM is stripped, so positions computed on the decoded text are offsets from the
/// start of each line in UTF-16 code units (one per byte for Windows-1252).
/// </summary>
internal static class SourceDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Windows-1252 differs from Latin-1 only in 0x80..0x9F; undefined slots map to the C1 control of the same value.
    private static readonly char[] Cp1252High =
    [
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    ];

    public static string Decode(ReadOnlySpan<byte> bytes) => Decode(bytes, out _);

    public static string Decode(ReadOnlySpan<byte> bytes, out SourceEncoding encoding)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            encoding = SourceEncoding.Utf8Bom;
            return Encoding.UTF8.GetString(bytes[3..]);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            encoding = SourceEncoding.Utf16LittleEndian;
            return Encoding.Unicode.GetString(bytes[2..]);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            encoding = SourceEncoding.Utf16BigEndian;
            return Encoding.BigEndianUnicode.GetString(bytes[2..]);
        }

        try
        {
            encoding = SourceEncoding.Utf8;
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            encoding = SourceEncoding.Windows1252;
            return DecodeWindows1252(bytes);
        }
    }

    public static string DecodeWindows1252(ReadOnlySpan<byte> bytes)
    {
        return string.Create(bytes.Length, bytes.ToArray(), static (span, data) =>
        {
            for (var i = 0; i < data.Length; i++)
            {
                var b = data[i];
                span[i] = b is >= 0x80 and <= 0x9F ? Cp1252High[b - 0x80] : (char)b;
            }
        });
    }

    public static string ReadFile(string path) => Decode(File.ReadAllBytes(path));
}
