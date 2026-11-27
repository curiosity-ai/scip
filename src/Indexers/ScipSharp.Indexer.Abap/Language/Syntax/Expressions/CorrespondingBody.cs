// Ported from abaplint packages/core/src/abap/5_syntax/expressions/corresponding_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class CorrespondingBody
{
    public static AbstractType? RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? targetType)
    {

        if (node == null)
        {
            return targetType;
        }

        var @base = node.FindDirectExpression<CorrespondingBodyBase>()?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (@base != null)
        {
            Source.RunSyntax(@base, input, targetType);
        }

        AbstractType? type = null;
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            if (type == null)
            {
                type = Source.RunSyntax(s, input);
            }
            else
            {
                Source.RunSyntax(s, input);
            }
        }

        return targetType != null ? targetType : type;
    }
}
