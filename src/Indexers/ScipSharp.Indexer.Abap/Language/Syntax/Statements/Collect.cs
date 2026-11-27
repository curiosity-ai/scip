// Ported from abaplint packages/core/src/abap/5_syntax/statements/collect.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Collect : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (source != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(source, input);
        }

        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (target != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
        }

        var fs = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget>();
        if (fs != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(fs, input, null);
        }
    }
}
