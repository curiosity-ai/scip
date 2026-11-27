using System.Diagnostics;
using static ScipSharp.Indexer.Pli.Tests.TestHelpers;

namespace ScipSharp.Indexer.Pli.Tests;

/// <summary>Robustness over real-world PL/I: a slice of the Multics sources (see testdata/SOURCES.md).</summary>
public class CorpusTests
{
    private static string Multics => Path.Combine(AppContext.BaseDirectory, "testdata", "corpus", "multics");

    private static readonly string[] Args = ["--margins=none", "--case-sensitive", "--package-name=multics"];

    [Test]
    public async Task MulticsCorpusIndexesLintClean()
    {
        var warnings = new List<string>();
        var sw = Stopwatch.StartNew();
        var index = new PliProjectIndexer(Multics, PliOptions.Parse(Args), Args, warnings.Add).Build();
        sw.Stop();

        var files = Directory.EnumerateFiles(Multics, "*.pl1", SearchOption.AllDirectories).Count();
        await Assert.That(index.Documents.Count).IsEqualTo(files);
        await Assert.That(Lint(index)).IsEmpty();
        await Assert.That(warnings).IsEmpty();
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));

        // Every source file defines its procedure (named after the file).
        foreach (var path in Directory.EnumerateFiles(Path.Combine(Multics, "src"), "*.pl1"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var doc = index.Documents.Single(d => d.RelativePath == "src/" + Path.GetFileName(path));
            await Assert.That(doc.Occurrences.Any(o => o.Symbol == $"scip-pli pli multics . {name}()." && (o.SymbolRoles & 1) != 0))
                .IsTrue().Because($"{name} should be defined in {doc.RelativePath}");
        }

        var occurrences = index.Documents.Sum(static d => d.Occurrences.Count);
        var implicitShare = index.Documents.Sum(static d => d.Occurrences.Count(static o => o.Symbol.StartsWith("local ", StringComparison.Ordinal))) / (double)occurrences;
        await Assert.That(occurrences).IsGreaterThan(5_000);
        await Assert.That(implicitShare).IsLessThan(0.02).Because("almost every Multics name is declared; implicit declarations point at parser gaps");
    }

    [Test]
    public async Task MulticsCrossFileReferencesResolve()
    {
        var index = Index(Multics, Args);
        var copyDir = index.Documents.Single(static d => d.RelativePath == "src/copy_dir.pl1");

        // copy_dir calls copy_dir_ (another file of the corpus) and declares it as an external entry.
        await Assert.That(copyDir.Occurrences.Any(static o => o.Symbol == "scip-pli pli multics . copy_dir_().")).IsTrue();
        await Assert.That(copyDir.Symbols.Any(static s => s.Symbol == "scip-pli pli multics . copy_dir_().")).IsFalse();

        // Members of structures from include files resolve to the include file's definition.
        var include = index.Documents.Single(static d => d.RelativePath == "include/copy_options.incl.pl1");
        await Assert.That(include.Occurrences.Any(static o => (o.SymbolRoles & 1) != 0 && o.Symbol.Contains("copy_options.", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task SingleFileWithoutIncludesStillIndexes()
    {
        var dir = Directory.CreateTempSubdirectory("scip-pli-corpus-");
        try
        {
            File.Copy(Path.Combine(Multics, "src", "date_time_.pl1"), Path.Combine(dir.FullName, "date_time_.pl1"));
            var warnings = new List<string>();
            var index = new PliProjectIndexer(dir.FullName, PliOptions.Parse(Args), Args, warnings.Add).Build();
            await Assert.That(index.Documents.Count).IsEqualTo(1);
            await Assert.That(warnings.Count).IsGreaterThan(0).Because("the missing %include members are reported");
            await Assert.That(Lint(index)).IsEmpty();
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
