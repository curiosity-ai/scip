using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Vb6.Tests;

public class IndexerTests
{
    private static string Features => Path.Combine(TestSupport.TestData, "snapshots", "input", "features");

    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new Vb6Indexer().Name).IsEqualTo("vb6");
    }

    [Test]
    public async Task WritesAParsableIndex()
    {
        var output = Path.Combine(Path.GetTempPath(), $"scip-vb6-{Guid.NewGuid():N}.scip");
        try
        {
            var code = await new Vb6Indexer().IndexAsync(new IndexerOptions(Features, output, ["--define=USE_LOG=1"]), CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-vb6");
            await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
            await Assert.That(index.Documents.Select(d => d.RelativePath).Order(StringComparer.Ordinal).ToArray())
                .IsEquivalentTo(["Features.vbp", "IShape.cls", "Shapes/cCircle.cls", "frmMain.frm", "modMain.bas"]);
            foreach (var d in index.Documents)
            {
                await Assert.That(d.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
            }

            await Assert.That(TestSupport.Lint(index)).IsEmpty();
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Test]
    public async Task HelpAndBadArguments()
    {
        var output = Path.Combine(Path.GetTempPath(), $"scip-vb6-{Guid.NewGuid():N}.scip");
        await Assert.That(await new Vb6Indexer().IndexAsync(new IndexerOptions(Features, output, ["--help"]), CancellationToken.None)).IsEqualTo(0);
        await Assert.That(await new Vb6Indexer().IndexAsync(new IndexerOptions(Features, output, ["--bogus"]), CancellationToken.None)).IsEqualTo(1);
        await Assert.That(await new Vb6Indexer().IndexAsync(new IndexerOptions(Features, output, ["--encoding=nope"]), CancellationToken.None)).IsEqualTo(1);
        await Assert.That(File.Exists(output)).IsFalse();
    }

    [Test]
    public async Task DefinesOverrideProjectConstants()
    {
        // USE_LOG=0 drops the #If-guarded Trace sub (and with it its definition).
        var index = TestSupport.Build(Features, Vb6Options.Parse(["--define=USE_LOG=0"]));
        var symbols = index.Documents.SelectMany(d => d.Symbols).Select(s => s.Symbol).ToList();
        await Assert.That(symbols.Any(s => s.EndsWith("modMain#Trace().", StringComparison.Ordinal))).IsFalse();
        index = TestSupport.Build(Features);
        symbols = index.Documents.SelectMany(d => d.Symbols).Select(s => s.Symbol).ToList();
        await Assert.That(symbols.Any(s => s.EndsWith("modMain#Trace().", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task ProjectOptionRestrictsTheIndexAndEmitTextEmbedsSources()
    {
        var options = Vb6Options.Parse(["--project", "Features.vbp", "--emit-text"]);
        var index = TestSupport.Build(Features, options);
        await Assert.That(index.Documents.Count).IsEqualTo(5);
        var bas = index.Documents.Single(d => d.RelativePath == "modMain.bas");
        await Assert.That(bas.Text).StartsWith("Attribute VB_Name = \"modMain\"");
    }

    [Test]
    public async Task ParsesOptions()
    {
        var o = Vb6Options.Parse(["--encoding=1251", "--define", "A=2", "--define=B", "--project=x.vbg", "--include-loose-files"]);
        await Assert.That(o.Encoding.CodePage).IsEqualTo(1251);
        await Assert.That(o.Defines["A"]).IsEqualTo(2L);
        await Assert.That(o.Defines["b"]).IsEqualTo(-1L);
        await Assert.That(o.Projects.Single()).IsEqualTo("x.vbg");
        await Assert.That(o.IncludeLooseFiles).IsTrue();
    }
}
