// Ported from abaplint packages/core/src/abap/5_syntax/expressions/inline_field_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class InlineFieldDefinition
{
    public static AbstractType? RunSyntax(INode node, SyntaxInput input, AbstractType? targetType = null)
    {

        AbstractType? type = null;

        var field = (node.FindDirectExpression<Field>() ?? node.FindDirectExpression<FieldSymbol>())?.GetFirstToken();
        if (field == null)
        {
            return null;
        }

        var source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (source != null)
        {
            type = Source.RunSyntax(source, input);
        }
        var typeName = node.FindDirectExpression<TypeName>();
        if (typeName != null)
        {
            type = new BasicTypes(input).ParseType(typeName);
        }
        if (targetType != null)
        {
            type = targetType;
        }
        if (type == null)
        {
            type = new UnknownType("InlineFieldDefinition, fallback");
        }

        var name = field.GetStr();
        if (input.Scope.FindVariable(name) != null)
        {
            var message = $"Variable {name} already defined";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        var identifier = new TypedIdentifier(field, input.Filename, type, new List<string> { IdentifierMeta.InlineDefinition });
        input.Scope.AddReference(field, identifier, ReferenceType.DataWriteReference, input.Filename);
        input.Scope.AddIdentifier(identifier);

        return type;
    }
}
