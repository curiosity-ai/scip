// Ported from abaplint packages/core/src/abap/5_syntax/statements/select.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Select : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        // for UNION statements there are multiple select parts
        var selects = node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Select>();
        for (var i = 0; i < selects.Count; i++)
        {
            var last = i == selects.Count - 1;
            var s = selects[i];
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Select.RunSyntax(s, input, last == false);
        }
    }
}
