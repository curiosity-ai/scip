// Ported from abaplint packages/core/src/abap/5_syntax/expressions/loop_group_by.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class LoopGroupBy
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        foreach (var t in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            var inline = t.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                InlineData.RunSyntax(inline, input, VoidType.Get("todoGroupBy"));
            }
            else
            {
                Target.RunSyntax(t, input);
            }
        }

        foreach (var t in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget>())
        {
            FSTarget.RunSyntax(t, input, VoidType.Get("todoGroupBy"));
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            Source.RunSyntax(t, input, VoidType.Get("todoGroupBy"));
        }

        foreach (var c in node.FindDirectExpressions<LoopGroupByComponent>())
        {
            foreach (var t in c.FindDirectExpressions<ComponentCompareSingle>())
            {
                ComponentCompare.RunSyntax(t, input);
            }
        }
    }
}
