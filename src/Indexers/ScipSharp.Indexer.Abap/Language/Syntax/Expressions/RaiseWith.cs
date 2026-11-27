// Ported from abaplint packages/core/src/abap/5_syntax/expressions/raise_with.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class RaiseWith
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        foreach (var f in node.FindAllExpressionsMulti([typeof(SimpleSource1), typeof(global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)]))
        {
            Source.RunSyntax(f, input);
        }
    }
}
