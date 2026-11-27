using System.Diagnostics;

namespace ScipSharp.Indexer.Vb6.Tests;

/// <summary>Robustness over a real-world VB6 project (OpenSoldat PolyWorks, MIT; see testdata/SOURCES.md).</summary>
public class CorpusTests
{
    private static string PolyWorks => Path.Combine(TestSupport.TestData, "corpus", "polyworks");

    [Test]
    public async Task IndexesPolyWorksLintCleanWithoutWarnings()
    {
        var warnings = new List<string>();
        var watch = Stopwatch.StartNew();
        var index = TestSupport.Build(PolyWorks, warnings: warnings);
        watch.Stop();

        await Assert.That(warnings).IsEmpty();
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        var sources = Directory.EnumerateFiles(PolyWorks).Count(f => Path.GetExtension(f) is ".frm" or ".bas" or ".vbp");
        await Assert.That(index.Documents.Count).IsEqualTo(sources);
        await Assert.That(watch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));

        // Every module and nearly every procedure is defined; cross-module references resolve.
        var symbols = index.Documents.SelectMany(d => d.Symbols).Select(s => s.Symbol).ToHashSet(StringComparer.Ordinal);
        await Assert.That(symbols).Contains("scip-vb6 vbp PolyWorks 1.7.0 frmOpenSoldatMapEditor#");
        await Assert.That(symbols).Contains("scip-vb6 vbp PolyWorks 1.7.0 PolyWorks/");
        var references = index.Documents.SelectMany(d => d.Occurrences).Count(o => o.SymbolRoles == 0 || o.SymbolRoles == (int)SymbolRole.WriteAccess);
        await Assert.That(references).IsGreaterThan(10_000);
        var definedIn = index.Documents.SelectMany(d => d.Symbols.Select(s => (d.RelativePath, s.Symbol)))
            .Where(x => !x.Symbol.StartsWith("local ", StringComparison.Ordinal))
            .ToDictionary(x => x.Symbol, x => x.RelativePath);
        var crossFile = index.Documents
            .SelectMany(d => d.Occurrences.Select(o => (d.RelativePath, o.Symbol)))
            .Count(x => definedIn.TryGetValue(x.Symbol, out var home) && home != x.RelativePath);
        await Assert.That(crossFile).IsGreaterThan(1_000);
    }

    [Test]
    public async Task EveryEventHandlerOfTheMainFormIsBound()
    {
        var index = TestSupport.Build(PolyWorks);
        var doc = index.Documents.Single(d => d.RelativePath == "frmOpenSoldatMapEditor.frm");
        var handlers = doc.Symbols.Where(s => s.Symbol.EndsWith("_Click().", StringComparison.Ordinal)).ToList();
        await Assert.That(handlers.Count).IsGreaterThan(50);

        // Two handlers (mnuVisible_Click, mnuRefresh_Click) belong to menus that no longer exist in the designer.
        var unbound = handlers.Where(h => h.Relationships.Count != 1).Select(h => h.DisplayName).Order(StringComparer.Ordinal).ToArray();
        await Assert.That(unbound).IsEquivalentTo(["mnuRefresh_Click", "mnuVisible_Click"]);
    }

    [Test]
    public async Task SurvivesTruncatedAndShuffledSources()
    {
        // Feed every prefix-truncation (by line) and a line-shuffled variant of a real form through the indexer.
        var source = File.ReadAllLines(Path.Combine(PolyWorks, "frmTools.frm"), SourceTextEncoding);
        using var dir = new TempDir();
        var random = new Random(42);
        for (var cut = 0; cut < source.Length; cut += Math.Max(1, source.Length / 25))
        {
            File.WriteAllLines(Path.Combine(dir.Path, $"cut{cut}.frm"), source.Take(cut), SourceTextEncoding);
        }

        File.WriteAllLines(Path.Combine(dir.Path, "shuffled.frm"), source.OrderBy(_ => random.Next()), SourceTextEncoding);
        File.WriteAllText(Path.Combine(dir.Path, "binary.bas"), "\0\u0001ÿ garbage \"unterminated\r\n#If\r\n#Else\r\n#Else\r\nEnd Sub\r\nEnd With");
        var index = TestSupport.Build(dir.Path);
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    private static readonly System.Text.Encoding SourceTextEncoding = ScipSharp.Indexer.Vb6.Text.SourceText.Windows1252;
}
