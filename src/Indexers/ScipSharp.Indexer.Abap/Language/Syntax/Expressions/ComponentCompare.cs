// Ported from abaplint packages/core/src/abap/5_syntax/expressions/component_compare.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ComponentCompare
{

    public static void RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? leftType = null, AbstractType? rightType = null)
    {

        var chain = node.FindDirectExpression<ComponentChainSimple>();
        if (chain == null)
        {
            var message = "ComponentCompare, chain not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var fieldType = ComponentChain.RunSyntax(leftType, chain, input);

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            var fieldChain = s.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
            var first = fieldChain?.GetFirstChild();
            if (rightType != null && fieldChain != null && first != null)
            {
                AbstractType? sourceType = rightType;
                if (JsUtils.ToUpper(first.ConcatTokens()) != "TABLE_LINE")
                {
                    sourceType = ComponentName.RunSyntax(sourceType, first, input);
                }
                ComponentChain.RunSyntax(sourceType, fieldChain, input);
            }
            else
            {
                Source.RunSyntax(s, input, fieldType);
            }
        }
    }
}
