using ScipSharp.Testing;

namespace ScipSharp.Indexer.Cobol.Tests;

/// <summary>
/// Golden snapshots (the <c>scip snapshot</c> format) of every fixture project under <c>testdata/snapshots/input</c>.
/// Regenerate with <c>SCIP_UPDATE_SNAPSHOTS=1 dotnet test --project tests/ScipSharp.Indexer.Cobol.Tests</c>.
/// </summary>
public class SnapshotTests
{
    private static IEnumerable<SourceFile> Snapshot(string inputDir)
    {
        var index = TestSupport.Index(inputDir);
        foreach (var doc in index.Documents.OrderBy(d => d.RelativePath, StringComparer.Ordinal))
        {
            var formatted = SnapshotFormatter.FormatSnapshot(doc, "*>", SymbolFormatter.Verbose, Path.Combine(inputDir, doc.RelativePath));
            yield return new SourceFile(Path.Combine(inputDir, doc.RelativePath), doc.RelativePath, formatted.Text);
        }
    }

    [Test]
    public async Task SnapshotsMatch()
    {
        var update = SnapshotTesting.UpdateRequested;
        var baseDir = update ? Path.Combine(TestSupport.SourceTestData, "snapshots") : Path.Combine(TestSupport.TestData, "snapshots");
        var mismatches = SnapshotTesting.RunDirectories(
            Path.Combine(baseDir, "input"),
            Path.Combine(baseDir, "output"),
            (input, _, _) => Snapshot(input).ToList(),
            update);
        await Assert.That(string.Join("\n", mismatches.Select(m => m.Diff))).IsEmpty();
    }

    [Test]
    public async Task FixturesAreLintClean()
    {
        foreach (var dir in Directory.GetDirectories(Path.Combine(TestSupport.TestData, "snapshots", "input")))
        {
            await Assert.That(TestSupport.Lint(TestSupport.Index(dir))).IsEmpty();
        }
    }
}
