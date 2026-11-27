// Ported from abaplint packages/core/src/abap/5_syntax/expressions/component_cond.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ComponentCond
{

    public static void RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? leftType = null, AbstractType? rightType = null)
    {

        foreach (var t in node.FindDirectExpressions<ComponentCondSub>())
        {
            var c = t.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCond>();
            if (c != null)
            {
                ComponentCond.RunSyntax(c, input, leftType, rightType);
            }
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCompare>())
        {
            ComponentCompare.RunSyntax(t, input, leftType, rightType);
        }
    }
}
