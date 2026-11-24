using System.Text.Json;
using System.Text.RegularExpressions;

namespace Scip.Indexer.TypeScript.Project;

/// <summary>A resolved tsconfig/jsconfig: the files it covers and the projects it references.</summary>
public sealed record ParsedConfig(List<string> FileNames, List<string> References, bool AllowJs, List<string>? Lib = null, string? Target = null, bool NoLib = false,
    List<string>? CustomConditions = null, string? PathsBase = null, Dictionary<string, List<string>>? Paths = null);

/// <summary>
/// Native tsconfig.json reader (JSONC, <c>extends</c>, <c>include</c>/<c>exclude</c>/<c>files</c>, <c>references</c>,
/// <c>allowJs</c>). A port of the parts of <c>ts.parseJsonConfigFileContent</c> that decide the file list.
/// </summary>
public static class TsConfig
{
    private static readonly JsonDocumentOptions Jsonc = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public const string AllowJsConfig = "{\"compilerOptions\":{\"allowJs\":true}}";
    public const string NoJsConfig = "{}";

    private sealed class Raw
    {
        public List<string>? Files;
        public List<string>? Include;
        public List<string>? Exclude;
        public string Dir = "";
        public string? IncludeDir;
        public string? ExcludeDir;
        public string? FilesDir;
        public bool? AllowJs;
        public List<string>? Lib;
        public string? Target;
        public bool NoLib;
        public List<string>? CustomConditions;
        public string? BaseUrl;
        public Dictionary<string, List<string>>? Paths;
        public string? PathsDir;
        public string? OutDir;
        public List<string> References = [];
        public bool IsJsConfig;
    }

    /// <summary>Loads <paramref name="configFile"/>. Returns null and sets <paramref name="error"/> on invalid configs.</summary>
    public static ParsedConfig? Load(string configFile, out string? error)
    {
        error = null;
        var raw = new Raw { Dir = Path.GetDirectoryName(Path.GetFullPath(configFile))!.Replace('\\', '/'), IsJsConfig = Path.GetFileName(configFile) == "jsconfig.json" };
        try
        {
            Merge(raw, Path.GetFullPath(configFile), top: true, depth: 0);
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            error = $"error: cannot read {configFile}: {e.Message}";
            return null;
        }

        var allowJs = raw.AllowJs ?? raw.IsJsConfig;
        var extensions = allowJs ? new[] { ".ts", ".tsx", ".d.ts", ".mts", ".cts", ".d.mts", ".d.cts", ".js", ".jsx", ".mjs", ".cjs" } : [".ts", ".tsx", ".d.ts", ".mts", ".cts", ".d.mts", ".d.cts"];
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in raw.Files ?? [])
        {
            var full = Path.GetFullPath(Path.Combine(raw.FilesDir ?? raw.Dir, f)).Replace('\\', '/');
            if (File.Exists(full) && seen.Add(full)) { files.Add(full); }
        }

        var includes = raw.Include ?? (raw.Files is null ? ["**/*"] : []);
        var includeDir = raw.IncludeDir ?? raw.Dir;
        var excludes = new List<string>(raw.Exclude ?? ["node_modules", "bower_components", "jspm_packages"]);
        if (raw.Exclude is null && raw.OutDir is not null) { excludes.Add(raw.OutDir); }
        var excludeDir = raw.ExcludeDir ?? raw.Dir;
        var excludeRegexes = excludes.Select(e => PatternToRegex(excludeDir, e, directoryPattern: true)).ToList();

        var wildcard = new List<string>();
        var groupOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var patternIndex = -1;
        foreach (var pattern in includes)
        {
            patternIndex++;
            var regex = PatternToRegex(includeDir, pattern, directoryPattern: true);
            var root = StaticRoot(includeDir, pattern);
            if (!Directory.Exists(root)) { continue; }
            foreach (var file in EnumerateFiles(root, excludeRegexes))
            {
                var norm = file.Replace('\\', '/');
                if (!extensions.Any(e => norm.EndsWith(e, StringComparison.Ordinal))) { continue; }
                if (!regex.IsMatch(norm) || excludeRegexes.Any(r => r.IsMatch(norm))) { continue; }
                wildcard.Add(norm);
                groupOf.TryAdd(norm, patternIndex);
            }
        }

        // When several files share a stem (a.ts, a.d.ts, a.js) TypeScript keeps the highest priority one.
        var kept = new List<string>();
        foreach (var group in wildcard.Distinct().GroupBy(f => StemOf(f)))
        {
            var best = group.OrderBy(f => Priority(f)).First();
            foreach (var f in group.Where(f => Priority(f) == Priority(best)))
            {
                if (seen.Add(f)) { kept.Add(f); }
            }
        }

