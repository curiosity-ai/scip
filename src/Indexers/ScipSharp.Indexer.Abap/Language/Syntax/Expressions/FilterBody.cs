// Ported from abaplint packages/core/src/abap/5_syntax/expressions/filter_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class FilterBody
{
    public static AbstractType? RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? targetType)
    {

        if (node == null)
        {
            return targetType;
        }

        List<AbstractType?> types = new List<AbstractType?>();
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            types.Add(Source.RunSyntax(s, input));
        }

        AbstractType? inputRowType = types.ElementAtOrDefault(0) is TableType ? ((TableType)types.ElementAtOrDefault(0)!).GetRowType() : (AbstractType?)null;
        AbstractType? filterRowType = types.ElementAtOrDefault(1) is TableType ? ((TableType)types.ElementAtOrDefault(1)!).GetRowType() : (AbstractType?)null;
        ComponentCond.RunSyntax(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCond>()!, input, inputRowType, filterRowType);

        return types.ElementAtOrDefault(0) != null ? types.ElementAtOrDefault(0) : targetType;
    }
}
