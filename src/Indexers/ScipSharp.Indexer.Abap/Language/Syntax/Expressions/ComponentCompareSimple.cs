// Ported from abaplint packages/core/src/abap/5_syntax/expressions/component_compare_simple.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ComponentCompareSimple
{

    public static void RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType rowType)
    {
        AbstractType? targetType = null;
        foreach (var c in node.GetChildren())
        {
            if (c is ExpressionNode)
            {
                if ((((ExpressionNode)c).Get()) is ComponentChainSimple)
                {
                    targetType = ComponentChain.RunSyntax(rowType, ((ExpressionNode)c), input);
                }
                else if ((((ExpressionNode)c).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
                {
                    targetType = null;
                }
                else if ((((ExpressionNode)c).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source || (((ExpressionNode)c).Get()) is SimpleSource4 || (((ExpressionNode)c).Get()) is SimpleSource2)
                {
                    var sourceType = Source.RunSyntax(((ExpressionNode)c), input, targetType);
                    if (targetType != null && new TypeUtils(input.Scope).IsAssignable(sourceType, targetType) == false)
                    {
                        var message = "ComponentCompareSimple, incompatible types";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                        return;
                    }
                }
                else
                {
                    var message = "ComponentCompareSimple, unexpected node";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
        }
    }
}
