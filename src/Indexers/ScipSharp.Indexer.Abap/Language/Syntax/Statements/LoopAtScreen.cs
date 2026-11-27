// Ported from abaplint packages/core/src/abap/5_syntax/statements/loop_at_screen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class LoopAtScreen : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (target != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
        }

        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, VoidType.Get("SCREEN"));
        }
    }
}
