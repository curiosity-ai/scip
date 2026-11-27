// Ported from abaplint packages/core/src/abap/5_syntax/statements/get_reference.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class GetReference : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var s = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);

        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        // todo: error if inline field symbol
        if (inline != null)
        {
            if (type is AnyType)
            {
                var message = "GET REFERENCE generic and inline declaration not possible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, type != null ? new DataReference(type) : (AbstractType?)null);
        }
        else if (target != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
        }
    }
}
