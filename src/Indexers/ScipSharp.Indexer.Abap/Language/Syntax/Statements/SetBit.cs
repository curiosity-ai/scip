// Ported from abaplint packages/core/src/abap/5_syntax/statements/set_bit.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class SetBit : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            var typ = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
            if (typ != null && new TypeUtils(input.Scope).IsHexLike(typ) == false)
            {
                var message = "Input must be byte-like";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, t.GetFirstToken(), message));
                return;
            }
        }
    }
}
