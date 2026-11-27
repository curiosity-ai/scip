// Ported from abaplint packages/core/src/abap/2_statements/expressions/component_chain_simple.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ComponentChainSimple : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var chain = StarPrio(AltPrio(
          Expr<Dereference>(),
          Seq(Expr<ArrowOrDash>(), Expr<ComponentName>())));

        var ret = Seq(Expr<ComponentName>(), chain, OptPrio(Expr<FieldOffset>()), OptPrio(Expr<FieldLength>()));

        return ret;
    }
}
