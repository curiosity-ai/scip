// Ported from abaplint packages/core/src/abap/2_statements/statements/insert_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InsertDatabase : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var sub = Seq(Tok<Tokens.WParenLeftW>(), Expr<Expressions.Select>(), Tok<Tokens.WParenRightW>());

        var fromItabOrWa = Seq("FROM",
                                 Opt("TABLE"),
                                 Expr<SQLSource>(),
                                 OptPrio(Expr<SQLIndicators>()),
                                 OptPrio(Expr<SQLMappingFromEntity>()));
        var fromSubquery = Seq("FROM", sub);
        var fromClause = Alt(fromItabOrWa, fromSubquery);

        var fromTrailer = Seq(Opt(Expr<SQLClient>()),
                                Opt(Expr<DatabaseConnection>()),
                                fromClause,
                                Opt("ACCEPTING DUPLICATE KEYS"),
                                OptPrio(Expr<SQLDmlOptions>()));

        var insertFromForm = Seq(Expr<DatabaseTable>(),
                                   Opt(Alt(fromTrailer, Expr<SQLClient>(), Expr<DatabaseConnection>())));

        var insertIntoForm = Seq("INTO",
                                   Expr<DatabaseTable>(),
                                   Opt(Expr<SQLClient>()),
                                   Opt(Expr<DatabaseConnection>()),
                                   "VALUES",
                                   Expr<SQLSource>(),
                                   OptPrio(Expr<SQLIndicators>()),
                                   OptPrio(Expr<SQLMappingFromEntity>()),
                                   OptPrio(Expr<SQLDmlOptions>()));

        return VerNotLang(LanguageVersion.KeyUser,
                          Seq("INSERT", Alt(insertFromForm, insertIntoForm)));
    }
}
