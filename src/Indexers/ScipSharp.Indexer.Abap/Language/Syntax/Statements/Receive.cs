// Ported from abaplint packages/core/src/abap/5_syntax/statements/receive.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Receive : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        // todo, lots of work here, similar to call_function.ts
        // just recurse
        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var t in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
        }
    }
}
