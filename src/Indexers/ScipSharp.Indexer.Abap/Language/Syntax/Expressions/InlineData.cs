// Ported from abaplint packages/core/src/abap/5_syntax/expressions/inline_data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class InlineData
{
    public static void RunSyntax(ExpressionNode node, SyntaxInput input, AbstractType? type)
    {
        var token = node.FindFirstExpression<TargetField>()?.GetFirstToken();
        if (token != null && token.GetStr().Length > 30)
        {
            var message = "DATA name too long, " + token.GetStr();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, token, message));
        }
        if (token != null && type != null)
        {
            if (type is CSequenceType || type is CLikeType)
            {
                type = StringType.Get();
            }
            else if (type is XSequenceType)
            {
                type = StringType.Get();
            }
            else if (type is CGenericType)
            {
                var message = "InlineData, generic type C cannot be used for inferred type";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }

            if (type.IsGeneric())
            {
                var message = "DATA definition cannot be generic, " + type.GetType().Name;
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                type = VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }

            var identifier = new TypedIdentifier(token, input.Filename, InlineData.StripDerivedFromConstant(type), new List<string> { IdentifierMeta.InlineDefinition });
            input.Scope.AddIdentifier(identifier);
            input.Scope.AddReference(token, identifier, ReferenceType.DataWriteReference, input.Filename);
        }
        else if (token != null)
        {
            var message = "InlineData, could not determine type for \"" + token.GetStr() + "\"";
            var identifier = new TypedIdentifier(token, input.Filename, new UnknownType(message), new List<string> { IdentifierMeta.InlineDefinition });
            input.Scope.AddIdentifier(identifier);
            input.Scope.AddReference(token, identifier, ReferenceType.DataWriteReference, input.Filename);
        }
    }

    // The inferred type is taken from the source expression, which might be a literal.
    // The variable itself is not a constant, so the relaxations that apply to literals
    // must not be inherited, eg. a "c" variable is not assignable to a "string" parameter
    private static AbstractType StripDerivedFromConstant(AbstractType type)
    {
        var data = type.GetAbstractTypeData();
        if (data?.DerivedFromConstant != true)
        {
            return type;
        }
        // const {derivedFromConstant, ...rest} = data;
        var rest = new AbstractTypeData { QualifiedName = data.QualifiedName, ConversionExit = data.ConversionExit, DdicName = data.DdicName, RTTIName = data.RTTIName, Description = data.Description };
        if (type is CharacterType)
        {
            return new CharacterType(((CharacterType)type).GetLength(), rest);
        }
        else if (type is IntegerType)
        {
            return IntegerType.Get(rest);
        }
        else if (type is StringType)
        {
            return StringType.Get(rest);
        }
        return type;
    }
}
