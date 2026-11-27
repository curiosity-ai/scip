// Ported from abaplint packages/core/src/abap/2_statements/expressions/component_compare_simple.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ComponentCompareSimple : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var source = AltPrio(Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP), Expr<SimpleSource4>());
        var ret = Seq(AltPrio(Expr<ComponentChainSimple>(), Expr<Dynamic>()), "=", source);
        return Plus(ret);
    }
}
