using System.Runtime.CompilerServices;
using ScipSharp.Indexer.Delphi.Project;
using ScipSharp.Testing;

namespace ScipSharp.Indexer.Delphi.Tests;

/// <summary>
/// Golden snapshot tests: every directory under <c>testdata/snapshots/input</c> is indexed and rendered with the
/// <c>scip snapshot</c> format; the result must equal <c>testdata/snapshots/output</c>. Set
/// <c>SCIP_UPDATE_SNAPSHOTS=1</c> to regenerate the expectations.
/// </summary>
public class SnapshotTests
{
    private static string SourceDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    private static string SnapshotRoot => Path.Combine(SourceDir(), "testdata", "snapshots");

    public static IEnumerable<string> Cases() =>
        Directory.EnumerateDirectories(Path.Combine(SnapshotRoot, "input")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!;

    private static List<SourceFile> Render(string inputDir)
    {
        var index = TestUtil.Index(inputDir, new DelphiIndexerOptions { ProjectName = "fixture", ProjectVersion = "1.0" });
        // Render from UTF-8 copies of the decoded sources so that ANSI (Windows-1252) fixtures display correctly;
        // positions are UTF-16 offsets of the decoded text either way.
        var decoded = Path.Combine(Path.GetTempPath(), "scip-delphi-snap-" + Guid.NewGuid().ToString("N"));
        foreach (var doc in index.Documents)
        {
            var target = Path.Combine(decoded, doc.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, Text.SourceDecoder.ReadFile(Path.Combine(inputDir, doc.RelativePath)));
        }

        var (files, errors) = SnapshotFormatter.FormatSnapshots(index, "//", SymbolFormatter.Verbose, decoded);
        Directory.Delete(decoded, recursive: true);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.Message)));
        }

        return files;
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task MatchesSnapshot(string testCase)
    {
        var input = Path.Combine(SnapshotRoot, "input", testCase);
        var output = Path.Combine(SnapshotRoot, "output", testCase);
        var mismatches = SnapshotTesting.Compare(testCase, output, Render(input), SnapshotTesting.UpdateRequested);
        await Assert.That(string.Join("\n", mismatches.Select(m => m.Diff))).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task SnapshotIndexIsLintClean(string testCase)
    {
        var index = TestUtil.Index(Path.Combine(SnapshotRoot, "input", testCase));
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
    }
}
