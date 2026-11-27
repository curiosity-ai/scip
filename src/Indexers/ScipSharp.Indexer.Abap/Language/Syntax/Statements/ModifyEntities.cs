// Ported from abaplint packages/core/src/abap/5_syntax/statements/modify_entities.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class ModifyEntities : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            var inline = t?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, VoidType.Get("ModifyEntities"));
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
            }
        }
    }
}
