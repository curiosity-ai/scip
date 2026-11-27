// Ported from abaplint packages/core/src/abap/5_syntax/statements/class_local_friends.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class ClassLocalFriends : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var classNames = node.FindAllExpressions<ClassName>();

        var found = classNames.ElementAtOrDefault(0);
        if (found != null)
        {
            var token = found.GetFirstToken();
            var name = token.GetStr();

            if (input.Scope.GetParentObj().GetType() == "CLAS" && JsUtils.ToUpper(name) != JsUtils.ToUpper(input.Scope.GetParentObj().GetName()))
            {
                var message = "Befriending must be " + JsUtils.ToUpper(input.Scope.GetParentObj().GetName());
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }

            var def = input.Scope.FindClassDefinition(name);
            if (def != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)def), ReferenceType.ObjectOrientedReference, input.Filename);
            }
            else
            {
                var message = $"Class {JsUtils.ToUpper(name)} not found";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        for (var i = 1; i < classNames.Count; i++)
        {
            var className = classNames[i].ConcatTokens();
            // make sure to check also DEFINITION DEFERRED
            var found2 = input.Scope.ExistsObject(className);
            if (found2 == null)
            {
                var message = $"Class {JsUtils.ToUpper(className)} not found";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else
            {
                var befriendedName = classNames.ElementAtOrDefault(0)?.GetFirstToken().GetStr();
                if (!string.IsNullOrEmpty(befriendedName))
                {
                    input.Scope.AddLocalFriend(befriendedName, className);
                }
            }
        }
    }
}
