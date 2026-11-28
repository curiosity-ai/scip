namespace ScipSharp.Indexer.Cobol.Project;

/// <summary>
/// Finds the file named by <c>COPY name [OF|IN library]</c>. Search order: a path-like literal relative to the including
/// file; a directory named like the library; the including file's directory; the configured copybook directories (in
/// order); finally any project file whose stem matches, preferring copybook extensions, conventional copybook
/// directory names, then the shortest path. Matching is case-insensitive, as on z/OS PDS members.
/// </summary>
internal sealed class CopybookResolver
{
    /// <summary>Directory names conventionally holding copybooks.</summary>
    private static readonly HashSet<string> ConventionalDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "copy", "copybook", "copybooks", "copylib", "copylibs", "cpy", "cpys", "include", "includes", "inc", "cobcopy", "copy-bms", "cpy-bms",
    };

    private readonly string _root;
    private readonly IReadOnlyList<string> _dirs;
    private readonly IReadOnlyList<string> _extensions;
    private readonly HashSet<string> _copybookExtensions;
    private readonly Dictionary<string, List<string>> _byStem = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _dirsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _listings = new(StringComparer.Ordinal);
    private readonly HashSet<(List<string> List, string Value)> _added = [];

    public CopybookResolver(string root, IReadOnlyList<string> copybookDirs, IReadOnlyList<string> copybookExtensions,
        IReadOnlyList<string> programExtensions, IEnumerable<string> projectFiles)
    {
        _root = root;
        _dirs = copybookDirs.Select(d => Path.GetFullPath(Path.Combine(root, d))).ToList();
        _copybookExtensions = new HashSet<string>(copybookExtensions.Select(e => "." + e.TrimStart('.')), StringComparer.OrdinalIgnoreCase);
        _extensions = [.. copybookExtensions.Concat(programExtensions).Select(e => "." + e.TrimStart('.')).Distinct(StringComparer.OrdinalIgnoreCase)];
        foreach (var file in projectFiles.Concat(_dirs.Where(Directory.Exists).SelectMany(SafeFiles)))
        {
            var ext = Path.GetExtension(file);
            if (ext.Length > 0 && !_extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            Add(_byStem, Path.GetFileNameWithoutExtension(file), file);
            if (ext.Length > 0)
            {
                Add(_byStem, Path.GetFileName(file), file);
            }

            var dir = Path.GetDirectoryName(file)!;
            Add(_dirsByName, Path.GetFileName(dir), dir);
        }
    }

    private static IEnumerable<string> SafeFiles(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Add(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }

        // Duplicate check through a set: list.Contains was quadratic when many directories share a name (mod*/cpy).
        if (_added.Add((list, value)))
        {
            list.Add(value);
        }
    }

    /// <summary>Returns the full path of the copybook, or null when it cannot be found.</summary>
    public string? Resolve(string name, string? library, string? includerDirectory)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return null;
        }

        if (name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal))
        {
            var rel = name.Replace('\\', '/');
            foreach (var baseDir in new[] { includerDirectory }.Concat(_dirs).Append(_root))
            {
                if (baseDir is not null && TryInDirectory(Path.GetDirectoryName(Path.Combine(baseDir, rel))!, Path.GetFileName(rel)) is { } p)
                {
                    return p;
                }
            }

            return null;
        }

        var searchDirs = new List<string>();
        if (library is { Length: > 0 })
        {
            if (_dirsByName.TryGetValue(library, out var libs))
            {
                searchDirs.AddRange(libs.Order(StringComparer.Ordinal));
            }

            foreach (var d in _dirs)
            {
                searchDirs.Add(Path.Combine(d, library));
            }
        }

        if (includerDirectory is not null)
        {
            searchDirs.Add(includerDirectory);
        }

        searchDirs.AddRange(_dirs);
        foreach (var dir in searchDirs)
        {
            if (TryInDirectory(dir, name) is { } found)
            {
                return found;
            }
        }

        if (!_byStem.TryGetValue(name, out var candidates))
        {
            return null;
        }

        return candidates
            .OrderBy(c => _copybookExtensions.Contains(Path.GetExtension(c)) ? 0 : Path.GetExtension(c).Length == 0 ? 1 : 2)
            .ThenBy(c => ConventionalDirs.Contains(Path.GetFileName(Path.GetDirectoryName(c)!)) ? 0 : 1)
            .ThenBy(c => c.Length)
            .ThenBy(c => c, StringComparer.Ordinal)
            .First();
    }

    private string? TryInDirectory(string dir, string name)
    {
        if (!_listings.TryGetValue(dir, out var listing))
        {
            listing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(dir))
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir).Order(StringComparer.Ordinal))
                    {
                        listing.TryAdd(Path.GetFileName(f), f);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            _listings[dir] = listing;
        }

        if (Path.HasExtension(name) && listing.TryGetValue(name, out var exact))
        {
            return exact;
        }

        foreach (var ext in _extensions)
        {
            if (listing.TryGetValue(name + ext, out var p))
            {
                return p;
            }
        }

        return listing.TryGetValue(name, out var bare) ? bare : null;
    }
}
