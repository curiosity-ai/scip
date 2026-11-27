// Ported from abaplint packages/core/src/abap/2_statements/expressions/loop_group_by_target.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class LoopGroupByTarget : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var into = Seq(Opt("REFERENCE"), "INTO", Expr<Target>());
        var assigning = Seq("ASSIGNING", Expr<FSTarget>());
        return OptPrio(Alt(into, assigning));
    }
}
