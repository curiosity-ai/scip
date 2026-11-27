using System.Diagnostics;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Fortran.Tests;

/// <summary>The <c>scip index fortran</c> entry point: options, help, streaming output.</summary>
public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new FortranIndexer().Name).IsEqualTo("fortran");
    }

    [Test]
    public async Task HelpExitsZeroAndUnknownOptionsFail()
    {
        var indexer = new FortranIndexer();
        var output = Path.Combine(Path.GetTempPath(), $"scip-fortran-{Guid.NewGuid():N}.scip");
        await Assert.That(await indexer.IndexAsync(new IndexerOptions(".", output, ["--help"]), CancellationToken.None)).IsEqualTo(0);
        await Assert.That(await indexer.IndexAsync(new IndexerOptions(".", output, ["--bogus"]), CancellationToken.None)).IsEqualTo(1);
        await Assert.That(await indexer.IndexAsync(new IndexerOptions(".", output, ["--form=weird"]), CancellationToken.None)).IsEqualTo(1);
        await Assert.That(File.Exists(output)).IsFalse();
    }

    [Test]
    public async Task ParsesOptions()
    {
        var o = FortranIndexer.ParseArguments(
            ["--form=fixed", "--fixed-line-length", "132", "-Iinclude", "--include-dir=more", "-DUSE_MPI", "--define=N=3",
             "--package-name=x", "--package-version=1.0", "--exclude=vendor"],
            TextWriter.Null, out var help)!;
        await Assert.That(help).IsFalse();
        await Assert.That(o.Form).IsEqualTo("fixed");
        await Assert.That(o.FixedLineLength).IsEqualTo(132);
        await Assert.That(o.IncludeDirs).IsEquivalentTo(new List<string> { "include", "more" });
        await Assert.That(o.Defines["USE_MPI"]).IsEqualTo("1");
        await Assert.That(o.Defines["N"]).IsEqualTo("3");
        await Assert.That(o.Evaluate).IsTrue();
        await Assert.That(o.PackageName).IsEqualTo("x");
        await Assert.That(o.Exclude).IsEquivalentTo(new List<string> { "vendor" });
    }

    [Test]
    public async Task WritesAParsableIndex()
    {
        var root = Path.Combine(TestSupport.TestData, "snapshots", "input", "f90-modules");
        var output = Path.Combine(Path.GetTempPath(), $"scip-fortran-{Guid.NewGuid():N}.scip");
        try
        {
            var code = await new FortranIndexer().IndexAsync(new IndexerOptions(root, output, []), CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-fortran");
            await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
            await Assert.That(index.Metadata.TextDocumentEncoding).IsEqualTo(TextEncoding.Utf8);
            await Assert.That(index.Documents.Select(static d => d.RelativePath).Order().ToList())
                .IsEquivalentTo(new List<string> { "app/main.f90", "src/geometry.f90" });
            foreach (var d in index.Documents)
            {
                await Assert.That(d.Language).IsEqualTo("Fortran");
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
    public async Task ExcludedAndHiddenDirectoriesAreSkipped()
    {
        var p = TestSupport.Build(new()
        {
            ["src/a.f90"] = "module a\nend module\n",
            ["vendor/b.f90"] = "module b\nend module\n",
            [".hidden/c.f90"] = "module c\nend module\n",
            ["build/d.f90"] = "module d\nend module\n",
        }, new FortranOptions { Exclude = ["vendor"] });
        await Assert.That(p.Index.Documents.Select(static d => d.RelativePath).ToList()).IsEquivalentTo(new List<string> { "src/a.f90" });
    }

    /// <summary>Optional cross-check: the fixtures are valid Fortran according to gfortran (skipped when it is not installed).</summary>
    [Test]
    public async Task FixturesCompileWithGfortranWhenAvailable()
    {
        var gfortran = FindOnPath("gfortran");
        if (gfortran is null)
        {
            return; // external toolchains are only used for optional cross-checks
        }

        var root = Path.Combine(TestSupport.TestData, "snapshots", "input");
        foreach (var file in new[] { "f2003-oop/shapes.f90", "legacy-f66/legacy.f" })
        {
            var work = Directory.CreateTempSubdirectory("scip-fortran-gfortran").FullName;
            var psi = new ProcessStartInfo(gfortran, ["-fsyntax-only", "-std=legacy", Path.Combine(root, file)])
            {
                WorkingDirectory = work, RedirectStandardError = true, RedirectStandardOutput = true,
            };
            using var proc = Process.Start(psi)!;
            var err = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            await Assert.That(err.Length > 0 ? proc.ExitCode : 0).IsEqualTo(0);
            await Assert.That(proc.ExitCode).IsEqualTo(0);
        }
    }

    private static string? FindOnPath(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(d => Path.Combine(d, exe)).FirstOrDefault(File.Exists);
}
