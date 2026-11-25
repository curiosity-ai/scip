using System.Text.Json;
using ScipSharp.Cli.Commands;
using ScipSharp.Testing;
using static ScipSharp.Cli.Tests.TestHelpers;

namespace ScipSharp.Cli.Tests;

public class SnapshotAndStatsTests
{
    private static (DirectoryInfo Dir, string IndexPath) SetUp()
    {
        var dir = Directory.CreateTempSubdirectory();
        File.WriteAllText(Path.Combine(dir.FullName, "a.txt"), "package foo\nfunc bar() {}\n");
        var doc = new Document { RelativePath = "a.txt" };
        doc.Occurrences.Add(Occ("local foo", new Range(0, 8, 0, 11), SymbolRole.Definition));
        doc.Occurrences.Add(Occ("local bar", new Range(1, 5, 1, 8), SymbolRole.Definition));
        doc.Occurrences.Add(Occ("local foo", new Range(1, 0, 1, 4)));
        doc.Symbols.Add(new SymbolInformation { Symbol = "local foo" });
        doc.Symbols.Add(new SymbolInformation { Symbol = "local bar" });
        var index = new Index { Metadata = new Metadata { ProjectRoot = new Uri(dir.FullName).AbsoluteUri }, Documents = { doc } };
        return (dir, WriteIndex(dir, index));
    }

    [Test]
    public async Task SnapshotWritesFilesAndStrictFailsOnBadSymbols()
    {
        var (dir, indexPath) = SetUp();
        try
        {
            var to = Path.Combine(dir.FullName, "out");
            var (code, output, _) = await RunCli("snapshot", "--from", indexPath, "--to", to, "--project-root", dir.FullName);
            await Assert.That(code).IsEqualTo(0);
            await Assert.That(output).Contains("done: " + to);
            var text = await File.ReadAllTextAsync(Path.Combine(to, "a.txt"));
            await Assert.That(text).Contains("//        ^^^ definition local foo");

            // Unparseable symbol: strict fails, lenient succeeds.
            var bad = new Index { Metadata = new Metadata { ProjectRoot = new Uri(dir.FullName).AbsoluteUri } };
            var badDoc = new Document { RelativePath = "a.txt" };
            badDoc.Occurrences.Add(Occ("not a symbol", new Range(0, 0, 0, 1)));
            bad.Documents.Add(badDoc);
            var badPath = WriteIndex(dir, bad, "bad.scip");
            var (strictCode, _, strictErr) = await RunCli("snapshot", "--from", badPath, "--to", to, "--project-root", dir.FullName);
            await Assert.That(strictCode).IsEqualTo(1);
            await Assert.That(strictErr).Contains("use --strict=false to ignore this error");
            var (lenientCode, _, _) = await RunCli("snapshot", "--from", badPath, "--to", to, "--project-root", dir.FullName, "--strict", "false");
            await Assert.That(lenientCode).IsEqualTo(0);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task StatsOutputsJson()
    {
        var (dir, indexPath) = SetUp();
        try
        {
            var (code, output, _) = await RunCli("stats", "--from", indexPath, "--project-root", dir.FullName);
            await Assert.That(code).IsEqualTo(0);
            using var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            await Assert.That(root.GetProperty("documents").GetInt32()).IsEqualTo(1);
            await Assert.That(root.GetProperty("occurrences").GetInt32()).IsEqualTo(3);
            await Assert.That(root.GetProperty("definitions").GetInt32()).IsEqualTo(2);
            await Assert.That(root.GetProperty("linesOfCode").GetInt32()).IsEqualTo(2);
            await Assert.That(root.GetProperty("documentSizes").GetProperty("comment").GetString()).IsEqualTo("sizes are in bytes");
            await Assert.That(root.GetProperty("occurrenceCounts").GetProperty("Percentiles").GetProperty("99.9").GetInt32()).IsEqualTo(3);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task StatsRequiresMetadata()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = WriteIndex(dir, new Index());
            var (code, _, error) = await RunCli("stats", "--from", path);
            await Assert.That(code).IsEqualTo(1);
            await Assert.That(error).Contains("Index.Metadata is nil");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task PercentileMatchesMontanaflynStats()
    {
        double[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        await Assert.That(Stats.Percentile(data, 50)).IsEqualTo(5); // whole rank: mean(5, 6) truncated
        await Assert.That(Stats.Percentile(data, 90)).IsEqualTo(9);
        await Assert.That(Stats.Percentile(data, 99)).IsEqualTo(9);
        await Assert.That(Stats.Percentile([], 50)).IsEqualTo(0);
        await Assert.That(Stats.Percentile([42], 99)).IsEqualTo(42);
    }

    [Test]
    public async Task SnapshotTestingComparesAndUpdates()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "snapshots", "input", "case1"));
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "snapshots", "input", "case1", "in.txt"), "hello");

            IEnumerable<SourceFile> Index(string input, string output, IReadOnlyList<SourceFile> sources) =>
                sources.Select(static s => new SourceFile(s.AbsolutePath, s.RelativePath, s.Text.ToUpperInvariant()));

            var missing = SnapshotTesting.Run(dir.FullName, Index, update: false);
            await Assert.That(missing.Count).IsEqualTo(1);
            await Assert.That(missing[0].Diff).Contains("+HELLO");

            await Assert.That(SnapshotTesting.Run(dir.FullName, Index, update: true)).IsEmpty();
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(dir.FullName, "snapshots", "output", "case1", "in.txt"))).IsEqualTo("HELLO");
            await Assert.That(SnapshotTesting.Run(dir.FullName, Index, update: false)).IsEmpty();
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
