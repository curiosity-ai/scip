using System.Diagnostics;
using System.Text.RegularExpressions;
using Scip.Indexer.Java.Project;
using Scip.Indexing;

namespace Scip.Indexer.Java;

/// <summary>
/// Native C# port of <c>scip-java</c>: a Java parser and symbol resolver, class file / jar / JDK image readers and
/// Maven/Gradle/<c>scip-java.json</c> metadata parsers. Needs no javac, plugin jar or build tool. See README.md.
/// </summary>
/// <remarks>
/// Arguments: <c>[aggregate | javac-plugin] [--config FILE] [--jdk-home DIR] [--jdk-version N] [--classpath JARS]</c>.
/// <c>aggregate</c> merges shards produced by the upstream javac plugin; <c>javac-plugin</c> additionally runs javac with it.
/// </remarks>
public sealed partial class JavaIndexer : IIndexer
{
    public const string PluginJarEnvironmentVariable = "SCIP_JAVA_PLUGIN_JAR";

    public string Name => "java";

    public string Languages => "Java (Scala and Kotlin are not supported)";

    public string UpstreamProject => "scip-java";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var reporter = new AggregatorReporter();
        var parsed = JavaIndexerArguments.Parse(options.Arguments);
        if (parsed.Error is not null)
        {
            Console.Error.WriteLine(parsed.Error);
            return 2;
        }

        var sourceRoot = Path.GetFullPath(options.ProjectRoot);
        var output = Path.GetFullPath(options.OutputPath, sourceRoot);

        if (!parsed.AggregateOnly && !parsed.UsePlugin)
        {
            var project = new ProjectDiscovery(new ArtifactLocator()).Discover(sourceRoot, parsed.ConfigPath);
            project.JavaHome = parsed.JdkHome ?? project.JavaHome;
            foreach (var jar in parsed.Classpath) ProjectDiscovery.AddJar(project, Path.GetFullPath(jar, sourceRoot));
            var rc = await NativeProjectIndexer.RunAsync(project, output, parsed.JdkVersion, options.Arguments, reporter, cancellationToken, lintSafeRelationships: !parsed.AllInverse);
            if (rc == 0 && !reporter.HasErrors) Console.WriteLine(output);
            return reporter.HasErrors ? 1 : rc;
        }

        // Optional compatibility mode: consume shards from the upstream javac plugin.
        var targetRoot = Path.GetFullPath(parsed.TargetRoot ?? Path.Combine("target", "scip"), sourceRoot);
        if (parsed.UsePlugin)
        {
            var pluginJar = parsed.PluginJar ?? Environment.GetEnvironmentVariable(PluginJarEnvironmentVariable);
            if (string.IsNullOrEmpty(pluginJar) || !File.Exists(pluginJar))
            {
                Console.Error.WriteLine($"javac-plugin mode needs --plugin-jar or {PluginJarEnvironmentVariable}");
                return 2;
            }
            var configPath = parsed.ConfigPath is { } c ? Path.GetFullPath(c, sourceRoot)
                : JavaProjectConfig.FileNames.Select(n => Path.Combine(sourceRoot, n)).FirstOrDefault(File.Exists);
            var config = configPath is null ? JavaProjectConfig.Empty : JavaProjectConfig.Parse(await File.ReadAllTextAsync(configPath, cancellationToken));
            if (Directory.Exists(targetRoot)) Directory.Delete(targetRoot, recursive: true);
            var rc = await new JavacBuildTool(sourceRoot, targetRoot, Path.GetFullPath(pluginJar), config, parsed.Strict)
                .RunAsync(reporter, cancellationToken);
            if (rc != 0) return rc;
        }

        var packages = ClasspathEntries.FromTargetRoot(targetRoot, sourceRoot).Distinct().ToList();
        ShardAggregator.Run(new AggregatorOptions(
            [targetRoot],
            output,
            sourceRoot,
            new ToolInfo { Name = "scip-java", Version = typeof(JavaIndexer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" },
            packages,
            new JdkPackage(parsed.JdkVersion ?? DetectJdkVersion()),
            reporter,
            EmitInverseRelationships: !parsed.NoInverse,
            AllowEmptyIndex: parsed.AllowEmpty));
        if (reporter.HasErrors) return 1;
        Console.WriteLine(output);
        return 0;
    }

    /// <summary>Major version of the JDK, from <c>$JAVA_HOME/release</c> or <c>java -version</c>; defaults to 17.</summary>
    internal static string DetectJdkVersion()
    {
        try
        {
            var home = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (home is not null && File.Exists(Path.Combine(home, "release")))
            {
                var m = JavaVersionLine().Match(File.ReadAllText(Path.Combine(home, "release")));
                if (m.Success) return JdkPackage.Parse(m.Groups[1].Value).JdkVersion;
            }
            var psi = new ProcessStartInfo("java", "-version") { RedirectStandardError = true };
            using var p = Process.Start(psi);
            if (p is not null)
            {
                var text = p.StandardError.ReadToEnd();
                p.WaitForExit();
                var m = VersionQuoted().Match(text);
                if (m.Success) return JdkPackage.Parse(m.Groups[1].Value).JdkVersion;
            }
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
        return "17";
    }

    [GeneratedRegex("JAVA_VERSION=\"([^\"]+)\"")]
    private static partial Regex JavaVersionLine();

    [GeneratedRegex("version \"([^\"]+)\"")]
    private static partial Regex VersionQuoted();
}

/// <summary>Parsed command-line arguments of the Java indexer.</summary>
public sealed record JavaIndexerArguments
{
    public bool AggregateOnly { get; init; }
    public bool UsePlugin { get; init; }
    public string? PluginJar { get; init; }
    public string? TargetRoot { get; init; }
    public string? ConfigPath { get; init; }
    public string? JdkVersion { get; init; }
    public string? JdkHome { get; init; }
    public List<string> Classpath { get; init; } = [];
    public bool Strict { get; init; }
    public bool NoInverse { get; init; }
    public bool AllInverse { get; init; }
    public bool AllowEmpty { get; init; }
    public string? Error { get; init; }

    public static JavaIndexerArguments Parse(IReadOnlyList<string> args)
    {
        var r = new JavaIndexerArguments();
        for (var i = 0; i < args.Count; i++)
        {
            string Next() => i + 1 < args.Count ? args[++i] : "";
            switch (args[i])
            {
                case "aggregate": r = r with { AggregateOnly = true }; break;
                case "javac-plugin": r = r with { UsePlugin = true }; break;
                case "--plugin-jar": r = r with { PluginJar = Next() }; break;
                case "--targetroot": r = r with { TargetRoot = Next() }; break;
                case "--config": r = r with { ConfigPath = Next() }; break;
                case "--jdk-version": r = r with { JdkVersion = Next() }; break;
                case "--jdk-home": r = r with { JdkHome = Next() }; break;
                case "--classpath": r.Classpath.AddRange(Next().Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)); break;
                case "--strict": r = r with { Strict = true }; break;
                case "--no-inverse-relationships": r = r with { NoInverse = true }; break;
                case "--all-inverse-relationships": r = r with { AllInverse = true }; break;
                case "--allow-empty-index": r = r with { AllowEmpty = true }; break;
                default: return r with { Error = $"unknown argument '{args[i]}'" };
            }
        }
        return r;
    }
}
