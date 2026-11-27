using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Tests;

public class LexerTests
{
    private static List<Token> Lex(string sql, SqlDialect d) =>
        SqlLexer.Lex(sql, d).Tokens.Where(t => t.Kind != TokenKind.EndOfFile).ToList();

    [Test]
    public async Task QuotedIdentifiersPerDialect()
    {
        var pg = Lex("\"My \"\"Table\"\"\"", SqlDialect.Postgres);
        await Assert.That(pg.Single().Kind).IsEqualTo(TokenKind.QuotedIdent);
        await Assert.That(pg.Single().Value).IsEqualTo("My \"Table\"");

        var ts = Lex("[Order Details].[Unit]]Price]", SqlDialect.TSql);
        await Assert.That(ts.Select(t => t.Kind).ToArray()).IsEquivalentTo(new[] { TokenKind.QuotedIdent, TokenKind.Op, TokenKind.QuotedIdent });
        await Assert.That(ts[2].Value).IsEqualTo("Unit]Price");

        var my = Lex("`weird``name` \"text\"", SqlDialect.MySql);
        await Assert.That(my[0].Kind).IsEqualTo(TokenKind.QuotedIdent);
        await Assert.That(my[0].Value).IsEqualTo("weird`name");
        await Assert.That(my[1].Kind).IsEqualTo(TokenKind.String); // MySQL double quotes are strings
    }

    [Test]
    public async Task StringFlavours()
    {
        var pg = Lex("E'a\\'b' $$x;y$$ $fn$ body $$ nested $fn$ U&'d\\0061t'", SqlDialect.Postgres);
        await Assert.That(pg.All(t => t.Kind == TokenKind.String)).IsTrue();
        await Assert.That(pg[0].Value).IsEqualTo("a'b");
        await Assert.That(pg[1].Value).IsEqualTo("x;y");
        await Assert.That(pg[2].Value).IsEqualTo(" body $$ nested ");

        var ora = Lex("q'[it's]' N'x'", SqlDialect.PlSql);
        await Assert.That(ora[0].Value).IsEqualTo("it's");
        await Assert.That(ora[1].Kind).IsEqualTo(TokenKind.String);

        var ts = Lex("N'unicode''s'", SqlDialect.TSql);
        await Assert.That(ts.Single().Value).IsEqualTo("unicode's");
    }

    [Test]
    public async Task Comments()
    {
        var pg = SqlLexer.Lex("/* a /* nested */ still */ SELECT 1 -- tail", SqlDialect.Postgres);
        await Assert.That(pg.Comments.Count).IsEqualTo(2);
        await Assert.That(pg.Tokens[0].Text).IsEqualTo("SELECT");

        var my = SqlLexer.Lex("# hash comment\nSELECT 1 /* x */", SqlDialect.MySql);
        await Assert.That(my.Comments[0].Text).IsEqualTo("# hash comment");
        await Assert.That(my.Tokens[0].Text).IsEqualTo("SELECT");

        // Not nested in MySQL: the first */ closes the comment.
        var flat = SqlLexer.Lex("/* a /* b */ SELECT", SqlDialect.MySql);
        await Assert.That(flat.Tokens[0].Text).IsEqualTo("SELECT");
    }

    [Test]
    public async Task BatchSeparatorsAndDelimiters()
    {
        var ts = Lex("SELECT 1\nGO\nSELECT 2\n  go 5\n", SqlDialect.TSql);
        await Assert.That(ts.Count(t => t.Kind == TokenKind.BatchSeparator)).IsEqualTo(2);

        var ora = Lex("BEGIN NULL; END;\n/\nSELECT 1 / 2 FROM dual;", SqlDialect.PlSql);
        await Assert.That(ora.Count(t => t.Kind == TokenKind.BatchSeparator)).IsEqualTo(1);

        var my = Lex("DELIMITER $$\nCREATE PROCEDURE p() BEGIN SELECT 1; END$$\nDELIMITER ;\nSELECT 2;", SqlDialect.MySql);
        await Assert.That(my.Count(t => t.Kind == TokenKind.Delimiter)).IsEqualTo(1);
        await Assert.That(my.Count(t => t.Kind == TokenKind.Semicolon)).IsEqualTo(1);
    }

    [Test]
    public async Task VariablesAndTemplates()
    {
        var ts = Lex("@x @@ROWCOUNT #tmp ##global", SqlDialect.TSql);
        await Assert.That(ts.Select(t => t.Kind).ToArray()).IsEquivalentTo(new[] { TokenKind.Variable, TokenKind.Variable, TokenKind.Word, TokenKind.Word });

        var pg = Lex("$1 :name x::int", SqlDialect.Postgres);
        await Assert.That(pg[0].Kind).IsEqualTo(TokenKind.Variable);
        await Assert.That(pg[1].Kind).IsEqualTo(TokenKind.Variable);
        await Assert.That(pg.Any(t => t.Text == "::")).IsTrue();

        var dbt = Lex("select * from {{ ref('stg_orders') }} join {{ source('raw', 'customers') }}", SqlDialect.Ansi);
        var templates = dbt.Where(t => t.Kind == TokenKind.Template).ToList();
        await Assert.That(templates[0].Value).IsEqualTo("ref:stg_orders");
        await Assert.That(templates[1].Value).IsEqualTo("source:raw.customers");
    }

    [Test]
    public async Task PsqlAndSqlPlusCommandsAreSkipped()
    {
        var pg = Lex("\\connect db\nSELECT 1;", SqlDialect.Postgres);
        await Assert.That(pg[0].Text).IsEqualTo("SELECT");
        var ora = Lex("SET SERVEROUTPUT ON\nPROMPT hello\nSELECT 1 FROM dual;", SqlDialect.PlSql);
        await Assert.That(ora[0].Text).IsEqualTo("SELECT");
    }
}
