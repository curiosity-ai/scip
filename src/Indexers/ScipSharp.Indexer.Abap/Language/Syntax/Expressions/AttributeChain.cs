// Ported from abaplint packages/core/src/abap/5_syntax/expressions/attribute_chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class AttributeChain
{
    public static AbstractType? RunSyntax(AbstractType? inputContext, INode node, SyntaxInput input, List<string> type)
    {

        if (inputContext is VoidType)
        {
            return ((VoidType)inputContext);
        }
        else if (!((inputContext is ObjectReferenceType)))
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), "Not an object reference(AttributeChain)"));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        var first = node.GetChildren().ElementAtOrDefault(0);
        if (!((first.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.AttributeName)))
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), "AttributeChain, unexpected first child"));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        var def = input.Scope.FindObjectDefinition(((ObjectReferenceType)inputContext).GetIdentifierName());
        if (def == null)
        {
            var message = "Definition for \"" + ((ObjectReferenceType)inputContext).GetIdentifierName() + "\" not found in scope(AttributeChain)";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }
        var nameToken = first.GetFirstToken();
        var name = nameToken.GetStr();
        var helper = new ObjectOriented(input.Scope);

        TypedIdentifier? context = helper.SearchAttributeName(def, name);
        if (context == null)
        {
            context = helper.SearchConstantName(def, name);
        }
        if (context == null)
        {
            var message = "Attribute or constant \"" + name + "\" not found in \"" + def.GetName() + "\"";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameToken, message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }
        foreach (var t in type)
        {
            input.Scope.AddReference(nameToken, context, t, input.Filename);
        }

        AbstractType? contextType = context.GetType();
        var children = node.GetChildren();
        for (var i = 1; i < children.Count; i++)
        {
            var child = children[i];

            if (contextType is VoidType || contextType is UnknownType)
            {
                return contextType;
            }
            else if (child.Get() is ArrowOrDash)
            {
                var @operator = child.GetFirstToken().GetStr();
                if (@operator == "-" && !((contextType is StructureType)))
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), "AttributeChain, not a structure"));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                else if ((@operator == "->" || @operator == "=>") && !((contextType is ObjectReferenceType)) && !((contextType is DataReference)))
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, child.GetFirstToken(), "AttributeChain, not a reference"));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
            }
            else if (child.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentName)
            {
                if (contextType is ObjectReferenceType || contextType is DataReference)
                {
                    contextType = AttributeName.RunSyntax(contextType, child, input, type);
                }
                else
                {
                    contextType = ComponentName.RunSyntax(contextType, child, input);
                }
            }
            else if (child.GetFirstToken().GetStr() == "*" && contextType is DataReference)
            {
                contextType = ((DataReference)contextType).GetType();
            }
            else if (child is ExpressionNode && (((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.TableExpression)
            {
                if (!((contextType is TableType)))
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, ((ExpressionNode)child).GetFirstToken(), "Table expression, expected table"));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                TableExpression.RunSyntax(((ExpressionNode)child), input, ((TableType)contextType));
                contextType = ((TableType)contextType).GetRowType();
            }
            else if (child is ExpressionNode && (((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldOffset)
            {
                var offset = FieldOffset.RunSyntax(((ExpressionNode)child), input);
                contextType = Substring.SubstringType(contextType, offset, null, ((ExpressionNode)child), input);
            }
            else if (child is ExpressionNode && (((ExpressionNode)child).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldLength)
            {
                var length = FieldLength.RunSyntax(((ExpressionNode)child), input);
                contextType = Substring.SubstringType(contextType, null, length, ((ExpressionNode)child), input);
            }
        }

        return contextType;
    }
}
