// Ported from abaplint packages/core/src/abap/5_syntax/expressions/reduce_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ReduceBody
{
    public static AbstractType? RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? targetType)
    {

        if (node == null)
        {
            return null;
        }

        bool scoped = false;
        var letNode = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Let>();
        if (letNode != null)
        {
            scoped = Let.RunSyntax(letNode, input);
        }

        AbstractType? first = null;
        foreach (var i in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineFieldDefinition>())
        {
            if (scoped == false)
            {
                input.Scope.Push(ScopeType.Let, "LET", node.GetFirstToken().GetStart(), input.Filename);
                scoped = true;
            }

            AbstractType? foundType = targetType;
            var source = i.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            if (source != null)
            {
                foundType = Source.RunSyntax(source, input, targetType);
            }

            var found = InlineFieldDefinition.RunSyntax(i, input, foundType);
            if (found != null && first == null)
            {
                first = found;
            }
        }

        int forScopes = 0;
        foreach (var forNode in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.For>() ?? new List<ExpressionNode>())
        {
            var scoped2 = For.RunSyntax(forNode, input);
            if (scoped2 == true)
            {
                forScopes++;
            }
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            Source.RunSyntax(s, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.ReduceNext>())
        {
            ReduceNext.RunSyntax(s, input);
        }

        if (scoped == true)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }

        for (var i = 0; i < forScopes; i++)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }

        if (first != null)
        {
            return first;
        }
        else
        {
            return new UnknownType("todo, ReduceBody");
        }
    }
}
