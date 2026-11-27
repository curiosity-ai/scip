// Ported from abaplint packages/core/src/abap/2_statements/expressions/loop_group_by_component.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class LoopGroupByComponent : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var groupSize = Seq(Expr<ComponentName>(), "=", "GROUP SIZE");
        var groupIndex = Seq(Expr<ComponentName>(), "=", "GROUP INDEX");
        var components = Alt(Expr<ComponentCompareSingle>(), groupSize, groupIndex);
        return components;
    }
}
