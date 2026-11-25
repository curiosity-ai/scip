using System.Text.Json;
using System.Text.RegularExpressions;
using ScipSharp.Indexer.Php.Syntax;

namespace ScipSharp.Indexer.Php.Composer;

/// <summary>
/// Native replacement for upstream's <c>Composer</c> class. Instead of <c>require</c>-ing composer's generated
/// autoloader and asking the PHP runtime (<c>class_exists</c>, <c>function_exists</c>, reflection, phpstorm-stubs), it
/// reads <c>composer.json</c>, <c>composer.lock</c> and <c>vendor/composer/*.php</c> directly and emulates composer's
/// <c>ClassLoader</c> lookup rules.
/// </summary>
internal sealed class ComposerProject
{
    private static readonly char Sep = Path.DirectorySeparatorChar;

    private readonly PhpFs _fs;
    private readonly Action<string>? _warn;
    private readonly string _root;
    private readonly string _vendorDir;
    private readonly string _phpVersion;

    private readonly Dictionary<string, string> _classMap = new(StringComparer.Ordinal);
    private readonly List<(string Prefix, List<string> Dirs)> _psr4 = [];
    private readonly List<(string Prefix, List<string> Dirs)> _psr0 = [];
    private readonly Dictionary<string, string> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _consts = new(StringComparer.Ordinal);
    private readonly List<(string Path, string Name, string Version)> _pkgsByPaths = [];
    private readonly Dictionary<string, string?> _classFileCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _fileExists = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _findFileCache = new(StringComparer.Ordinal);

