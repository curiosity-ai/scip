// Ported from abaplint packages/core/src/abap/5_syntax/expressions/message_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class MessageSource
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        foreach (var f in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            Source.RunSyntax(f, input);
        }
        foreach (var f in node.FindDirectExpressions<SimpleSource3>())
        {
            Source.RunSyntax(f, input);
        }

        if (JsUtils.ToUpper(node.GetFirstToken().GetStr()) == "ID")
        {
            var id = node.FindExpressionAfterToken("ID")?.ConcatTokens();
            string? number = node.FindDirectExpression<MessageNumber>()?.ConcatTokens();
            if (number == null)
            {
                var num = node.FindExpressionAfterToken("NUMBER")?.ConcatTokens();
                if ((num?.StartsWith("'", StringComparison.Ordinal)) == true)
                {
                    number = JsUtils.ToUpper(JsUtils.Substring(num, 1, num.Length - 1));
                }
            }
            if ((id?.StartsWith("'", StringComparison.Ordinal)) == true && !string.IsNullOrEmpty(number))
            {
                var messageClass = JsUtils.ToUpper(JsUtils.Substring(id, 1, id.Length - 1));
                input.Scope.GetMSAGReferences().AddUsing(input.Filename, node.GetFirstToken(), messageClass, number);
            }
        }
        else
        {
            var typeAndNumber = node.FindDirectExpression<MessageTypeAndNumber>()?.ConcatTokens();
            var messageNumber = JsUtils.Substring(typeAndNumber, 1);
            var messageClass = JsUtils.ToUpper(node.FindDirectExpression<MessageClass>()?.ConcatTokens());
            if (!string.IsNullOrEmpty(messageNumber) && !string.IsNullOrEmpty(messageClass))
            {
                input.Scope.GetMSAGReferences().AddUsing(input.Filename, node.GetFirstToken(), messageClass, messageNumber);
            }
        }
    }
}
