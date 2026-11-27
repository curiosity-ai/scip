// Ported from abaplint packages/core/src/abap/5_syntax/statements/form.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Form : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var nameExpression = node.FindDirectExpression<FormName>();
        var name = nameExpression?.ConcatTokens();
        if (nameExpression == null || name == null)
        {
            var message = "Form, could not find name";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
        if (name.Length > 30)
        {
            var message = "FORM name longer than 30 characters";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameExpression.GetFirstToken(), message));
        }
        input.Scope.Push(ScopeType.Form, name, node.GetFirstToken().GetStart(), input.Filename);

        var form = new FormDefinition(node, input);
        // the definitions are built up front, before the program level types are known, replace it
        // with this one, which is resolved in the scope of the FORM
        input.Scope.UpdateFormDefinition(form);
        input.Scope.AddList(form.GetUsingParameters());
        input.Scope.AddList(form.GetChangingParameters());
        input.Scope.AddList(form.GetTablesParameters());
    }
}
