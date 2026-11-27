// Ported from abaplint packages/core/src/abap/5_syntax/expressions/table_expression.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class TableExpression
{
    public static void RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? rowType)
    {
        if (node == null)
        {
            return;
        }

        AbstractType? context = rowType;
        if (context is TableType)
        {
            context = ((TableType)context).GetRowType();
        }

        if (node.GetChildren().Count == 3)
        {
            var s = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            Source.RunSyntax(s, input, context);
        }
        else if (node.FindDirectTokenByText("INDEX") != null)
        {
            var s = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            Source.RunSyntax(s, input);
        }
        else
        {
            AbstractType? fieldType = null;
            foreach (var c in node.GetChildren())
            {
                if (c is ExpressionNode && (((ExpressionNode)c).Get()) is ComponentChainSimple)
                {
                    fieldType = ComponentChain.RunSyntax(context, ((ExpressionNode)c), input);
                }
                else if (c is ExpressionNode && (((ExpressionNode)c).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)
                {
                    Source.RunSyntax(((ExpressionNode)c), input, fieldType);
                }
            }
        }
    }
}
