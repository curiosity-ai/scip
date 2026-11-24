using System.Text.Json;
using Scip.Indexer.Python.Syntax;

namespace Scip.Indexer.Python.Semantics;

/// <summary>A pip-installed distribution (port of <c>PythonPackage.ts</c>).</summary>
public sealed class PythonPackage(string name, string version, IReadOnlyList<string> files)
{
    public string Name { get; } = name;

    public string Version { get; } = version;

    public IReadOnlyList<string> Files { get; } = files;
}

/// <summary>The installed distributions, with module-name to package lookups (port of <c>PythonEnvironment.ts</c>).</summary>
public sealed class PythonEnvironment(IReadOnlyList<PythonPackage> packages)
{
    private Dictionary<string, int>? _moduleToPackage;

    public IReadOnlyList<PythonPackage> Packages { get; } = packages;

    public PythonPackage? GuessPackage(string moduleName)
    {
        var first = moduleName.Split('.')[0].ToLowerInvariant();
        return Packages.FirstOrDefault(p => p.Name.Equals(first, StringComparison.OrdinalIgnoreCase));
    }

    public PythonPackage? GetPackageForModule(string moduleName)
    {
        if (_moduleToPackage is null)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < Packages.Count; i++)
            {
                foreach (var file in Packages[i].Files)
                {
                    var noExt = file[..(file.Length - Path.GetExtension(file).Length)];
                    var normalized = noExt.Replace('\\', '.').Replace('/', '.').ToLowerInvariant();
                    map.TryAdd(normalized, i);
                    if (normalized.EndsWith(".__init__", StringComparison.Ordinal))
                    {
                        map.TryAdd(normalized[..^".__init__".Length], i);
                    }
                }
            }

