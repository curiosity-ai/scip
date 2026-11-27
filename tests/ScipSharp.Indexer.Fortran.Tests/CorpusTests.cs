namespace ScipSharp.Indexer.Fortran.Tests;

/// <summary>
/// Robustness over real code (see testdata/SOURCES.md): a slice of Reference-LAPACK/BLAS (FORTRAN 77 fixed form plus
/// the Fortran 90 la_constants/dlartg/dnrm2 and cpp-preprocessed la_xisnan), fortran-lang/minpack (modern free form)
/// and a few fortran-lang/fpm modules (OOP, cpp, iso_c_binding).
/// </summary>
public class CorpusTests
{
    private static string Corpus(string name) => Path.Combine(TestSupport.TestData, "corpus", name);

    public static IEnumerable<string> Corpora() => ["lapack", "minpack", "fpm"];

    [Test]
    [MethodDataSource(nameof(Corpora))]
    public async Task IndexesLintClean(string corpus)
    {
        var indexer = new FortranProjectIndexer(Corpus(corpus));
        var index = indexer.Build();
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        var sources = Directory.EnumerateFiles(Corpus(corpus), "*.*", SearchOption.AllDirectories)
            .Count(static f => Path.GetExtension(f).ToLowerInvariant() is ".f" or ".f90");
        await Assert.That(index.Documents.Count).IsEqualTo(sources);

        // Most expression names resolve (the rest are mostly members of modules that are not part of the slice).
        var occurrences = index.Documents.Sum(static d => d.Occurrences.Count);
        await Assert.That(indexer.Unresolved.Count).IsLessThan(occurrences / 200);
    }

    [Test]
    public async Task LapackCallGraphResolves()
    {
        var index = TestSupport.IndexDirectory(Corpus("lapack"));
        var defined = index.Documents.SelectMany(static d => d.Symbols).Select(static s => s.Symbol).ToHashSet();
        const string pkg = "scip-fortran . lapack . ";
        foreach (var routine in new[] { "dgesv().", "dgetrf().", "dgetrf2().", "dgetrs().", "dlaswp().", "dgemm().", "dtrsm().", "lsame().", "ilaenv().", "dlamch()." })
        {
            await Assert.That(defined).Contains(pkg + routine);
        }

        var dgesv = index.Documents.Single(static d => d.RelativePath == "SRC/dgesv.f");
        var calls = dgesv.Occurrences.Select(static o => o.Symbol).ToHashSet();
        await Assert.That(calls).Contains(pkg + "dgetrf().");
        await Assert.That(calls).Contains(pkg + "dgetrs().");
        await Assert.That(calls).Contains(pkg + "xerbla().");

        // Doxygen documentation is attached to the routine.
        var info = dgesv.Symbols.Single(static s => s.Symbol == pkg + "dgesv().");
        await Assert.That(info.Documentation.Single()).Contains("DGESV computes the solution to a real system of linear equations");
        await Assert.That(info.SignatureDocumentation.Text).StartsWith("SUBROUTINE DGESV( N, NRHS, A, LDA, IPIV, B, LDB, INFO )");

        // Fortran 90 module use from fixed-form-era code: dlartg uses la_constants.
        var dlartg = index.Documents.Single(static d => d.RelativePath == "SRC/dlartg.f90");
        await Assert.That(dlartg.Occurrences.Select(static o => o.Symbol)).Contains(pkg + "la_constants/");
    }

    [Test]
    public async Task MinpackUsesFpmPackageNameAndResolvesModuleProcedures()
    {
        var index = TestSupport.IndexDirectory(Corpus("minpack"));
        var symbols = index.Documents.SelectMany(static d => d.Symbols).Select(static s => s.Symbol).ToHashSet();
        await Assert.That(symbols).Contains("scip-fortran fpm minpack 2.0.0 minpack_module/lmdif1().");
        var example = index.Documents.Single(static d => d.RelativePath == "examples/example_lmdif1.f90");
        await Assert.That(example.Occurrences.Select(static o => o.Symbol)).Contains("scip-fortran fpm minpack 2.0.0 minpack_module/lmdif1().");
    }
}
