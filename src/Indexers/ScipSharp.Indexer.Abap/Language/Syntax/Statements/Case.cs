// Ported from abaplint packages/core/src/abap/5_syntax/statements/case.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Case : StatementSyntax
{

    // Before 7.40 SP02 the operands of CASE and WHEN are enhanced functional operand positions: besides
    // data objects and functional methods, only built-in functions with exactly one unnamed argument are allowed
    public static void CheckCaseOperand(ExpressionNode source, SyntaxInput input)
    {
        if (Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v740sp02) || input.Scope.GetLanguageVersion() == LanguageVersion.Cloud || input.Scope.GetOpenABAP())
        {
            return;
        }

        var chain = source.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallChain>();
        if (chain == null || source.GetChildren().Count != 1 || chain.GetChildren().Count != 1)
        {
            return;
        }
        var call = chain.FindDirectExpression<MethodCall>();
        var nameToken = call?.FindDirectExpression<MethodName>()?.GetFirstToken();
        var name = nameToken?.GetStr();
        if (nameToken == null || BuiltIn.SearchBuiltin(name) == null || BuiltIn.HasUnnamedArgument(name))
        {
            return;
        }

        // a method of the enclosing class takes precedence over the built-in function
        var meType = input.Scope.FindVariable("me")?.GetType();
        if (meType is ObjectReferenceType)
        {
            var def = input.Scope.FindObjectDefinition(((ObjectReferenceType)meType).GetIdentifierName());
            if (new ObjectOriented(input.Scope).SearchMethodName(def, name).method != null)
            {
                return;
            }
        }

        var message = "Built-in function " + JsUtils.Str(name) + " not allowed as CASE/WHEN operand before v740sp02";
        input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameToken, message));
    }
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        // just recurse
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            CheckCaseOperand(s, input);
        }
    }
}
