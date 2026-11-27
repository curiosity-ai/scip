// Ported from abaplint packages/core/src/abap/5_syntax/expressions/field_length.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class FieldLength
{
    public static int? RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        var field = node.FindDirectExpression<SimpleFieldChain2>();
        if (field != null)
        {
            FieldChain.RunSyntax(field, input, ReferenceType.DataReadReference);
            return null;
        }
        else
        {
            var children = node.GetChildren();
            var num = children.ElementAtOrDefault(children.Count - 2);
            if (num.GetLastToken().GetStr() == "*")
            {
                return null;
            }
            return JsUtils.ParseInt(num.GetLastToken().GetStr(), 10);
        }
    }
}
