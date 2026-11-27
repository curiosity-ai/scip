// Ported from abaplint packages/core/src/abap/5_syntax/statements/format.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Format : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindAllExpressionsMulti([typeof(global::ScipSharp.Indexer.Abap.Statements.Expressions.Source), typeof(SimpleSource3)]))
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
    }
}
