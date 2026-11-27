// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_join_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLJoinSource : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var paren = Seq(
          Tok<Tokens.WParenLeftW>(),
          AltPrio(new SQLJoinSource(), Expr<SQLFromSource>()),
          StarPrio(Seq(OptPrio(Tok<Tokens.WParenRightW>()), new SQLJoin())),
          OptPrio(Tok<Tokens.WParenRightW>()));
        return AltPrio(paren, Expr<SQLFromSource>());
    }
}
