// Ported from abaplint packages/core/src/abap/5_syntax/statements/do.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Do : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var afterDo = node.FindExpressionAfterToken("DO");

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            if (s == afterDo && new TypeUtils(input.Scope).IsAssignable(type, IntegerType.Get()) == false)
            {
                var message = "DO TIMES must be numeric";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
        }
    }
}
