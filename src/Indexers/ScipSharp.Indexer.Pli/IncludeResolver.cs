namespace ScipSharp.Indexer.Pli;

/// <summary>
/// Locates <c>%INCLUDE</c> members. A member name is tried as given and with every include/source extension, in the
/// including file's directory, the configured include directories and the project root (case-insensitively); as a last
/// resort any project file whose name (without extension, or without <c>.incl.pl1</c>) matches is used.
/// </summary>
internal sealed class IncludeResolver
{
    private readonly string _root;
    private readonly List<string> _directories;
    private readonly List<string> _extensions;
    private readonly Dictionary<string, Dictionary<string, string>> _listings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _byStem = new(StringComparer.OrdinalIgnoreCase);

    public IncludeResolver(string root, PliOptions options, IEnumerable<string> projectFiles)
    {
        _root = root;
        _directories = options.IncludeDirectories.Select(d => Path.GetFullPath(Path.Combine(root, d))).ToList();
        _extensions = ["", .. options.IncludeExtensions, .. options.Extensions, ".pli", ".pl1", ".inc", ".incl.pl1", ".cpy", ".txt"];
        _extensions = _extensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var file in projectFiles.Order(StringComparer.Ordinal))
        {
            foreach (var stem in Stems(Path.GetFileName(file)))
            {
                _byStem.TryAdd(stem, file);
            }
        }
    }

    /// <summary>Resolves <paramref name="member"/> included from <paramref name="includingFile"/> (absolute paths).</summary>
    public string? Resolve(string member, string includingFile)
    {
        member = member.Trim();
        if (member.Length == 0)
        {
            return null;
        }

        var dirs = new List<string> { Path.GetDirectoryName(includingFile) ?? _root };
        dirs.AddRange(_directories);
        dirs.Add(_root);
        foreach (var dir in dirs)
        {
            var listing = Listing(dir);
            foreach (var ext in _extensions)
            {
                var candidate = member + ext;
                if (candidate.Contains('/') || candidate.Contains('\\'))
                {
                    var full = Path.GetFullPath(Path.Combine(dir, candidate));
                    if (File.Exists(full))
                    {
                        return full;
                    }

                    continue;
                }

                if (listing.TryGetValue(candidate, out var found))
                {
                    return found;
                }
            }
        }

        if (_byStem.TryGetValue(Path.GetFileName(member), out var byStem))
        {
            return byStem;
        }

        // Multics segments carry several names: `%include query_info_;` finds query_info.incl.pl1.
        return member.Length > 1 && member.EndsWith('_') ? Resolve(member[..^1], includingFile) : null;
    }

    /// <summary>Names a file can be included by: <c>A.inc</c> → <c>A.inc</c>, <c>A</c>; <c>a.incl.pl1</c> → also <c>a</c>.</summary>
    private static IEnumerable<string> Stems(string fileName)
    {
        yield return fileName;
        var dot = fileName.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0)
        {
            yield return fileName[..dot];
        }
    }

    private Dictionary<string, string> Listing(string dir)
    {
        if (_listings.TryGetValue(dir, out var listing))
        {
            return listing;
        }

        listing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.EnumerateFiles(dir).Order(StringComparer.Ordinal))
            {
                listing.TryAdd(Path.GetFileName(f), f);
            }
        }

        _listings[dir] = listing;
        return listing;
    }
}
