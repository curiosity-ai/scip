// Ported from abaplint packages/core/src/abap/5_syntax/expressions/fstarget.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class FSTarget
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? type)
    {

        var inlinefs = node?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineFS>();
        if (inlinefs != null)
        {
            InlineFS.RunSyntax(inlinefs, input, type);
        }

        var target = node?.FindDirectExpression<TargetFieldSymbol>();
        if (target != null)
        {
            var token = target.GetFirstToken();
            var found = input.Scope.FindVariable(token.GetStr());
            if (found == null)
            {
                var message = $"\"{token.GetStr()}\" not found, FSTarget";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            input.Scope.AddReference(token, found, ReferenceType.DataWriteReference, input.Filename);
        }
    }
}
