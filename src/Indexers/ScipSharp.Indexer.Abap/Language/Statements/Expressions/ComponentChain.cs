// Ported from abaplint packages/core/src/abap/2_statements/expressions/component_chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ComponentChain : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var chain = Seq(Expr<ComponentName>(),
                          Star(AltPrio(Seq(Expr<ArrowOrDash>(), AltPrio("*", Expr<ComponentName>())), Expr<TableExpression>())));

        var ret = Seq(chain, OptPrio(Expr<TableBody>()), OptPrio(Expr<FieldOffset>()), OptPrio(Expr<FieldLength>()));

        return ret;
    }
}
