// Ported from abaplint packages/core/src/abap/5_syntax/expressions/constant.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Constant
{
    public static AbstractType RunSyntax(ExpressionNode node)
    {
        // todo: ConcatenatedConstant is not used?
        if ((node.FindDirectExpression<Integer>()) != null)
        {
            return IntegerType.Get(new AbstractTypeData { DerivedFromConstant = true });
        }

        var first = node.GetFirstToken().GetStr();
        if (first.StartsWith("'", StringComparison.Ordinal))
        {
            int len = first.Length - 2;
            if (len <= 0)
            {
                len = 1;
            }
            return new CharacterType(len, new AbstractTypeData { DerivedFromConstant = true });
        }
        else if (first.StartsWith("`", StringComparison.Ordinal))
        {
            return StringType.Get(new AbstractTypeData { DerivedFromConstant = true });
        }
        else
        {
            return StringType.Get();
        }
    }
}
