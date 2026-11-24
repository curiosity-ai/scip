using System.Text;
using System.Text.Json;

namespace Scip;

/// <summary>A source file and helpers to slice it by SCIP ranges (port of <c>source_file.go</c>).</summary>
public sealed class SourceFile
{
    public SourceFile(string absolutePath, string relativePath, string text)
    {
        AbsolutePath = absolutePath;
        RelativePath = relativePath;
        Text = text;
        Lines = text.Split('\n');
    }

    public string AbsolutePath { get; }

    public string RelativePath { get; }

    public string Text { get; }

    /// <summary>The text split on <c>\n</c> (a trailing newline yields a final empty line).</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>Reads <paramref name="absolutePath"/> from disk.</summary>
    public static SourceFile FromPath(string absolutePath, string relativePath) =>
        new(absolutePath, relativePath, File.ReadAllText(absolutePath));

    /// <summary>Recursively reads every regular file under <paramref name="directory"/>.</summary>
    public static List<SourceFile> FromDirectory(string directory)
    {
        var result = new List<SourceFile>();
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            result.Add(FromPath(path, Path.GetRelativePath(directory, path)));
        }

        return result;
    }

    /// <summary>
    /// Returns the substring enclosed by <paramref name="range"/>, whose characters are offsets in
    /// <paramref name="encoding"/> (default UTF-8 bytes, as in the Go implementation). Lines are joined with <c>\n</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The range lies outside the file or splits a character.</exception>
    public string RangeText(Range range, PositionEncoding encoding = PositionEncoding.Utf8CodeUnitOffsetFromLineStart)
    {
        var result = new StringBuilder();
        for (var line = range.Start.Line; line <= range.End.Line; line++)
        {
            if (line < 0 || line >= Lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "Line is outside the file.");
            }

            var text = Lines[line];
            var start = 0;
            if (line > range.Start.Line)
            {
                result.Append('\n');
            }
            else
            {
                start = ToUtf16(text, range.Start.Character, encoding);
            }

            var end = line < range.End.Line ? text.Length : ToUtf16(text, range.End.Character, encoding);
            if (end < start)
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "Range ends before it starts.");
            }

            result.Append(text, start, end - start);
        }

        return result.ToString();
    }

    private static int ToUtf16(string line, int character, PositionEncoding encoding) =>
        PositionEncodings.Convert(line, character, encoding, PositionEncoding.Utf16CodeUnitOffsetFromLineStart);

    /// <summary>JSON rendering with the same field names as the Go implementation.</summary>
    public override string ToString()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString(nameof(AbsolutePath), AbsolutePath);
            w.WriteString(nameof(RelativePath), RelativePath);
            w.WriteString(nameof(Text), Text);
            w.WriteStartArray(nameof(Lines));
            foreach (var line in Lines)
            {
                w.WriteStringValue(line);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
