using System.Text.Json;
using ScipSharp.Indexer.TypeScript.Syntax;

namespace ScipSharp.Indexer.TypeScript.Semantics;

/// <summary>A set of parsed source files plus module resolution. Files are parsed lazily, so imports into other
/// project files and <c>node_modules</c> declaration files resolve without being indexed.</summary>
public sealed class TsProgram
{
    private readonly Dictionary<string, TsSourceFile?> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Binder> _binders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _moduleCache = new(StringComparer.Ordinal);

    public Action<string>? Warn { get; set; }

    /// <summary>Conditions used to resolve package.json <c>exports</c> (besides <c>types</c>/<c>import</c>/<c>node</c>/<c>default</c>).</summary>
    public List<string> CustomConditions { get; set; } = [];

    /// <summary><c>paths</c> mappings of the tsconfig and the directory they are relative to.</summary>
    public Dictionary<string, List<string>>? Paths { get; set; }

    public string? PathsBase { get; set; }

    /// <summary>The built-in lib declarations visible as globals in this program.</summary>
    public LibSet Libs { get; set; } = LibSet.Empty;

    public static string Normalize(string path) => Path.GetFullPath(path).Replace('\\', '/');

    public TsSourceFile? GetFile(string fileName)
    {
        fileName = Normalize(fileName);
        if (_files.TryGetValue(fileName, out var existing)) { return existing; }
        TsSourceFile? parsed = null;
        try
        {
            if (File.Exists(fileName))
            {
                var source = File.ReadAllText(fileName);
                parsed = Parser.Parse(fileName, source);
                foreach (var (pos, message) in parsed.Diagnostics)
                {
                    var (l, c) = parsed.LineAndCharacter(Math.Min(pos, source.Length));
                    Warn?.Invoke($"warning: syntax error in {fileName}:{l + 1}:{c + 1}: {message}");
                }
            }
        }
        catch (SyntaxException e)
        {
            var text = File.ReadAllText(fileName);
            var (line, ch) = new TsSourceFile { FileName = fileName, Text = text }.LineAndCharacter(Math.Min(e.Position, text.Length));
            Warn?.Invoke($"warning: syntax error in {fileName}:{line + 1}:{ch + 1}: {e.Message}");
        }
        catch (IOException e)
        {
            Warn?.Invoke($"warning: cannot read {fileName}: {e.Message}");
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException or InsufficientExecutionStackException)
        {
            // A parser bug on one file must not abort the whole project.
            Warn?.Invoke($"warning: cannot parse {fileName}: {e.GetType().Name}: {e.Message}");
        }

        _files[fileName] = parsed;
        return parsed;
    }

    /// <summary>
    /// The order <c>ts.createProgram</c> lists source files in: roots in order, each file preceded by the files it imports
    /// (depth first). Symbols shared across files (anonymous type literals, object literal properties) are numbered by the
    /// first file that names them, so indexing in this order reproduces the numbering of the original indexer.
    /// </summary>
    public List<string> ProgramOrder(IReadOnlyList<string> roots)
    {
        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<(string Name, IEnumerator<string>? Imports)>();
        foreach (var root in roots)
        {
            var normalized = Normalize(root);
            if (!seen.Add(normalized)) { continue; }
            stack.Push((normalized, ImportedFiles(normalized).GetEnumerator()));
            while (stack.Count > 0)
            {
                var (name, imports) = stack.Peek();
                if (imports is not null && imports.MoveNext())
                {
                    var next = imports.Current;
                    if (seen.Add(next)) { stack.Push((next, ImportedFiles(next).GetEnumerator())); }
                    continue;
                }

                stack.Pop();
                order.Add(name);
            }
        }

        return order;
    }

