// Ported from abaplint packages/core/src/abap/5_syntax/expressions/data_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class DataDefinition
{
    public static TypedIdentifier? RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        var tt = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTable>();
        if (tt != null)
        {
            return TypeTable.RunSyntax(node, input);
        }

        var valueNode = node.FindFirstExpression<Value>();
        string? value = null;
        if (valueNode != null)
        {
            value = new BasicTypes(input).FindValue(node);
        }

        var name = node.FindFirstExpression<DefinitionName>();
        var typeStructure = node.FindFirstExpression<TypeStructure>();
        if (typeStructure != null && name != null)
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, VoidType.Get("DataDefinition, TypeStructure"));
        }

        var bfound = new BasicTypes(input).SimpleType(node);
        if (bfound != null)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return new TypedIdentifier(bfound.GetToken(), input.Filename, bfound.GetType(), bfound.GetMeta(), value);
            }
            else
            {
                return bfound;
            }
        }

        if (name != null)
        {
            return new TypedIdentifier(name.GetFirstToken(), input.Filename, new UnknownType("DataDefinition, fallback"));
        }

        return null;
    }
}
