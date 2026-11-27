namespace ScipSharp.Indexer.Sql.Tests;

/// <summary>Name resolution: case folding, aliases, CTEs, subqueries, procedural variables.</summary>
public class ResolutionTests
{
    private static List<string> Refs(string sql, string text, SqlDialect d = SqlDialect.Postgres) =>
        TestSupport.SymbolsAt(TestSupport.IndexSql(sql, d), sql, text);

    [Test]
    public async Task PostgresFoldsUnquotedNamesToLowerCase()
    {
        const string sql = "CREATE TABLE Users (Id int, \"Name\" text);\nSELECT ID, \"Name\", name FROM USERS;";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        await Assert.That(TestSupport.SymbolsAt(index, sql, "USERS")).IsEquivalentTo(new[] { "public/users#" });
        await Assert.That(TestSupport.SymbolsAt(index, sql, "ID")).IsEquivalentTo(new[] { "public/users#id." });
        await Assert.That(TestSupport.SymbolsAt(index, sql, "\"Name\"")).IsEquivalentTo(new[] { "public/users#Name." });
        // Unquoted name folds to "name", which is not the quoted "Name" column: attributed to the sole table but unresolved.
        await Assert.That(TestSupport.SymbolsAt(index, sql, " name ").Count).IsEqualTo(0);
        await Assert.That(index.ExternalSymbols.Select(s => TestSupport.Short(s.Symbol))).Contains("public/users#name.");
    }

    [Test]
    public async Task OracleFoldsToUpperCase()
    {
        await Assert.That(Refs("CREATE TABLE emp (id NUMBER);\nSELECT Id FROM Emp;", "Emp", SqlDialect.PlSql)).IsEquivalentTo(new[] { "default/EMP#" });
    }

    [Test]
    public async Task TsqlIsCaseInsensitiveAndBracketed()
    {
        const string sql = "CREATE TABLE dbo.[Order Lines] (LineID int)\nGO\nSELECT lineid FROM DBO.[order lines]";
        await Assert.That(Refs(sql, "lineid", SqlDialect.TSql)).IsEquivalentTo(new[] { "dbo/`order lines`#lineid." });
    }

    [Test]
    public async Task AliasesAndCorrelatedSubqueries()
    {
        const string sql = "CREATE TABLE a (id int, x int); CREATE TABLE b (id int, a_id int);\n"
            + "SELECT q.x FROM a q WHERE EXISTS (SELECT 1 FROM b WHERE b.a_id = q.id AND x > 0);";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        await Assert.That(TestSupport.SymbolsAt(index, sql, "a_id").Last()).IsEqualTo("public/b#a_id.");
        await Assert.That(TestSupport.SymbolsAt(index, sql, "q.id".Substring(2))).Contains("public/a#id.");
        await Assert.That(TestSupport.SymbolsAt(index, sql, " x ")).IsEmpty();
        await Assert.That(TestSupport.SymbolsAt(index, sql, "x").Last()).IsEqualTo("public/a#x."); // correlated: outer a.x
        await Assert.That(TestSupport.SymbolsAt(index, sql, "q", definitions: true)).IsEquivalentTo(new[] { "local" });
    }

