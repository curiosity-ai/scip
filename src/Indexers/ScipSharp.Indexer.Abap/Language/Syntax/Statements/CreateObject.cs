// Ported from abaplint packages/core/src/abap/5_syntax/statements/create_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class CreateObject : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        IClassDefinition? cdef = null;

        // CREATE OBJECT, TYPE
        var type = node.FindExpressionAfterToken("TYPE");
        if (type != null && type.Get() is ClassName)
        {
            var token = type.GetFirstToken();
            var name = token.GetStr();
            cdef = input.Scope.FindClassDefinition(name);
            if (cdef != null)
            {
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)cdef), ReferenceType.ObjectOrientedReference, input.Filename);
                input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)cdef), ReferenceType.ConstructorReference, input.Filename, new IReferenceExtras { OoName = cdef.GetName() });
                if (cdef.IsAbstract() == true)
                {
                    var message = cdef.GetName() + " is abstract, cannot be instantiated";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
            else if (input.Scope.GetDDIC().InErrorNamespace(name) == false)
            {
                input.Scope.AddReference(token, null, ReferenceType.ObjectOrientedVoidReference, input.Filename, new IReferenceExtras { OoName = name, OoType = "CLAS" });
            }
            else
            {
                var message = "TYPE \"" + name + "\" not found";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        // just recurse
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var t2 in node.FindDirectExpression<ParameterListExceptions>()?.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>() ?? new List<ExpressionNode>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t2, input);
        }

        var t = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        object? found = null;
        if (t != null)
        {
            found = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
            if (found is VoidType)
            {
                // do nothing
            }
            else if (found is UnknownType)
            {
                var message = "Target type unknown, " + t.ConcatTokens();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else if (!((found is ObjectReferenceType)) && !((found is AnyType)) && !((found is DataType)) && !((found is GenericObjectReferenceType)))
            {
                var message = "Target must be an object reference, " + t.ConcatTokens();
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else if (found is GenericObjectReferenceType && type == null)
            {
                var message = "Generic type, cannot be instantiated";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else if (found is ObjectReferenceType)
            {
                var identifier = ((ObjectReferenceType)found).GetIdentifier();
                var idFound = input.Scope.FindObjectDefinition(identifier.GetName());
                if (idFound is InterfaceDefinition && type == null)
                {
                    var message = "Interface reference, cannot be instantiated";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                else if (found is ObjectReferenceType && type == null && input.Scope.FindInterfaceDefinition(((ObjectReferenceType)found).GetQualifiedName()) != null)
                {
                    var message = "Interface reference, cannot be instantiated";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                else if (idFound is ClassDefinition && cdef == null)
                {
                    cdef = ((ClassDefinition)idFound);
                }
                if (type == null && idFound is ClassDefinition && ((ClassDefinition)idFound).IsAbstract() == true)
                {
                    var message = identifier.GetName() + " is abstract, cannot be instantiated";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }

            if (found is ObjectReferenceType && cdef == null)
            {
                cdef = input.Scope.FindClassDefinition(((ObjectReferenceType)found).GetQualifiedName());
            }
        }

        foreach (var t2 in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(t2, input);
        }

        string? ooName = cdef?.GetName();
        if (ooName == null && found is VoidType)
        {
            ooName = ((VoidType)found).GetVoided();
        }

        input.Scope.AddReference(t?.GetFirstToken(), ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)cdef), ReferenceType.ConstructorReference, input.Filename, new IReferenceExtras { OoName = ooName });

        if (type != null && type.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
        {
            // the class is only known at runtime, cdef is the static type of the target
            this.ValidateParameters(null, node, input);
            return;
        }

        if (cdef != null)
        {
            var err = CreateObject.CheckInstantiationAllowed(cdef, input);
            if (!string.IsNullOrEmpty(err))
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), err));
                return;
            }
        }

        this.ValidateParameters(cdef, node, input);
    }

    public static string? CheckInstantiationAllowed(IClassDefinition cdef, SyntaxInput input)
    {
        var createVis = cdef.GetCreateVisibility();
        if (createVis == Visibility.Public)
        {
            return null;
        }
        var enclosingClass = input.Scope.GetEnclosingClassName();
        if (enclosingClass == null)
        {
            return cdef.GetName() + " cannot be instantiated, class is defined as " + (createVis == Visibility.Private ? "private" : "protected");
        }
        if (JsUtils.ToUpper(enclosingClass) == JsUtils.ToUpper(cdef.GetName()))
        {
            return null;
        }
        if (new ObjectOriented(input.Scope).HasFriendship(cdef, enclosingClass))
        {
            return null;
        }
        if (createVis == Visibility.Protected)
        {
            // subclasses are also allowed
            string? sup = input.Scope.FindClassDefinition(enclosingClass)?.GetSuperClass();
            while (sup != null)
            {
                if (JsUtils.ToUpper(sup) == JsUtils.ToUpper(cdef.GetName()))
                {
                    return null;
                }
                sup = input.Scope.FindClassDefinition(sup)?.GetSuperClass();
            }
        }
        return cdef.GetName() + " cannot be instantiated, class is defined as " + (createVis == Visibility.Private ? "private" : "protected");
    }

    private void ValidateParameters(IClassDefinition? cdef, StatementNode node, SyntaxInput input)
    {
        if (cdef == null)
        {
            var sources = node.FindDirectExpression<ParameterListS>()?.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            foreach (var s in sources ?? new List<ExpressionNode>())
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            }
            return;
        }

        var methodDef = new ObjectOriented(input.Scope).SearchMethodName(((IInterfaceDefinition?)cdef), "CONSTRUCTOR");
        var methodParameters = methodDef.method?.GetParameters();

        var allImporting = methodParameters?.GetImporting() ?? new List<TypedIdentifier>();
        var requiredImporting = new HashSet<string>(methodParameters?.GetRequiredParameters().Select(i => JsUtils.ToUpper(i.GetName())).ToList() ?? new List<string>());

        foreach (var p in node.FindDirectExpression<ParameterListS>()?.FindAllExpressions<ParameterS>() ?? new List<ExpressionNode>())
        {
            var name = JsUtils.ToUpper(p.FindDirectExpression<ParameterName>()?.ConcatTokens());
            if (name == null)
            {
                continue;
            }

            var found = allImporting?.FirstOrDefault(p2 => JsUtils.ToUpper(p2.GetName()) == name);
            var source = p.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            var sourceType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(source, input, found?.GetType());

            if (found == null)
            {
                var message = $"constructor parameter \"{name}\" does not exist";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            else if (new TypeUtils(input.Scope).IsAssignableStrict(sourceType, found.GetType(), source) == false)
            {
                var message = $"constructor parameter \"{name}\" type not compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }

            requiredImporting.Remove(name);
        }

        foreach (var r in requiredImporting)
        {
            var message = $"constructor parameter \"{r}\" must be supplied";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
    }
}
