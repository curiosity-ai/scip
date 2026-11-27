// Ported from abaplint packages/core/src/abap/5_syntax/expressions/cond_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class CondBody
{
    public static AbstractType? RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? targetType)
    {

        if (node == null)
        {
            return null;
        }

        bool scoped = false;
        var l = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Let>();
        if (l != null)
        {
            scoped = Let.RunSyntax(l, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Cond>())
        {
            Cond.RunSyntax(s, input);
        }

        AbstractType? type = null;
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            if (type == null)
            {
                type = Source.RunSyntax(s, input, targetType);
            }
            else
            {
                Source.RunSyntax(s, input, targetType ?? type);
            }
        }

        if (scoped == true)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }

        return type;
    }
}