    public ComposerProject(string projectRoot, string phpVersion, PhpFs fs, Action<string>? warn = null)
    {
        _fs = fs;
        _warn = warn;
        _phpVersion = phpVersion;
        _root = fs.Realpath(projectRoot) ?? Path.GetFullPath(projectRoot);

        var jsonPath = Path.Combine(_root, "composer.json");
        var jsonText = File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : SynthesizeComposerJson(_root);
        if (jsonText == null)
        {
            throw new InvalidOperationException($"Cannot read {jsonPath}.");
        }

        using var doc = JsonDocument.Parse(jsonText, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var json = doc.RootElement;

        var vendor = "vendor";
        if (json.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object
            && config.TryGetProperty("vendor-dir", out var vd) && vd.ValueKind == JsonValueKind.String)
        {
            var dir = vd.GetString()!.Trim('/');
            if (dir != "")
            {
                vendor = dir;
            }
        }

        var vendorPath = Path.Combine(_root, vendor);
        _vendorDir = _fs.Realpath(vendorPath) ?? vendorPath;

        PkgName = json.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String ? nm.GetString()! : "__root__";
        PkgVersion = "dev";
        LoadInstalled();
        LoadAutoloaders();

        var files = new List<string>();
        if (json.TryGetProperty("bin", out var bin) && bin.ValueKind == JsonValueKind.Array)
        {
            files.AddRange(CollectPaths(bin));
        }

        var autoload = json.TryGetProperty("autoload", out var al) ? al : default;
        var autoloadDev = json.TryGetProperty("autoload-dev", out var ad) ? ad : default;
        files.AddRange(LoadProjectFiles(autoload));
        files.AddRange(LoadProjectFiles(autoloadDev));
        ProjectFiles = files.Distinct(StringComparer.Ordinal).ToList();

        // Classes of project files that the autoloader cannot find (e.g. several classes in one file).
        foreach (var f in ProjectFiles)
        {
            AddAdditionalClasses(f);
        }

        LoadLockAutoloadFiles();
        IndexAutoloadFiles();
    }

    /// <summary>
    /// Plain PHP projects (e.g. WordPress) have no <c>composer.json</c>. Index every PHP file below the root (except
    /// <c>vendor</c>, <c>node_modules</c> and dot directories) as if it were listed in <c>autoload.files</c>.
    /// Returns null when there is no PHP file at all.
    /// </summary>
    private static string? SynthesizeComposerJson(string root)
    {
        var files = new List<string>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
        foreach (var f in Directory.EnumerateFiles(root, "*.php", options).OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/');
            if (rel.StartsWith("vendor/", StringComparison.Ordinal) || rel.StartsWith("node_modules/", StringComparison.Ordinal)
                || rel.StartsWith('.') || rel.Contains("/.", StringComparison.Ordinal) || rel.Contains("/node_modules/", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(rel);
        }

        if (files.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(new Dictionary<string, object> { ["autoload"] = new Dictionary<string, object> { ["files"] = files } });
    }

    public string PkgName { get; private set; }

    public string PkgVersion { get; private set; }

    public string Root => _root;

    public IReadOnlyList<string> ProjectFiles { get; }

    // ------------------------------------------------------------------ loading

    private void LoadInstalled()
    {
        var installedPath = Path.Combine(_vendorDir, "composer", "installed.php");
        PhpArr? installed = File.Exists(installedPath) ? PhpArrayEval.EvaluateReturnedArray(installedPath, _fs) : null;
        if (installed?.Get("root") is PhpArr rootInfo)
        {
            if (rootInfo.Get("name") is string name && name != "")
            {
                PkgName = name;
            }

            if ((rootInfo.Get("reference") ?? rootInfo.Get("version")) is string v && v != "")
            {
                PkgVersion = v;
            }
        }
        else
        {
            _warn?.Invoke($"Cannot read {installedPath}: package versions are unknown. Did you run 'composer install'?");
        }

        if (installed?.Get("versions") is PhpArr versions)
        {
            foreach (var kv in versions.Items)
            {
                // Replaced packages do not have an install path.
                if (kv.Value is not PhpArr info || info.Get("install_path") is not string install)
                {
                    continue;
                }

                var path = _fs.Realpath(install);
                if (path == null)
                {
                    _warn?.Invoke($"Invalid install path of package {kv.Key}: {install}.");
                    continue;
                }

                if (kv.Key != PkgName && info.Get("reference") is string reference && reference != "")
                {
                    _pkgsByPaths.Add((path, kv.Key, reference));
                }
            }
        }

        _pkgsByPaths.Add((Path.Combine(_vendorDir, "composer"), "composer", "dev"));
    }

    private void LoadAutoloaders()
    {
        var composerDir = Path.Combine(_vendorDir, "composer");
        if (ReadMap(Path.Combine(composerDir, "autoload_classmap.php")) is { } classmap)
        {
            foreach (var kv in classmap.Items)
            {
                if (kv.Value is string p)
                {
                    _classMap[kv.Key] = _fs.Realpath(p) ?? p;
                }
            }
        }

        // composer's own loader class is `require`d by autoload_real.php and therefore not part of the classmap.
        var loaderFile = Path.Combine(composerDir, "ClassLoader.php");
        if (File.Exists(loaderFile) && _fs.Parse(loaderFile, _warn) is { } loaderAst)
        {
            foreach (var c in Declarations.Scan(loaderAst).Classes)
            {
                _classMap.TryAdd(c, _fs.Realpath(loaderFile) ?? loaderFile);
            }
        }

        LoadPrefixes(Path.Combine(composerDir, "autoload_psr4.php"), _psr4);
        LoadPrefixes(Path.Combine(composerDir, "autoload_namespaces.php"), _psr0);
    }

    private PhpArr? ReadMap(string path) => File.Exists(path) ? PhpArrayEval.EvaluateReturnedArray(path, _fs) : null;

    private void LoadPrefixes(string path, List<(string Prefix, List<string> Dirs)> into)
    {
        if (ReadMap(path) is not { } map)
        {
            return;
        }

        foreach (var kv in map.Items)
        {
            var dirs = new List<string>();
            if (kv.Value is PhpArr arr)
            {
                dirs.AddRange(arr.Items.Select(i => i.Value).OfType<string>());
            }
            else if (kv.Value is string s)
            {
                dirs.Add(s);
            }

            into.Add((kv.Key, dirs));
        }
    }

    private List<string> CollectPaths(JsonElement paths)
    {
        var files = new List<string>();
        foreach (var p in paths.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(p.GetString()))
            {
                continue;
            }

            var real = _fs.Realpath(Path.Combine(_root, p.GetString()!));
            if (real != null)
            {
                files.Add(real);
            }
        }

        return files;
    }

    /// <summary>Port of <c>Composer::loadProjectFiles</c> (uses a native classmap scan instead of composer/class-map-generator).</summary>
    private List<string> LoadProjectFiles(JsonElement autoload)
    {
        var result = new List<string>();
        if (autoload.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        Regex? exclusion = null;
        if (autoload.TryGetProperty("exclude-from-classmap", out var ex) && ex.ValueKind == JsonValueKind.Array)
        {
            var parts = ex.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String && e.GetString() != "").Select(e => e.GetString()!).ToList();
            if (parts.Count > 0)
            {
                try
                {
                    exclusion = new Regex("(" + string.Join('|', parts) + ")", RegexOptions.CultureInvariant);
                }
                catch (ArgumentException)
                {
                    exclusion = null;
                }
            }
        }

        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (autoload.TryGetProperty("classmap", out var cm) && cm.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in cm.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String && e.GetString() != ""))
            {
                ScanPaths(Path.Combine(_root, p.GetString()!), exclusion, null, null, map);
            }
        }

        foreach (var type in new[] { "psr-4", "psr-0" })
        {
            if (!autoload.TryGetProperty(type, out var section) || section.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entry in section.EnumerateObject())
            {
                if (entry.Name == "")
                {
                    continue;
                }

                var paths = entry.Value.ValueKind == JsonValueKind.String
                    ? [entry.Value.GetString()!]
                    : entry.Value.ValueKind == JsonValueKind.Array
                        ? entry.Value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
                        : new List<string>();
                foreach (var p in paths.Where(p => p != ""))
                {
                    ScanPaths(Path.Combine(_root, p).TrimEnd(Sep), exclusion, type, entry.Name, map);
                }
            }
        }

        var classFiles = map.Values.Distinct(StringComparer.Ordinal).ToList();
        if (autoload.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            result.AddRange(CollectPaths(files));
        }

        result.AddRange(classFiles);
        return result;
    }

    private void ScanPaths(string path, Regex? exclusion, string? type, string? ns, SortedDictionary<string, string> map)
    {
        IEnumerable<string> files;
        if (File.Exists(path))
        {
            files = [path];
        }
        else if (Directory.Exists(path))
        {
            files = Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System })
                .Where(f => f.EndsWith(".php", StringComparison.Ordinal) || f.EndsWith(".inc", StringComparison.Ordinal) || f.EndsWith(".hh", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal);
        }
        else
        {
            return;
        }

        var basePath = _fs.Realpath(path) ?? path;
        foreach (var f in files)
        {
            var real = _fs.Realpath(f);
            if (real == null)
            {
                continue;
            }

            var slashed = real.Replace(Sep, '/');
            if (slashed.Contains("/.git/", StringComparison.Ordinal) || (exclusion != null && exclusion.IsMatch(slashed)))
            {
                continue;
            }

            var parsed = _fs.Parse(real, _warn);
            if (parsed == null)
            {
                continue;
            }

            foreach (var cls in Declarations.Scan(parsed).Classes)
            {
                if (type != null && !MatchesPsr(cls, real, basePath, type, ns!))
                {
                    continue;
                }

                map.TryAdd(cls, real);
            }
        }
    }

