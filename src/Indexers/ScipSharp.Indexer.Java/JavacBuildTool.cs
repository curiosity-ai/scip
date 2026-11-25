using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ScipSharp.Indexer.Java;

/// <summary>Contents of <c>scip-java.json</c> / <c>lsif-java.json</c> (the upstream "SCIP build tool" config).</summary>
public sealed record JavaProjectConfig(
    string? JavaHome,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<string> Classpath,
    IReadOnlyList<string> JavacOptions,
    IReadOnlyList<string> Dependencies,
    bool ReportWarningOnEmptyIndex = true)
{
    public static readonly string[] FileNames = ["scip-java.json", "lsif-java.json"];

    public static JavaProjectConfig Empty { get; } = new(null, [], [], [], []);

    public static JavaProjectConfig Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("expected a JSON object");
        var root = doc.RootElement;
        List<string> Strings(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText()).ToList()
            : [];
        return new JavaProjectConfig(
            root.TryGetProperty("javaHome", out var jh) && jh.ValueKind == JsonValueKind.String ? jh.GetString() : null,
            Strings("sourceFiles"),
            Strings("classpath"),
            Strings("javacOptions"),
            Strings("dependencies"),
            !root.TryGetProperty("reportWarningOnEmptyIndex", out var r) || r.ValueKind != JsonValueKind.False);
    }
}

/// <summary>
/// Compiles the project's Java sources with plain <c>javac</c> and the upstream scip-javac plugin attached
/// (<c>-Xplugin:scip</c>), producing shards under <c>&lt;targetroot&gt;/META-INF/scip</c>. Port of the upstream
/// <c>ScipBuildTool</c> (Java part). Gradle/Maven integration and Kotlin are not ported; see README.md.
/// </summary>
public sealed class JavacBuildTool(string sourceRoot, string targetRoot, string pluginJar, JavaProjectConfig config, bool strict)
{
    // Required so the plugin can reach internal javac APIs on JDK 9+ (upstream gradle/javac-internals.properties).
    private static readonly string[] JavacLauncherJvmOptions =
    [
        "-J--add-exports=jdk.compiler/com.sun.tools.javac.api=ALL-UNNAMED",
        "-J--add-exports=jdk.compiler/com.sun.tools.javac.code=ALL-UNNAMED",
        "-J--add-exports=jdk.compiler/com.sun.tools.javac.model=ALL-UNNAMED",
        "-J--add-exports=jdk.compiler/com.sun.tools.javac.tree=ALL-UNNAMED",
        "-J--add-exports=jdk.compiler/com.sun.tools.javac.util=ALL-UNNAMED",
    ];

    public List<string> CollectJavaSources()
    {
        var found = new List<string>();
        var roots = config.SourceFiles.Count == 0
            ? [sourceRoot]
            : config.SourceFiles.Select(p => Path.GetFullPath(p, sourceRoot)).ToList();
        var target = Path.GetFullPath(targetRoot);
        foreach (var root in roots)
        {
            if (File.Exists(root) && root.EndsWith(".java", StringComparison.Ordinal)) found.Add(root);
            else if (Directory.Exists(root)) Walk(root);
        }
        found.RemoveAll(f => Path.GetFileName(f) == "module-info.java");
        found.Sort(StringComparer.Ordinal);
        return found;

        void Walk(string dir)
        {
            if (Path.GetFullPath(dir) == target) return;
            foreach (var f in Directory.EnumerateFiles(dir, "*.java")) found.Add(f);
            foreach (var d in Directory.EnumerateDirectories(dir)) Walk(d);
        }
    }

    /// <summary>Builds the javac argument list (one argument per element, no shell quoting).</summary>
    public List<string> BuildArguments(IReadOnlyList<string> sources, string classesDir, string generatedDir)
    {
        var args = new List<string> { "-encoding", "utf8", "-nowarn", "-d", classesDir, "-s", generatedDir };
        var classpath = new List<string> { pluginJar };
        classpath.AddRange(config.Classpath.Select(p => Path.GetFullPath(p, sourceRoot)));
        args.AddRange(["-classpath", string.Join(Path.PathSeparator, classpath)]);
        args.Add($"-Xplugin:scip -targetroot:{targetRoot} -sourceroot:{sourceRoot}");
        args.AddRange(FixJavacOptions(config.JavacOptions));
        args.AddRange(sources);
        return args;
    }

    /// <summary>Drops options that break indexing builds (<c>--release</c>, ErrorProne, <c>-XD</c>, a duplicate plugin).</summary>
    public static List<string> FixJavacOptions(IReadOnlyList<string> options)
    {
        var result = new List<string>();
        for (var i = 0; i < options.Count; i++)
        {
            var o = options[i];
            if (o == "--release") i++;
            else if (o.StartsWith("-Xep", StringComparison.Ordinal) || o.StartsWith("-Xplugin:scip", StringComparison.Ordinal)
                     || o.StartsWith("-XD", StringComparison.Ordinal)) { }
            else result.Add(o);
        }
        return result;
    }

    public async Task<int> RunAsync(AggregatorReporter reporter, CancellationToken ct)
    {
        var sources = CollectJavaSources();
        if (sources.Count == 0)
        {
            if (config.ReportWarningOnEmptyIndex) reporter.Warning($"doing nothing, no files matching pattern '{sourceRoot}/**.java'");
            return 0;
        }
        if (config.Dependencies.Count > 0)
        {
            reporter.ReportError("Maven coordinates in 'dependencies' are not resolved; pre-resolve them into 'classpath'.");
            return 1;
        }

        Directory.CreateDirectory(targetRoot);
        var tmp = Directory.CreateTempSubdirectory("scip-java").FullName;
        try
        {
            var classes = Directory.CreateDirectory(Path.Combine(tmp, "d")).FullName;
            var generated = Directory.CreateDirectory(Path.Combine(tmp, "s")).FullName;
            var args = BuildArguments(sources, classes, generated);
            var argsFile = Path.Combine(targetRoot, "javacopts.txt");
            await File.WriteAllLinesAsync(argsFile, args.Select(a => $"\"{a.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\""), ct);

            var javac = JavacPath();
            reporter.Info($"$ {javac} @{argsFile}");
            var psi = new ProcessStartInfo(javac)
            {
                WorkingDirectory = sourceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("@" + argsFile);
            foreach (var o in JavacLauncherJvmOptions) psi.ArgumentList.Add(o);
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start javac");
            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);

            var generatedShards = Directory.Exists(Path.Combine(targetRoot, "META-INF"));
            if (process.ExitCode == 0) return 0;
            if (strict || !generatedShards)
            {
                reporter.ReportError($"javac exited with code {process.ExitCode}\n{output}");
                return 1;
            }
            reporter.Info("Some SCIP files got generated even though there were compile errors; the locations with errors are not indexed.");
            return 0;
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch (IOException) { }
        }
    }

    private string JavacPath()
    {
        var home = config.JavaHome ?? Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrEmpty(home)) return "javac";
        var exe = OperatingSystem.IsWindows() ? "javac.exe" : "javac";
        return Path.Combine(home, "bin", exe);
    }
}
