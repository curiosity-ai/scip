// Ported from abaplint packages/core/src/abap/5_syntax/statements/assert.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Assert : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Cond>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Cond.RunSyntax(s, input);
        }
    }
}
