// Ported from abaplint packages/core/src/abap/5_syntax/expressions/function_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class FunctionParameters
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        // EXPORTING: process sources in each FunctionExportingParameter
        foreach (var param in node.FindAllExpressions<FunctionExportingParameter>())
        {
            var s = param.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            if (s != null)
            {
                Source.RunSyntax(s, input);
            }
            var s3 = param.FindDirectExpression<SimpleSource3>();
            if (s3 != null)
            {
                Source.RunSyntax(s3, input);
            }
        }

        // IMPORTING / CHANGING / TABLES: process targets in each ParameterT
        var changingList = node.FindExpressionAfterToken("CHANGING");
        foreach (var paramList in node.FindDirectExpressions<ParameterListT>())
        {
            foreach (var param in paramList.FindDirectExpressions<ParameterT>())
            {
                var t = param.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
                if (t != null)
                {
                    Target.RunSyntax(t, input);
                }

                if (paramList == changingList && t != null)
                {
                    // hmm, does this do the scoping correctly? handle constants etc? todo
                    var found = input.Scope.FindVariable(t.ConcatTokens());
                    if (found != null && found.GetMeta().Contains(IdentifierMeta.ReadOnly))
                    {
                        var message = $"\"{t.ConcatTokens()}\" cannot be modified, it is readonly";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, t.GetFirstToken(), message));
                    }
                }
            }
        }

        // EXCEPTIONS: process field chains and optional MESSAGE targets
        foreach (var exc in node.FindAllExpressions<ParameterException>())
        {
            foreach (var s in exc.FindDirectExpressions<SimpleFieldChain>())
            {
                FieldChain.RunSyntax(s, input, ReferenceType.DataReadReference);
            }
            var t = exc.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
            if (t != null)
            {
                Target.RunSyntax(t, input);
            }
        }
    }
}
