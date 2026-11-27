// Ported from abaplint packages/core/src/abap/5_syntax/expressions/method_call_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class MethodCallBody
{

    public static void RunSyntax(ExpressionNode node, SyntaxInput input, object method)
    {
        var parameters = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodParameters>();
        if (parameters != null)
        {
            new MethodParameters().RunSyntax(parameters, input, method);
        }

        var param = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallParam>();
        if (param != null)
        {
            MethodCallParam.RunSyntax(param, input, method);
        }

        // for PARAMETER-TABLE and EXCEPTION-TABLE
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            Source.RunSyntax(s, input);
        }
    }
}
