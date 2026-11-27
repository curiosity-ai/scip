// Ported from abaplint packages/core/src/abap/5_syntax/expressions/cond.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Cond
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        foreach (var t in node.FindDirectExpressions<CondSub>())
        {
            var c = t.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Cond>();
            if (c != null)
            {
                Cond.RunSyntax(c, input);
            }
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Compare>())
        {
            Compare.RunSyntax(t, input);
        }
    }
}
