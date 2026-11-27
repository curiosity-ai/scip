using System.Diagnostics;

namespace ScipSharp.Indexer.Sql.Tests;

/// <summary>Project-level behaviour: file ordering, migrations, dialect detection, CLI.</summary>
public class ProjectTests
{
    [Test]
    public async Task NaturalSortOrdersMigrations()
    {
        string[] files = ["V10__c.sql", "V2__b.sql", "V1__a.sql", "V1_1__a2.sql", "sub/V3.sql", "a.sql"];
        var sorted = files.Order(NaturalComparer.Instance).ToArray();
        await Assert.That(sorted).IsEquivalentTo(new[] { "a.sql", "sub/V3.sql", "V1__a.sql", "V1_1__a2.sql", "V2__b.sql", "V10__c.sql" });
    }

    [Test]
    public async Task MigrationsReplayAlterTableInOrder()
    {
        var index = TestSupport.IndexFiles(new Dictionary<string, string>
        {
            ["V1__init.sql"] = "CREATE TABLE t (a int);",
            ["V2__add.sql"] = "ALTER TABLE t ADD COLUMN b int;",
            ["V10__rename.sql"] = "ALTER TABLE t RENAME COLUMN b TO c;\nSELECT a, c FROM t;",
            ["V11__drop.sql"] = "ALTER TABLE t DROP COLUMN c;\nDROP TABLE t;",
            ["V12__recreate.sql"] = "CREATE TABLE t (z int);",
        }, SqlDialect.Postgres);
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        await Assert.That(index.Documents.Select(d => d.RelativePath).ToArray())
            .IsEquivalentTo(new[] { "V1__init.sql", "V2__add.sql", "V10__rename.sql", "V11__drop.sql", "V12__recreate.sql" });
        var rename = index.Documents.Single(d => d.RelativePath == "V10__rename.sql");
        var refs = rename.Occurrences.Where(o => o.SymbolRoles == 0).Select(o => TestSupport.Short(o.Symbol)).ToList();
        await Assert.That(refs).Contains("public/t#b.");
        await Assert.That(refs).Contains("public/t#c.");
        await Assert.That(refs).Contains("public/t#a.");
        var drop = index.Documents.Single(d => d.RelativePath == "V11__drop.sql");
        await Assert.That(drop.Occurrences.Select(o => TestSupport.Short(o.Symbol))).Contains("public/t#c.");
        await Assert.That(index.ExternalSymbols).IsEmpty();
    }

    [Test]
    public async Task RenamedTableKeepsColumnSymbols()
    {
        const string sql = "CREATE TABLE old_t (a int);\nALTER TABLE old_t RENAME TO new_t;\nSELECT a FROM new_t;";
        var index = TestSupport.IndexSql(sql, SqlDialect.Postgres);
        await Assert.That(TestSupport.SymbolsAt(index, sql, "a").Last()).IsEqualTo("public/old_t#a.");
        await Assert.That(TestSupport.SymbolsAt(index, sql, "new_t", definitions: true)).IsEquivalentTo(new[] { "public/new_t#" });
    }

    [Test]
    public async Task DbtModelsAreRelationsNamedAfterTheirFile()
    {
        var index = TestSupport.IndexFiles(new Dictionary<string, string>
        {
            ["dbt_project.yml"] = "name: p\n",
            ["models/stg_a.sql"] = "select id as a_id, name from {{ source('raw', 'a') }}",
            ["models/b.sql"] = "{{ config(materialized='view') }}\nselect a_id from {{ ref('stg_a') }} {% if is_incremental() %} where a_id > 0 {% endif %}",
        });
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
        var b = index.Documents.Single(d => d.RelativePath == "models/b.sql");
        var refs = b.Occurrences.Select(o => TestSupport.Short(o.Symbol)).ToList();
        await Assert.That(refs).Contains("default/stg_a#");
        await Assert.That(refs.Count(r => r == "default/stg_a#a_id.")).IsEqualTo(2);
        await Assert.That(index.ExternalSymbols.Select(s => TestSupport.Short(s.Symbol))).Contains("raw/a#");
    }

