// Ported from abaplint packages/core/src/abap/2_statements/expressions/component_compare_single.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ComponentCompareSingle : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = Seq(AltPrio(Expr<ComponentChainSimple>(), Expr<Dynamic>()), "=", Expr<Source>());
        return ret;
    }
}
