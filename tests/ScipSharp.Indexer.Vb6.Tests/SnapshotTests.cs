using ScipSharp.Testing;

namespace ScipSharp.Indexer.Vb6.Tests;

/// <summary>
/// Golden snapshots of <c>testdata/snapshots/input/&lt;case&gt;</c> in <c>testdata/snapshots/output/&lt;case&gt;</c>.
/// Regenerate with <c>SCIP_UPDATE_SNAPSHOTS=1 dotnet test --project tests/ScipSharp.Indexer.Vb6.Tests</c>.
/// </summary>
public class SnapshotTests
{
    [Test]
    public async Task MatchesGoldenSnapshots()
    {
        var mismatches = SnapshotTesting.Run(Path.Combine(TestSupport.TestData), (input, output, _) =>
        {
            input = RootOf(input);
            var index = TestSupport.Build(input);
            var files = index.Documents
                .Select(d => new SourceFile(Path.Combine(output, d.RelativePath), d.RelativePath, TestSupport.Snapshot(d, input)))
                .ToList();
            if (index.ExternalSymbols.Count > 0)
            {
                files.Add(new SourceFile(Path.Combine(output, "external_symbols.txt"), "external_symbols.txt", TestSupport.ExternalSymbols(index)));
            }

            return files;
        });
        await Assert.That(string.Join("\n", mismatches.Select(m => m.Diff))).IsEmpty();
    }

    [Test]
    [Arguments("features")]
    [Arguments("vba")]
    [Arguments("vbasyncsocket-chat")]
    public async Task SnapshotCasesAreLintClean(string name)
    {
        var index = TestSupport.Build(RootOf(Path.Combine(TestSupport.TestData, "snapshots", "input", name)));
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task ModulesOutsideTheRootBecomeExternalSymbols()
    {
        // test/Chat/Project1.vbp pulls in ..\..\src\cAsyncSocket.cls, which lies outside the indexed root.
        var index = TestSupport.Build(RootOf(Path.Combine(TestSupport.TestData, "snapshots", "input", "vbasyncsocket-chat")));
        await Assert.That(index.Documents.Any(d => d.RelativePath.Contains("cAsyncSocket", StringComparison.Ordinal))).IsFalse();
        var external = index.ExternalSymbols.Select(s => s.Symbol).ToList();
        await Assert.That(external).Contains("scip-vb6 vbp Project1 1.0.0 cAsyncSocket#OnReceive().");
        await Assert.That(external).Contains("scip-vb6 vbp Project1 1.0.0 cAsyncSocket#");
    }

    /// <summary>A case may index a sub-directory (named in its <c>SNAPSHOT_ROOT</c> file), leaving files outside the root.</summary>
    private static string RootOf(string caseDir)
    {
        var marker = Path.Combine(caseDir, "SNAPSHOT_ROOT");
        return File.Exists(marker) ? Path.GetFullPath(Path.Combine(caseDir, File.ReadAllText(marker).Trim())) : caseDir;
    }
}
