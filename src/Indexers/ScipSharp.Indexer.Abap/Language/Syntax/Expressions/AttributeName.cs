// Ported from abaplint packages/core/src/abap/5_syntax/expressions/attribute_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class AttributeName
{
    public static AbstractType? RunSyntax(AbstractType? context, INode node, SyntaxInput input, object? type = null, bool error = true)
    {

        if (context is VoidType)
        {
            return ((VoidType)context);
        }

        var helper = new ObjectOriented(input.Scope);

        AbstractType? ret = null;

        if (context is ObjectReferenceType)
        {
            var def = input.Scope.FindObjectDefinition(((ObjectReferenceType)context).GetIdentifierName());
            if (def == null)
            {
                var message = "Definition for \"" + ((ObjectReferenceType)context).GetIdentifierName() + "\" not found in scope(AttributeName)";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            var token = node.GetFirstToken();
            var name = token.GetStr();
            TypedIdentifier? found = helper.SearchAttributeName(def, name);
            if (found == null)
            {
                found = helper.SearchConstantName(def, name);
            }
            if (found == null)
            {
                var message = "Attribute or constant \"" + name + "\" not found in \"" + def.GetName() + "\"";
                if (error == true)
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                }
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            if (found is ClassAttribute || found is ClassConstant)
            {
                var owner = helper.FindAttributeOwner(def, found);
                var notVisible = helper.MemberNotVisible(found is ClassAttribute ? ((ClassAttribute)found).GetVisibility() : ((ClassConstant)found).GetVisibility(), def, ((IInterfaceDefinition?)owner), input.Scope.GetEnclosingClassName());
                if (notVisible != null)
                {
                    var message = $"Attribute \"{name}\" is {notVisible} and cannot be accessed";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            if (type != null)
            {
                input.Scope.AddReference(token, found, type, input.Filename);
            }
            if (found != null && name.Contains("~", StringComparison.Ordinal))
            {
                var idef = input.Scope.FindInterfaceDefinition(name.Split("~").ToList().ElementAtOrDefault(0));
                if (idef != null)
                {
                    input.Scope.AddReference(token, ((global::ScipSharp.Indexer.Abap.FileInformation.Identifier?)idef), ReferenceType.ObjectOrientedReference, input.Filename);
                }
            }
            ret = found.GetType();
        }
        else if (context is DataReference)
        {
            var sub = ((DataReference)context).GetType();
            var name = node.GetFirstToken().GetStr();
            if (name == "*" || sub is VoidType || sub is AnyType)
            {
                return sub;
            }
            if (!((sub is StructureType)))
            {
                var message = "Data reference not structured";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            ret = ((StructureType)sub).GetComponentByName(name);
            if (ret == null)
            {
                var message = "Component \"" + name + "\" not found in data reference structure";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
        }
        else
        {
            var message = "Not an object reference, attribute name";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        return ret;
    }
}
