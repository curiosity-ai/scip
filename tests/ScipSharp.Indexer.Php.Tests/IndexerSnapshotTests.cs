using System.Text;

namespace ScipSharp.Indexer.Php.Tests;

/// <summary>Port of upstream <c>IndexerTest</c>: index the fixture project and compare with the golden snapshots.</summary>
public class IndexerSnapshotTests
{
    private static string TestData => Path.Combine(AppContext.BaseDirectory, "testdata", "indexer");

    private static Index BuildIndex(List<string>? warnings = null) =>
        new PhpProjectIndexer(Path.Combine(TestData, "scip-php-test"), "test", [], phpVersion: "8.5.4", warn: warnings is null ? null : warnings.Add).Build();

    public static IEnumerable<string> GoldenFiles() =>
        Directory.EnumerateFiles(Path.Combine(TestData, "golden"), "*.php", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Path.Combine(TestData, "golden"), f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal);

    [Test]
    public async Task IndexesTheSameFilesAsUpstream()
    {
        var warnings = new List<string>();
        var index = BuildIndex(warnings);
        await Assert.That(index.Documents.Select(d => d.RelativePath).Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(GoldenFiles().ToArray());
        await Assert.That(warnings).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(GoldenFiles))]
    public async Task MatchesGoldenSnapshot(string relativePath)
    {
        var index = BuildIndex();
        var doc = index.Documents.Single(d => d.RelativePath == relativePath);
        var source = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(TestData, "scip-php-test", relativePath)));
        var actual = SnapshotFormatter.Format(doc, source);
        var golden = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(TestData, "golden", relativePath)));
        await Assert.That(actual).IsEqualTo(golden);
    }

    [Test]
    public async Task EmitsExternalSymbolsForDependenciesOnly()
    {
        var index = BuildIndex();
        var symbols = index.ExternalSymbols.Select(s => s.Symbol).ToList();
        await Assert.That(symbols).Contains("scip-php composer davidrjenni/scip-php-test-dep 3e11662443768bf3887b227b8510bc789ed151c6 Test/Dep/ClassI#");
        await Assert.That(symbols).Contains("scip-php composer php 8.5.4 Exception#");
        await Assert.That(symbols.Any(s => s.Contains("davidrjenni/scip-php-test ", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task MetadataDescribesTheTool()
    {
        var index = BuildIndex();
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-php");
        await Assert.That(index.Metadata.TextDocumentEncoding).IsEqualTo(TextEncoding.Utf8);
        await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
        foreach (var d in index.Documents)
        {
            await Assert.That(d.PositionEncoding).IsEqualTo(PositionEncoding.Utf8CodeUnitOffsetFromLineStart);
            await Assert.That(d.Language).IsEqualTo("PHP");
        }
    }

    [Test]
    public async Task IndexerWritesAParsableScipFile()
    {
        var output = Path.Combine(Path.GetTempPath(), $"scip-php-{Guid.NewGuid():N}.scip");
        try
        {
            var indexer = new PhpIndexer();
            var code = await indexer.IndexAsync(
                new ScipSharp.Indexing.IndexerOptions(Path.Combine(TestData, "scip-php-test"), output, ["--php-version=8.5.4"]),
                CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var parsed = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            await Assert.That(parsed.Documents.Count).IsEqualTo(GoldenFiles().Count());
            await Assert.That(parsed.Metadata.ToolInfo.Name).IsEqualTo("scip-php");
        }
        finally
        {
            File.Delete(output);
        }
    }
}
