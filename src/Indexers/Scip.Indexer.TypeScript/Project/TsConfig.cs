using System.Text.Json;
using System.Text.RegularExpressions;

namespace Scip.Indexer.TypeScript.Project;

/// <summary>A resolved tsconfig/jsconfig: the files it covers and the projects it references.</summary>
public sealed record ParsedConfig(List<string> FileNames, List<string> References, bool AllowJs);

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
        foreach (var pattern in includes)
        {
            var regex = PatternToRegex(includeDir, pattern, directoryPattern: true);
            var root = StaticRoot(includeDir, pattern);
            if (!Directory.Exists(root)) { continue; }
            foreach (var file in EnumerateFiles(root, excludeRegexes))
            {
                var norm = file.Replace('\\', '/');
                if (!extensions.Any(e => norm.EndsWith(e, StringComparison.Ordinal))) { continue; }
                if (!regex.IsMatch(norm) || excludeRegexes.Any(r => r.IsMatch(norm))) { continue; }
                wildcard.Add(norm);
            }
        }

        // When several files share a stem (a.ts, a.d.ts, a.js) TypeScript keeps the highest priority one.
        foreach (var group in wildcard.Distinct().GroupBy(f => StemOf(f)))
        {
            var best = group.OrderBy(f => Priority(f)).First();
            foreach (var f in group.Where(f => Priority(f) == Priority(best)).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (seen.Add(f)) { files.Add(f); }
            }
        }

        files.Sort(StringComparer.Ordinal);
        return new ParsedConfig(files, raw.References, allowJs);
    }

    private static string StemOf(string f)
    {
        foreach (var e in new[] { ".d.ts", ".d.mts", ".d.cts" })
        {
            if (f.EndsWith(e, StringComparison.Ordinal)) { return f[..^e.Length]; }
        }

        return f[..^Path.GetExtension(f).Length];
    }

    private static int Priority(string f) => f.EndsWith(".ts", StringComparison.Ordinal) && !f.EndsWith(".d.ts", StringComparison.Ordinal) || f.EndsWith(".tsx", StringComparison.Ordinal) ? 0
        : f.Contains(".d.", StringComparison.Ordinal) ? 1 : 2;

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
