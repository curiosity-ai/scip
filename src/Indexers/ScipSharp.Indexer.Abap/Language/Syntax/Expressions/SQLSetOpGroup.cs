// Ported from abaplint packages/core/src/abap/5_syntax/expressions/sql_set_op_group.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SQLSetOpGroup
{

    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        if ((node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLFrom>()) != null)
        {
            Select.RunSyntax(node, input, true);
        }

        foreach (var group in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSetOpGroup>())
        {
            SQLSetOpGroup.RunSyntax(group, input);
        }

        foreach (var setop in node.FindDirectExpressions<SQLSetOp>())
        {
            SQLSetOpGroup.RunSyntax(setop, input);
        }
    }
}