            _moduleToPackage = map;
        }

        return _moduleToPackage.TryGetValue(moduleName.ToLowerInvariant(), out var idx) ? Packages[idx] : null;
    }

    /// <summary>Loads the experimental <c>--environment</c> JSON file: <c>[{name, version, files}]</c>.</summary>
    public static PythonEnvironment FromJsonFile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var list = new List<PythonPackage>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var files = e.TryGetProperty("files", out var f) ? f.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
            list.Add(new PythonPackage(e.GetProperty("name").GetString() ?? "", e.GetProperty("version").GetString() ?? "", files));
        }

        return new PythonEnvironment(list);
    }

    /// <summary>
    /// Discovers installed distributions natively by reading <c>*.dist-info</c> metadata (<c>METADATA</c> and <c>RECORD</c>) in
    /// the given site-packages directories. No Python or pip process is involved.
    /// </summary>
    public static PythonEnvironment FromSitePackages(IEnumerable<string> siteDirs)
    {
        var list = new List<PythonPackage>();
        foreach (var dir in siteDirs.Where(Directory.Exists))
        {
            foreach (var dist in Directory.EnumerateDirectories(dir, "*.dist-info").OrderBy(d => d, StringComparer.Ordinal))
            {
                try
                {
                    string? name = null, version = null;
                    var metadata = Path.Combine(dist, "METADATA");
                    if (File.Exists(metadata))
                    {
                        foreach (var line in File.ReadLines(metadata))
                        {
                            if (line.Length == 0)
                            {
                                break;
                            }

                            if (line.StartsWith("Name:", StringComparison.Ordinal))
                            {
                                name ??= line[5..].Trim();
                            }
                            else if (line.StartsWith("Version:", StringComparison.Ordinal))
                            {
                                version ??= line[8..].Trim();
                            }
                        }
                    }

                    if (name is null)
                    {
                        var stem = Path.GetFileName(dist)[..^".dist-info".Length];
                        var dash = stem.LastIndexOf('-');
                        name = dash > 0 ? stem[..dash] : stem;
                        version ??= dash > 0 ? stem[(dash + 1)..] : "";
                    }

                    var files = new List<string>();
                    var record = Path.Combine(dist, "RECORD");
                    if (File.Exists(record))
                    {
                        foreach (var line in File.ReadLines(record))
                        {
                            var comma = line.IndexOf(',');
                            var file = (comma >= 0 ? line[..comma] : line).Trim('"');
                            if (file.StartsWith("..", StringComparison.Ordinal) || file.Contains("__pycache__", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (file.EndsWith(".py", StringComparison.Ordinal) || file.EndsWith(".pyi", StringComparison.Ordinal))
                            {
                                files.Add(file);
                            }
                        }
                    }

                    list.Add(new PythonPackage(name, version ?? "", files));
                }
                catch (IOException)
                {
                    // unreadable distribution: skip
                }
            }
        }

        return new PythonEnvironment(list);
    }
}

/// <summary>Result of resolving an import to a file on disk.</summary>
/// <param name="FilePath">The module file (<c>.py</c>/<c>.pyi</c>), or null for namespace packages and stdlib modules.</param>
public sealed record ResolvedModule(string? FilePath, bool IsNamespacePackage, bool IsStdlib, string? DirectoryPath);

/// <summary>
/// Import resolution, module loading/caching and project file discovery. Everything is read from the file system;
/// the standard library is recognised by name only (see <see cref="Hardcoded.StdlibModuleNames"/>).
/// </summary>
public sealed class ModuleSystem
{
    private readonly Dictionary<string, SourceModule?> _modules = new(StringComparer.Ordinal);
    private readonly HashSet<string> _wildcardInProgress = [];
    private readonly Dictionary<Node, SourceModule> _byTree = [];

    public ModuleSystem(string projectRoot, IReadOnlyList<string> extraPaths, IReadOnlyList<string> siteDirs, PythonEnvironment environment)
    {
        ProjectRoot = Path.GetFullPath(projectRoot);
        var roots = new List<string> { ProjectRoot };
        roots.AddRange(extraPaths);
        SearchRoots = roots;
        SitePackages = siteDirs;
        Environment = environment;
    }

    public string ProjectRoot { get; }

    public IReadOnlyList<string> SearchRoots { get; }

    public IReadOnlyList<string> SitePackages { get; }

    public PythonEnvironment Environment { get; }

    public bool IsUnderProject(string path) =>
        Path.GetFullPath(path).StartsWith(ProjectRoot, StringComparison.Ordinal);

    /// <summary>
    /// Dotted module name of a file as pyright's <c>getModuleNameForImport</c> computes it: relative to the project root or to
    /// an extra import root, whichever gives the shortest name; site-packages files are relative to their site directory.
    /// </summary>
    public string ModuleNameOf(string path)
    {
        path = Path.GetFullPath(path);
        var roots = SearchRoots.Select(Path.GetFullPath)
            .Where(r => path.StartsWith(r.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToList();
        if (roots.Count == 0)
        {
            var site = SitePackages.Select(Path.GetFullPath).FirstOrDefault(s => path.StartsWith(s, StringComparison.Ordinal));
            if (site is null)
            {
                return Path.GetFileNameWithoutExtension(path);
            }

            roots.Add(site);
        }

        return roots.Select(r => NameRelativeTo(r, path)).OrderBy(n => n.Split('.', StringSplitOptions.RemoveEmptyEntries).Length).First();
    }

    private static string NameRelativeTo(string root, string path)
    {
        var rel = Path.GetRelativePath(root, path);
        rel = rel[..(rel.Length - Path.GetExtension(rel).Length)].Replace('\\', '/');
        var parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 0 && parts[^1] == "__init__")
        {
            parts.RemoveAt(parts.Count - 1);
        }

        return string.Join('.', parts);
    }

    public SourceModule? Load(string path)
    {
        path = Path.GetFullPath(path);
        if (_modules.TryGetValue(path, out var existing))
        {
            return existing;
        }

        _modules[path] = null; // guards re-entrancy
        SourceModule? module = null;
        try
        {
            var text = File.ReadAllText(path);
            var tree = Parser.Parse(text);
            module = new SourceModule(path, ModuleNameOf(path), text, tree, IsUnderProject(path)) { Modules = this };
            _byTree[tree] = module;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        _modules[path] = module;
        return module;
    }

    /// <summary>The source module that contains <paramref name="node"/>.</summary>
    public SourceModule ModuleOf(Node node)
    {
        var root = node;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        return _byTree[root];
    }

    public ResolvedModule? Resolve(ModuleNameNode node, SourceModule from) =>
        Resolve(node.NameParts.Select(p => p.Value).ToList(), node.LeadingDots, from);

    public ResolvedModule? Resolve(IReadOnlyList<string> parts, int leadingDots, SourceModule? from)
    {
        if (leadingDots > 0)
        {
            if (from is null)
            {
                return null;
            }

            var dir = Path.GetDirectoryName(from.Path)!;
            for (var i = 1; i < leadingDots; i++)
            {
                dir = Path.GetDirectoryName(dir) ?? dir;
            }

            return ResolveIn(dir, parts, allowEmpty: true);
        }

        foreach (var root in SearchRoots)
        {
            var r = ResolveIn(root, parts, allowEmpty: false);
            if (r is not null)
            {
                return r;
            }
        }

        // typeshed's stdlib stubs win over a sibling module of the importing file (`import typing` next to a typing.py)
        if (parts.Count > 0 && (Hardcoded.StdlibModuleNames.Contains(parts[0]) || Hardcoded.IsTypeshedOnlyStdlibModule(parts[0])))
        {
            return new ResolvedModule(null, false, true, null);
        }

        // pyright also tries the directory of the importing file
        if (from is not null && ResolveIn(Path.GetDirectoryName(from.Path)!, parts, allowEmpty: false) is { } local)
        {
            return local;
        }

        foreach (var root in SitePackages)
        {
            var r = ResolveIn(root, parts, allowEmpty: false);
            if (r is not null)
            {
                return r;
            }
        }

        return parts.Count > 0 && Hardcoded.StdlibModuleNames.Contains(parts[0]) ? new ResolvedModule(null, false, true, null) : null;
    }

    private static ResolvedModule? ResolveIn(string root, IReadOnlyList<string> parts, bool allowEmpty)
    {
        var dir = root;
        if (parts.Count == 0)
        {
            return allowEmpty ? InitOf(dir) : null;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (i == parts.Count - 1)
            {
                foreach (var ext in new[] { ".pyi", ".py" })
                {
                    var f = Path.Combine(dir, part + ext);
                    if (File.Exists(f))
                    {
                        return new ResolvedModule(f, false, false, null);
                    }
                }

                var sub = Path.Combine(dir, part);
                return Directory.Exists(sub) ? InitOf(sub) : null;
            }

            dir = Path.Combine(dir, part);
            if (!Directory.Exists(dir))
            {
                return null;
            }
        }

        return null;
    }

    private static ResolvedModule? InitOf(string dir)
    {
        foreach (var ext in new[] { ".pyi", ".py" })
        {
            var f = Path.Combine(dir, "__init__" + ext);
            if (File.Exists(f))
            {
                return new ResolvedModule(f, false, false, dir);
            }
        }

        return Directory.Exists(dir) ? new ResolvedModule(null, true, false, dir) : null;
    }

    /// <summary>Names bound by <c>from m import *</c>: <c>__all__</c> if present, otherwise public top-level names.</summary>
    public IEnumerable<string> WildcardNames(SourceModule from, ImportFromNode node)
    {
        var resolved = Resolve(node.Module, from);
        if (resolved?.FilePath is null)
        {
            return [];
        }

        if (!_wildcardInProgress.Add(resolved.FilePath))
        {
            return [];
        }

        try
        {
            var target = Load(resolved.FilePath);
            if (target is null)
            {
                return [];
            }

            var all = FindDunderAll(target);
            if (all is not null)
            {
                return all;
            }

            return target.ModuleScope.Symbols
                .Where(kv => !kv.Key.StartsWith('_') && kv.Value.Declarations.Any(d => d.MemberOwner is null))
                .Select(kv => kv.Key)
                .ToList();
        }
        finally
        {
            _wildcardInProgress.Remove(resolved.FilePath);
        }
    }

    private static List<string>? FindDunderAll(SourceModule module)
    {
        foreach (var stmt in module.Tree.Statements)
        {
            if (stmt is AssignmentNode { Targets: [NameNode { Value: "__all__" }], Value: SequenceNode seq })
            {
                return seq.Items.OfType<StringListNode>().Select(s => string.Concat(s.Strings.Select(x => x.Value))).ToList();
            }
        }

        return null;
    }

    /// <summary>Enumerates the <c>.py</c>/<c>.pyi</c> files below <paramref name="root"/> honouring simple glob excludes.</summary>
    public static IEnumerable<string> EnumerateSourceFiles(string root, Func<string, bool> isExcluded)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            if (File.Exists(Path.Combine(dir, "pyvenv.cfg")) || File.Exists(Path.Combine(dir, "bin", "activate"))
                || File.Exists(Path.Combine(dir, "Scripts", "activate")))
            {
                continue; // auto-exclude virtual environments
            }

            foreach (var f in Directory.EnumerateFiles(dir).OrderBy(x => x, StringComparer.Ordinal))
            {
                if ((f.EndsWith(".py", StringComparison.Ordinal) || f.EndsWith(".pyi", StringComparison.Ordinal)) && !isExcluded(f))
                {
                    yield return f;
                }
            }

            foreach (var d in Directory.EnumerateDirectories(dir).OrderByDescending(x => x, StringComparer.Ordinal))
            {
                if (!isExcluded(d))
                {
                    stack.Push(d);
                }
            }
        }
    }
}
