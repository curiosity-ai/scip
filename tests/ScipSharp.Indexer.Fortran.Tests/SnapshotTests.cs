using ScipSharp.Testing;

namespace ScipSharp.Indexer.Fortran.Tests;

/// <summary>
/// Golden snapshots (rendered like <c>scip snapshot --comment-syntax !</c>) for the fixtures under
/// <c>testdata/snapshots/input/&lt;case&gt;</c>. Regenerate with <c>SCIP_UPDATE_SNAPSHOTS=1 dotnet test</c>; the update is
/// written to the checked-in <c>testdata</c> directory.
/// </summary>
public class SnapshotTests
{
    private static string InputRoot => Path.Combine(TestSupport.TestData, "snapshots", "input");

    public static IEnumerable<string> Cases() =>
        Directory.EnumerateDirectories(InputRoot).Select(static d => Path.GetFileName(d)!).Order(StringComparer.Ordinal);

    internal static List<SourceFile> Render(string inputDir)
    {
        var index = TestSupport.IndexDirectory(inputDir);
        var (files, errors) = SnapshotFormatter.FormatSnapshots(index, "!", SymbolFormatter.Verbose, inputDir);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join('\n', errors.Select(static e => e.Message)));
        }

        return files;
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task MatchesGoldenSnapshot(string testCase)
    {
        var input = Path.Combine(InputRoot, testCase);
        var obtained = Render(input);
        var outputDir = SnapshotTesting.UpdateRequested
            ? Path.Combine(TestSupport.SourceTestData(), "snapshots", "output", testCase)
            : Path.Combine(TestSupport.TestData, "snapshots", "output", testCase);
        if (SnapshotTesting.UpdateRequested && Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }

        var mismatches = SnapshotTesting.Compare(testCase, outputDir, obtained, SnapshotTesting.UpdateRequested);
        await Assert.That(string.Join('\n', mismatches.Select(static m => m.Diff))).IsEqualTo("");

        // No stale expectations: every golden file corresponds to an obtained document.
        if (!SnapshotTesting.UpdateRequested)
        {
            var expected = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(outputDir, f).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.Ordinal).ToList();
            await Assert.That(expected).IsEquivalentTo(obtained.Select(static f => f.RelativePath).Order(StringComparer.Ordinal).ToList());
        }
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task FixtureIsLintClean(string testCase)
    {
        var index = TestSupport.IndexDirectory(Path.Combine(InputRoot, testCase));
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        await Assert.That(index.Documents.Count).IsGreaterThan(0);
    }
}
