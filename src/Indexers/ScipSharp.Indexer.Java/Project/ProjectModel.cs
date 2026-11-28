using System.Text.RegularExpressions;
using System.Xml;

namespace ScipSharp.Indexer.Java.Project;

/// <summary>A source root together with the package (Maven coordinates) its classes are attributed to.</summary>
public sealed record SourceRoot(string Directory, JavaPackage Package, int? Release = null);

/// <summary>What to index: source roots, explicit files, dependency jars and the JDK to use. Built natively, no build tool run.</summary>
public sealed class JavaProject
{
    public string Root { get; init; } = "";
    public List<SourceRoot> SourceRoots { get; } = [];
    public List<(string File, JavaPackage Package)> SourceFiles { get; } = [];
    public List<MavenPackage> Dependencies { get; } = [];
    public string? JavaHome { get; set; }
    public List<string> Notes { get; } = [];
}

/// <summary>Locates Maven artifacts in the local Maven repository and the Gradle module cache.</summary>
public sealed class ArtifactLocator(IEnumerable<string>? mavenRepos = null, IEnumerable<string>? gradleCaches = null)
{
    private readonly List<string> _maven = (mavenRepos ?? DefaultMavenRepos()).Where(Directory.Exists).ToList();
    private readonly List<string> _gradle = (gradleCaches ?? DefaultGradleCaches()).Where(Directory.Exists).ToList();

    private static IEnumerable<string> DefaultMavenRepos()
    {
        var env = Environment.GetEnvironmentVariable("SCIP_JAVA_MAVEN_REPO");
        if (!string.IsNullOrEmpty(env)) yield return env;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2", "repository");
    }

    private static IEnumerable<string> DefaultGradleCaches()
    {
        var home = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
        if (string.IsNullOrEmpty(home)) home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gradle");
        yield return Path.Combine(home, "caches", "modules-2", "files-2.1");
    }

    public string? Find(string group, string artifact, string version, string extension, string? classifier = null)
    {
        var file = $"{artifact}-{version}{(classifier is null ? "" : "-" + classifier)}.{extension}";
        foreach (var repo in _maven)
        {
            var p = Path.Combine(repo, group.Replace('.', Path.DirectorySeparatorChar), artifact, version, file);
            if (File.Exists(p)) return p;
        }
        foreach (var cache in _gradle)
        {
            var dir = Path.Combine(cache, group, artifact, version);
            if (!Directory.Exists(dir)) continue;
            var hit = Directory.EnumerateFiles(dir, file, SearchOption.AllDirectories).FirstOrDefault();
            if (hit is not null) return hit;
        }
        return null;
    }

    /// <summary>Latest version directory available locally, used when a dependency declares no version.</summary>
    public string? FindAnyVersion(string group, string artifact)
    {
        foreach (var repo in _maven)
        {
            var dir = Path.Combine(repo, group.Replace('.', Path.DirectorySeparatorChar), artifact);
            if (Directory.Exists(dir))
            {
                var v = Directory.EnumerateDirectories(dir).Select(Path.GetFileName).OrderByDescending(x => x, StringComparer.Ordinal).FirstOrDefault();
                if (v is not null) return v;
            }
        }
        foreach (var cache in _gradle)
        {
            var dir = Path.Combine(cache, group, artifact);
            if (Directory.Exists(dir))
            {
                var v = Directory.EnumerateDirectories(dir).Select(Path.GetFileName).OrderByDescending(x => x, StringComparer.Ordinal).FirstOrDefault();
                if (v is not null) return v;
            }
        }
        return null;
    }
}

public sealed record PomDependency(string Group, string Artifact, string? Version, string Scope, bool Optional, string? Type);

