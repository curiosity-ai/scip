using System.Text;
using System.Xml;

namespace Scip.Indexer.Java;

/// <summary>Canonical shard layout shared by the compiler plugins (port of <c>ScipShardPaths.java</c>).</summary>
public static class ShardPaths
{
    public const string ScipRoot = "META-INF/scip";

    /// <summary>On-disk shard path for a source file under <paramref name="sourceRoot"/>, or null when it is outside.</summary>
    public static string? ShardPath(string targetRoot, string sourceRoot, string absoluteSourcePath)
    {
        var rel = RelativeIfUnder(sourceRoot, absoluteSourcePath);
        return rel is null ? null : Path.Combine(targetRoot, "META-INF", "scip", rel + ".scip");
    }

    /// <summary>Unix-style <c>Document.relative_path</c>; absolute path when the file is outside the source root.</summary>
    public static string RelativePath(string sourceRoot, string absoluteSourcePath)
    {
        var path = RelativeIfUnder(sourceRoot, absoluteSourcePath) ?? absoluteSourcePath;
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string? RelativeIfUnder(string sourceRoot, string file)
    {
        var rel = Path.GetRelativePath(sourceRoot, file);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel;
    }
}

/// <summary>
/// A classpath entry used to attribute symbols to Maven packages (port of <c>ClasspathEntry.kt</c>). Supports
/// <c>*dependencies.txt</c> (tab separated <c>group artifact version path</c>) and POM files next to jars.
/// </summary>
public static class ClasspathEntries
{
    public static List<MavenPackage> FromTargetRoot(string targetRoot, string sourceRoot)
    {
        var javacopts = Path.Combine(targetRoot, "javacopts.txt");
        return File.Exists(javacopts) ? FromJavacOpts(javacopts, sourceRoot) : FromDependencyFiles(targetRoot);
    }

    private static List<MavenPackage> FromDependencyFiles(string targetRoot)
    {
        if (!Directory.Exists(targetRoot)) return [];
        return Directory.EnumerateFiles(targetRoot)
            .Where(f => f.EndsWith("dependencies.txt", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .SelectMany(FromDependencies)
            .Distinct()
            .ToList();
    }

    public static IEnumerable<MavenPackage> FromDependencies(string file)
    {
        foreach (var line in File.ReadLines(file, Encoding.UTF8))
        {
            var parts = line.Split('\t');
            if (parts.Length == 4) yield return new MavenPackage(parts[3], parts[0], parts[1], parts[2]);
        }
    }

    private static List<MavenPackage> FromJavacOpts(string javacopts, string sourceRoot)
    {
        var lines = File.ReadAllLines(javacopts, Encoding.UTF8)
            .Select(l => l.TrimStart('"').TrimEnd('"')).ToArray();
        var result = new List<MavenPackage>();
        for (var i = 0; i < lines.Length - 1; i++)
        {
            switch (lines[i])
            {
                case "-d":
                    if (FromClassesDirectory(lines[i + 1], sourceRoot) is { } dir) result.Add(dir);
                    break;
                case "-cp" or "-classpath":
                    foreach (var jar in lines[i + 1].Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var pom = Path.ChangeExtension(jar, ".pom");
                        if (FromPom(pom, jar) is { } entry) result.Add(entry);
                    }
                    break;
            }
        }
        return result;
    }

    private static MavenPackage? FromClassesDirectory(string classesDir, string sourceRoot)
    {
        var current = Path.GetDirectoryName(Path.GetFullPath(classesDir));
        var root = Path.GetFullPath(sourceRoot);
        while (current is not null && current.StartsWith(root, StringComparison.Ordinal))
        {
            if (FromPom(Path.Combine(current, "pom.xml"), classesDir) is { } entry) return entry;
            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    /// <summary>Reads group/artifact/version from a POM (inheriting from <c>&lt;parent&gt;</c>), with DTDs disabled.</summary>
    public static MavenPackage? FromPom(string pom, string entry)
    {
        if (!File.Exists(pom)) return null;
        var doc = new XmlDocument { XmlResolver = null };
        using (var reader = XmlReader.Create(pom, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            doc.Load(reader);
        }
        if (doc.DocumentElement is not { } root) return null;

        string TextOf(string key)
        {
            var direct = root.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.Name == key);
            if (direct is not null) return direct.InnerText.Trim();
            var parent = root.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.Name == "parent");
            return parent?.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.Name == key)?.InnerText.Trim() ?? "";
        }

        return new MavenPackage(entry, TextOf("groupId"), TextOf("artifactId"), TextOf("version"));
    }
}
