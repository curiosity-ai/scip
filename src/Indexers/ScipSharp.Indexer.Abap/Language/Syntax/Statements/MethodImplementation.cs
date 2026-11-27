// Ported from abaplint packages/core/src/abap/5_syntax/statements/method_implementation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class MethodImplementation : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var helper = new ObjectOriented(input.Scope);

        var className = input.Scope.GetName();
        var methodToken = node.FindFirstExpression<MethodName>()!.GetFirstToken();
        var methodName = methodToken?.GetStr();

        var classDefinition = input.Scope.FindClassDefinition(className);
        if (classDefinition == null)
        {
            var message = "Class definition for \"" + className + "\" not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var __d0 = helper.SearchMethodName(((IInterfaceDefinition?)classDefinition), methodName);
        var methodDefinition = __d0.method;

        if (classDefinition.IsForTesting() && methodDefinition?.GetVisibility() != Visibility.Private && new List<string> { "SETUP", "TEARDOWN", "CLASS_SETUP", "CLASS_TEARDOWN" }.Contains(JsUtils.ToUpper(methodName)))
        {
            var message = "Special test method \"" + methodName + "\" must be private";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var start = node.GetFirstToken().GetStart();
        if (methodDefinition?.IsStatic() == false || methodDefinition == null)
        {
            input.Scope.Push(ScopeType.MethodInstance, methodName, start, input.Filename);
            input.Scope.AddList(classDefinition.GetAttributes().GetInstance().Cast<TypedIdentifier>().ToList());
        }

        input.Scope.Push(ScopeType.Method, methodName, start, input.Filename);

        // note: the scope must be pushed before returning
        if (methodDefinition == null)
        {
            var message = "Method definition \"" + methodName + "\" not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        input.Scope.AddReference(methodToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)methodDefinition), ReferenceType.MethodImplementationReference, input.Filename);
        input.Scope.AddList(methodDefinition.GetParameters().GetAll());

        foreach (var i in helper.FindInterfaces(((IInterfaceDefinition?)classDefinition)))
        {
            if (JsUtils.ToUpper(methodName).StartsWith(JsUtils.ToUpper(i.Name) + "~", StringComparison.Ordinal) == false)
            {
                continue;
            }
            var idef = input.Scope.FindInterfaceDefinition(i.Name);
            if (idef == null)
            {
                continue;
            }
            input.Scope.AddReference(methodToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename);
        }
    }
}
