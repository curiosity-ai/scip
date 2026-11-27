using ScipSharp.Testing;

namespace ScipSharp.Indexer.Sql.Tests;

/// <summary>
/// Golden snapshots of <c>testdata/snapshots/input/&lt;case&gt;</c> (one project per case). Regenerate with
/// <c>SCIP_UPDATE_SNAPSHOTS=1 dotnet test --project tests/ScipSharp.Indexer.Sql.Tests</c>.
/// </summary>
public class SnapshotTests
{
    /// <summary>The checked-in testdata directory (so that updates land in the source tree), or the copy next to the tests.</summary>
    private static string SnapshotRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "ScipSharp.Indexer.Sql.Tests", "testdata");
            if (Directory.Exists(Path.Combine(candidate, "snapshots", "input")))
            {
                return candidate;
            }
        }

        return TestSupport.TestData;
    }

    internal static Index IndexCase(string inputDir) =>
        new SqlProjectIndexer(inputDir, new SqlIndexerOptions { PackageName = Path.GetFileName(inputDir), Arguments = [] }).Build();

    [Test]
    public async Task MatchesGoldenSnapshots()
    {
        var mismatches = SnapshotTesting.Run(SnapshotRoot(), (inputDir, _, _) =>
        {
            var index = IndexCase(inputDir);
            var (files, errors) = SnapshotFormatter.FormatSnapshots(index, "--", SymbolFormatter.Verbose, inputDir);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.Message)));
            }

            return files;
        });
        await Assert.That(string.Join("\n", mismatches.Select(m => m.Diff))).IsEmpty();
    }

    public static IEnumerable<string> Cases() =>
        Directory.EnumerateDirectories(Path.Combine(TestSupport.TestData, "snapshots", "input")).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal);

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task SnapshotCaseIsLintClean(string name)
    {
        var index = IndexCase(Path.Combine(TestSupport.TestData, "snapshots", "input", name));
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        await Assert.That(index.Documents.Count).IsGreaterThan(0);
        foreach (var doc in index.Documents)
        {
            await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
            await Assert.That(doc.Language).IsEqualTo("SQL");
        }
    }
}
