using System.Diagnostics;

namespace ScipSharp.Indexer.Delphi.Tests;

/// <summary>
/// Robustness over real-world, permissively licensed Delphi projects (see testdata/SOURCES.md): no warnings or
/// crashes, lint-clean output and a sane amount of resolved references.
/// </summary>
public class CorpusTests
{
    public static IEnumerable<string> Corpora() => ["Delphi-Mocks", "DUnitX", "JsonDataObjects"];

    private static string Dir(string name) => Path.Combine(TestUtil.TestData, "corpus", name);

    [Test]
    [MethodDataSource(nameof(Corpora))]
    public async Task IndexesCorpusCleanly(string name)
    {
        var warnings = new List<string>();
        var sw = Stopwatch.StartNew();
        var index = TestUtil.Index(Dir(name), warnings: warnings);
        sw.Stop();
        await Assert.That(warnings).IsEmpty();
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(60));
        var sources = Directory.EnumerateFiles(Dir(name), "*.pas", SearchOption.AllDirectories).Count();
        await Assert.That(index.Documents.Count(d => d.RelativePath.EndsWith(".pas", StringComparison.OrdinalIgnoreCase))).IsEqualTo(sources);
        foreach (var doc in index.Documents.Where(d => d.RelativePath.EndsWith(".pas", StringComparison.OrdinalIgnoreCase)))
        {
            await Assert.That(doc.Occurrences.Count).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task JsonDataObjectsResolvesItsClassHierarchy()
    {
        var index = TestUtil.Index(Dir("JsonDataObjects"));
        var doc = index.Documents.Single();
        var symbols = doc.Symbols.ToDictionary(s => s.Symbol);
        var obj = symbols.Keys.Single(s => s.EndsWith(" JsonDataObjects/TJsonObject#", StringComparison.Ordinal));
        await Assert.That(symbols[obj].Relationships.Any(r => r.Symbol.EndsWith("/TJsonBaseObject#", StringComparison.Ordinal))).IsTrue();
        var refs = doc.Occurrences.Count(o => o.Symbol == obj && (o.SymbolRoles & (int)SymbolRole.Definition) == 0);
        await Assert.That(refs).IsGreaterThan(50);

        // Most method implementations link back to their declarations (references, not new definitions).
        var methodDefs = doc.Occurrences.Count(o => o.Symbol.StartsWith(obj, StringComparison.Ordinal) && o.Symbol.EndsWith(").", StringComparison.Ordinal) &&
                                                    (o.SymbolRoles & (int)SymbolRole.Definition) != 0);
        var methodSymbols = symbols.Keys.Count(s => s.StartsWith(obj, StringComparison.Ordinal) && s.EndsWith(").", StringComparison.Ordinal));
        await Assert.That(methodDefs).IsEqualTo(methodSymbols);
    }

    [Test]
    public async Task DelphiMocksPackageAndProjectsResolveUnits()
    {
        var index = TestUtil.Index(Dir("Delphi-Mocks"));
        var dpk = index.Documents.Single(d => d.RelativePath.EndsWith("DelphiMocks.dpk", StringComparison.Ordinal));
        var unitRefs = dpk.Occurrences.Where(o => o.Symbol.Contains(" Delphi/Mocks/", StringComparison.Ordinal)).Select(o => o.Symbol).Distinct().Count();
        await Assert.That(unitRefs).IsGreaterThanOrEqualTo(16);
        await Assert.That(index.ExternalSymbols.Any(s => s.Symbol.Contains("unknown . Delphi/Mocks", StringComparison.Ordinal))).IsFalse();
    }
}
