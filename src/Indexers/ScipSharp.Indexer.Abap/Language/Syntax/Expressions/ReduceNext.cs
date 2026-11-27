// Ported from abaplint packages/core/src/abap/5_syntax/expressions/reduce_next.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ReduceNext
{
    public static void RunSyntax(ExpressionNode? node, SyntaxInput input)
    {
        if (node == null)
        {
            return;
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            Source.RunSyntax(s, input);
        }

        foreach (var s in node.FindDirectExpressions<SimpleTarget>())
        {
            Target.RunSyntax(s, input);
        }
    }
}
