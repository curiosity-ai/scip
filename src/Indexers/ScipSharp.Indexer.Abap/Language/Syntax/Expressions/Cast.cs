// Ported from abaplint packages/core/src/abap/5_syntax/expressions/cast.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Cast
{
    public static AbstractType RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? targetType)
    {
        var sourceNode = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (sourceNode == null)
        {
            var message = "Cast, source node not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        var sourceType = Source.RunSyntax(sourceNode, input);
        AbstractType? tt = null;

        var typeExpression = node.FindDirectExpression<TypeNameOrInfer>();
        var typeName = typeExpression?.ConcatTokens();
        if (typeName == null)
        {
            var message = "Cast, child TypeNameOrInfer not found";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }
        else if (typeName == "#" && targetType != null)
        {
            tt = targetType;
        }
        else if (typeName == "#")
        {
            var message = "Cast, todo, infer type";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        if (tt == null && typeExpression != null)
        {
            var basic = new BasicTypes(input);
            tt = basic.ParseType(typeExpression);
            if (tt == null || tt is VoidType || tt is UnknownType)
            {
                var found = input.Scope.FindObjectDefinition(typeName);
                if (found != null)
                {
                    tt = new ObjectReferenceType(((Identifier?)found), new AbstractTypeData { QualifiedName = typeName });
                    input.Scope.AddReference(typeExpression.GetFirstToken(), ((Identifier?)found), ReferenceType.ObjectOrientedReference, input.Filename);
                }
            }
            else
            {
                tt = new DataReference(tt, typeName);
            }
            if (tt == null && input.Scope.GetDDIC().InErrorNamespace(typeName) == false)
            {
                tt = VoidType.Get(typeName);
            }
            else if (JsUtils.ToUpper(typeName) == "OBJECT")
            {
                return new GenericObjectReferenceType();
            }
            else if (tt == null)
            {
                // todo, this should be an UnknownType instead?
                var message = "Type \"" + typeName + "\" not found in scope, Cast";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, typeExpression.GetFirstToken(), message));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
        }
        Source.AddIfInferred(node, input, tt);

        if (new TypeUtils(input.Scope).IsCastable(sourceType, tt) == false)
        {
            var message = "Cast, incompatible types";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        return tt!;
    }
}
