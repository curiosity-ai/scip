using System.Diagnostics;
using Scip.Indexer.Java.Project;

namespace Scip.Indexer.Java.Tests;

/// <summary>Opt-in stress test: set <c>SCIP_JAVA_INDEX_DIR</c> to a Java project to index it and report statistics.</summary>
public class RealWorldTests
{
    [Test]
    public async Task IndexesDirectoryFromEnvironment()
    {
        var dir = Environment.GetEnvironmentVariable("SCIP_JAVA_INDEX_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        var statsFile = Environment.GetEnvironmentVariable("SCIP_JAVA_STATS") ?? Path.Combine(Path.GetTempPath(), "scip-java-stats.txt");
        var output = Path.Combine(Directory.CreateTempSubdirectory("scipjava-rw").FullName, "index.scip");
        var reporter = new Collecting();
        var sw = Stopwatch.StartNew();
        var project = new ProjectDiscovery(new ArtifactLocator()).Discover(dir);
        var rc = await NativeProjectIndexer.RunAsync(project, output, null, [], reporter, CancellationToken.None);
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        var occ = index.Documents.Sum(d => d.Occurrences.Count);
        var refs = index.Documents.Sum(d => d.Occurrences.Count(o => (o.SymbolRoles & 1) == 0));
        var external = index.Documents.SelectMany(d => d.Occurrences).Count(o => o.Symbol.Contains("maven jdk", StringComparison.Ordinal));
        File.WriteAllText(statsFile,
            $"rc={rc} docs={index.Documents.Count} occurrences={occ} references={refs} jdkRefs={external} seconds={sw.Elapsed.TotalSeconds:F1}\n" +
            string.Join('\n', reporter.Messages));
        await Assert.That(rc).IsEqualTo(0);
    }

    private sealed class Collecting : AggregatorReporter
    {
        public List<string> Messages { get; } = [];

        public override void Warning(string message) => Messages.Add("warning: " + message);

        public override void ReportError(string message)
        {
            Messages.Add("error: " + message);
            base.ReportError(message);
        }
    }
}
