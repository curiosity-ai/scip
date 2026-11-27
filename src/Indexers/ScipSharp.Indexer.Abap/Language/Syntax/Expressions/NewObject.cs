// Ported from abaplint packages/core/src/abap/5_syntax/expressions/new_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Syntax.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class NewObject
{
    public static AbstractType RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? targetType)
    {
        AbstractType? ret = null;

        var typeExpr = node.FindDirectExpression<TypeNameOrInfer>();
        var typeToken = typeExpr?.GetFirstToken();
        if (typeToken == null)
        {
            throw new Exception("NewObject, child TypeNameOrInfer not found");
        }
        var typeName = typeExpr?.ConcatTokens();

        if (typeName == null)
        {
            throw new AssertError("NewObject, child TypeNameOrInfer not found");
        }
        else if (typeName == "#" && targetType != null && targetType is ObjectReferenceType)
        {
            var clas = input.Scope.FindClassDefinition(((ObjectReferenceType)targetType).GetIdentifierName());
            if (clas != null)
            {
                var tid = new TypedIdentifier(typeToken, input.Filename, ((ObjectReferenceType)targetType));
                input.Scope.AddReference(typeToken, tid, ReferenceType.InferredType, input.Filename);

                input.Scope.AddReference(typeToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)clas), ReferenceType.ConstructorReference, input.Filename, new IReferenceExtras { OoName = clas.GetName() });
            }
            else
            {
                var intf = input.Scope.FindInterfaceDefinition(((ObjectReferenceType)targetType).GetIdentifierName());
                if (intf != null)
                {
                    var message = intf.GetName() + " is an interface, cannot be instantiated";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            ret = ((ObjectReferenceType)targetType);

            if (clas?.IsAbstract() == true)
            {
                var message = clas.GetName() + " is abstract, cannot be instantiated";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            if (clas != null)
            {
                var err = CreateObject.CheckInstantiationAllowed(clas, input);
                if (!string.IsNullOrEmpty(err))
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), err));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
        }
        else if (typeName == "#" && targetType != null)
        {
            ret = targetType;
        }
        else if (typeName == "#")
        {
            throw new AssertError("NewObject, todo, infer type");
        }

        if (ret == null)
        {
            var objDefinition = input.Scope.FindObjectDefinition(typeName);
            if (objDefinition != null)
            {
                var objref = new ObjectReferenceType(((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)objDefinition));
                var tid = new TypedIdentifier(typeToken, input.Filename, objref);
                input.Scope.AddReference(typeToken, tid, ReferenceType.InferredType, input.Filename);

                input.Scope.AddReference(typeToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)objDefinition), ReferenceType.ObjectOrientedReference, input.Filename);
                input.Scope.AddReference(typeToken, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)objDefinition), ReferenceType.ConstructorReference, input.Filename, new IReferenceExtras { OoName = objDefinition.GetName() });
                var clas = input.Scope.FindClassDefinition(objref.GetIdentifierName());
                if (clas?.IsAbstract() == true)
                {
                    var message = clas.GetName() + " is abstract, cannot be instantiated";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                if (clas != null)
                {
                    var err = CreateObject.CheckInstantiationAllowed(clas, input);
                    if (!string.IsNullOrEmpty(err))
                    {
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), err));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
                ret = objref;
            }
        }

        if (ret == null)
        {
            var basic = new BasicTypes(input);
            var type = basic.ResolveTypeName(typeExpr);
            if (type is UnknownType)
            {
                ret = ((UnknownType)type);
            }
            else if (type != null && !((type is VoidType)))
            {
                ret = new DataReference(type);
            }
            else if (type is VoidType)
            {
                ret = ((VoidType)type);
            }
            else
            {
                var message = "Type \"" + typeName + "\" not found in scope, NewObject";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
        }

        if (ret is ObjectReferenceType)
        {
            NewObject.Parameters(node, ((ObjectReferenceType)ret), input);
        }
        else
        {
            foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
            {
                Source.RunSyntax(s, input, ret);
            }
        }

        if (ret is UnknownType && input.Scope.GetDDIC().InErrorNamespace(typeName) == true)
        {
            var message = "Class or type \"" + typeName + "\" not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        return ret;
    }

    private static void Parameters(ExpressionNode node, ObjectReferenceType obj, SyntaxInput input)
    {
        var name = obj.GetIdentifier().GetName();
        var def = input.Scope.FindObjectDefinition(name);
        var helper = new ObjectOriented(input.Scope);
        // eslint-disable-next-line prefer-const
        var __d0 = helper.SearchMethodName(def, "CONSTRUCTOR");
        var method = __d0.method;
        var requiredParameters = method?.GetParameters().GetRequiredParameters() ?? new List<TypedIdentifier>();

        var source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        var parameters = node.FindDirectExpression<ParameterListS>();
        if (source != null)
        {
            // single unnamed parameter
            var type = NewObject.DefaultImportingType(method);
            if (type == null)
            {
                var message = "NewObject, no default importing parameter found for constructor, " + name;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            var sourceType = Source.RunSyntax(source, input, type);
            if (new TypeUtils(input.Scope).IsAssignableStrict(sourceType, type) == false)
            {
                var message = "NEW parameter type not compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
        else if (parameters != null)
        {
            // parameters with names
            if (method == null)
            {
                var message = "NewObject, no parameters for constructor found, " + name;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            new MethodParameters().CheckExporting(parameters, input, method);
        }
        else if (requiredParameters.Count > 0)
        {
            var message = $"constructor parameter \"{requiredParameters.ElementAtOrDefault(0).GetName()}\" must be supplied, " + name;
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
    }

    private static AbstractType? DefaultImportingType(IMethodDefinition? method)
    {
        AbstractType? targetType = null;
        if (method == null)
        {
            return null;
        }
        var name = method.GetParameters().GetDefaultImporting();
        foreach (var i in method.GetParameters().GetImporting())
        {
            if (JsUtils.ToUpper(i.GetName()) == name)
            {
                targetType = i.GetType();
            }
        }
        return targetType;
    }
}
