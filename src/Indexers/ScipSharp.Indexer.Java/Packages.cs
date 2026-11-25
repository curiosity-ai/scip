using System.IO.Compression;

namespace ScipSharp.Indexer.Java;

/// <summary>The package (Maven artifact, JDK, or none) a symbol is attributed to.</summary>
public abstract record JavaPackage
{
    public static JavaPackage Empty { get; } = new EmptyPackage();

    /// <summary>Scheme written by scip-java 0.12.3 (its SemanticDB-derived symbols keep the <c>semanticdb</c> scheme).</summary>
    public const string Scheme = "semanticdb";

    public abstract string RepoName { get; }

    public abstract string Version { get; }

    /// <summary>The <c>manager name version</c> triple of a SCIP symbol, e.g. <c>maven maven/g/a 1.0</c>.</summary>
    public string ScipTypedEncoding => $"maven {Encode(RepoName)} {Encode(Version)}";

    private static string Encode(string value) => value.Contains(' ') ? $"`{value}`" : value;

    private sealed record EmptyPackage : JavaPackage
    {
        public override string RepoName => ".";

        public override string Version => ".";
    }
}

/// <summary>A Maven dependency (or the project's own output) backed by a jar or a classes directory.</summary>
public sealed record MavenPackage(string Jar, string GroupId, string ArtifactId, string PackageVersion) : JavaPackage
{
    public override string RepoName => $"maven/{GroupId}/{ArtifactId}";

    public override string Version => PackageVersion;
}

/// <summary>The JDK, identified by its major version.</summary>
public sealed record JdkPackage(string JdkVersion) : JavaPackage
{
    public override string RepoName => "jdk";

    public override string Version => JdkVersion;

    /// <summary>Parses a <c>java.version</c>-style string, keeping only the major version.</summary>
    public static JdkPackage Parse(string version)
    {
        var dot = version.IndexOf('.', StringComparison.Ordinal);
        return new JdkPackage(dot < 0 ? version : version[..dot]);
    }
}

/// <summary>
/// Maps top-level symbols to the package that defines them by indexing the class files of every classpath entry
/// (port of <c>PackageTable.java</c>). C# cannot read the JVM's <c>jrt:/</c> file system, so JDK classes are
/// recognised by package prefix instead (see <see cref="IsJdkClassfile"/>).
/// </summary>
public sealed class PackageTable
{
    private static readonly string[] JdkPrefixes =
    [
        "java/", "javax/", "jdk/", "sun/", "com/sun/", "org/w3c/dom/", "org/xml/sax/", "org/ietf/jgss/", "org/omg/",
    ];

    private readonly Dictionary<string, JavaPackage> _byClassfile = new(StringComparer.Ordinal);
    private readonly JdkPackage _jdk;

    public PackageTable(IEnumerable<MavenPackage> packages, JdkPackage jdk, bool indexDirectoryEntries)
    {
        _jdk = jdk;
        // Maven packages win over the JDK (some artifacts redefine JDK classes).
        foreach (var pkg in packages)
        {
            if (pkg.Jar.EndsWith(".jar", StringComparison.Ordinal) && File.Exists(pkg.Jar)) IndexJar(pkg);
            else if (indexDirectoryEntries && Directory.Exists(pkg.Jar)) IndexDirectory(pkg);
        }
    }

    public JavaPackage? PackageForSymbol(string symbol)
    {
        if (JavaSymbols.TopLevel(symbol) is not { } top) return null;
        var classfile = top.Owner + top.Descriptor.Name + ".class";
        if (_byClassfile.TryGetValue(classfile, out var pkg)) return pkg;
        return IsJdkClassfile(classfile) ? _jdk : null;
    }

    public static bool IsJdkClassfile(string classfile) =>
        JdkPrefixes.Any(p => classfile.StartsWith(p, StringComparison.Ordinal));

    private void IndexJar(MavenPackage pkg)
    {
        using var zip = ZipFile.OpenRead(pkg.Jar);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith(".class", StringComparison.Ordinal) && !entry.FullName.Contains('$'))
            {
                _byClassfile[entry.FullName] = pkg;
            }
        }
    }

    private void IndexDirectory(MavenPackage pkg)
    {
        foreach (var file in Directory.EnumerateFiles(pkg.Jar, "*.class", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(pkg.Jar, file).Replace('\\', '/');
            if (!rel.Contains('$')) _byClassfile[rel] = pkg;
        }
    }
}

/// <summary>Rewrites bare shard symbols into <c>scip-java maven &lt;repo&gt; &lt;version&gt; descriptor</c> symbols.</summary>
public sealed class SymbolRewriter(PackageTable packages)
{
    public string Rewrite(string symbol)
    {
        if (string.IsNullOrEmpty(symbol) || JavaSymbols.IsLocal(symbol)) return symbol;
        var pkg = packages.PackageForSymbol(symbol) ?? JavaPackage.Empty;
        return $"{JavaPackage.Scheme} {pkg.ScipTypedEncoding} {symbol}";
    }
}
