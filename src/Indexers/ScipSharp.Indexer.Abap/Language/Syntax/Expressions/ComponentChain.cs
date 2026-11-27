// Ported from abaplint packages/core/src/abap/5_syntax/expressions/component_chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ComponentChain
{
    public static AbstractType? RunSyntax(AbstractType? context, ExpressionNode node, SyntaxInput input)
    {

        if (context == null)
        {
            return null;
        }

        var children = node.GetChildren();
        for (var i = 0; i < children.Count; i++)
        {
            if (context is VoidType || context is UnknownType)
            {
                return context;
            }

            var child = children[i];
            if (i == 0 && JsUtils.ToUpper(child.ConcatTokens()) == "TABLE_LINE")
            {
                continue;
            }
            else if (child.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dereference)
            {
                if (!((context is DataReference)))
                {
                    var message = "Not a data reference, cannot be dereferenced";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                context = ((DataReference)context).GetType();
                continue;
            }
            else if (child.Get() is ArrowOrDash)
            {
                var concat = child.ConcatTokens();
                if (concat == "-")
                {
                    if (!((context is StructureType)))
                    {
                        var message = "ComponentChain, not a structure";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
                else if (concat == "=>")
                {
                    if (!((context is ObjectReferenceType)))
                    {
                        var message = "ComponentChain, not a reference";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
                else if (concat == "->")
                {
                    if (!((context is ObjectReferenceType)) && !((context is DataReference)))
                    {
                        var message = "ComponentChain, not a reference";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
            }
            else if (child.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName)
            {
                var name = child.ConcatTokens();

                if (context is DataReference)
                {
                    context = ((DataReference)context).GetType();
                    if (name == "*")
                    {
                        continue;
                    }
                }

                if (context is StructureType)
                {
                    context = ((StructureType)context).GetComponentByName(name);
                    if (context == null)
                    {
                        var message = "Component \"" + name + "\" not found in structure";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                }
                else if (context is ObjectReferenceType)
                {
                    var id = ((ObjectReferenceType)context).GetIdentifier();
                    var def = input.Scope.FindObjectDefinition(id.GetName());
                    if (def == null)
                    {
                        var message = id.GetName() + " not found in scope";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }

                    var helper = new ObjectOriented(input.Scope);
                    var found = helper.SearchAttributeName(def, name);

                    context = found?.GetType();
                    if (context == null)
                    {
                        var message = "Attribute \"" + name + "\" not found";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                    }
                    else
                    {
                        IReferenceExtras extra = new IReferenceExtras { OoName = id.GetName(), OoType = id is ClassDefinition ? "CLAS" : "INTF" };
                        input.Scope.AddReference(child.GetFirstToken(), found, ReferenceType.DataWriteReference, input.Filename, extra);
                    }
                }
                else
                {
                    var message = "ComponentChain, not a structure, " + context?.GetType().Name;
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), message));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
        }

        return context;
    }
}
