// Ported from abaplint packages/core/src/abap/5_syntax/expressions/conv_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ConvBody
{
    public static AbstractType? RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? targetType = null)
    {
        bool scoped = false;
        var l = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Let>();
        if (l != null)
        {
            scoped = Let.RunSyntax(l, input);
        }

        var s = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (s == null)
        {
            var message = "ConvBody, no source found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }
        var sourceType = Source.RunSyntax(s, input, targetType);

        if (scoped == true)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }

        return sourceType;
    }
}
