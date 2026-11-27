using ScipSharp.Indexer.Sql.Lexing;
using ScipSharp.Indexer.Sql.Syntax;

namespace ScipSharp.Indexer.Sql.Tests;

public class SplitterTests
{
    private static int Count(string sql, SqlDialect d)
    {
        var lex = SqlLexer.Lex(sql, d);
        return StatementSplitter.Split(lex.Tokens, d).Count;
    }

    [Test]
    public async Task SemicolonsInsideBlocksDoNotSplit()
    {
        await Assert.That(Count("SELECT 1; SELECT 2;", SqlDialect.Ansi)).IsEqualTo(2);
        await Assert.That(Count("CREATE PROCEDURE p AS BEGIN SELECT 1; SELECT 2; END; SELECT 3;", SqlDialect.TSql)).IsEqualTo(2);
        await Assert.That(Count("CREATE OR REPLACE PROCEDURE p IS v NUMBER; BEGIN v := 1; IF v = 1 THEN NULL; END IF; END p;\n/\nSELECT 1 FROM dual;", SqlDialect.PlSql)).IsEqualTo(2);
        await Assert.That(Count("CREATE TRIGGER t AFTER INSERT ON a BEGIN UPDATE a SET x = 1; DELETE FROM b; END; SELECT 1;", SqlDialect.Sqlite)).IsEqualTo(2);
    }

    [Test]
    public async Task PostgresBeginIsATransaction()
    {
        await Assert.That(Count("BEGIN; INSERT INTO t VALUES (1); COMMIT;", SqlDialect.Postgres)).IsEqualTo(3);
        await Assert.That(Count("CREATE FUNCTION f() RETURNS int AS $$ BEGIN RETURN 1; END $$ LANGUAGE plpgsql; SELECT f();", SqlDialect.Postgres)).IsEqualTo(2);
    }

    [Test]
    public async Task TsqlGoEndsBatches()
    {
        var script = SqlParser.ParseScript("DECLARE @a int\nGO\nSELECT @a\nGO\n", SqlDialect.TSql);
        await Assert.That(script.Statements.Select(s => s.Batch).ToArray()).IsEquivalentTo(new[] { 0, 1 });
    }

    [Test]
    public async Task TsqlScriptWithoutSemicolons()
    {
        var script = SqlParser.ParseScript("CREATE TABLE a (x int)\nCREATE TABLE b (y int)\nINSERT INTO a VALUES (1)\nSELECT * FROM b\n", SqlDialect.TSql);
        await Assert.That(script.Statements.Select(s => s.GetType().Name).ToArray())
            .IsEquivalentTo(new[] { "CreateTableStmt", "CreateTableStmt", "InsertStmt", "QueryStmt" });
    }
}
