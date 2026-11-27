using System.Text;

namespace ScipSharp.Indexer.Cobol.Project;

/// <summary>A source file known to the indexer (a program or a copybook).</summary>
internal sealed class SourceFileEntry(int id, string fullPath, string? relativePath)
{
    private string[]? _lines;

    public int Id { get; } = id;

    public string FullPath { get; } = fullPath;

    /// <summary>Path relative to the project root with '/' separators, or null for files outside the root (no document is emitted).</summary>
    public string? RelativePath { get; } = relativePath;

    public string[] Lines => _lines ??= Syntax.Lexer.SplitLines(SourceFiles.ReadText(FullPath));

    /// <summary>Drops the cached text (main files are read once per pass).</summary>
    public void Release() => _lines = null;
}

/// <summary>Registry of source files by id; ids are stable for the run.</summary>
internal sealed class SourceFiles(string root)
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly List<SourceFileEntry> _files = [];
    private readonly Dictionary<string, int> _byPath = new(StringComparer.Ordinal);

    public string Root { get; } = root;

    public SourceFileEntry this[int id] => _files[id];

    public int GetOrAdd(string fullPath)
    {
        fullPath = Path.GetFullPath(fullPath);
        if (_byPath.TryGetValue(fullPath, out var id))
        {
            return id;
        }

        id = _files.Count;
        var rel = Path.GetRelativePath(Root, fullPath);
        string? relative = rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel.Replace('\\', '/');
        _files.Add(new SourceFileEntry(id, fullPath, relative));
        _byPath[fullPath] = id;
        return id;
    }

    /// <summary>Reads a source file as UTF-8, falling back to Latin-1 for legacy (e.g. EBCDIC-converted) sources.</summary>
    public static string ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var span = bytes.AsSpan();
        if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            span = span[3..];
        }

        try
        {
            return StrictUtf8.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(span);
        }
    }
}
