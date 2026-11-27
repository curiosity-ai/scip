// Ported from abaplint packages/core/src/abap/2_statements/statements/modify_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ModifyDatabase : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var sub = Seq(Tok<Tokens.WParenLeftW>(), Expr<Expressions.Select>(), Tok<Tokens.WParenRightW>());
        var fromTable = Seq("FROM", Opt("TABLE"), Expr<SQLSource>(), OptPrio(Expr<SQLIndicators>()), OptPrio(Expr<SQLMappingFromEntity>()));
        var fromSubquery = Seq("FROM", sub);
        var from = Alt(fromTable, fromSubquery);

        var options = Per(Expr<DatabaseConnection>(), from, Expr<SQLClient>());

        return VerNotLang(LanguageVersion.KeyUser,
                          Seq("MODIFY", Expr<DatabaseTable>(), options, OptPrio(Expr<SQLDmlOptions>())));
    }
}
