// Ported from abaplint packages/core/src/abap/2_statements/statements/delete_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DeleteDatabase : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var where = Seq("WHERE", AltPrio(Expr<SQLCond>(), Expr<Dynamic>()));

        var upToOffset = Ver(Release.v764, AltPrio(
          Seq(Expr<SQLUpTo>(), OptPrio(Expr<SQLOffset>())),
          Seq(Expr<SQLOffset>(), OptPrio(Expr<SQLUpTo>()))));
        var orderByTail = Ver(Release.v764, Seq(Expr<SQLOrderBy>(), OptPrio(upToOffset)));
        var trailing = OptPrio(AltPrio(orderByTail, upToOffset));

        var from = Seq("FROM", Expr<DatabaseTable>(), OptPrio(Expr<SQLClient>()), OptPrio(Expr<DatabaseConnection>()),
                         Opt(where), trailing, OptPrio(Expr<SQLDmlOptions>()));

        var fromSomething = Seq("FROM", Opt("TABLE"), Expr<SQLSourceSimple>(), OptPrio(Expr<SQLIndicators>()), OptPrio(Expr<SQLMappingFromEntity>()));

        var table = Seq(Expr<DatabaseTable>(),
                          OptPrio(Expr<SQLClient>()),
                          OptPrio(Expr<DatabaseConnection>()),
                          OptPrio(fromSomething),
                          OptPrio(Expr<SQLDmlOptions>()));

        var ret = Seq("DELETE", AltPrio(from, table));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
