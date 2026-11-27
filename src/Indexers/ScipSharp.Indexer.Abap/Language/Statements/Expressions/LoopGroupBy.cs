// Ported from abaplint packages/core/src/abap/2_statements/expressions/loop_group_by.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class LoopGroupBy : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var components = Seq(Tok<Tokens.WParenLeftW>(), Plus(Expr<LoopGroupByComponent>()), Tok<Tokens.WParenRightW>());

        var ret = Seq(Alt(Expr<Source>(), components),
                        OptPrio("ASCENDING"),
                        OptPrio("WITHOUT MEMBERS"),
                        Expr<LoopGroupByTarget>());

        return ret;
    }
}
