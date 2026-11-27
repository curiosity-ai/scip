using ScipSharp.Indexer.Cobol.Syntax;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Cobol.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new CobolIndexer().Name).IsEqualTo("cobol");
        await Assert.That(new CobolIndexer().Languages).IsEqualTo("COBOL");
    }

    [Test]
    public async Task ParsesOptions()
    {
        var (options, help, quiet, error) = CobolIndexer.ParseArguments(
        [
            "--source-format=free", "--copybook-dir", "copy:more", "--copybook-dir=lib", "--program-extensions=.CBL,*.cob",
            "--copybook-extensions", "cpy", "--debug-lines", "--no-unused-copybooks", "--exclude=old", "--project-name=acme",
            "--project-version=1.2", "--quiet",
        ]);
        await Assert.That(error).IsNull();
        await Assert.That(help).IsFalse();
        await Assert.That(quiet).IsTrue();
        await Assert.That(options!.Format).IsEqualTo(SourceFormat.Free);
        await Assert.That(options.CopybookDirectories.ToArray()).IsEquivalentTo(new[] { "copy", "more", "lib" });
        await Assert.That(options.ProgramExtensions.ToArray()).IsEquivalentTo(new[] { "CBL", "cob" });
        await Assert.That(options.CopybookExtensions.ToArray()).IsEquivalentTo(new[] { "cpy" });
        await Assert.That(options.DebugLines).IsTrue();
        await Assert.That(options.IndexUnusedCopybooks).IsFalse();
        await Assert.That(options.Exclude.ToArray()).IsEquivalentTo(new[] { "old" });
        await Assert.That(options.PackageName).IsEqualTo("acme");
        await Assert.That(options.PackageVersion).IsEqualTo("1.2");
    }

    [Test]
    public async Task RejectsUnknownOptionsAndFormats()
    {
        await Assert.That(CobolIndexer.ParseArguments(["--bogus"]).Error).IsNotNull();
        await Assert.That(CobolIndexer.ParseArguments(["--source-format=card"]).Error).IsNotNull();
        await Assert.That(CobolIndexer.ParseArguments(["--copybook-dir"]).Error).IsNotNull();
        await Assert.That(CobolIndexer.ParseArguments(["-h"]).Help).IsTrue();
        await Assert.That(CobolIndexer.Usage).Contains("--copybook-dir");
    }

    [Test]
    public async Task HelpAndBadArgumentsExitCodes()
    {
        var options = new IndexerOptions(".", Path.Combine(Path.GetTempPath(), "unused.scip"), ["--help"]);
        await Assert.That(await new CobolIndexer().IndexAsync(options, CancellationToken.None)).IsEqualTo(0);
        await Assert.That(await new CobolIndexer().IndexAsync(options with { Arguments = ["--nope"] }, CancellationToken.None)).IsEqualTo(2);
    }

    [Test]
    public async Task WritesAParsableIndex()
    {
        var root = Path.Combine(TestSupport.TestData, "snapshots", "input", "copybooks");
        var output = Path.Combine(Path.GetTempPath(), $"scip-cobol-{Guid.NewGuid():N}.scip");
        try
        {
            var code = await new CobolIndexer().IndexAsync(new IndexerOptions(root, output, ["--project-name=fixture", "--quiet"]), CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-cobol");
            await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
            await Assert.That(index.Metadata.ToolInfo.Arguments.ToArray()).IsEquivalentTo(new[] { "--project-name=fixture", "--quiet" });
            await Assert.That(index.Documents.Count).IsEqualTo(11);
            foreach (var doc in index.Documents)
            {
                await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
                await Assert.That(doc.Language).IsEqualTo("COBOL");
            }

            await Assert.That(TestSupport.Lint(index)).IsEmpty();
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Test]
    public async Task UnusedCopybooksCanBeSkipped()
    {
        var root = Path.Combine(TestSupport.TestData, "snapshots", "input", "copybooks");
        var all = TestSupport.Index(root);
        var used = TestSupport.Index(root, new CobolIndexerOptions { PackageName = "fixture", IndexUnusedCopybooks = false });
        await Assert.That(all.Documents.Any(d => d.RelativePath == "copylib/UNUSED.cpy")).IsTrue();
        await Assert.That(used.Documents.Any(d => d.RelativePath == "copylib/UNUSED.cpy")).IsFalse();
        await Assert.That(used.Documents.Count).IsEqualTo(all.Documents.Count - 1);
    }
}
