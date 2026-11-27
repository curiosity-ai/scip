using ScipSharp.Indexer.Vb6.Semantics;
using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6.Project;

/// <summary>
/// Discovers the projects and module files below a root directory. Every .vbp becomes a project; a file listed by several
/// projects belongs to the first one (by path) and is indexed once. Module files that no .vbp lists (plain VBA exports,
/// loose .bas/.cls) form one synthetic project per directory.
/// </summary>
internal sealed class Workspace
{
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    private Workspace(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public List<ProjectInfo> Projects { get; } = [];

    public List<ModuleInfo> Modules { get; } = [];

    /// <summary>(relative path, .vbg) of project groups.</summary>
    public List<(string RelativePath, VbgFile Group, SourceText Source)> Groups { get; } = [];

    public Dictionary<ProjectInfo, SourceText> ProjectSources { get; } = [];

    public static Workspace Load(string root, Vb6Options options, Action<string>? warn)
    {
        var ws = new Workspace(root);
        var byPath = new Dictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);
        var vbpPaths = new List<string>();
        var explicitProjects = options.Projects.Select(p => Path.GetFullPath(Path.Combine(root, p))).ToList();
        if (explicitProjects.Count > 0)
        {
            foreach (var p in explicitProjects)
            {
                if (p.EndsWith(".vbg", StringComparison.OrdinalIgnoreCase))
                {
                    var src = SourceText.Read(p, options.Encoding);
                    var group = VbgFile.Parse(p, src.Lines);
                    ws.Groups.Add((Relative(root, p)!, group, src));
                    vbpPaths.AddRange(group.Projects.Select(g => ResolvePath(Path.GetDirectoryName(p)!, g.RawPath)).OfType<string>());
                }
                else
                {
                    vbpPaths.Add(p);
                }
            }
        }
        else
        {
            vbpPaths.AddRange(Directory.EnumerateFiles(root, "*.vbp", Recursive));
            foreach (var g in Directory.EnumerateFiles(root, "*.vbg", Recursive).Order(StringComparer.Ordinal))
            {
                var src = SourceText.Read(g, options.Encoding);
                ws.Groups.Add((Relative(root, g)!, VbgFile.Parse(g, src.Lines), src));
            }
        }

        var vbps = vbpPaths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => Relative(root, p) is not null)
            .OrderBy(p => Relative(root, p), StringComparer.Ordinal)
            .ToList();
        foreach (var vbpPath in vbps)
        {
            var src = SourceText.Read(vbpPath, options.Encoding);
            var vbp = VbpFile.Parse(vbpPath, src.Lines);
            var name = string.IsNullOrWhiteSpace(vbp.Name) ? Path.GetFileNameWithoutExtension(vbpPath) : vbp.Name;
            var project = new ProjectInfo(name, vbp, Relative(root, vbpPath));
            ws.Projects.Add(project);
            ws.ProjectSources[project] = src;
            var dir = Path.GetDirectoryName(vbpPath)!;
            foreach (var item in vbp.Items)
            {
                var path = ResolvePath(dir, item.RawPath);
                if (path is null)
                {
                    warn?.Invoke($"{Relative(root, vbpPath)}: missing {item.Kind.ToString().ToLowerInvariant()} file '{item.RawPath}'");
                    continue;
                }

                if (!byPath.TryGetValue(path, out var module))
                {
                    module = new ModuleInfo(path, Relative(root, path), item.Kind) { Owner = project, DeclaredName = item.Name };
                    byPath[path] = module;
                    ws.Modules.Add(module);
                }

                project.Modules.Add(module);
            }
        }

        // Loose module files: one synthetic project per directory.
        var loose = Directory.EnumerateFiles(root, "*", Recursive)
            .Where(f => ModuleInfo.KindOfExtension(f) is not null && !byPath.ContainsKey(f))
            .OrderBy(f => Relative(root, f), StringComparer.Ordinal)
            .GroupBy(f => Path.GetDirectoryName(f)!, StringComparer.Ordinal);
        if (explicitProjects.Count == 0 || options.IncludeLooseFiles)
        {
            foreach (var dir in loose)
            {
                var rel = Relative(root, dir.Key);
                var name = rel is null or "." or "" ? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)) : Path.GetFileName(dir.Key);
                if (string.IsNullOrEmpty(name))
                {
                    name = "vba";
                }

                var project = new ProjectInfo(name, null, null);
                ws.Projects.Add(project);
                foreach (var file in dir)
                {
                    var module = new ModuleInfo(file, Relative(root, file), ModuleInfo.KindOfExtension(file)!.Value) { Owner = project };
                    byPath[file] = module;
                    ws.Modules.Add(module);
                    project.Modules.Add(module);
                }
            }
        }

        // Project references (Reference=*\A..\Other.vbp) and unique package names.
        var byVbp = ws.Projects.Where(p => p.Vbp is not null).ToDictionary(p => p.Vbp!.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var project in ws.Projects.Where(p => p.Vbp is not null))
        {
            foreach (var r in project.Vbp!.References.Where(r => r.ProjectPath is not null))
            {
                var target = ResolvePath(Path.GetDirectoryName(project.Vbp.Path)!, r.ProjectPath!);
                if (target is not null && byVbp.TryGetValue(target, out var referenced) && referenced != project)
                {
                    project.References.Add(referenced);
                }
            }
        }

        foreach (var clash in ws.Projects.GroupBy(p => (p.Name.ToLowerInvariant(), p.Version)).Where(g => g.Count() > 1))
        {
            foreach (var p in clash.Skip(1))
            {
                var where = p.VbpRelativePath ?? Relative(root, Path.GetDirectoryName(p.Modules[0].AbsolutePath)!) ?? "";
                p.PackageName = $"{p.Name}({where})";
            }
        }

        ws.Modules.Sort((a, b) => string.CompareOrdinal(a.RelativePath ?? "\uffff" + a.AbsolutePath, b.RelativePath ?? "\uffff" + b.AbsolutePath));
        return ws;
    }

    /// <summary>Path relative to <paramref name="root"/> with '/' separators, or null when outside it.</summary>
    public static string? Relative(string root, string path)
    {
        var rel = Path.GetRelativePath(root, path);
        if (rel == ".")
        {
            return "";
        }

        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
        {
            return null;
        }

        return rel.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Resolves a path written in a .vbp (Windows separators, any letter case) against <paramref name="baseDir"/>, matching each
    /// segment case-insensitively on case-sensitive file systems. Returns null when the file does not exist.
    /// </summary>
    public static string? ResolvePath(string baseDir, string raw)
    {
        var p = raw.Trim().Trim('"').Replace('\\', '/');
        if (p.Length == 0)
        {
            return null;
        }

        if (p.Length >= 2 && p[1] == ':')
        {
            return null; // absolute Windows path (C:\...): not resolvable here
        }

        var current = p.StartsWith('/') ? "/" : baseDir;
        foreach (var segment in p.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }

            var direct = Path.Combine(current, segment);
            if (File.Exists(direct) || Directory.Exists(direct))
            {
                current = direct;
                continue;
            }

            if (!Directory.Exists(current))
            {
                return null;
            }

            var match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(e => string.Equals(Path.GetFileName(e), segment, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return null;
            }

            current = match;
        }

        return File.Exists(current) ? Path.GetFullPath(current) : null;
    }
}
