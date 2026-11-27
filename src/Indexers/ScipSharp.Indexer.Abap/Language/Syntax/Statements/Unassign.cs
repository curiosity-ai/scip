// Ported from abaplint packages/core/src/abap/5_syntax/statements/unassign.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Unassign : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var target = node?.FindDirectExpression<TargetFieldSymbol>();
        if (target != null)
        {
            var token = target.GetFirstToken();
            var found = input.Scope.FindVariable(token.GetStr());
            if (found == null)
            {
                var message = $"\"{token.GetStr()}\" not found, Unassign";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            input.Scope.AddReference(token, found, ReferenceType.DataWriteReference, input.Filename);
        }
    }
}
