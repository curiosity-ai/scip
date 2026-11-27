// Ported from abaplint packages/core/src/abap/5_syntax/statements/get_bit.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class GetBit : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        AbstractType? lastType = null;
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            lastType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        if (lastType != null && new TypeUtils(input.Scope).IsHexLike(lastType) == false)
        {
            var message = "Input must be byte-like";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            var inline = t?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(t, input, IntegerType.Get());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
            }
        }
    }
}
