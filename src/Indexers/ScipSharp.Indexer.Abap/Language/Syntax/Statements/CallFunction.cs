// Ported from abaplint packages/core/src/abap/5_syntax/statements/call_function.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class CallFunction : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        // todo, lots of work here, similar to receive.ts
        var name = node.FindFirstExpression<FunctionName>();
        var chain = name?.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
        if (chain != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.FieldChain.RunSyntax(chain, input, ReferenceType.DataReadReference);
        }
        else if (input.Scope.GetLanguageVersion() == LanguageVersion.Cloud && node.FindDirectExpression<Destination>() == null)
        {
            var functionName = JsUtils.RegexReplace(name?.ConcatTokens(), @"'", "", RegexOptions.ECMAScript);
            if (input.Scope.FindFunctionModule(functionName) == null)
            {
                var message = $"Function module \"{JsUtils.Str(functionName)}\" not found/released";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        foreach (var s in node.FindDirectExpressions<SimpleSource2>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var d in node.FindDirectExpressions<Destination>())
        {
            var source = d.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            if (source != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(source, input);
            }
        }

        var fp = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FunctionParameters>();
        if (fp != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.FunctionParameters.RunSyntax(fp, input);
        }
    }
}
