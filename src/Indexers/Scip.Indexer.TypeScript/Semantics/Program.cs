using System.Text.Json;
using Scip.Indexer.TypeScript.Syntax;

namespace Scip.Indexer.TypeScript.Semantics;

/// <summary>A set of parsed source files plus module resolution. Files are parsed lazily, so imports into other
/// project files and <c>node_modules</c> declaration files resolve without being indexed.</summary>
public sealed class TsProgram
{
    private readonly Dictionary<string, TsSourceFile?> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Binder> _binders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _moduleCache = new(StringComparer.Ordinal);

    public Action<string>? Warn { get; set; }

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
                parsed = Parser.Parse(fileName, File.ReadAllText(fileName));
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

        _files[fileName] = parsed;
        return parsed;
    }

    public Binder BinderFor(TsSourceFile file)
    {
        if (!_binders.TryGetValue(file.FileName, out var b))
        {
            b = new Binder(this, file);
            _binders[file.FileName] = b;
        }

        return b;
    }

    private static readonly string[] Extensions = [".ts", ".tsx", ".d.ts", ".mts", ".cts", ".d.mts", ".d.cts", ".js", ".jsx", ".mjs", ".cjs"];

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
        if (File.Exists(path) && !path.EndsWith(".json", StringComparison.Ordinal))
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
                            var r = (File.Exists(target) ? target : null) ?? TryFile(target) ?? TryFile(Path.Combine(target, "index"));
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
            resolved = ResolveModulePath(dir, specifier);
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

    private static string? ResolveModulePath(string dir, string specifier)
    {
        if (specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal) || specifier is "." or ".." || Path.IsPathRooted(specifier))
        {
            return TryResolveFileOrDirectory(Path.GetFullPath(Path.Combine(dir, specifier)));
        }

        if (specifier.StartsWith("node:", StringComparison.Ordinal)) { specifier = specifier[5..]; }
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            var nm = Path.Combine(d.FullName, "node_modules");
            if (!Directory.Exists(nm)) { continue; }
            var r = TryResolveFileOrDirectory(Path.Combine(nm, specifier));
            if (r is not null) { return RealPath(r); }
            var typesName = specifier.StartsWith('@') ? specifier[1..].Replace("/", "__", StringComparison.Ordinal) : specifier;
            r = TryResolveFileOrDirectory(Path.Combine(nm, "@types", typesName));
            if (r is not null) { return RealPath(r); }
        }

        return null;
    }
}
