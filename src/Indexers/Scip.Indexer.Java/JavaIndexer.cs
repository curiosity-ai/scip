using System.Diagnostics;
using System.Text.RegularExpressions;
using Scip.Indexing;

namespace Scip.Indexer.Java;

/// <summary>
/// Port of <c>scip-java</c>. The semantic analysis is done by the upstream javac plugin (which emits one SCIP shard
/// per source file); this indexer orchestrates <c>javac</c> and merges the shards natively. See README.md.
/// </summary>
/// <remarks>
/// Arguments: <c>[aggregate] [--plugin-jar JAR] [--targetroot DIR] [--config FILE] [--strict]
/// [--no-inverse-relationships] [--allow-empty-index] [--jdk-version N]</c>.
/// </remarks>
public sealed partial class JavaIndexer() : IIndexer
{
    public const string PluginJarEnvironmentVariable = "SCIP_JAVA_PLUGIN_JAR";

    public string Name => "java";

    public string Languages => "Java, Scala, Kotlin";

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
        var targetRoot = Path.GetFullPath(parsed.TargetRoot ?? Path.Combine("target", "scip"), sourceRoot);

        if (!parsed.AggregateOnly)
        {
            var configPath = parsed.ConfigPath is { } c ? Path.GetFullPath(c, sourceRoot)
                : JavaProjectConfig.FileNames.Select(n => Path.Combine(sourceRoot, n)).FirstOrDefault(File.Exists);
            JavaProjectConfig config;
            try
            {
                config = configPath is null ? JavaProjectConfig.Empty : JavaProjectConfig.Parse(await File.ReadAllTextAsync(configPath, cancellationToken));
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException)
            {
                Console.Error.WriteLine($"cannot read {configPath}: {e.Message}");
                return 1;
            }

            var pluginJar = parsed.PluginJar ?? Environment.GetEnvironmentVariable(PluginJarEnvironmentVariable);
            if (string.IsNullOrEmpty(pluginJar) || !File.Exists(pluginJar))
            {
                Console.Error.WriteLine(
                    $"The scip-java javac plugin jar is required. Pass --plugin-jar or set {PluginJarEnvironmentVariable} " +
                    "(build it from .reference/scip-java, task :scip-javac:shadowJar). Gradle/Maven auto-detection is not ported; " +
                    "alternatively compile with -Xplugin:scip yourself and run `scip index java aggregate --targetroot DIR`.");
                return 2;
            }

            if (Directory.Exists(targetRoot)) Directory.Delete(targetRoot, recursive: true);
            var rc = await new JavacBuildTool(sourceRoot, targetRoot, Path.GetFullPath(pluginJar), config, parsed.Strict)
                .RunAsync(reporter, cancellationToken);
            if (rc != 0) return rc;
        }

        var packages = ClasspathEntries.FromTargetRoot(targetRoot, sourceRoot).Distinct().ToList();
        var aggregatorOptions = new AggregatorOptions(
            [targetRoot],
            output,
            sourceRoot,
            new ToolInfo { Name = "scip-java", Version = typeof(JavaIndexer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0" },
            packages,
            new JdkPackage(parsed.JdkVersion ?? DetectJdkVersion()),
            reporter,
            EmitInverseRelationships: !parsed.NoInverse,
            AllowEmptyIndex: parsed.AllowEmpty);
        ShardAggregator.Run(aggregatorOptions);
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
    public string? PluginJar { get; init; }
    public string? TargetRoot { get; init; }
    public string? ConfigPath { get; init; }
    public string? JdkVersion { get; init; }
    public bool Strict { get; init; }
    public bool NoInverse { get; init; }
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
                case "--plugin-jar": r = r with { PluginJar = Next() }; break;
                case "--targetroot": r = r with { TargetRoot = Next() }; break;
                case "--config": r = r with { ConfigPath = Next() }; break;
                case "--jdk-version": r = r with { JdkVersion = Next() }; break;
                case "--strict": r = r with { Strict = true }; break;
                case "--no-inverse-relationships": r = r with { NoInverse = true }; break;
                case "--allow-empty-index": r = r with { AllowEmpty = true }; break;
                default: return r with { Error = $"unknown argument '{args[i]}'" };
            }
        }
        return r;
    }
}
