// Ported from abaplint packages/core/src/abap/5_syntax/statements/when_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class WhenType : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var nameToken = node.FindFirstExpression<ClassName>()?.GetFirstToken();
        if (nameToken == null)
        {
            return;
        }

        AbstractType? type = null;
        var className = JsUtils.ToUpper(nameToken.GetStr());
        var found = input.Scope.ExistsObject(className);
        if ((found?.id) != null)
        {
            type = new ObjectReferenceType(found.Value.id);
            input.Scope.AddReference(nameToken, found.Value.id, ReferenceType.ObjectOrientedReference, input.Filename);
        }
        else if (input.Scope.GetDDIC().InErrorNamespace(className) == false)
        {
            type = VoidType.Get(className);
            input.Scope.AddReference(nameToken, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, new IReferenceExtras { OoName = className, OoType = "Void" });
        }
        else
        {
            var message = "Class " + className + " not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameToken, message));
            return;
        }

        var target = node?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, type);
        }
        else if (target != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
        }
    }
}
