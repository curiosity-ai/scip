// Ported from abaplint packages/core/src/abap/5_syntax/expressions/string_template.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class StringTemplate
{
    public static AbstractType RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        var typeUtils = new TypeUtils(input.Scope);
        var ret = StringType.Get();

        foreach (var child in node.GetChildren())
        {
            if (child is ExpressionNode && (((ExpressionNode)child).Get()) is StringTemplateSource)
            {
                var s = ((ExpressionNode)child).FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
                var type = Source.RunSyntax(s, input, ret);
                if (type == null)
                {
                    var message = "No target type determined";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if ((typeUtils.IsCharLike(type) == false && typeUtils.IsHexLike(type) == false && !((type is UTCLongType))) || type is StructureType)
                {
                    var message = "String template, not character like, " + type.GetType().Name;
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }

                var format = ((ExpressionNode)child).FindDirectExpression<StringTemplateFormatting>();
                var formatConcat = format?.ConcatTokens();
                foreach (var formatSource in format?.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>() ?? new List<ExpressionNode>())
                {
                    Source.RunSyntax(formatSource, input);
                }

                if (format != null && (formatConcat?.Contains("ALPHA = ", StringComparison.Ordinal)) == true && !((type is UnknownType)) && !((type is VoidType)) && !((type is StringType)) && !((type is CLikeType)) && !((type is CharacterType)) && !((type is NumericGenericType)) && !((type is NumericType)) && !((type is AnyType)))
                {
                    var message = $"Cannot apply ALPHA to this type ({type.GetType().Name})";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, format.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            else if (child is TokenNode)
            {
                var token = ((TokenNode)child).Get();
                if (token is global::ScipSharp.Indexer.Abap.Lexer.Tokens.StringTemplate || token is StringTemplateBegin || token is StringTemplateMiddle || token is StringTemplateEnd)
                {
                    var issue = StringTemplate.ValidateEscapeSequences(token.GetStr(), input, ((TokenNode)child));
                    if (issue != null)
                    {
                        input.Issues.Add(issue);
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
            }
        }

        return ret;
    }

    private static Issue? ValidateEscapeSequences(string str, SyntaxInput input, TokenNode node)
    {
        // Valid escape sequences in ABAP string templates: \|, \{, \}, \\, \n, \r, \t
        var validEscapes = new HashSet<string>(new List<string> { "\\|", "\\{", "\\}", "\\\\", "\\n", "\\r", "\\t" });

        for (var i = 0; i < str.Length; i++)
        {
            if (str[i].ToString() == "\\")
            {
                var escape = JsUtils.Substring(str, i, i + 2);
                if (!validEscapes.Contains(escape))
                {
                    return SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), $"Invalid escape sequence \"{escape}\" in string template");
                }
                i++;
                // skip the next character as it's part of the escape sequence
            }
        }
        return null;
    }
}
