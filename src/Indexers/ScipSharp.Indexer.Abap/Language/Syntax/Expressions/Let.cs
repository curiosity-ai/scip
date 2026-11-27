// Ported from abaplint packages/core/src/abap/5_syntax/expressions/let.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Let
{
    public static bool RunSyntax(ExpressionNode? node, SyntaxInput input, bool skipScope = false)
    {
        if (node == null)
        {
            return false;
        }

        if (skipScope != true)
        {
            input.Scope.Push(ScopeType.Let, "LET", node.GetFirstToken().GetStart(), input.Filename);
        }

        foreach (var f in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineFieldDefinition>())
        {
            InlineFieldDefinition.RunSyntax(f, input);
        }

        return true;
    }
}
