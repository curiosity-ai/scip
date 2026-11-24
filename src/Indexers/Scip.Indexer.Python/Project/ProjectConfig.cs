using System.Text;
using System.Text.RegularExpressions;

namespace Scip.Indexer.Python.Project;

/// <summary>
/// A deliberately small TOML reader: tables, <c>key = "string"</c> and arrays of strings. That is all scip-python reads from
/// <c>pyproject.toml</c> (project name/version and the <c>[tool.scip]</c>/<c>[tool.pyright]</c> file selection).
/// </summary>
public static class MiniToml
{
    public static Dictionary<string, Dictionary<string, object>> Parse(string text)
    {
        var tables = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal) { [""] = [] };
        var current = tables[""];
        var currentName = "";
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                var name = line.TrimStart('[').TrimEnd(']').Trim();
                name = string.Join('.', name.Split('.').Select(p => p.Trim().Trim('"', '\'')));
                currentName = name;
                if (!tables.TryGetValue(name, out current!))
                {
                    tables[name] = current = [];
                }

                continue;
            }

            var eq = IndexOfTopLevelEquals(line);
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim().Trim('"', '\'');
            var value = line[(eq + 1)..].Trim();
            if (value.StartsWith('{'))
            {
                // inline table: `key = { a = "x", b = "y" }` becomes table `<current>.key`
                var inner = new Dictionary<string, object>();
                foreach (var pair in value.Trim('{', '}').Split(','))
                {
                    var peq = IndexOfTopLevelEquals(pair);
                    if (peq > 0 && pair[(peq + 1)..].Trim() is { Length: >= 2 } pv && pv[0] is '"' or '\'')
                    {
                        inner[pair[..peq].Trim().Trim('"', '\'')] = pv[1..pv.LastIndexOf(pv[0])];
                    }
                }

                tables[currentName.Length == 0 ? key : currentName + "." + key] = inner;
            }
            else if (value.StartsWith('['))
            {
                var sb = new StringBuilder(value);
                while (Depth(sb.ToString()) > 0 && i + 1 < lines.Length)
                {
                    sb.Append(' ').Append(StripComment(lines[++i]));
                }

                current[key] = TopLevelStrings(sb.ToString());
            }
            else if (value.StartsWith("\"\"\"", StringComparison.Ordinal) || value.StartsWith("'''", StringComparison.Ordinal))
            {
                var delim = value[..3];
                var body = new StringBuilder(value[3..]);
                while (!body.ToString().Contains(delim, StringComparison.Ordinal) && i + 1 < lines.Length)
                {
                    body.Append('\n').Append(lines[++i]);
                }

                var s = body.ToString();
                var end = s.IndexOf(delim, StringComparison.Ordinal);
                current[key] = end >= 0 ? s[..end] : s;
            }
            else if (value.Length >= 2 && value[0] is '"' or '\'')
            {
                var close = value.LastIndexOf(value[0]);
                current[key] = value[1..(close > 0 ? close : value.Length)];
            }
        }

        return tables;
    }

    /// <summary>
    /// True when some array mixes element kinds (string, number, table, array...), which TOML 0.5 parsers such as
    /// <c>@iarna/toml</c> reject. Only a lexical scan: strings and comments are skipped.
    /// </summary>
    public static bool HasMixedTypeArray(string text)
    {
        // Frame kind: 'A' array, 'T' inline table; First is the kind of the first element of an array.
        var stack = new Stack<(char Frame, char First)>();
        var mixed = false;

        void Element(char kind)
        {
            if (stack.Count == 0 || stack.Peek().Frame != 'A')
            {
                return;
            }

            var top = stack.Pop();
            if (top.First == '\0')
            {
                top.First = kind;
            }
            else if (top.First != kind)
            {
                mixed = true;
            }

            stack.Push(top);
        }

        for (var i = 0; i < text.Length && !mixed; i++)
        {
            var c = text[i];
            if (c == '#')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c is '"' or '\'')
            {
                Element('s');
                if (i + 2 < text.Length && text[i + 1] == c && text[i + 2] == c)
                {
                    var end = text.IndexOf(new string(c, 3), i + 3, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 2;
                }
                else
                {
                    var j = i + 1;
                    while (j < text.Length && text[j] != c && text[j] != '\n')
                    {
                        j += c == '"' && text[j] == '\\' ? 2 : 1;
                    }

                    i = j;
                }
            }
            else if (c == '[')
            {
                if (stack.Count == 0)
                {
                    // a table header (the '[' starts the line)
                    var lineStart = i == 0 ? 0 : text.LastIndexOf('\n', i - 1) + 1;
                    if (text[lineStart..i].Trim().Length == 0)
                    {
                        while (i < text.Length && text[i] != '\n')
                        {
                            i++;
                        }

                        continue;
                    }
                }

                Element('a');
                stack.Push(('A', '\0'));
            }
            else if (c == '{')
            {
                Element('t');
                stack.Push(('T', '\0'));
            }
            else if (c is ']' or '}')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
            else if (stack.Count > 0 && stack.Peek().Frame == 'A' && !char.IsWhiteSpace(c) && c != ',')
            {
                var j = i;
                while (j < text.Length && !char.IsWhiteSpace(text[j]) && text[j] is not (',' or ']' or '}'))
                {
                    j++;
                }

                Element(c is 't' or 'f' ? 'b' : 'n');
                i = j - 1;
            }
        }

        return mixed;
    }

    private static int IndexOfTopLevelEquals(string line)
    {
        var q = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (q != '\0')
            {
                if (c == q)
                {
                    q = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                q = c;
            }
            else if (c == '=')
            {
                return i;
            }
        }

        return -1;
    }

    private static string StripComment(string line)
    {
        var q = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (q != '\0')
            {
                if (c == '\\' && q == '"')
                {
                    i++;
                }
                else if (c == q)
                {
                    q = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                q = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static int Depth(string s)
    {
        var depth = 0;
        var q = '\0';
        foreach (var c in s)
        {
            if (q != '\0')
            {
                if (c == q)
                {
                    q = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                q = c;
            }
            else if (c is '[' or '{')
            {
                depth++;
            }
            else if (c is ']' or '}')
            {
                depth--;
            }
        }

        return depth;
    }

    private static List<string> TopLevelStrings(string s)
    {
        var list = new List<string>();
        var depth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c is '[' or '{')
            {
                depth++;
            }
            else if (c is ']' or '}')
            {
                depth--;
            }
            else if (c is '"' or '\'')
            {
                var end = s.IndexOf(c, i + 1);
                if (end < 0)
                {
                    break;
                }

                if (depth == 1)
                {
                    list.Add(s[(i + 1)..end]);
                }

                i = end;
            }
        }

        return list;
    }
}

/// <summary>Project name/version and file selection, read from <c>pyproject.toml</c> (cf. <c>indexer.ts</c> and <c>config.ts</c>).</summary>
public sealed class ProjectConfig
{
    private static readonly string[] DefaultExcludes = ["**/node_modules", "**/__pycache__", "**/.*"];

    public string? PyprojectPath { get; private init; }

    public string ConfigDirectory { get; private init; } = "";

    public string? Name { get; private init; }

    public string? Version { get; private init; }

    public List<string> Include { get; } = [];

    public List<string> Exclude { get; } = [];

    public List<string> ExtraPaths { get; } = [];

    public static ProjectConfig Load(string projectRoot)
    {
        var path = FindPyproject(projectRoot);
        if (path is null)
        {
            return new ProjectConfig { ConfigDirectory = projectRoot };
        }

        Dictionary<string, Dictionary<string, object>> toml;
        try
        {
            var contents = File.ReadAllText(path);
            if (MiniToml.HasMixedTypeArray(contents))
            {
                // scip-python 0.6.8 parses pyproject.toml with a TOML 0.5 reader (@iarna/toml 2.x) that rejects arrays mixing
                // value types (e.g. `[{include-group = "x"}, "y"]`). The failure is swallowed: no name, version or
                // [tool.scip]/[tool.pyright] settings are read, but the project root stays the pyproject directory.
                return new ProjectConfig { PyprojectPath = path, ConfigDirectory = Path.GetDirectoryName(path)! };
            }

            toml = MiniToml.Parse(contents);
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
        {
            return new ProjectConfig { ConfigDirectory = projectRoot };
        }

        string? Str(string table, string key) =>
            toml.TryGetValue(table, out var t) && t.TryGetValue(key, out var v) ? v as string : null;

        var name = Str("project", "name");
        var version = Str("project", "version");
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
        {
            name = string.IsNullOrEmpty(name) ? Str("tool.poetry", "name") : name;
            version = string.IsNullOrEmpty(version) ? Str("tool.poetry", "version") : version;
        }

        var cfg = new ProjectConfig
        {
            PyprojectPath = path,
            ConfigDirectory = Path.GetDirectoryName(path)!,
            Name = name,
            Version = version,
        };

        // scip-python reads [tool.scip] if present, else [tool.pyright].
        var section = toml.ContainsKey("tool.scip") ? "tool.scip" : toml.ContainsKey("tool.pyright") ? "tool.pyright" : null;
        if (section is not null)
        {
            void Read(string key, List<string> into)
            {
                if (toml[section].TryGetValue(key, out var v) && v is List<string> l)
                {
                    into.AddRange(l);
                }
            }

            Read("include", cfg.Include);
            Read("exclude", cfg.Exclude);
            Read("extraPaths", cfg.ExtraPaths);
        }

        return cfg;
    }

    private static string? FindPyproject(string start)
    {
        for (var dir = Path.GetFullPath(start); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var candidate = Path.Combine(dir, "pyproject.toml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// The effective extra import roots. scip-python 0.6.8 (the published build) passes <c>autoSearchPaths = false</c>, so a
    /// conventional <c>src</c> directory is not added implicitly: modules below it are named <c>src.pkg.mod</c>.
    /// </summary>
    public List<string> ResolveExtraPaths() =>
        ExtraPaths.Select(p => Path.GetFullPath(Path.Combine(ConfigDirectory, p))).ToList();

    /// <summary>Returns a predicate that tells whether a path is excluded, and the list of include roots.</summary>
    public (List<string> Roots, Func<string, bool> IsExcluded) FileSelection(string projectRoot)
    {
        var baseDir = PyprojectPath is null ? projectRoot : ConfigDirectory;
        var roots = Include.Count > 0
            ? Include.Select(i => Path.GetFullPath(Path.Combine(baseDir, GlobRoot(i)))).ToList()
            : [Path.GetFullPath(baseDir)];
        var excludes = (Exclude.Count > 0 ? Exclude : [.. DefaultExcludes]).Select(e => GlobToRegex(baseDir, e)).ToList();
        return (roots, path => excludes.Any(r => r.IsMatch(path.Replace('\\', '/'))));
    }

    private static string GlobRoot(string pattern)
    {
        var parts = pattern.Split('/');
        var keep = parts.TakeWhile(p => !p.Contains('*') && !p.Contains('?')).ToArray();
        return keep.Length == 0 ? "." : string.Join('/', keep);
    }

    /// <summary>Pyright-style file spec: relative to the config directory, matches the path and everything below it.</summary>
    public static Regex GlobToRegex(string baseDir, string pattern)
    {
        var sb = new StringBuilder("^").Append(Regex.Escape(Path.GetFullPath(baseDir).Replace('\\', '/').TrimEnd('/')));
        var parts = pattern.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => p != ".");
        foreach (var part in parts)
        {
            if (part == "**")
            {
                sb.Append("(?:/[^/]+)*");
                continue;
            }

            sb.Append('/');
            foreach (var c in part)
            {
                sb.Append(c switch { '*' => "[^/]*", '?' => "[^/]", _ => Regex.Escape(c.ToString()) });
            }
        }

        sb.Append("(?:/.*)?$");
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant);
    }

    /// <summary>The current commit hash, read from <c>.git</c> without running git.</summary>
    public static string? GitHead(string start)
    {
        for (var dir = Path.GetFullPath(start); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            var dotGit = Path.Combine(dir, ".git");
            string? gitDir = null;
            if (Directory.Exists(dotGit))
            {
                gitDir = dotGit;
            }
            else if (File.Exists(dotGit))
            {
                var line = File.ReadAllText(dotGit).Trim();
                if (line.StartsWith("gitdir:", StringComparison.Ordinal))
                {
                    gitDir = Path.GetFullPath(Path.Combine(dir, line[7..].Trim()));
                }
            }

            if (gitDir is null)
            {
                continue;
            }

            try
            {
                var head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
                if (!head.StartsWith("ref:", StringComparison.Ordinal))
                {
                    return head;
                }

                var reference = head[4..].Trim();
                var common = gitDir;
                var commonFile = Path.Combine(gitDir, "commondir");
                if (File.Exists(commonFile))
                {
                    common = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonFile).Trim()));
                }

                foreach (var d in new[] { gitDir, common })
                {
                    var refFile = Path.Combine(d, reference);
                    if (File.Exists(refFile))
                    {
                        return File.ReadAllText(refFile).Trim();
                    }
                }

                var packed = Path.Combine(common, "packed-refs");
                if (File.Exists(packed))
                {
                    foreach (var l in File.ReadLines(packed))
                    {
                        if (l.EndsWith(" " + reference, StringComparison.Ordinal))
                        {
                            return l.Split(' ')[0];
                        }
                    }
                }
            }
            catch (IOException)
            {
            }

            return null;
        }

        return null;
    }
}
