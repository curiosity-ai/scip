namespace ScipSharp.Indexer.Cobol.Tests;

/// <summary>
/// Robustness over real code (see testdata/SOURCES.md): AWS CardDemo (IBM Enterprise COBOL, CICS, DB2, IMS, MQ,
/// copybooks with partial-word REPLACING) and part of the NIST COBOL85 suite (nested programs, CALL, COPY, OO-less
/// COBOL 85 in every corner of the language). No exceptions, lint clean, and spot checks of resolution.
/// </summary>
public class CorpusTests
{
    private static string Corpus(string name) => Path.Combine(TestSupport.TestData, "corpus", name);

    private static readonly Lazy<Index> CardDemo = new(() => TestSupport.Index(Corpus("carddemo"), new CobolIndexerOptions { PackageName = "carddemo" }));

    private static readonly Lazy<Index> Nist = new(() =>
        TestSupport.Index(Corpus("nist85"), new CobolIndexerOptions { PackageName = "nist", ProgramExtensions = ["CBL"] }));

    [Test]
    public async Task CardDemoIndexesEveryProgramAndCopybook()
    {
        var index = CardDemo.Value;
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        var paths = index.Documents.Select(d => d.RelativePath).ToHashSet();
        foreach (var file in Directory.EnumerateFiles(Corpus("carddemo"), "*.*", SearchOption.AllDirectories)
                     .Where(f => Path.GetExtension(f).ToUpperInvariant() is ".CBL" or ".CPY"))
        {
            await Assert.That(paths).Contains(Path.GetRelativePath(Corpus("carddemo"), file).Replace('\\', '/'));
        }

        foreach (var program in index.Documents.Where(d => d.RelativePath.StartsWith("app/cbl/", StringComparison.Ordinal)))
        {
            var name = Path.GetFileNameWithoutExtension(program.RelativePath).ToUpperInvariant();
            await Assert.That(program.Symbols.Any(s => s.Symbol == $"scip-cobol cobol carddemo 0.0.0 {name}/")).IsTrue();
            await Assert.That(program.Occurrences.Count(o => o.SymbolRoles == (int)SymbolRole.ReadAccess)).IsGreaterThan(0);
        }

        await Assert.That(index.Documents.Sum(d => d.Occurrences.Count)).IsGreaterThan(15_000);
    }

    [Test]
    public async Task CardDemoCrossProgramAndCopybookNavigation()
    {
        var index = CardDemo.Value;

        // Copybook fields are shared symbols: the account record copybook is referenced from many programs.
        const string acctId = "scip-cobol cobol carddemo 0.0.0 `app/cpy/CVACT01Y.cpy`/ACCOUNT-RECORD.ACCT-ID.";
        var users = index.Documents.Where(d => d.Occurrences.Any(o => o.Symbol == acctId && o.SymbolRoles == (int)SymbolRole.ReadAccess)).ToList();
        await Assert.That(users.Count).IsGreaterThan(3);

        // CALL 'CSUTLDTC' (date validation) resolves to the program defined in app/cbl/CSUTLDTC.cbl.
        const string dateUtil = "scip-cobol cobol carddemo 0.0.0 CSUTLDTC/";
        await Assert.That(index.Documents.Where(d => d.RelativePath != "app/cbl/CSUTLDTC.cbl")
            .Any(d => d.Occurrences.Any(o => o.Symbol == dateUtil))).IsTrue();

        // Partial-word REPLACING ((TESTVAR1) etc.) in a procedure copybook produces references to the replaced names.
        var setaty = index.Documents.Single(d => d.RelativePath == "app/cpy/CSSETATY.cpy");
        await Assert.That(setaty.Occurrences.Count(o => o.Symbol.Contains("/WS-MISC-STORAGE.", StringComparison.Ordinal))).IsGreaterThan(20);

        // System routines are external symbols.
        await Assert.That(index.ExternalSymbols.Any(s => s.Symbol.EndsWith(" CEEDAYS/", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task NistSuiteIsLintClean()
    {
        var index = Nist.Value;
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        await Assert.That(index.Documents.Count).IsEqualTo(Directory.GetFiles(Corpus("nist85"), "*.CBL").Length);
        var occurrences = index.Documents.Sum(d => d.Occurrences.Count);
        await Assert.That(occurrences).IsGreaterThan(8_000);
    }

    [Test]
    public async Task NistNestedProgramsAndCalls()
    {
        var index = Nist.Value;
        var ic223 = index.Documents.Single(d => d.RelativePath == "IC223A.CBL");
        // IC223A holds two programs separated by END PROGRAM; the first calls the second.
        var programs = ic223.Symbols.Where(s => s.Kind == SymbolInformation.Types.Kind.Module).Select(s => s.Symbol).Order().ToArray();
        await Assert.That(programs).IsEquivalentTo(new[] { "scip-cobol cobol nist 0.0.0 IC223A-1/", "scip-cobol cobol nist 0.0.0 IC223A/" });
        await Assert.That(ic223.Occurrences.Count(o => o.Symbol == "scip-cobol cobol nist 0.0.0 IC223A-1/" && o.SymbolRoles == (int)SymbolRole.ReadAccess)).IsGreaterThan(0);
        await Assert.That(index.ExternalSymbols.Any(s => s.Symbol.Contains("IC223A-1", StringComparison.Ordinal))).IsFalse();

        // Every paragraph PERFORMed in the NC tests resolves (the test driver's "PERFORM PRINT-DETAIL" etc.).
        var nc = index.Documents.Single(d => d.RelativePath == "NC111A.CBL");
        await Assert.That(nc.Occurrences.Any(o => o.Symbol.EndsWith("PRINT-DETAIL().", StringComparison.Ordinal) && o.SymbolRoles == (int)SymbolRole.ReadAccess)).IsTrue();
    }

    [Test]
    public async Task IndexingIsDeterministic()
    {
        var first = TestSupport.Index(Corpus("carddemo"), new CobolIndexerOptions { PackageName = "carddemo" });
        await Assert.That(first.Documents.Select(d => d.ToString()).SequenceEqual(CardDemo.Value.Documents.Select(d => d.ToString()))).IsTrue();
    }
}
