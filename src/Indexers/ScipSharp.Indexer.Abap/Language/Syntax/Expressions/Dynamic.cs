// Ported from abaplint packages/core/src/abap/5_syntax/expressions/dynamic.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Dynamic
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        var chain = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
        if (chain != null)
        {
            FieldChain.RunSyntax(chain, input, ReferenceType.DataReadReference);
        }
    }
}
