// Ported from abaplint packages/core/src/abap/5_syntax/statements/shift.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Shift : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (target == null)
        {
            var message = "Shift, Target not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
        if (JsUtils.ToUpper(node.ConcatTokens()).Contains(" IN BYTE MODE", StringComparison.Ordinal))
        {
            if (new TypeUtils(input.Scope).IsHexLike(targetType) == false)
            {
                var message = "Shift, Target not hex like";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
        else
        {
            if (new TypeUtils(input.Scope).IsCharLike(targetType) == false)
            {
                var message = "Shift, Target not char like";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
    }
}
