using System.Text.Json;

namespace ScipSharp.Indexer.TypeScript.Tests;

public class OptionsTests
{
    // Port of parseHumanByteSizeIntoNumber.test.ts.
    [Test]
    [Arguments("100", 100d)]
    [Arguments("1kb", 1_000d)]
    [Arguments("1KB", 1_000d)]
    [Arguments("2mb", 2_000_000d)]
    [Arguments("1.5gb", 1_500_000_000d)]
    public async Task ParsesByteSizes(string text, double expected) =>
        await Assert.That(TypeScriptOptions.ParseHumanByteSize(text)).IsEqualTo(expected);

    [Test]
    public async Task RejectsInvalidByteSize() =>
        await Assert.That(double.IsNaN(TypeScriptOptions.ParseHumanByteSize("lots"))).IsTrue();

    [Test]
    public async Task ParsesFlagsAndProjects()
    {
        var parsed = TypeScriptOptions.Parse("/work/app", "index.scip",
            ["--infer-tsconfig", "--npm-workspaces", "--max-file-byte-size", "2kb", "packages/a", "--output", "out/x.scip"], out var error);
        await Assert.That(error).IsNull();
        await Assert.That(parsed!.InferTsconfig).IsTrue();
        await Assert.That(parsed.NpmWorkspaces).IsTrue();
        await Assert.That(parsed.MaxFileByteSize).IsEqualTo(2000);
        await Assert.That(parsed.Projects).IsEquivalentTo(["packages/a"]);
        await Assert.That(parsed.Output).IsEqualTo(Path.GetFullPath("/work/app/out/x.scip"));
    }

    [Test]
    public async Task UnknownOptionIsAnError()
    {
        var parsed = TypeScriptOptions.Parse("/work/app", "index.scip", ["--bogus"], out var error);
        await Assert.That(parsed).IsNull();
        await Assert.That(error).Contains("--bogus");
    }
}

