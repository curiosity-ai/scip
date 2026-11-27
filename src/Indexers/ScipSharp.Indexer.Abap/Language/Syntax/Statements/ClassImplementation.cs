// Ported from abaplint packages/core/src/abap/5_syntax/statements/class_implementation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class ClassImplementation : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var helper = new ObjectOriented(input.Scope);

        var className = helper.FindClassName(node);

        input.Scope.Push(ScopeType.ClassImplementation, className, node.GetFirstToken().GetStart(), input.Filename);

        var classDefinition = input.Scope.FindClassDefinition(className);
        if (classDefinition == null)
        {
            var message = "Class definition for \"" + className + "\" not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var types = classDefinition.GetTypeDefinitions();
        if (types != null)
        {
            foreach (var t in types.GetAll())
            {
                input.Scope.AddType(t.Type);
            }
        }

        var sup = input.Scope.FindClassDefinition(classDefinition.GetSuperClass());
        if (sup != null)
        {
            input.Scope.AddIdentifier(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), "super"), BuiltIn.filename, new ObjectReferenceType(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)sup))));
        }
        else
        {
            // todo: instead of the void type, do proper typing, ie. only empty constructor method
            input.Scope.AddIdentifier(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), "super"), BuiltIn.filename, VoidType.Get("noSuperClass")));
        }
        input.Scope.AddIdentifier(new TypedIdentifier(new global::ScipSharp.Indexer.Abap.Lexer.Tokens.Identifier(new Position(1, 1), "me"), BuiltIn.filename, new ObjectReferenceType(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)classDefinition))));
        helper.AddAliasedAttributes(classDefinition);
        helper.AddAliasedTypes(classDefinition);

        var classAttributes = classDefinition.GetAttributes();
        if (classAttributes != null)
        {
            input.Scope.AddList(classAttributes.GetConstants().Cast<TypedIdentifier>().ToList());
            input.Scope.AddList(classAttributes.GetStatic().Cast<TypedIdentifier>().ToList());
            foreach (var i in classAttributes.GetInstance())
            {
                input.Scope.AddExtraLikeType(i);
            }
        }

        helper.FromSuperClassesAndInterfaces(classDefinition);
    }
}
