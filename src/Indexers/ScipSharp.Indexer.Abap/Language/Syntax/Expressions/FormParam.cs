// Ported from abaplint packages/core/src/abap/5_syntax/expressions/form_param.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class FormParam
{
    public static TypedIdentifier RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        var formParamName = node.FindFirstExpression<FormParamName>();
        if (formParamName == null)
        {
            throw new AssertError("FormParam, could not find FormParamName");
        }
        AbstractToken nameToken = formParamName.GetFirstToken();
        if (formParamName.GetChildren().Count > 1)
        {
            nameToken = new Identifier(nameToken.GetStart(), formParamName.ConcatTokens());
        }

        if (node.FindDirectTokenByText("STRUCTURE") != null && nameToken != null)
        {
            // STRUCTURES typing
            var typeName2 = node.FindDirectExpression<SimpleFieldChain>()?.GetFirstToken().GetStr();
            object? type = null;
            if (!string.IsNullOrEmpty(typeName2))
            {
                type = input.Scope.FindType(typeName2)?.GetType();
                if (((AbstractType?)type) == null)
                {
                    type = input.Scope.GetDDIC().LookupTableOrView(typeName2).Type;
                }
            }
            else
            {
                type = new UnknownType("todo, FORM STRUCTURES typing");
            }
            return new TypedIdentifier(nameToken, input.Filename, ((AbstractType)type), new List<string> { IdentifierMeta.FormParameter, IdentifierMeta.FormParameterStructure });
        }

        if (node.GetChildren().Count == 1 && nameToken != null)
        {
            // untyped FORM parameter
            return new TypedIdentifier(nameToken, input.Filename, AnyType.Get(), new List<string> { IdentifierMeta.FormParameter });
        }

        AbstractType? bfound = new BasicTypes(input).ParseType(node);
        var typeName = JsUtils.ToUpper(node.FindFirstExpression<TypeName>()?.ConcatTokens());
        var hasExplicitLength = node.FindFirstExpression<Length>() != null || node.FindFirstExpression<ConstantFieldLength>() != null;
        // the length cannot be specified for FORM parameters, so these types are generic
        if (hasExplicitLength == false)
        {
            if (typeName == "C" && bfound is CharacterType)
            {
                bfound = CGenericType.Get();
            }
            else if (typeName == "X" && bfound is HexType)
            {
                bfound = XGenericType.Get();
            }
            else if (typeName == "P" && bfound is PackedType)
            {
                bfound = PGenericType.Get();
            }
            else if (typeName == "N" && bfound is NumericType)
            {
                bfound = NGenericType.Get();
            }
        }

        if (nameToken != null && bfound != null)
        {
            return new TypedIdentifier(nameToken, input.Filename, bfound, new List<string> { IdentifierMeta.FormParameter });
        }

        if (nameToken != null)
        {
            return new TypedIdentifier(nameToken, input.Filename, new UnknownType("FormParam, todo"), new List<string> { IdentifierMeta.FormParameter });
        }

        throw new AssertError("FormParam, unexpected node");
    }
}
