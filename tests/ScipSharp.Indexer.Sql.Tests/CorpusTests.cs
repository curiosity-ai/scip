using System.Diagnostics;

namespace ScipSharp.Indexer.Sql.Tests;

/// <summary>Robustness over real-world SQL (sqlfluff dialect fixtures, PostgreSQL regression tests): no crash, lint-clean.</summary>
public class CorpusTests
{
    public static IEnumerable<(string Directory, string Dialect)> Corpora() =>
    [
        ("sqlfluff/ansi", "ansi"),
        ("sqlfluff/postgres", "postgres"),
        ("sqlfluff/tsql", "tsql"),
        ("sqlfluff/mysql", "mysql"),
        ("sqlfluff/sqlite", "sqlite"),
        ("sqlfluff/oracle", "plsql"),
        ("postgres-regress", "postgres"),
    ];

    [Test]
    [MethodDataSource(nameof(Corpora))]
    public async Task IndexesCorpusWithoutErrors(string directory, string dialect)
    {
        var d = Dialects.TryParse(dialect, out var parsed) ? parsed : throw new ArgumentException(dialect);
        var dir = Path.Combine(TestSupport.TestData, "corpus", directory);
        var warnings = new List<string>();
        var sw = Stopwatch.StartNew();
        var index = new SqlProjectIndexer(dir, new SqlIndexerOptions { Dialect = d, PackageName = "corpus" }, warnings.Add).Build();
        sw.Stop();
        await Assert.That(warnings).IsEmpty();
        await Assert.That(index.Documents.Count).IsEqualTo(Directory.GetFiles(dir, "*.sql").Length);
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        var definitions = index.Documents.Sum(doc => doc.Occurrences.Count(o => (o.SymbolRoles & (int)SymbolRole.Definition) != 0));
        var references = index.Documents.Sum(doc => doc.Occurrences.Count(o => (o.SymbolRoles & (int)SymbolRole.Definition) == 0));
        await Assert.That(definitions).IsGreaterThan(50);
        await Assert.That(references).IsGreaterThan(definitions);
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(60));
    }

    [Test]
    public async Task AutoDetectionIndexesTheWholeCorpus()
    {
        var index = new SqlProjectIndexer(Path.Combine(TestSupport.TestData, "corpus"), new SqlIndexerOptions { PackageName = "corpus" }).Build();
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }
}
