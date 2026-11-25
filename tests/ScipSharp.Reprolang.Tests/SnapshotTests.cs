using ScipSharp.Reprolang;

namespace ScipSharp.Reprolang.Tests;

/// <summary>Port of Go <c>TestSCIPSnapshots</c>: each directory under <c>snapshots/input</c> is indexed (siblings act as dependencies) and compared with <c>snapshots/output</c>.</summary>
public class SnapshotTests
{
    private static readonly string TestData = Path.Combine(AppContext.BaseDirectory, "testdata", "snapshots");

    public static IEnumerable<string> Cases() =>
        Directory.EnumerateDirectories(Path.Combine(TestData, "input")).Select(d => Path.GetFileName(d)!).Order(StringComparer.Ordinal);

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task Snapshot(string name)
    {
        var inputDirectory = Path.Combine(TestData, "input", name);
        var outputDirectory = Path.Combine(TestData, "output", name);

        var index = ReproIndexer.Index(inputDirectory, withSiblingDependencies: true);
        var (snapshots, errors) = SnapshotFormatter.FormatSnapshots(index, "#", ReproIndexer.SnapshotSymbolFormatter(name), inputDirectory);
        await Assert.That(errors).IsEmpty();
        await Assert.That(snapshots).IsNotEmpty();

        foreach (var snapshot in snapshots)
        {
            var expected = File.ReadAllText(Path.Combine(outputDirectory, snapshot.RelativePath));
            await Assert.That(snapshot.Text).IsEqualTo(expected);
        }

        // Every expected output file is produced.
        var produced = snapshots.Select(s => s.RelativePath).Order(StringComparer.Ordinal).ToArray();
        var expectedFiles = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(outputDirectory, f)).Order(StringComparer.Ordinal).ToArray();
        await Assert.That(produced).IsEquivalentTo(expectedFiles);
    }
}
