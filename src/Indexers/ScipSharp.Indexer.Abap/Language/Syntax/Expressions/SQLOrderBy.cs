// Ported from abaplint packages/core/src/abap/5_syntax/expressions/sql_order_by.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SQLOrderBy
{

    public static void RunSyntax(INode node, SyntaxInput input)
    {
        var dyn = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>();
        if (dyn != null)
        {
            Dynamic.RunSyntax(dyn, input);
        }
    }
}
