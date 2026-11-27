// Ported from abaplint packages/core/src/abap/5_syntax/statements/field_group.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class FieldGroup : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var nameExpression = node.FindFirstExpression<Field>();
        if (nameExpression == null)
        {
            return;
        }
        var name = nameExpression?.ConcatTokens();
        if (name == null)
        {
            return;
        }
        var id = new TypedIdentifier(nameExpression?.GetFirstToken(), input.Filename, VoidType.Get("FIELD-GROUP"));
        input.Scope.AddIdentifier(id);
    }
}
