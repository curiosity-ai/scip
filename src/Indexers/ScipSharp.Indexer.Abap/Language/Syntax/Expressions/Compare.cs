// Ported from abaplint packages/core/src/abap/5_syntax/expressions/compare.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Compare
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        List<AbstractType?> sourceTypes = new List<AbstractType?>();
        var sources = node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        foreach (var t in sources)
        {
            sourceTypes.Add(Source.RunSyntax(t, input));
        }

        foreach (var t in node.FindDirectExpressions<SourceFieldSymbolChain>())
        {
            SourceFieldSymbol.RunSyntax(t, input);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallChain>())
        {
            MethodCallChain.RunSyntax(t, input);
        }

        var initialMessage = Compare.CheckInitialOperand(node, input);
        if (initialMessage != null)
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), initialMessage));
            return;
        }

        var predicateMessage = Compare.CheckPredicateOperand(sources, input);
        if (predicateMessage != null)
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), predicateMessage));
            return;
        }

        var typeUtils = new TypeUtils(input.Scope);
        var compareOperator = JsUtils.ToUpper(node.FindDirectExpression<CompareOperator>()?.ConcatTokens());

        if (compareOperator == "CS" && sourceTypes.Count == 2 && sourceTypes.Any(type => typeUtils.IsCharLikeForCompare(type) == false))
        {
            var message = "CS operands must be character-like (data type C, N, D, T, or STRING)";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }
        else if ((node.FindDirectExpression<CompareOperator>()) != null && typeUtils.IsCompareable(sourceTypes.ElementAtOrDefault(0), sourceTypes.ElementAtOrDefault(1), sources.ElementAtOrDefault(0), sources.ElementAtOrDefault(1)) == false && sourceTypes.Count == 2)
        {
            string message = "Incompatible types for comparison";
            if ((sourceTypes.ElementAtOrDefault(0) is DateType && sourceTypes.ElementAtOrDefault(1) is IntegerType && sources.ElementAtOrDefault(1).FindFirstExpression<ArithOperator>() != null) || (sourceTypes.ElementAtOrDefault(1) is DateType && sourceTypes.ElementAtOrDefault(0) is IntegerType && sources.ElementAtOrDefault(0).FindFirstExpression<ArithOperator>() != null))
            {
                message = "Date cannot be compared with arithmetic result of type Integer";
            }
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }
    }

    // a built-in function with a character-like result cannot be the operand of IS [NOT] INITIAL,
    // "Unexpected operator IS" on a system, while one with a numeric result, lines( ), can
    private static string? CheckInitialOperand(ExpressionNode node, SyntaxInput input)
    {
        if (input.Scope.GetOpenABAP())
        {
            return null;
        }
        var tokens = node.GetChildren().Where(c => c is TokenNode).ToList().Select(c => JsUtils.ToUpper(c.GetFirstToken().GetStr())).ToList();
        if (tokens.Contains("IS") == false || tokens.ElementAtOrDefault(tokens.Count - 1) != "INITIAL")
        {
            return null;
        }
        var chain = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>()?.GetChildren();
        // xsdbool( ) and boolc( ) are their own form in the Source grammar, a condition in parentheses
        var first = JsUtils.ToUpper(chain?.ElementAtOrDefault(0)?.GetFirstToken().GetStr());
        if (chain?.Count == 4 && chain.ElementAtOrDefault(0) is TokenNode && (first == "XSDBOOL" || first == "BOOLC"))
        {
            return $"Built-in function \"{chain.ElementAtOrDefault(0).GetFirstToken().GetStr()}\" cannot be the operand of IS INITIAL";
        }
        var name = Compare.BuiltinCallName(node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>(), input);
        var returning = BuiltIn.SearchBuiltin(name)?.GetParameters().GetReturning()?.GetType();
        if (returning is StringType || returning is CharacterType)
        {
            return $"Built-in function \"{JsUtils.Str(name)}\" cannot be the operand of IS INITIAL";
        }
        return null;
    }

    // a predicate function, contains( ) or line_exists( ), is a relational expression and not a value,
    // it can stand alone as a condition or inside boolc( ) or xsdbool( ), but cannot be an operand of a comparison
    private static string? CheckPredicateOperand(List<ExpressionNode> sources, SyntaxInput input)
    {
        if (input.Scope.GetOpenABAP())
        {
            return null;
        }
        foreach (var source in sources)
        {
            var name = Compare.BuiltinCallName(source, input);
            if (BuiltIn.IsPredicate(name) == true)
            {
                return $"Predicate function \"{JsUtils.Str(name)}\" cannot be the operand of a comparison";
            }
        }
        return null;
    }

    // the name of the built-in function when the source is exactly one call of it, not hidden by a method of the same name
    private static string? BuiltinCallName(ExpressionNode? source, SyntaxInput input)
    {
        var chain = source?.GetChildren();
        if (chain?.Count != 1 || !((chain.ElementAtOrDefault(0) is ExpressionNode)) || !((chain.ElementAtOrDefault(0).Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.MethodCallChain)))
        {
            return null;
        }
        var calls = chain.ElementAtOrDefault(0).GetChildren();
        if (calls.Count != 1 || !((calls.ElementAtOrDefault(0) is ExpressionNode)) || !((calls.ElementAtOrDefault(0).Get() is MethodCall)))
        {
            return null;
        }
        var name = calls.ElementAtOrDefault(0).FindDirectExpression<MethodName>()?.ConcatTokens();
        if (name == null || BuiltIn.SearchBuiltin(name) == null)
        {
            return null;
        }
        // a method of the same name hides the built-in function
        var enclosing = input.Scope.FindClassDefinition(input.Scope.GetEnclosingClassName());
        if (enclosing != null && new ObjectOriented(input.Scope).SearchMethodName(((IInterfaceDefinition?)enclosing), name).method != null)
        {
            return null;
        }
        return name;
    }
}
