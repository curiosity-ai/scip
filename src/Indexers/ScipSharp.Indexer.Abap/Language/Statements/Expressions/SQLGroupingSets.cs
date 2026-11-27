// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_grouping_sets.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLGroupingSets : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var set = Seq(Tok<Tokens.WParenLeftW>(),
                        OptPrio(Seq(Expr<SQLField>(), StarPrio(Seq(",", Expr<SQLField>())))),
                        Tok<Tokens.WParenRightW>());
        var sets = Seq(set, StarPrio(Seq(",", set)));
        return Seq("GROUPING SETS", Tok<Tokens.WParenLeftW>(), sets, Tok<Tokens.WParenRightW>());
    }
}
