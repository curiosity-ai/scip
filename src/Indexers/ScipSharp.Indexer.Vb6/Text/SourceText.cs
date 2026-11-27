using System.Text;
using System.Text.Unicode;

namespace ScipSharp.Indexer.Vb6.Text;

/// <summary>
/// A decoded VB6 source file split into lines. VB6/VBA sources are stored in the ANSI code page of the machine that saved
/// them (usually Windows-1252) with CRLF line ends; exports from newer tools may be UTF-8. <see cref="Decode"/> picks UTF-8
/// when the bytes carry a BOM or form valid UTF-8, and the configured ANSI code page otherwise.
/// Positions produced by the indexer are UTF-16 code unit offsets into <see cref="Lines"/>.
/// </summary>
internal sealed class SourceText
{
    private SourceText(string text, Encoding detected)
    {
        Text = text;
        Encoding = detected;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].EndsWith('\r'))
            {
                lines[i] = lines[i][..^1];
            }
        }

        Lines = lines;
    }

    /// <summary>The whole decoded text (line ends preserved).</summary>
    public string Text { get; }

    /// <summary>The lines of <see cref="Text"/> split on <c>\n</c>, without the trailing <c>\r</c>.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>The encoding the bytes were decoded with.</summary>
    public Encoding Encoding { get; }

    public static SourceText FromString(string text) => new(text, Encoding.UTF8);

    public static SourceText Decode(ReadOnlySpan<byte> bytes, Encoding ansi)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return new SourceText(Encoding.UTF8.GetString(bytes[3..]), Encoding.UTF8);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return new SourceText(Encoding.Unicode.GetString(bytes[2..]), Encoding.Unicode);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return new SourceText(Encoding.BigEndianUnicode.GetString(bytes[2..]), Encoding.BigEndianUnicode);
        }

        if (!bytes.ContainsAnyExceptInRange((byte)0, (byte)0x7F))
        {
            return new SourceText(Encoding.ASCII.GetString(bytes), ansi);
        }

        if (Utf8.IsValid(bytes))
        {
            return new SourceText(Encoding.UTF8.GetString(bytes), Encoding.UTF8);
        }

        return new SourceText(ansi.GetString(bytes), ansi);
    }

    public static SourceText Read(string path, Encoding ansi) => Decode(File.ReadAllBytes(path), ansi);

    /// <summary>Resolves an encoding name or code page number (e.g. <c>1252</c>, <c>windows-1251</c>, <c>shift_jis</c>, <c>utf-8</c>).</summary>
    public static Encoding ResolveEncoding(string nameOrCodePage)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return int.TryParse(nameOrCodePage, out var cp) ? Encoding.GetEncoding(cp) : Encoding.GetEncoding(nameOrCodePage);
    }

    /// <summary>Windows-1252, the default ANSI code page of western VB6 installations.</summary>
    public static Encoding Windows1252 => ResolveEncoding("1252");

    /// <summary>Returns the text between two positions, with VB line continuations removed and whitespace collapsed.</summary>
    public string Slice(int startLine, int startCol, int endLine, int endCol)
    {
        var b = new StringBuilder();
        for (var line = startLine; line <= endLine && line < Lines.Count; line++)
        {
            var text = Lines[line];
            var from = line == startLine ? Math.Min(startCol, text.Length) : 0;
            var to = line == endLine ? Math.Min(endCol, text.Length) : text.Length;
            if (to < from)
            {
                continue;
            }

            var part = text[from..to].TrimEnd();
            if (line != endLine && part.EndsWith('_'))
            {
                part = part[..^1];
            }

            part = part.Trim();
            if (b.Length > 0 && b[^1] != '(' && !part.StartsWith(')'))
            {
                b.Append(' ');
            }

            b.Append(part);
        }

        return CollapseWhitespace(b.ToString());
    }

    private static string CollapseWhitespace(string s)
    {
        var b = new StringBuilder(s.Length);
        var inString = false;
        var space = false;
        foreach (var c in s)
        {
            if (c == '"')
            {
                inString = !inString;
            }

            if (!inString && c is ' ' or '\t')
            {
                space = true;
                continue;
            }

            if (space && b.Length > 0)
            {
                b.Append(' ');
            }

            space = false;
            b.Append(c);
        }

        return b.ToString();
    }
}