    [Test]
    public async Task CtesAndDerivedTables()
    {
        const string sql = "CREATE TABLE t (id int, v int);\n"
            + "WITH c (k, total) AS (SELECT id, sum(v) FROM t GROUP BY id)\n"
            + "SELECT d.k, d.total2 FROM (SELECT k, total AS total2 FROM c) d;";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        var doc = index.Documents.Single();
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        // k resolves to the CTE column alias local both inside the derived table and through it.
        var kRefs = TestSupport.SymbolsAt(index, sql, "k");
        await Assert.That(kRefs.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(kRefs.All(s => s == "local")).IsTrue();
        await Assert.That(TestSupport.SymbolsAt(index, sql, "total2")).IsEquivalentTo(new[] { "local" });
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("public/t#v.", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task RecursiveCte()
    {
        const string sql = "CREATE TABLE n (id int, parent int);\n"
            + "WITH RECURSIVE tree (id, depth) AS (SELECT id, 0 FROM n WHERE parent IS NULL UNION ALL SELECT n.id, tree.depth + 1 FROM n JOIN tree ON n.parent = tree.id)\n"
            + "SELECT depth FROM tree;";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        await Assert.That(TestSupport.SymbolsAt(index, sql, "depth").All(s => s == "local")).IsTrue();
        await Assert.That(TestSupport.SymbolsAt(index, sql, "depth").Count).IsEqualTo(2);
    }

    [Test]
    public async Task UnknownTablesBecomeExternalSymbols()
    {
        var index = TestSupport.IndexSql("SELECT x.a, b FROM nowhere x;", SqlDialect.Postgres);
        var ext = index.ExternalSymbols.Select(s => TestSupport.Short(s.Symbol)).ToList();
        await Assert.That(ext).Contains("public/nowhere#");
        await Assert.That(ext).Contains("public/nowhere#a.");
        await Assert.That(ext).Contains("public/nowhere#b.");
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task StarExpansionDefinesViewColumns()
    {
        const string sql = "CREATE TABLE t (a int, b int);\nCREATE VIEW v AS SELECT * FROM t;\nSELECT b FROM v;";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        await Assert.That(TestSupport.SymbolsAt(index, sql, "b").Last()).IsEqualTo("public/v#b.");
        var info = index.Documents.Single().Symbols.Single(s => s.Symbol.EndsWith("public/v#b.", StringComparison.Ordinal));
        await Assert.That(TestSupport.Short(info.Relationships.Single().Symbol)).IsEqualTo("public/t#b.");
    }

    [Test]
    public async Task ForwardReferencesResolveAgainstTheFinalCatalog()
    {
        // pg_dump order: functions before the tables they use.
        const string sql = "CREATE FUNCTION f() RETURNS bigint LANGUAGE sql AS $$ SELECT count(*) FROM later WHERE flag $$;\nCREATE TABLE later (flag boolean);";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        await Assert.That(TestSupport.SymbolsAt(index, sql, "later")).IsEquivalentTo(new[] { "public/later#" });
        await Assert.That(TestSupport.SymbolsAt(index, sql, "flag")).IsEquivalentTo(new[] { "public/later#flag." });
        await Assert.That(index.ExternalSymbols).IsEmpty();
    }

    [Test]
    public async Task OverloadsAreDisambiguatedByParameterTypes()
    {
        const string sql = "CREATE FUNCTION area(r float8) RETURNS float8 LANGUAGE sql AS 'SELECT r';\n"
            + "CREATE FUNCTION area(w int, h int) RETURNS int LANGUAGE sql AS 'SELECT w * h';\n"
            + "SELECT area(2, 3), area(1.5);\nDROP FUNCTION area(integer, integer);";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        var refs = TestSupport.SymbolsAt(index, sql, "area");
        await Assert.That(refs).IsEquivalentTo(new[] { "public/area(`integer,integer`).", "public/area(`double precision`).", "public/area(`integer,integer`)." });
        await Assert.That(TestSupport.SymbolsAt(index, sql, "w").Last()).IsEqualTo("public/area(`integer,integer`).(w)");
    }

    [Test]
    public async Task TsqlVariablesAreBatchScoped()
    {
        const string sql = "DECLARE @x int = 1\nSELECT @x\nGO\nSELECT @x\n";
        var refs = Refs(sql, "@x", SqlDialect.TSql);
        await Assert.That(refs).IsEquivalentTo(new[] { "local" }); // the second batch's @x is undeclared
    }

    [Test]
    public async Task DefaultSchemaOption()
    {
        var index = TestSupport.IndexSql("CREATE TABLE t (a int); SELECT a FROM app.t;", SqlDialect.Postgres, defaultSchema: "app");
        await Assert.That(index.Documents.Single().Symbols.Select(s => TestSupport.Short(s.Symbol))).Contains("app/t#");
        await Assert.That(index.ExternalSymbols.Select(s => TestSupport.Short(s.Symbol))).IsEquivalentTo(new[] { "app/" });
    }

    [Test]
    public async Task CommentsBecomeDocumentation()
    {
        const string sql = "-- The users.\nCREATE TABLE u (\n  id int, -- surrogate key\n  name text\n);\nCOMMENT ON COLUMN u.name IS 'Display name';";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        var infos = index.Documents.Single().Symbols.ToDictionary(s => TestSupport.Short(s.Symbol));
        await Assert.That(infos["public/u#"].Documentation).Contains("The users.");
        await Assert.That(infos["public/u#id."].Documentation).Contains("surrogate key");
        await Assert.That(infos["public/u#name."].Documentation).Contains("Display name");
        await Assert.That(infos["public/u#name."].Documentation[0]).IsEqualTo("```sql\nname text\n```");
    }
}
