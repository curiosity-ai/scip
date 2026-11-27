using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Delphi.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new DelphiIndexer().Name).IsEqualTo("delphi");
    }

    [Test]
    public async Task ParsesOptions()
    {
        var o = DelphiIndexer.ParseArguments(
            ["--project-name=acme", "--project-version", "2.0", "-DFOO;BAR", "--define=BAZ", "--undefine=MSWINDOWS", "--search-path=lib;src",
             "--namespace=Vcl", "--platform=Win64", "--compiler-version=15", "--exclude=backup", "--no-forms", "--no-dproj"],
            out var error, out var help);
        await Assert.That(error).IsNull();
        await Assert.That(help).IsFalse();
        await Assert.That(o!.ProjectName).IsEqualTo("acme");
        await Assert.That(o.ProjectVersion).IsEqualTo("2.0");
        await Assert.That(o.Defines.ToArray()).IsEquivalentTo(["FOO", "BAR", "BAZ"]);
        await Assert.That(o.Undefines.Single()).IsEqualTo("MSWINDOWS");
        await Assert.That(o.SearchPaths.ToArray()).IsEquivalentTo(["lib", "src"]);
        await Assert.That(o.Platform).IsEqualTo("Win64");
        await Assert.That(o.CompilerVersion).IsEqualTo(15);
        await Assert.That(o.IndexForms).IsFalse();
        await Assert.That(o.UseDproj).IsFalse();
        await Assert.That(o.Excludes.Single()).IsEqualTo("backup");
    }

    [Test]
    public async Task RejectsUnknownOptionsAndShowsHelp()
    {
        await Assert.That(DelphiIndexer.ParseArguments(["--bogus"], out var error, out _)).IsNull();
        await Assert.That(error).Contains("--bogus");
        DelphiIndexer.ParseArguments(["--help"], out _, out var help);
        await Assert.That(help).IsTrue();
        await Assert.That(DelphiIndexer.Usage).Contains("--search-path");
    }

    [Test]
    public async Task WritesAParsableIndexFile()
    {
        var dir = TestUtil.NewProject(new Dictionary<string, string>
        {
            ["Hello.dpr"] = "program Hello;\nuses SysUtils;\nbegin\n  WriteLn(IntToStr(42));\nend.\n",
        });
        var output = Path.Combine(dir, "index.scip");
        var code = await new DelphiIndexer().IndexAsync(new IndexerOptions(dir, output, ["--project-version=1"]), CancellationToken.None);
        await Assert.That(code).IsEqualTo(0);
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-delphi");
        await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
        var doc = index.Documents.Single(d => d.RelativePath == "Hello.dpr");
        await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
        await Assert.That(doc.Language).IsEqualTo("Delphi");
        await Assert.That(doc.Occurrences.Any(o => o.Symbol == "scip-delphi delphi Hello 1 Hello/")).IsTrue();
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task InvalidArgumentsReturnNonZero()
    {
        var dir = TestUtil.NewProject(new Dictionary<string, string> { ["A.pas"] = "unit A; interface implementation end." });
        var code = await new DelphiIndexer().IndexAsync(new IndexerOptions(dir, Path.Combine(dir, "x.scip"), ["--nope"]), CancellationToken.None);
        await Assert.That(code).IsEqualTo(1);
    }
}
