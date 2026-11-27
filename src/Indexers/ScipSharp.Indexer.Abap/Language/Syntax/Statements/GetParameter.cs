// Ported from abaplint packages/core/src/abap/5_syntax/statements/get_parameter.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class GetParameter : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();

        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, new CharacterType(40));
        }
        else if (target != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
    }
}
