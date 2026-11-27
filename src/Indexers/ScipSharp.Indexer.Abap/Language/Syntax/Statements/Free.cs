// Ported from abaplint packages/core/src/abap/5_syntax/statements/free.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Free : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
        }
    }
}
