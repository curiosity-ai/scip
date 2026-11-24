using Scip.Indexer.Java.Project;

namespace Scip.Indexer.Java.Tests;

/// <summary>
/// Compares the native indexer's output against the upstream scip-java snapshots (<c>scip-snapshots/expected/java</c>).
/// Skipped when <c>.reference</c> is not available. JDK symbols use the JDK 17 label to match the upstream goldens.
/// </summary>
public class SnapshotTests
{
    private static string? ExpectedDir(string root) => Path.Combine(root, "scip-snapshots", "expected", "java", "common", "scip-snapshots", "cases", "java", "common", "src", "main", "java", "minimized");

    internal static Dictionary<string, (string Actual, string Expected)> Run(string reference, string? dump = null)
    {
        var sourceRoot = Path.Combine(reference, "scip-snapshots", "cases", "java", "common");
        var output = Path.Combine(Directory.CreateTempSubdirectory("scipjava-snap").FullName, "index.scip");
        var project = new ProjectDiscovery(new ArtifactLocator()).Discover(sourceRoot);
        var reporter = new AggregatorReporter();
        var rc = NativeProjectIndexer.RunAsync(project, output, "17", [], reporter, CancellationToken.None).GetAwaiter().GetResult();
        if (rc != 0) throw new InvalidOperationException("indexing failed");
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        var result = new Dictionary<string, (string, string)>();
        var expectedDir = ExpectedDir(reference)!;
        foreach (var doc in index.Documents)
        {
            var name = Path.GetFileName(doc.RelativePath);
            var expectedFile = Path.Combine(expectedDir, name);
            if (!File.Exists(expectedFile)) continue;
            var abs = Path.Combine(sourceRoot, doc.RelativePath);
            var actual = SnapshotFormatter.FormatSnapshot(doc, "//", SymbolFormatter.Verbose, abs).Text;
            if (dump is not null)
            {
                Directory.CreateDirectory(dump);
                File.WriteAllText(Path.Combine(dump, name + ".actual"), actual);
            }
            result[name] = (actual, File.ReadAllText(expectedFile));
        }
        return result;
    }

    [Test]
    public async Task MatchesUpstreamSnapshots()
    {
        var reference = ParserTests.FindReference();
        if (reference is null) return;
        var results = Run(reference, Environment.GetEnvironmentVariable("SCIP_SNAPSHOT_DUMP"));
        var ok = results.Where(kv => Normalize(kv.Value.Actual) == Normalize(kv.Value.Expected)).Select(kv => kv.Key).Order().ToList();
        var bad = results.Keys.Except(ok).Order().ToList();
        Console.WriteLine($"snapshots matching: {ok.Count}/{results.Count}");
        Console.WriteLine("passing: " + string.Join(", ", ok));
        Console.WriteLine("failing: " + string.Join(", ", bad));
        await Assert.That(results.Count).IsGreaterThan(10);
        await Assert.That(ok.Count).IsGreaterThanOrEqualTo(MinimumMatching);
    }

    // LombokBuilder.java needs annotation processing (generated code) and is the one known mismatch.
    private const int MinimumMatching = 25;

    private static string Normalize(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
}