    private static bool MatchesPsr(string cls, string file, string basePath, string type, string ns)
    {
        var baseNs = ns.TrimEnd('\\');
        if (baseNs != "")
        {
            baseNs += "\\";
        }

        var rel = Path.GetRelativePath(basePath, file);
        if (rel.StartsWith("..", StringComparison.Ordinal))
        {
            return true;
        }

        var dot = rel.LastIndexOf('.');
        var noExt = (dot > 0 ? rel[..dot] : rel).Replace(Sep, '\\');
        if (type == "psr-4")
        {
            return cls == baseNs + noExt;
        }

        return cls == noExt || cls.Replace('_', '\\') == noExt.Replace('_', '\\') || cls.StartsWith(baseNs, StringComparison.Ordinal);
    }

    private void AddAdditionalClasses(string file)
    {
        var parsed = _fs.Parse(file, _warn);
        if (parsed == null)
        {
            return;
        }

        foreach (var c in Declarations.Scan(parsed).Classes)
        {
            if (FindClassFile(c) == null)
            {
                _classMap[c] = file;
                _classFileCache.Remove(c);
                _findFileCache.Remove(c);
            }
        }
    }

    private void LoadLockAutoloadFiles()
    {
        var lockPath = Path.Combine(_root, "composer.lock");
        if (!File.Exists(lockPath))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
            if (!doc.RootElement.TryGetProperty("packages", out var pkgs) || pkgs.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var pkg in pkgs.EnumerateArray())
            {
                if (!pkg.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String
                    || !pkg.TryGetProperty("autoload", out var al) || al.ValueKind != JsonValueKind.Object
                    || !al.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var f in files.EnumerateArray().Where(f => f.ValueKind == JsonValueKind.String && f.GetString() != ""))
                {
                    var path = Path.Combine(_vendorDir, n.GetString()!, f.GetString()!);
                    var real = _fs.Realpath(path);
                    if (real != null)
                    {
                        AddAdditionalClasses(real);
                    }
                }
            }
        }
        catch (JsonException e)
        {
            _warn?.Invoke($"Cannot parse composer.lock: {e.Message}");
        }
    }

    /// <summary>
    /// Functions and constants exist at runtime only if their file was loaded; composer loads exactly the files of
    /// <c>autoload_files.php</c>. They are indexed by scanning those files.
    /// </summary>
    private void IndexAutoloadFiles()
    {
        var filesMap = ReadMap(Path.Combine(_vendorDir, "composer", "autoload_files.php"));
        var files = filesMap?.Items.Select(i => i.Value).OfType<string>().Select(p => _fs.Realpath(p) ?? p).ToList() ?? [];

        // Without generated metadata, fall back to the project's own files.
        if (filesMap == null)
        {
            files.AddRange(ProjectFiles);
        }

        foreach (var f in files)
        {
            var parsed = _fs.Parse(f, _warn);
            if (parsed == null)
            {
                continue;
            }

            var d = Declarations.Scan(parsed);
            foreach (var fn in d.Functions)
            {
                _functions.TryAdd(fn, f);
            }

            foreach (var c in d.Constants)
            {
                _consts.TryAdd(c, f);
            }
        }
    }

    // ------------------------------------------------------------------ class loader emulation

    private bool Exists(string path)
    {
        if (!_fileExists.TryGetValue(path, out var e))
        {
            e = File.Exists(path);
            _fileExists[path] = e;
        }

        return e;
    }

    /// <summary>Port of composer's <c>ClassLoader::findFile</c> (classmap, PSR-4, PSR-0 and fallback directories).</summary>
    private string? FindClassFile(string cls)
    {
        cls = cls.TrimStart('\\');
        if (_classFileCache.TryGetValue(cls, out var cached))
        {
            return cached;
        }

        var found = FindClassFileUncached(cls);
        if (found != null)
        {
            found = _fs.Realpath(found);
        }

        _classFileCache[cls] = found;
        return found;
    }

    private string? FindClassFileUncached(string cls)
    {
        if (_classMap.TryGetValue(cls, out var mapped))
        {
            return mapped;
        }

        var logical4 = cls.Replace('\\', Sep) + ".php";
        var subPath = cls;
        int lastPos;
        while ((lastPos = subPath.LastIndexOf('\\')) >= 0)
        {
            subPath = subPath[..lastPos];
            var search = subPath + "\\";
            foreach (var (prefix, dirs) in _psr4)
            {
                if (prefix != search)
                {
                    continue;
                }

                var pathEnd = Sep + logical4[(lastPos + 1)..];
                foreach (var dir in dirs)
                {
                    if (Exists(dir + pathEnd))
                    {
                        return dir + pathEnd;
                    }
                }
            }
        }

        foreach (var (prefix, dirs) in _psr4)
        {
            if (prefix == "")
            {
                foreach (var dir in dirs)
                {
                    if (Exists(dir + Sep + logical4))
                    {
                        return dir + Sep + logical4;
                    }
                }
            }
        }

        string logical0;
        var pos = cls.LastIndexOf('\\');
        if (pos >= 0)
        {
            logical0 = logical4[..(pos + 1)] + logical4[(pos + 1)..].Replace('_', Sep);
        }
        else
        {
            logical0 = cls.Replace('_', Sep) + ".php";
        }

        foreach (var (prefix, dirs) in _psr0)
        {
            if (prefix == "" || !cls.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var dir in dirs)
            {
                if (Exists(dir + Sep + logical0))
                {
                    return dir + Sep + logical0;
                }
            }
        }

        foreach (var (prefix, dirs) in _psr0)
        {
            if (prefix == "")
            {
                foreach (var dir in dirs)
                {
                    if (Exists(dir + Sep + logical0))
                    {
                        return dir + Sep + logical0;
                    }
                }
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ queries (mirror upstream's Composer API)

    public bool IsDependency(string ident) => !IsFromProject(ident);

    public bool IsConst(string c) => Builtins.Constants.Contains(c) || _consts.ContainsKey(c);

    public bool IsClassLike(string c)
    {
        if (Builtins.Classes.Contains(c) || Builtins.IsEnumInterface(c) || c.Contains("anon-class-", StringComparison.Ordinal))
        {
            return true;
        }

        var file = FindClassFile(c);
        if (file == null)
        {
            return false;
        }

        // The loader also "finds" the file of a namespaced function (PSR-4 maps its name to a path). Like upstream, only
        // call it a class when no such function exists or the file really declares the class.
        if (!_functions.ContainsKey(c))
        {
            return true;
        }

        var parsed = _fs.Parse(file, _warn);
        return parsed != null && Declarations.Scan(parsed).Classes.Contains(c, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsFunc(string f) =>
        _functions.ContainsKey(f) || Builtins.Functions.Contains(f) || f.Contains("anon-func-", StringComparison.Ordinal);

    public bool IsStub(string ident) => Stub(ident) != null || Builtins.IsEnumInterface(ident);

    private static string? Stub(string ident) =>
        Builtins.Classes.Contains(ident) ? "class" : Builtins.Functions.Contains(ident) ? "function" : Builtins.Constants.Contains(ident) ? "const" : null;

    /// <summary>The file that defines <paramref name="ident"/>; built-ins yield a <c>stub:</c> pseudo path.</summary>
    public string? FindFile(string ident)
    {
        if (_findFileCache.TryGetValue(ident, out var cached))
        {
            return cached;
        }

        string? result;
        if (Stub(ident) != null)
        {
            result = "stub:" + ident;
        }
        else
        {
            result = FindClassFile(ident);
            if (result == null && _functions.TryGetValue(ident, out var ff))
            {
                result = _fs.Realpath(ff) ?? ff;
            }

            if (result == null && _consts.TryGetValue(ident, out var cf))
            {
                result = _fs.Realpath(cf) ?? cf;
            }
        }

        _findFileCache[ident] = result;
        return result;
    }

    public (string Name, string Version)? Pkg(string ident)
    {
        if (IsStub(ident))
        {
            return ("php", _phpVersion);
        }

        if (IsFromProject(ident))
        {
            return (PkgName, PkgVersion);
        }

        var f = FindFile(ident);
        if (f == null)
        {
            return null;
        }

        foreach (var (path, name, version) in _pkgsByPaths)
        {
            if (InDir(f, path))
            {
                return (name, version);
            }
        }

        return null;
    }

    private static bool InDir(string file, string dir) =>
        file.StartsWith(dir, StringComparison.Ordinal) && (file.Length == dir.Length || file[dir.Length] == Sep);

    private bool IsFromProject(string ident)
    {
        if (ident.Contains("anon-class-", StringComparison.Ordinal) || ident.Contains("anon-func-", StringComparison.Ordinal))
        {
            return true;
        }

        if (IsStub(ident))
        {
            return false;
        }

        var f = FindFile(ident);
        if (f == null)
        {
            return false;
        }

        foreach (var (path, _, _) in _pkgsByPaths)
        {
            if (InDir(f, path))
            {
                return false;
            }
        }

        return !InDir(f, _vendorDir);
    }
}
