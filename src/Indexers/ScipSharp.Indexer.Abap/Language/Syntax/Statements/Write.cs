// Ported from abaplint packages/core/src/abap/5_syntax/statements/write.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Write : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        // todo, more
        IExpressionOrTokenNode second = node.GetChildren().ElementAtOrDefault(1);
        if (second.Get() is WriteOffsetLength)
        {
            second = node.GetChildren().ElementAtOrDefault(2);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            if (s == second && new TypeUtils(input.Scope).IsCharLike(type) == false && new TypeUtils(input.Scope).IsHexLike(type) == false)
            {
                var message = "Source not character like";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, s.GetFirstToken(), message));
                return;
            }
        }

        foreach (var s in node.FindDirectExpression<WriteOffsetLength>()?.FindDirectExpressions<SimpleFieldChain2>() ?? new List<ExpressionNode>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.FieldChain.RunSyntax(s, input, ReferenceType.DataReadReference);
        }

        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(s, input);
        }

        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (target != null)
        {
            var targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
            if (new TypeUtils(input.Scope).IsCharLikeField(targetType) == false)
            {
                var message = $"\"{target.ConcatTokens()}\" must be a character-like field (data type C, N, D, or T, got \"{(JsUtils.Str(targetType?.GetType().Name))}\")";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, target.GetFirstToken(), message));
            }
        }
    }
}
