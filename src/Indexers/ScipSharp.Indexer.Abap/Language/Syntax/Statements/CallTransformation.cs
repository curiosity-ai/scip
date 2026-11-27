// Ported from abaplint packages/core/src/abap/5_syntax/statements/call_transformation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class CallTransformation : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var resultParameters = node.FindExpressionAfterToken("RESULT");
        var resultSources = new HashSet<ExpressionNode>(resultParameters?.FindAllExpressions<SimpleSource3>() ?? new List<ExpressionNode>());

        foreach (var s in node.FindAllExpressions<SimpleSource3>())
        {
            var isResult = resultSources.Contains(s);
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input, null, isResult, false, isResult == false);
        }

        foreach (var d in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(d, input);
        }

        foreach (var t in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            var inline = t?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, XStringType.Get());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
            }
        }
    }
}