/// <summary>Minimal native <c>pom.xml</c> model: coordinates, parent, properties, modules, dependencies, dependencyManagement.</summary>
public sealed class Pom
{
    public string Path { get; init; } = "";
    public string? Group { get; set; }
    public string Artifact { get; set; } = "";
    public string? Version { get; set; }
    public string Packaging { get; set; } = "jar";
    public (string Group, string Artifact, string Version)? Parent { get; set; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
    public List<string> Modules { get; } = [];
    public List<PomDependency> Dependencies { get; } = [];
    public Dictionary<string, string> Managed { get; } = new(StringComparer.Ordinal);
    public string? SourceDirectory { get; set; }
    public string? TestSourceDirectory { get; set; }
    public Pom? ParentPom { get; set; }

    /// <summary><c>release</c> / <c>testRelease</c> of the maven-compiler-plugin configuration (build or pluginManagement).</summary>
    public string? CompilerRelease { get; set; }

    public string? CompilerTestRelease { get; set; }

    /// <summary>The <c>--release</c> javac gets for the main or test sources, or null when none is configured.</summary>
    public int? Release(bool test)
    {
        string? Walk(Func<Pom, string?> pick) { for (var p = this; p is not null; p = p.ParentPom) { if (pick(p) is { } v) return v; } return null; }
        var main = Walk(p => p.CompilerRelease) ?? Lookup("maven.compiler.release");
        var value = test ? Walk(p => p.CompilerTestRelease) ?? Lookup("maven.compiler.testRelease") ?? main : main;
        return value is not null && int.TryParse(Resolve(value), out var r) ? r : null;
    }

    public string EffectiveGroup => Group ?? ParentPom?.EffectiveGroup ?? Parent?.Group ?? "";

    public string EffectiveVersion => Version ?? ParentPom?.EffectiveVersion ?? Parent?.Version ?? "";

    public string Resolve(string? value)
    {
        if (value is null) return "";
        for (var depth = 0; depth < 8 && value.Contains("${", StringComparison.Ordinal); depth++)
        {
            value = Regex.Replace(value, @"\$\{([^}]+)\}", m => Lookup(m.Groups[1].Value) ?? m.Value);
        }
        return value;
    }

    private string? Lookup(string key) => key switch
    {
        "project.version" or "pom.version" or "version" => EffectiveVersion,
        "project.groupId" or "pom.groupId" => EffectiveGroup,
        "project.artifactId" => Artifact,
        "project.parent.version" => Parent?.Version,
        "project.parent.groupId" => Parent?.Group,
        _ => Properties.TryGetValue(key, out var v) ? v : ParentPom?.Lookup(key),
    };

    public string? ManagedVersion(string group, string artifact) =>
        Managed.TryGetValue(group + ":" + artifact, out var v) ? Resolve(v) : ParentPom?.ManagedVersion(group, artifact);

    /// <summary>Evaluates a Maven profile <c>&lt;jdk&gt;</c> activation: a version prefix, a range such as <c>[17,)</c>, or a negation.</summary>
    public static bool JdkActivation(string spec, int major)
    {
        spec = spec.Trim();
        if (spec.StartsWith('!')) return !JdkActivation(spec[1..], major);
        if (spec.Length > 1 && spec[0] is '[' or '(' && spec[^1] is ']' or ')')
        {
            var parts = spec[1..^1].Split(',');
            static int? Num(string t)
            {
                t = t.Trim();
                var segs = t.Split('.');
                return int.TryParse(t.StartsWith("1.", StringComparison.Ordinal) && segs.Length > 1 ? segs[1] : segs[0], out var n) ? n : null;
            }
            if (parts.Length == 1) return Num(parts[0]) == major;
            var lo = Num(parts[0]);
            var hi = Num(parts[1]);
            if (lo is { } l && (spec[0] == '[' ? major < l : major <= l)) return false;
            if (hi is { } h && (spec[^1] == ']' ? major > h : major >= h)) return false;
            return true;
        }
        var v = spec.StartsWith("1.", StringComparison.Ordinal) ? spec.Split('.')[1] : spec.Split('.')[0];
        return v == major.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static Pom? Load(string path, int? jdkMajor = null)
    {
        if (!File.Exists(path)) return null;
        var doc = new XmlDocument { XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
        var root = doc.DocumentElement;
        if (root is null) return null;
        var pom = new Pom { Path = path };
        static XmlElement? Child(XmlElement e, string name) => e.ChildNodes.OfType<XmlElement>().FirstOrDefault(c => c.LocalName == name);
        static string? Text(XmlElement e, string name) => Child(e, name)?.InnerText.Trim();
        pom.Group = Text(root, "groupId");
        pom.Artifact = Text(root, "artifactId") ?? "";
        pom.Version = Text(root, "version");
        pom.Packaging = Text(root, "packaging") ?? "jar";
        if (Child(root, "parent") is { } parent)
        {
            pom.Parent = (Text(parent, "groupId") ?? "", Text(parent, "artifactId") ?? "", Text(parent, "version") ?? "");
        }
        if (Child(root, "properties") is { } props)
        {
            foreach (var p in props.ChildNodes.OfType<XmlElement>()) pom.Properties[p.LocalName] = p.InnerText.Trim();
        }
        if (Child(root, "profiles") is { } profiles)
        {
            foreach (var profile in profiles.ChildNodes.OfType<XmlElement>().Where(x => x.LocalName == "profile"))
            {
                var jdk = Child(profile, "activation") is { } act ? Text(act, "jdk") : null;
                if (jdk is null || jdkMajor is not { } major || !JdkActivation(jdk, major)) continue;
                if (Child(profile, "properties") is { } pp) foreach (var p in pp.ChildNodes.OfType<XmlElement>()) pom.Properties[p.LocalName] = p.InnerText.Trim();
            }
        }
        if (Child(root, "modules") is { } mods)
        {
            foreach (var m in mods.ChildNodes.OfType<XmlElement>().Where(x => x.LocalName == "module")) pom.Modules.Add(m.InnerText.Trim());
        }
        if (Child(root, "build") is { } build)
        {
            pom.SourceDirectory = Text(build, "sourceDirectory");
            pom.TestSourceDirectory = Text(build, "testSourceDirectory");
            var plugins = Child(build, "plugins")?.ChildNodes.OfType<XmlElement>().Concat(Child(Child(build, "pluginManagement") ?? build, "plugins")?.ChildNodes.OfType<XmlElement>() ?? []) ?? [];
            foreach (var plugin in plugins.Where(x => x.LocalName == "plugin" && Text(x, "artifactId") == "maven-compiler-plugin"))
            {
                if (Child(plugin, "configuration") is not { } cfg) continue;
                pom.CompilerRelease ??= Text(cfg, "release");
                pom.CompilerTestRelease ??= Text(cfg, "testRelease");
            }
        }
        PomDependency? Dep(XmlElement d) => Text(d, "groupId") is { } g && Text(d, "artifactId") is { } a
            ? new PomDependency(g, a, Text(d, "version"), Text(d, "scope") ?? "compile", Text(d, "optional") == "true", Text(d, "type"))
            : null;
        if (Child(root, "dependencies") is { } deps)
        {
            foreach (var d in deps.ChildNodes.OfType<XmlElement>().Where(x => x.LocalName == "dependency"))
            {
                if (Dep(d) is { } dep) pom.Dependencies.Add(dep);
            }
        }
        if (Child(root, "dependencyManagement") is { } dm && Child(dm, "dependencies") is { } mdeps)
        {
            foreach (var d in mdeps.ChildNodes.OfType<XmlElement>().Where(x => x.LocalName == "dependency"))
            {
                if (Dep(d) is { Version: { } v } dep) pom.Managed[dep.Group + ":" + dep.Artifact] = v;
            }
        }
        return pom;
    }
}

/// <summary>Discovers a <see cref="JavaProject"/> from <c>scip-java.json</c>, <c>pom.xml</c>, Gradle build scripts or plain directories.</summary>
public sealed class ProjectDiscovery(ArtifactLocator locator)
{
    private static readonly string[] IgnoredDirs = ["target", "build", "out", "node_modules", ".git", ".gradle", ".idea", "bin", "obj"];

    private int? _jdkMajor;

    public JavaProject Discover(string root, string? configPath = null)
    {
        _jdkMajor = Classpath.ClassPath.DetectJdkMajor(null);
        root = Path.GetFullPath(root);
        var project = new JavaProject { Root = root };
        var config = configPath is not null
            ? Path.GetFullPath(configPath, root)
            : JavaProjectConfig.FileNames.Select(n => Path.Combine(root, n)).FirstOrDefault(File.Exists);
        if (config is not null && File.Exists(config))
        {
            FromConfig(project, JavaProjectConfig.Parse(File.ReadAllText(config)));
            return project;
        }
        if (File.Exists(Path.Combine(root, "pom.xml")))
        {
            FromMaven(project, root);
            if (project.SourceRoots.Count > 0) return project;
        }
        if (new[] { "settings.gradle", "settings.gradle.kts", "build.gradle", "build.gradle.kts" }.Any(f => File.Exists(Path.Combine(root, f))))
        {
            FromGradle(project, root);
            if (project.SourceRoots.Count > 0) return project;
        }
        Fallback(project, root);
        return project;
    }

    // ------------------------------------------------------------------ scip-java.json

    private void FromConfig(JavaProject project, JavaProjectConfig config)
    {
        project.JavaHome = config.JavaHome;
        var root = project.Root;
        if (config.SourceFiles.Count == 0) project.SourceRoots.Add(new SourceRoot(root, JavaPackage.Empty));
        foreach (var f in config.SourceFiles)
        {
            var p = Path.GetFullPath(f, root);
            if (Directory.Exists(p)) project.SourceRoots.Add(new SourceRoot(p, JavaPackage.Empty));
            else if (File.Exists(p)) project.SourceFiles.Add((p, JavaPackage.Empty));
        }
        foreach (var cp in config.Classpath) AddJar(project, Path.GetFullPath(cp, root));
        if (config.Dependencies.Count > 0) project.Notes.Add("'dependencies' in scip-java.json are not resolved from the network; use 'classpath'");
    }

    // ------------------------------------------------------------------ Maven

    private void FromMaven(JavaProject project, string dir)
    {
        var modules = new List<(string Dir, Pom Pom)>();
        CollectModules(dir, null, modules, []);
        var internalCoords = new HashSet<string>(modules.Select(m => m.Pom.EffectiveGroup + ":" + m.Pom.Artifact));
        foreach (var (mdir, pom) in modules)
        {
            var pkg = new MavenPackage(mdir, pom.EffectiveGroup, pom.Artifact, pom.EffectiveVersion);
            if (pom.Packaging == "pom") continue;
            var main = System.IO.Path.GetFullPath(pom.SourceDirectory is { } sd ? pom.Resolve(sd) : System.IO.Path.Combine("src", "main", "java"), mdir);
            var test = System.IO.Path.GetFullPath(pom.TestSourceDirectory is { } td ? pom.Resolve(td) : System.IO.Path.Combine("src", "test", "java"), mdir);
            if (Directory.Exists(main)) project.SourceRoots.Add(new SourceRoot(main, pkg, pom.Release(test: false)));
            if (Directory.Exists(test)) project.SourceRoots.Add(new SourceRoot(test, pkg, pom.Release(test: true)));
            var seen = new HashSet<string>();
            foreach (var dep in pom.Dependencies) ResolveMavenDependency(project, pom, dep, internalCoords, seen, 0);
        }
        AddLooseJars(project, dir);
    }

    private void CollectModules(string dir, Pom? parent, List<(string, Pom)> acc, HashSet<string> visited)
    {
        var path = System.IO.Path.Combine(dir, "pom.xml");
        if (!visited.Add(path)) return;
        var pom = Pom.Load(path, _jdkMajor);
        if (pom is null) return;
        pom.ParentPom = parent ?? LoadParentPom(pom, dir);
        acc.Add((dir, pom));
        foreach (var m in pom.Modules) CollectModules(System.IO.Path.GetFullPath(m, dir), pom, acc, visited);
    }

    private Pom? LoadParentPom(Pom pom, string dir)
    {
        if (pom.Parent is null) return null;
        var rel = System.IO.Path.GetFullPath(System.IO.Path.Combine("..", "pom.xml"), dir);
        var p = Pom.Load(rel, _jdkMajor);
        return p is not null && p.Artifact == pom.Parent.Value.Artifact ? p : null;
    }

    private void ResolveMavenDependency(JavaProject project, Pom ctx, PomDependency dep, HashSet<string> internalCoords, HashSet<string> seen, int depth)
    {
        if (depth > 6 || dep.Optional && depth > 0) return;
        if (dep.Scope is "provided" or "system" && depth > 0) return;
        if (dep.Scope is "test" && depth > 0) return;
        var group = ctx.Resolve(dep.Group);
        var artifact = ctx.Resolve(dep.Artifact);
        if (internalCoords.Contains(group + ":" + artifact) || !seen.Add(group + ":" + artifact)) return;
        var version = dep.Version is null ? ctx.ManagedVersion(group, artifact) : ctx.Resolve(dep.Version);
        version ??= locator.FindAnyVersion(group, artifact);
        if (string.IsNullOrEmpty(version)) return;
        if (dep.Type is "pom") return;
        var jar = locator.Find(group, artifact, version, "jar");
        if (jar is not null) project.Dependencies.Add(new MavenPackage(jar, group, artifact, version));
        var pomFile = locator.Find(group, artifact, version, "pom");
        if (pomFile is null || Pom.Load(pomFile, _jdkMajor) is not { } depPom) return;
        depPom.ParentPom = ctx.ParentPom ?? ctx;
        foreach (var t in depPom.Dependencies)
        {
            if (t.Scope is "test" or "provided" or "system") continue;
            ResolveMavenDependency(project, depPom, t with { Version = t.Version is null ? depPom.ManagedVersion(t.Group, t.Artifact) ?? ctx.ManagedVersion(t.Group, t.Artifact) : t.Version }, internalCoords, seen, depth + 1);
        }
    }

    // ------------------------------------------------------------------ Gradle (best effort, regex based)

    private static readonly Regex GradleDep = new(
        @"\b(?:api|implementation|compileOnly|runtimeOnly|testImplementation|testCompileOnly|compile|testCompile|annotationProcessor|kapt)\s*\(?\s*(?:platform\()?['""]([^:'""\s]+):([^:'""\s]+)(?::([^:'""\s@]+))?(?:@\w+)?['""]",
        RegexOptions.Compiled);

    private static readonly Regex GradleProject = new(@"project\(\s*['""]:?([^'""]+)['""]\s*\)", RegexOptions.Compiled);

    private static readonly Regex GradleInclude = new(@"include\s*\(?\s*((?:['""][^'""]+['""]\s*,?\s*)+)\)?", RegexOptions.Compiled);

    private void FromGradle(JavaProject project, string root)
    {
        var dirs = new List<string> { root };
        foreach (var settings in new[] { "settings.gradle", "settings.gradle.kts" }.Select(f => System.IO.Path.Combine(root, f)).Where(File.Exists))
        {
            var text = File.ReadAllText(settings);
            foreach (Match inc in GradleInclude.Matches(text))
            {
                foreach (Match q in Regex.Matches(inc.Groups[1].Value, @"['""]([^'""]+)['""]"))
                {
                    var d = System.IO.Path.Combine(root, q.Groups[1].Value.TrimStart(':').Replace(':', System.IO.Path.DirectorySeparatorChar));
                    if (Directory.Exists(d)) dirs.Add(d);
                }
            }
        }
        var seen = new HashSet<string>();
        foreach (var dir in dirs.Distinct())
        {
            var script = new[] { "build.gradle", "build.gradle.kts" }.Select(f => System.IO.Path.Combine(dir, f)).FirstOrDefault(File.Exists);
            var text = script is null ? "" : File.ReadAllText(script);
            var group = Regex.Match(text, @"\bgroup\s*=\s*['""]([^'""]+)['""]").Groups[1].Value;
            var version = Regex.Match(text, @"\bversion\s*=\s*['""]([^'""]+)['""]").Groups[1].Value;
            var pkg = group.Length > 0 || dir != root
                ? new MavenPackage(dir, group, System.IO.Path.GetFileName(dir.TrimEnd('/', '\\')), version.Length > 0 ? version : "unspecified")
                : (JavaPackage)JavaPackage.Empty;
            foreach (var rel in new[] { "src/main/java", "src/test/java" })
            {
                var sdir = System.IO.Path.Combine(dir, rel);
                if (Directory.Exists(sdir)) project.SourceRoots.Add(new SourceRoot(sdir, pkg));
            }
            foreach (Match m in GradleDep.Matches(text))
            {
                var g = m.Groups[1].Value;
                var a = m.Groups[2].Value;
                var v = m.Groups[3].Success ? m.Groups[3].Value : locator.FindAnyVersion(g, a);
                if (v is null || !seen.Add($"{g}:{a}")) continue;
                if (locator.Find(g, a, v, "jar") is { } jar) project.Dependencies.Add(new MavenPackage(jar, g, a, v));
            }
        }
        AddLooseJars(project, root);
        if (dirs.Count > 0 && Regex.IsMatch(string.Join('\n', dirs.Select(d => System.IO.Path.Combine(d, "build.gradle.kts")).Where(File.Exists).Select(File.ReadAllText)), "libs\\."))
        {
            project.Notes.Add("Gradle version catalog references (libs.*) are not resolved");
        }
    }

    // ------------------------------------------------------------------ fallback / helpers

    private void Fallback(JavaProject project, string root)
    {
        var added = false;
        foreach (var rel in new[] { "src/main/java", "src/test/java" })
        {
            var d = System.IO.Path.Combine(root, rel);
            if (Directory.Exists(d)) { project.SourceRoots.Add(new SourceRoot(d, JavaPackage.Empty)); added = true; }
        }
        if (!added)
        {
            var src = System.IO.Path.Combine(root, "src");
            project.SourceRoots.Add(new SourceRoot(Directory.Exists(src) ? src : root, JavaPackage.Empty));
        }
        AddLooseJars(project, root);
    }

    private static void AddLooseJars(JavaProject project, string root)
    {
        foreach (var libs in new[] { "lib", "libs" }.Select(l => System.IO.Path.Combine(root, l)).Where(Directory.Exists))
        {
            foreach (var jar in Directory.EnumerateFiles(libs, "*.jar")) AddJar(project, jar);
        }
    }

    /// <summary>Adds a jar, deriving Maven coordinates from a sibling pom, the repository layout, or the file name.</summary>
    public static void AddJar(JavaProject project, string jar)
    {
        if (!File.Exists(jar) && !Directory.Exists(jar)) return;
        if (project.Dependencies.Any(d => d.Jar == jar)) return;
        var pomPath = System.IO.Path.ChangeExtension(jar, ".pom");
        if (File.Exists(pomPath) && Pom.Load(pomPath) is { } pom && pom.Artifact.Length > 0)
        {
            project.Dependencies.Add(new MavenPackage(jar, pom.EffectiveGroup, pom.Artifact, pom.EffectiveVersion));
            return;
        }
        var file = System.IO.Path.GetFileNameWithoutExtension(jar);
        var m = Regex.Match(file, @"^(.+?)-(\d[\w.\-]*)$");
        var version = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(jar) ?? "");
        // ~/.m2 layout: <group dirs>/<artifact>/<version>/<artifact>-<version>.jar
        var versionDir = System.IO.Path.GetDirectoryName(jar);
        var artifactDir = versionDir is null ? null : System.IO.Path.GetDirectoryName(versionDir);
        if (m.Success && m.Groups[2].Value == version && artifactDir is not null && System.IO.Path.GetFileName(artifactDir) == m.Groups[1].Value)
        {
            var repo = jar.IndexOf(System.IO.Path.DirectorySeparatorChar + "repository" + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);
            var group = repo >= 0
                ? System.IO.Path.GetRelativePath(jar[..(repo + "/repository".Length)], System.IO.Path.GetDirectoryName(artifactDir)!).Replace(System.IO.Path.DirectorySeparatorChar, '.')
                : "";
            project.Dependencies.Add(new MavenPackage(jar, group, m.Groups[1].Value, version));
            return;
        }
        project.Dependencies.Add(m.Success ? new MavenPackage(jar, "", m.Groups[1].Value, m.Groups[2].Value) : new MavenPackage(jar, "", file, "unspecified"));
    }

    public static IEnumerable<string> EnumerateJavaFiles(string dir)
    {
        // Depth-first, files before subdirectories. Each real directory is visited once: a symlink cycle
        // (`ln -s .. up`) used to recurse until the path grew too long.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(dir);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(DirectoryKey(current))) continue;
            foreach (var f in Directory.EnumerateFiles(current, "*.java")) yield return f;
            var subdirs = new List<string>();
            foreach (var d in Directory.EnumerateDirectories(current))
            {
                var name = System.IO.Path.GetFileName(d);
                if (name.StartsWith('.') || IgnoredDirs.Contains(name)) continue;
                subdirs.Add(d);
            }
            for (var i = subdirs.Count - 1; i >= 0; i--) stack.Push(subdirs[i]);
        }
    }

    /// <summary>Identity of a directory for cycle detection: a symbolic link stands for its final target.</summary>
    private static string DirectoryKey(string dir)
    {
        try
        {
            if (new DirectoryInfo(dir).ResolveLinkTarget(returnFinalTarget: true) is { } target) return System.IO.Path.GetFullPath(target.FullName).TrimEnd('/', '\\');
        }
        catch (IOException)
        {
            // dangling or looping link: use the path itself
        }
        return System.IO.Path.GetFullPath(dir).TrimEnd('/', '\\');
    }
}
