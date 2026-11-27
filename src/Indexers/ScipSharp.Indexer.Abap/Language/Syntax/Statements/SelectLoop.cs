// Ported from abaplint packages/core/src/abap/5_syntax/statements/select_loop.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class SelectLoop : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var s = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Select>();
        if (s != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Select.RunSyntax(s, input, false, true);
        }
    }
}
