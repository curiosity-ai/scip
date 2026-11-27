// Ported from abaplint packages/core/src/abap/5_syntax/expressions/switch_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SwitchBody
{
    public static AbstractType? RunSyntax(ExpressionNode? node, SyntaxInput input, AbstractType? targetType = null)
    {
        if (node == null)
        {
            return null;
        }

        var thenSource = node.FindExpressionAfterToken("THEN");
        if (!(((thenSource?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)))
        {
            var message = "SwitchBody, unexpected";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }
        var type = Source.RunSyntax(thenSource, input, targetType);

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            if (s == thenSource)
            {
                continue;
            }
            Source.RunSyntax(s, input, targetType ?? type);
        }

        return type;
    }
}