    [Test]
    public async Task DetectsDialects()
    {
        await Assert.That(Dialects.Detect(["CREATE TABLE [dbo].[x] (id INT IDENTITY(1,1))\nGO\n"])).IsEqualTo(SqlDialect.TSql);
        await Assert.That(Dialects.Detect(["CREATE FUNCTION f() RETURNS trigger AS $$ BEGIN END $$ LANGUAGE plpgsql;"])).IsEqualTo(SqlDialect.Postgres);
        await Assert.That(Dialects.Detect(["CREATE TABLE `x` (id INT AUTO_INCREMENT) ENGINE=InnoDB;"])).IsEqualTo(SqlDialect.MySql);
        await Assert.That(Dialects.Detect(["CREATE OR REPLACE PACKAGE BODY p AS v VARCHAR2(10); END;\n/\n"])).IsEqualTo(SqlDialect.PlSql);
        await Assert.That(Dialects.Detect(["SELECT 1;"])).IsEqualTo(SqlDialect.Ansi);
        await Assert.That(Dialects.TryParse("mssql", out var d) && d == SqlDialect.TSql).IsTrue();
    }

    [Test]
    public async Task CliWritesAParsableIndex()
    {
        var output = Path.Combine(Path.GetTempPath(), $"scip-sql-{Guid.NewGuid():N}.scip");
        try
        {
            var code = await new SqlIndexer().IndexAsync(
                new ScipSharp.Indexing.IndexerOptions(Path.Combine(TestSupport.TestData, "snapshots", "input", "postgres"), output, ["--dialect=postgres", "--package-name=shop"]),
                CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-sql");
            await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
            await Assert.That(index.Documents.Count).IsEqualTo(4);
            await Assert.That(index.Documents[0].Symbols[0].Symbol).StartsWith("scip-sql . shop . ");
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Test]
    public async Task CliRejectsUnknownOptionsAndPrintsHelp()
    {
        var indexer = new SqlIndexer();
        await Assert.That(indexer.Name).IsEqualTo("sql");
        var help = await indexer.IndexAsync(new ScipSharp.Indexing.IndexerOptions(".", "unused.scip", ["--help"]), CancellationToken.None);
        await Assert.That(help).IsEqualTo(0);
        var bad = await indexer.IndexAsync(new ScipSharp.Indexing.IndexerOptions(".", "unused.scip", ["--dialect=cobol"]), CancellationToken.None);
        await Assert.That(bad).IsEqualTo(1);
    }

    /// <summary>Optional cross-check: the tables and columns we define for the SQLite fixture match what sqlite3 creates.</summary>
    [Test]
    public async Task SqliteSchemaMatchesSqlite3WhenAvailable()
    {
        var fixture = Path.Combine(TestSupport.TestData, "snapshots", "input", "sqlite", "app.sql");
        string output;
        try
        {
            var psi = new ProcessStartInfo("sqlite3", [":memory:"]) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi);
            if (p is null)
            {
                Skip.Test("sqlite3 could not be started");
            }

            await p.StandardInput.WriteAsync(File.ReadAllText(fixture));
            await p.StandardInput.WriteLineAsync("\nSELECT m.name || '.' || lower(c.name) FROM sqlite_master m, pragma_table_info(m.name) c WHERE m.type IN ('table', 'view') ORDER BY 1;");
            p.StandardInput.Close();
            output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Skip.Test("sqlite3 is not installed");
            return;
        }

        var expected = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var index = new SqlProjectIndexer(Path.GetDirectoryName(fixture)!, new SqlIndexerOptions { Dialect = SqlDialect.Sqlite, PackageName = "x" }).Build();
        var ours = index.Documents.SelectMany(d => d.Symbols).Select(s => TestSupport.Short(s.Symbol))
            .Where(s => s.StartsWith("main/", StringComparison.Ordinal) && s.EndsWith('.') && s.Count(ch => ch == '#') == 1)
            .Select(s => s["main/".Length..^1].Replace('#', '.'))
            .ToHashSet();
        await Assert.That(ours.SetEquals(expected)).IsTrue().Because($"sqlite3: {string.Join(", ", expected.Order())}; ours: {string.Join(", ", ours.Order())}");
    }
}
