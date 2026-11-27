using ScipSharp.Testing;
using static ScipSharp.Indexer.Pli.Tests.TestHelpers;

namespace ScipSharp.Indexer.Pli.Tests;

/// <summary>
/// Golden snapshots of <c>testdata/snapshots/input/&lt;case&gt;</c> in <c>testdata/snapshots/output/&lt;case&gt;</c>, rendered
/// with the core <see cref="ScipSharp.SnapshotFormatter"/>. Regenerate with <c>SCIP_UPDATE_SNAPSHOTS=1 dotnet test</c>.
/// </summary>
public class SnapshotTests
{
    /// <summary>Indexer arguments per test case (everything else uses the defaults).</summary>
    private static readonly Dictionary<string, string[]> CaseArguments = new(StringComparer.Ordinal)
    {
        ["multics"] = ["--margins=none", "--case-sensitive"],
    };

    private static string Input => Path.Combine(SourceTestData(), "snapshots", "input");

    private static string Output => Path.Combine(SourceTestData(), "snapshots", "output");

    private static string[] ArgumentsFor(string testCase) => CaseArguments.GetValueOrDefault(testCase, []);

    public static IEnumerable<string> Cases() =>
        Directory.EnumerateDirectories(Path.Combine(SourceTestData(), "snapshots", "input")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!;

    [Test]
    public async Task SnapshotsMatch()
    {
        var mismatches = SnapshotTesting.RunDirectories(Input, Output, (inputDir, _, _) =>
        {
            var index = Index(inputDir, ArgumentsFor(Path.GetFileName(inputDir)));
            var (files, errors) = ScipSharp.SnapshotFormatter.FormatSnapshots(index, "//", SymbolFormatter.Verbose, inputDir);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors.Select(static e => e.Message)));
            }

            return files;
        });
        await Assert.That(string.Join("\n", mismatches.Select(static m => m.Diff))).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task SnapshotCaseIsLintClean(string testCase)
    {
        var index = Index(Path.Combine(Input, testCase), ArgumentsFor(testCase));
        await Assert.That(Lint(index)).IsEmpty();
        foreach (var doc in index.Documents)
        {
            await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
            await Assert.That(doc.Language).IsEqualTo("PLI");
        }

        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-pli");
        await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
    }

    [Test]
    public async Task IndexerWritesAParsableLintCleanFile()
    {
        var output = Path.Combine(Path.GetTempPath(), $"scip-pli-{Guid.NewGuid():N}.scip");
        try
        {
            var code = await new PliIndexer().IndexAsync(
                new ScipSharp.Indexing.IndexerOptions(Path.Combine(Input, "basics"), output, []), CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var parsed = ScipSharp.Index.Parser.ParseFrom(File.ReadAllBytes(output));
            await Assert.That(parsed.Documents.Select(static d => d.RelativePath).Order(StringComparer.Ordinal))
                .IsEquivalentTo(new[] { "common.inc", "logger.pli", "payroll.pli", "summary.pli" });
            await Assert.That(Lint(parsed)).IsEmpty();
        }
        finally
        {
            File.Delete(output);
        }
    }
}
