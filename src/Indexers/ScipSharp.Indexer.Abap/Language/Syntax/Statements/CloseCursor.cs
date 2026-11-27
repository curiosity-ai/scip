// Ported from abaplint packages/core/src/abap/5_syntax/statements/close_cursor.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class CloseCursor : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var t in node.FindAllExpressions<SQLSourceSimple>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.SQLSource.RunSyntax(t, input);
        }
    }
}
