// Ported from abaplint packages/core/src/abap/5_syntax/statements/at_selection_screen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class AtSelectionScreen : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        // "AT SELECTION-SCREEN ON <field>", "ON VALUE-REQUEST FOR <field>",
        // "ON HELP-REQUEST FOR <field>" and "ON END OF <field>" all reference
        // a parameter or select-option, register these as read references so
        // they are not reported as unused.
        List<ExpressionNode> fields = new List<ExpressionNode>([.. node.FindDirectExpressions<FieldSub>(), .. node.FindDirectExpressions<Field>()]);

        foreach (var field in fields)
        {
            var token = field.GetFirstToken();
            if (input.Scope.FindVariable(token.GetStr()) != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.SourceField.RunSyntax(field, input, ReferenceType.DataReadReference, false);
            }
        }
    }
}
