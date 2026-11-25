using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.TypeScript.Project;

/// <summary>
/// Native workspace discovery. Upstream shells out to <c>yarn</c>/<c>pnpm</c>; we read the same information
/// (<c>workspaces</c> in package.json, <c>pnpm-workspace.yaml</c>) directly.
/// </summary>
public static class Workspaces
{
    private static readonly JsonDocumentOptions Jsonc = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static List<string> FromPackageJson(string directory, bool requireName = false)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "package.json")), Jsonc);
        var patterns = new List<string>();
        if (doc.RootElement.TryGetProperty("workspaces", out var ws))
        {
            var array = ws.ValueKind == JsonValueKind.Array ? ws : ws.ValueKind == JsonValueKind.Object && ws.TryGetProperty("packages", out var p) ? p : default;
            if (array.ValueKind == JsonValueKind.Array)
            {
                patterns.AddRange(array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!));
            }
        }

        var found = Expand(directory, patterns);
        return requireName ? found.Where(HasName).ToList() : found;
    }

    private static bool HasName(string dir)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "package.json")), Jsonc);
            return doc.RootElement.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static List<string> FromPnpm(string directory)
    {
        var patterns = new List<string>();
        var file = Path.Combine(directory, "pnpm-workspace.yaml");
        if (File.Exists(file))
        {
            var inPackages = false;
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.TrimEnd();
                if (line.StartsWith("packages:", StringComparison.Ordinal)) { inPackages = true; continue; }
                if (inPackages)
                {
                    var m = Regex.Match(line, @"^\s*-\s*['""]?([^'""#]+?)['""]?\s*(#.*)?$");
                    if (m.Success) { patterns.Add(m.Groups[1].Value); }
                    else if (line.Length > 0 && !char.IsWhiteSpace(line[0])) { inPackages = false; }
                }
            }
        }

        var result = Expand(directory, patterns).Where(HasName).ToList();
        if (File.Exists(Path.Combine(directory, "package.json"))) { result.Insert(0, directory.Replace('\\', '/')); }
        return result;
    }

    /// <summary>Directories under <paramref name="directory"/> matching workspace globs (with <c>!</c> negation) that contain a package.json.</summary>
    public static List<string> Expand(string directory, List<string> patterns)
    {
        var includes = new List<string>();
        var excludes = new List<Regex>();
        foreach (var raw in patterns)
        {
            var negated = raw.StartsWith('!');
            var pattern = raw.TrimStart('!').TrimStart('.', '/').TrimEnd('/');
            var regex = new Regex("^" + TsConfig.GlobToRegexBody(pattern) + "$", RegexOptions.CultureInvariant);
            if (negated) { excludes.Add(regex); } else { includes.Add(pattern); }
        }

        var includeRegexes = includes.Select(i => new Regex("^" + TsConfig.GlobToRegexBody(i) + "$", RegexOptions.CultureInvariant)).ToList();
        var result = new List<string>();
        var root = Path.GetFullPath(directory);
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.GetDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (name is "node_modules" or ".git") { continue; }
                var rel = Path.GetRelativePath(root, sub).Replace('\\', '/');
                if (includeRegexes.Any(r => r.IsMatch(rel)) && !excludes.Any(r => r.IsMatch(rel)) && File.Exists(Path.Combine(sub, "package.json")))
                {
                    result.Add(sub.Replace('\\', '/'));
                }

                stack.Push(sub);
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