    private IEnumerable<string> ImportedFiles(string fileName)
    {
        var file = GetFile(fileName);
        if (file is null || file.FileName.Contains("/node_modules/", StringComparison.Ordinal)) { yield break; }
        var specifiers = new List<string>();
        foreach (var st in file.Root.Elements)
        {
            switch (st.Kind)
            {
                case SK.ImportDeclaration or SK.ExportDeclaration when st.ModuleSpecifier is { Kind: SK.StringLiteral } ms:
                    specifiers.Add(ms.Text);
                    break;
                case SK.ImportEqualsDeclaration when st.Expression is { Kind: SK.ExternalModuleReference, Expression: { Kind: SK.StringLiteral } er }:
                    specifiers.Add(er.Text);
                    break;
            }
        }

        // dynamic `import("x")` and (in JavaScript) `require("x")`
        var stack = new Stack<Node>();
        stack.Push(file.Root);
        var dynamic = new List<(int Pos, string Text)>();
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.Kind == SK.ImportCall && n.Arguments is [{ Kind: SK.StringLiteral } a]) { dynamic.Add((n.Start, a.Text)); }
            else if (file.IsJavaScript && n.Kind == SK.CallExpression && n.Expression is { Kind: SK.Identifier, Text: "require" } && n.Arguments is [{ Kind: SK.StringLiteral } r]) { dynamic.Add((n.Start, r.Text)); }
            foreach (var c in n.Children) { stack.Push(c); }
        }

        specifiers.AddRange(dynamic.OrderBy(d => d.Pos).Select(d => d.Text));
        foreach (var spec in specifiers)
        {
            var resolved = ResolveModule(file, spec);
            if (resolved is not null) { yield return resolved.FileName; }
        }
    }

    private readonly Dictionary<string, List<Node>> _augmentations = new(StringComparer.Ordinal);

    /// <summary>
    /// Finds <c>declare module "x" { ... }</c> augmentations in <paramref name="fileNames"/> (cheap text pre-filter, then
    /// parse) so that exports of module x can include declarations merged in from other files.
    /// </summary>
    public void RegisterAugmentations(IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            string text;
            try { text = File.ReadAllText(fileName); }
            catch (IOException) { continue; }
            if (!text.Contains("declare module", StringComparison.Ordinal)) { continue; }
            var file = GetFile(fileName);
            if (file is null) { continue; }
            foreach (var st in file.Root.Elements)
            {
                if (st.Kind != SK.ModuleDeclaration || st.Name is not { Kind: SK.StringLiteral } name) { continue; }
                var target = ResolveModule(file, name.Text);
                if (target is null) { continue; }
                if (!_augmentations.TryGetValue(target.FileName, out var list)) { _augmentations[target.FileName] = list = []; }
                list.Add(st);
            }
        }
    }

    public List<Node> AugmentationsOf(TsSourceFile file) => _augmentations.TryGetValue(file.FileName, out var l) ? l : [];

    public Binder BinderFor(TsSourceFile file)
    {
        if (file.IsLib && !ReferenceEquals(this, LibStore.Program)) { return LibStore.Program.BinderFor(file); }
        if (!_binders.TryGetValue(file.FileName, out var b))
        {
            b = new Binder(this, file);
            _binders[file.FileName] = b;
        }

        return b;
    }

    private static readonly string[] Extensions = [".ts", ".tsx", ".d.ts", ".mts", ".cts", ".d.mts", ".d.cts", ".js", ".jsx", ".mjs", ".cjs"];

    private static bool IsSourceFile(string path) => Array.Exists(Extensions, e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private static string? TryFile(string basePath)
    {
        foreach (var ext in Extensions)
        {
            if (File.Exists(basePath + ext)) { return basePath + ext; }
        }

        return null;
    }

    private static string? TryResolveFileOrDirectory(string path)
    {
        if (File.Exists(path) && IsSourceFile(path))
        {
            return path;
        }

        // `./a.js` may refer to `./a.ts`.
        foreach (var (from, tos) in new[] { (".js", new[] { ".ts", ".tsx", ".d.ts" }), (".jsx", [".tsx"]), (".mjs", [".mts", ".d.mts"]), (".cjs", [".cts", ".d.cts"]) })
        {
            if (path.EndsWith(from, StringComparison.Ordinal))
            {
                var stem = path[..^from.Length];
                foreach (var to in tos)
                {
                    if (File.Exists(stem + to)) { return stem + to; }
                }
            }
        }

        var f = TryFile(path);
        if (f is not null) { return f; }
        if (Directory.Exists(path))
        {
            var pkg = Path.Combine(path, "package.json");
            if (File.Exists(pkg))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(pkg), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                    foreach (var key in new[] { "types", "typings", "main" })
                    {
                        if (doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                        {
                            var target = Path.GetFullPath(Path.Combine(path, v.GetString()!));
                            var r = (File.Exists(target) && IsSourceFile(target) ? target : null) ?? TryFile(target) ?? TryFile(Path.Combine(target, "index"));
                            if (r is not null && !r.EndsWith(".json", StringComparison.Ordinal)) { return r; }
                        }
                    }
                }
                catch (JsonException)
                {
                    // ignore malformed package.json
                }
            }

            return TryFile(Path.Combine(path, "index"));
        }

        return null;
    }

    /// <summary>Resolves a module specifier as TypeScript's node module resolution would (simplified).</summary>
    public TsSourceFile? ResolveModule(TsSourceFile from, string specifier)
    {
        var dir = Path.GetDirectoryName(from.FileName)!;
        var key = dir + "\0" + specifier;
        if (!_moduleCache.TryGetValue(key, out var resolved))
        {
            resolved = ResolveModulePath(dir, specifier, from.FileName);
            _moduleCache[key] = resolved;
        }

        return resolved is null ? null : GetFile(resolved);
    }

    /// <summary>Resolves symlinked directories (npm/pnpm/yarn workspaces link packages into node_modules).</summary>
    public static string RealPath(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var current = Path.GetPathRoot(full) ?? "/";
            foreach (var part in full[current.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var next = Path.Combine(current, part);
                if (new DirectoryInfo(next).LinkTarget is not null && new DirectoryInfo(next).ResolveLinkTarget(true) is { } target)
                {
                    next = target.FullName;
                }

                current = next;
            }

            return current;
        }
        catch (IOException)
        {
            return path;
        }
    }

    private string? ResolveModulePath(string dir, string specifier, string fromFile)
    {
        if (specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal) || specifier is "." or ".." || Path.IsPathRooted(specifier))
        {
            return TryResolveFileOrDirectory(Path.GetFullPath(Path.Combine(dir, specifier)));
        }

        if (specifier.StartsWith("node:", StringComparison.Ordinal)) { specifier = specifier[5..]; }
        if (Paths is not null && PathsBase is not null)
        {
            var mapped = ResolvePaths(specifier);
            if (mapped is not null) { return mapped; }
        }

        var (packageName, subpath) = SplitPackageSpecifier(specifier);
        var isRequire = fromFile.EndsWith(".cts", StringComparison.Ordinal) || fromFile.EndsWith(".cjs", StringComparison.Ordinal);
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            // Self-reference: a package can import itself by name through its own `exports`.
            var ownPackage = Path.Combine(d.FullName, "package.json");
            if (File.Exists(ownPackage) && ReadPackage(ownPackage) is { } own && own.Name == packageName && own.Exports is { } ownExports)
            {
                var viaExports = ResolveExports(d.FullName, ownExports, subpath, isRequire);
                if (viaExports is not null) { return RealPath(viaExports); }
            }

            var nm = Path.Combine(d.FullName, "node_modules");
            if (!Directory.Exists(nm)) { continue; }
            var packageDir = Path.Combine(nm, packageName);
            var manifest = Path.Combine(packageDir, "package.json");
            if (File.Exists(manifest) && ReadPackage(manifest) is { Exports: { } exports })
            {
                var viaExports = ResolveExports(packageDir, exports, subpath, isRequire);
                if (viaExports is not null) { return RealPath(viaExports); }
            }

            var r = TryResolveFileOrDirectory(Path.Combine(nm, specifier));
            if (r is not null) { return RealPath(r); }
            var typesName = specifier.StartsWith('@') ? specifier[1..].Replace("/", "__", StringComparison.Ordinal) : specifier;
            r = TryResolveFileOrDirectory(Path.Combine(nm, "@types", typesName));
            if (r is not null) { return RealPath(r); }
        }

        return null;
    }

    private string? ResolvePaths(string specifier)
    {
        foreach (var (pattern, targets) in Paths!)
        {
            string? star = null;
            var star1 = pattern.IndexOf('*', StringComparison.Ordinal);
            if (star1 < 0) { if (pattern != specifier) { continue; } }
            else
            {
                var prefix = pattern[..star1];
                var suffix = pattern[(star1 + 1)..];
                if (specifier.Length < prefix.Length + suffix.Length || !specifier.StartsWith(prefix, StringComparison.Ordinal) || !specifier.EndsWith(suffix, StringComparison.Ordinal)) { continue; }
                star = specifier[prefix.Length..^suffix.Length];
            }

            foreach (var target in targets)
            {
                var path = star is null ? target : target.Replace("*", star, StringComparison.Ordinal);
                var r = TryResolveFileOrDirectory(Path.GetFullPath(Path.Combine(PathsBase!, path)));
                if (r is not null) { return r; }
            }
        }

        return null;
    }

    private static (string Package, string Subpath) SplitPackageSpecifier(string specifier)
    {
        var parts = specifier.Split('/');
        var count = specifier.StartsWith('@') ? Math.Min(2, parts.Length) : 1;
        var name = string.Join('/', parts.Take(count));
        var rest = parts.Length > count ? "./" + string.Join('/', parts.Skip(count)) : ".";
        return (name, rest);
    }

    private sealed record PackageManifest(string? Name, JsonElement? Exports);

    private readonly Dictionary<string, PackageManifest?> _manifests = new(StringComparer.Ordinal);

    private PackageManifest? ReadPackage(string path)
    {
        if (_manifests.TryGetValue(path, out var cached)) { return cached; }
        PackageManifest? result = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                JsonElement? exports = root.TryGetProperty("exports", out var e) ? e.Clone() : null;
                result = new PackageManifest(name, exports);
            }
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // unreadable manifest: treat as absent
        }

        _manifests[path] = result;
        return result;
    }

    private string? ResolveExports(string packageDir, JsonElement exports, string subpath, bool isRequire)
    {
        JsonElement? target = null;
        string? star = null;
        if (exports.ValueKind == JsonValueKind.Object && exports.EnumerateObject().Any(p => p.Name.StartsWith('.')))
        {
            if (exports.TryGetProperty(subpath, out var direct)) { target = direct; }
            else
            {
                foreach (var prop in exports.EnumerateObject())
                {
                    var i = prop.Name.IndexOf('*', StringComparison.Ordinal);
                    if (i < 0) { continue; }
                    var prefix = prop.Name[..i];
                    var suffix = prop.Name[(i + 1)..];
                    if (subpath.Length >= prefix.Length + suffix.Length && subpath.StartsWith(prefix, StringComparison.Ordinal) && subpath.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        target = prop.Value;
                        star = subpath[prefix.Length..^suffix.Length];
                        break;
                    }
                }
            }
        }
        else if (subpath == ".") { target = exports; }

        return target is null ? null : ResolveExportTarget(packageDir, target.Value, star, isRequire, 0);
    }

    private string? ResolveExportTarget(string packageDir, JsonElement target, string? star, bool isRequire, int depth)
    {
        if (depth > 8) { return null; }
        switch (target.ValueKind)
        {
            case JsonValueKind.String:
            {
                var t = target.GetString()!;
                if (star is not null) { t = t.Replace("*", star, StringComparison.Ordinal); }
                var full = Path.GetFullPath(Path.Combine(packageDir, t));
                return TryResolveFileOrDirectory(full);
            }

            case JsonValueKind.Array:
                foreach (var item in target.EnumerateArray())
                {
                    var r = ResolveExportTarget(packageDir, item, star, isRequire, depth + 1);
                    if (r is not null) { return r; }
                }

                return null;
            case JsonValueKind.Object:
                foreach (var prop in target.EnumerateObject())
                {
                    var name = prop.Name;
                    var active = name == "default" || name == "types" || name == "node" || name == (isRequire ? "require" : "import") || CustomConditions.Contains(name);
                    if (!active) { continue; }
                    var r = ResolveExportTarget(packageDir, prop.Value, star, isRequire, depth + 1);
                    if (r is not null) { return r; }
                }

                return null;
            default:
                return null;
        }
    }
}
