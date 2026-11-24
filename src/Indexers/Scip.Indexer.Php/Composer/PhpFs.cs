using System.Text;
using Scip.Indexer.Php.Syntax;

namespace Scip.Indexer.Php.Composer;

/// <summary>File helpers: <c>realpath</c> and cached parsing of PHP files.</summary>
internal sealed class PhpFs
{
    private readonly Dictionary<string, string?> _real = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ParsedFile?> _parsed = new(StringComparer.Ordinal);

    public static string ReadLatin1(string path) => Encoding.Latin1.GetString(File.ReadAllBytes(path));

    /// <summary>Resolves symlinks and relative segments; returns null when the path does not exist.</summary>
    public string? Realpath(string path)
    {
        if (_real.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var result = Resolve(path, 0);
        _real[path] = result;
        return result;
    }

    private static string? Resolve(string path, int depth)
    {
        if (depth > 32)
        {
            return null;
        }

        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "/";
        var current = root;
        var rest = full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in rest)
        {
            var next = Path.Combine(current, seg);
            FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            if (!info.Exists && info.LinkTarget == null)
            {
                return null;
            }

            if (info.LinkTarget != null)
            {
                var target = info.LinkTarget;
                if (!Path.IsPathRooted(target))
                {
                    target = Path.Combine(current, target);
                }

                var resolved = Resolve(target, depth + 1);
                if (resolved == null)
                {
                    return null;
                }

                next = resolved;
            }

            current = next;
        }

        return current.Length > 1 ? current.TrimEnd(Path.DirectorySeparatorChar) : current;
    }

    /// <summary>Parses a file once; returns null (and remembers) if it cannot be read or parsed.</summary>
    public ParsedFile? Parse(string path, Action<string>? onError = null)
    {
        if (_parsed.TryGetValue(path, out var cached))
        {
            return cached;
        }

        ParsedFile? file = null;
        try
        {
            var text = ReadLatin1(path);
            if (text.Length == 0)
            {
                // Upstream refuses empty files as well ("Cannot parse file").
                throw new ParseException("empty file", 0);
            }

            file = PhpParser.Parse(text);
        }
        catch (Exception e) when (e is ParseException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
        {
            onError?.Invoke($"{path}: {e.Message}");
        }

        _parsed[path] = file;
        return file;
    }

    /// <summary>Drops the cached AST of a file (used for project files once they are indexed).</summary>
    public void Forget(string path) => _parsed.Remove(path);
}
