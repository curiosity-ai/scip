namespace ScipSharp.Indexer.Pli.Tests;

public class OptionsTests
{
    [Test]
    public async Task Defaults()
    {
        var o = PliOptions.Parse([]);
        await Assert.That(o.Margins).IsEqualTo(Margins.Default);
        await Assert.That(o.MarginMode).IsEqualTo(MarginMode.Fixed);
        await Assert.That(o.CaseSensitive).IsFalse();
        await Assert.That(o.Extensions).Contains(".pli");
    }

    [Test]
    public async Task ParsesAllOptions()
    {
        var o = PliOptions.Parse(["--margins=1,100", "--include-dir=inc", "--include-dir=copy", "--extensions=pli,.pl1", "--include-extensions=cpy",
            "--case-sensitive", "--package-name=payroll", "--package-version=1.2"]);
        await Assert.That(o.Margins).IsEqualTo(new Margins(1, 100));
        await Assert.That(o.IncludeDirectories).IsEquivalentTo(new[] { "inc", "copy" });
        await Assert.That(o.Extensions).IsEquivalentTo(new[] { ".pli", ".pl1" });
        await Assert.That(o.IncludeExtensions).IsEquivalentTo(new[] { ".cpy" });
        await Assert.That(o.CaseSensitive).IsTrue();
        await Assert.That(o.PackageName).IsEqualTo("payroll");
        await Assert.That(o.PackageVersion).IsEqualTo("1.2");
    }

    [Test]
    public async Task MarginsNoneAndAuto()
    {
        await Assert.That(PliOptions.Parse(["--margins=none"]).Margins.IsNone).IsTrue();
        await Assert.That(PliOptions.Parse(["--margins=auto"]).MarginMode).IsEqualTo(MarginMode.Auto);
        await Assert.That(PliOptions.ParseMargins("2")).IsEqualTo(new Margins(2, 0));
    }

    [Test]
    [Arguments("--margins=0,72")]
    [Arguments("--margins=10,5")]
    [Arguments("--margins=x")]
    [Arguments("--bogus")]
    public async Task RejectsInvalidArguments(string arg)
    {
        await Assert.That(() => PliOptions.Parse([arg])).Throws<ArgumentException>();
    }

    [Test]
    public async Task IndexerReportsHelpAndBadOptions()
    {
        var indexer = new PliIndexer();
        await Assert.That(indexer.Name).IsEqualTo("pli");
        var dir = Directory.CreateTempSubdirectory("scip-pli-opt-");
        try
        {
            var output = Path.Combine(dir.FullName, "index.scip");
            await Assert.That(await indexer.IndexAsync(new ScipSharp.Indexing.IndexerOptions(dir.FullName, output, ["--help"]), CancellationToken.None)).IsEqualTo(0);
            await Assert.That(File.Exists(output)).IsFalse();
            await Assert.That(await indexer.IndexAsync(new ScipSharp.Indexing.IndexerOptions(dir.FullName, output, ["--nope"]), CancellationToken.None)).IsEqualTo(1);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task UsageMentionsEveryOption()
    {
        foreach (var option in new[] { "--margins", "--include-dir", "--extensions", "--include-extensions", "--case-sensitive", "--package-name", "--package-version" })
        {
            await Assert.That(PliOptions.Usage).Contains(option);
        }
    }
}
