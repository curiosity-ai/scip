// Ported from abaplint packages/core/src/abap/5_syntax/statements/with.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class With : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Select>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Select.RunSyntax(s, input);
        }
    }
}
