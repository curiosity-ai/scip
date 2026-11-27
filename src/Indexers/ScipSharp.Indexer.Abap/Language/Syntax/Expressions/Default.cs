// Ported from abaplint packages/core/src/abap/5_syntax/expressions/default.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Default
{
    public static AbstractType? RunSyntax(ExpressionNode node, SyntaxInput input)
    {

        var chain = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
        if (chain != null)
        {
            return FieldChain.RunSyntax(chain, input, ReferenceType.DataReadReference);
        }

        var constant = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Constant>();
        if (constant != null)
        {
            return Constant.RunSyntax(constant);
        }

        return null;
    }
}