        // TypeScript's `matchFiles`: results are grouped by the include pattern that matched first; within a group a
        // directory lists its files (sorted) before descending into its sub-directories (sorted).
        kept.Sort((a, b) => groupOf[a] != groupOf[b] ? groupOf[a].CompareTo(groupOf[b]) : CompareTraversal(a, b));
        files.AddRange(kept);
        return new ParsedConfig(files, raw.References, allowJs, raw.Lib, raw.Target, raw.NoLib, raw.CustomConditions, raw.BaseUrl ?? raw.PathsDir, raw.Paths);
    }

    private static int CompareTraversal(string a, string b)
    {
        var pa = a.Split('/');
        var pb = b.Split('/');
        for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            var c = string.CompareOrdinal(pa[i], pb[i]);
            if (c == 0) { continue; }
            var aIsFile = i == pa.Length - 1;
            var bIsFile = i == pb.Length - 1;
            if (aIsFile != bIsFile) { return aIsFile ? -1 : 1; }
            return c;
        }

        return pa.Length.CompareTo(pb.Length);
    }

    /// <summary>
    /// Files that shadow each other share a stem; <c>.ts/.tsx/.d.ts</c>, <c>.cts/.d.cts</c> and <c>.mts/.d.mts</c> are
    /// separate families (TypeScript's <c>removeWildcardFilesWithLowerPriorityExtension</c>), as are the JS ones.
    /// </summary>
    private static string StemOf(string f)
    {
        foreach (var (e, family) in new[] { (".d.ts", "ts"), (".d.mts", "mts"), (".d.cts", "cts") })
        {
            if (f.EndsWith(e, StringComparison.Ordinal)) { return f[..^e.Length] + "\0" + family; }
        }

        var ext = Path.GetExtension(f);
        var family2 = ext switch { ".ts" or ".tsx" => "ts", ".mts" => "mts", ".cts" => "cts", ".js" or ".jsx" => "js", _ => ext };
        return f[..^ext.Length] + "\0" + family2;
    }

    private static int Priority(string f) => f.Contains(".d.", StringComparison.Ordinal) ? 1
        : f.EndsWith(".ts", StringComparison.Ordinal) || f.EndsWith(".tsx", StringComparison.Ordinal) || f.EndsWith(".mts", StringComparison.Ordinal) || f.EndsWith(".cts", StringComparison.Ordinal) ? 0
        : 2;

    private static void Merge(Raw raw, string configFile, bool top, int depth)
    {
        if (depth > 16) { return; }
        using var doc = JsonDocument.Parse(File.ReadAllText(configFile), Jsonc);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) { throw new JsonException("tsconfig must be an object"); }
        var dir = Path.GetDirectoryName(configFile)!.Replace('\\', '/');

        if (root.TryGetProperty("extends", out var ext))
        {
            var bases = ext.ValueKind == JsonValueKind.Array ? ext.EnumerateArray().Select(e => e.GetString()!).ToList() : [ext.GetString()!];
            foreach (var b in bases)
            {
                var resolved = ResolveExtends(dir, b);
                if (resolved is not null) { Merge(raw, resolved, top: false, depth + 1); }
            }
        }

        if (root.TryGetProperty("compilerOptions", out var co) && co.ValueKind == JsonValueKind.Object)
        {
            if (co.TryGetProperty("allowJs", out var aj) && aj.ValueKind is JsonValueKind.True or JsonValueKind.False) { raw.AllowJs = aj.GetBoolean(); }
            if (co.TryGetProperty("lib", out var lb) && lb.ValueKind == JsonValueKind.Array) { raw.Lib = lb.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList(); }
            if (co.TryGetProperty("target", out var tg) && tg.ValueKind == JsonValueKind.String) { raw.Target = tg.GetString(); }
            if (co.TryGetProperty("noLib", out var nl) && nl.ValueKind is JsonValueKind.True or JsonValueKind.False) { raw.NoLib = nl.GetBoolean(); }
            if (co.TryGetProperty("customConditions", out var cc) && cc.ValueKind == JsonValueKind.Array) { raw.CustomConditions = cc.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList(); }
            if (co.TryGetProperty("baseUrl", out var bu) && bu.ValueKind == JsonValueKind.String) { raw.BaseUrl = Path.GetFullPath(Path.Combine(dir, bu.GetString()!)).Replace('\\', '/'); }
            if (co.TryGetProperty("paths", out var pa) && pa.ValueKind == JsonValueKind.Object)
            {
                raw.Paths = [];
                raw.PathsDir = dir;
                foreach (var prop in pa.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array) { raw.Paths[prop.Name] = prop.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList(); }
                }
            }

            if (co.TryGetProperty("outDir", out var od) && od.ValueKind == JsonValueKind.String) { raw.OutDir = od.GetString(); }
        }

        List<string>? Strings(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : null;

        if (Strings("files") is { } files) { raw.Files = files; raw.FilesDir = dir; }
        if (Strings("include") is { } include) { raw.Include = include; raw.IncludeDir = dir; }
        if (Strings("exclude") is { } exclude) { raw.Exclude = exclude; raw.ExcludeDir = dir; }
        if (top && root.TryGetProperty("references", out var refs) && refs.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in refs.EnumerateArray())
            {
                if (r.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String)
                {
                    raw.References.Add(Path.GetFullPath(Path.Combine(dir, p.GetString()!)).Replace('\\', '/'));
                }
            }
        }
    }

    private static string? ResolveExtends(string dir, string spec)
    {
        string? Try(string p)
        {
            if (File.Exists(p)) { return p; }
            if (File.Exists(p + ".json")) { return p + ".json"; }
            var t = Path.Combine(p, "tsconfig.json");
            return File.Exists(t) ? t : null;
        }

        if (spec.StartsWith('.') || Path.IsPathRooted(spec)) { return Try(Path.GetFullPath(Path.Combine(dir, spec))); }
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "node_modules", spec);
            var r = Try(candidate);
            if (r is not null) { return r; }
            var pkg = Path.Combine(candidate, "package.json");
            if (File.Exists(pkg))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(pkg), Jsonc);
                if (doc.RootElement.TryGetProperty("tsconfig", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    var tr = Path.Combine(candidate, t.GetString()!);
                    if (File.Exists(tr)) { return tr; }
                }
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ globbing
    public static string GlobToRegexBody(string glob)
    {
        var sb = new System.Text.StringBuilder();
        if (glob.StartsWith('/')) { sb.Append('/'); }
        var parts = glob.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var last = i == parts.Length - 1;
            if (part == "**")
            {
                sb.Append(last ? ".*" : "(?:[^/]+/)*");
                continue;
            }

            foreach (var c in part)
            {
                sb.Append(c switch { '*' => "[^/]*", '?' => "[^/]", _ => Regex.Escape(c.ToString()) });
            }

            if (!last) { sb.Append('/'); }
        }

        return sb.ToString();
    }

    private static Regex PatternToRegex(string baseDir, string pattern, bool directoryPattern)
    {
        _ = directoryPattern;
        var absolute = Path.GetFullPath(Path.Combine(baseDir, pattern.Replace('\\', '/'))).Replace('\\', '/').TrimEnd('/');
        var last = absolute[(absolute.LastIndexOf('/') + 1)..];
        var body = GlobToRegexBody(absolute);
        // A final component without wildcard or extension names a directory: match everything below it too.
        if (last.Contains('*') || last.Contains('?') || last.Contains('.')) { return new Regex("^" + body + "$", RegexOptions.CultureInvariant); }
        return new Regex("^" + body + "(?:/.*)?$", RegexOptions.CultureInvariant);
    }

    private static string StaticRoot(string baseDir, string pattern)
    {
        pattern = pattern.Replace('\\', '/');
        var parts = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var fixedParts = new List<string>();
        foreach (var p in parts)
        {
            if (p.Contains('*') || p.Contains('?')) { break; }
            fixedParts.Add(p);
        }

        var root = Path.GetFullPath(Path.Combine(baseDir, string.Join('/', fixedParts))).Replace('\\', '/');
        return File.Exists(root) ? Path.GetDirectoryName(root)!.Replace('\\', '/') : root;
    }

    private static IEnumerable<string> EnumerateFiles(string root, List<Regex> excludes)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            var norm = dir.Replace('\\', '/');
            if (excludes.Any(r => r.IsMatch(norm))) { continue; }
            string[] files;
            string[] dirs;
            try
            {
                files = Directory.GetFiles(dir);
                dirs = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var f in files) { yield return f; }
            foreach (var d in dirs) { stack.Push(d); }
        }
    }

    // ------------------------------------------------------------------ port of inferTsconfig.ts
    public static string InferTsconfig(string projectPath)
    {
        var hasTs = false;
        var hasJs = false;
        var visited = 0;
        bool Visit(string path)
        {
            if (path.EndsWith(".ts", StringComparison.Ordinal) || path.EndsWith(".tsx", StringComparison.Ordinal)) { hasTs = true; return true; }
            if (path.EndsWith(".js", StringComparison.Ordinal) || path.EndsWith(".jsx", StringComparison.Ordinal)) { hasJs = true; }
            if (!Directory.Exists(path)) { return false; }
            foreach (var child in Directory.GetFileSystemEntries(path))
            {
                visited++;
                if (visited > 1000) { return true; }
                if (Visit(child)) { return true; }
            }

            return false;
        }

        Visit(projectPath);
        return hasTs || !hasJs ? NoJsConfig : AllowJsConfig;
    }
}
