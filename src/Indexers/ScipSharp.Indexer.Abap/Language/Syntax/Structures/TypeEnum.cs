// Ported from abaplint packages/core/src/abap/5_syntax/structures/type_enum.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

public class TypeEnum
{
    public (List<TypedIdentifier> values, List<TypedIdentifier> types) RunSyntax(StructureNode node, SyntaxInput input)
    {
        List<TypedIdentifier> values = new List<TypedIdentifier>();
        List<TypedIdentifier> types = new List<TypedIdentifier>();

        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Structures.Structures.TypeEnum)))
        {
            throw new AssertError("TypeEnum, unexpected type");
        }

        var begin = node.FindDirectStatement<TypeEnumBegin>();
        if (begin == null)
        {
            throw new AssertError("TypeEnum, unexpected type, begin");
        }

        foreach (var type in node.FindDirectStatements<global::ScipSharp.Indexer.Abap.Statements.Statements.Type>())
        {
            var expr = type.FindFirstExpression<NamespaceSimpleName>();
            if (expr == null)
            {
                continue;
            }
            var token = expr.GetFirstToken();
            // integer is default if BASE TYPE is not specified
            values.Add(new TypedIdentifier(token, input.Filename, IntegerType.Get()));
        }
        foreach (var type in node.FindDirectStatements<global::ScipSharp.Indexer.Abap.Statements.Statements.TypeEnum>())
        {
            var expr = type.FindFirstExpression<NamespaceSimpleName>();
            if (expr == null)
            {
                continue;
            }
            var token = expr.GetFirstToken();
            // integer is default if BASE TYPE is not specified
            values.Add(new TypedIdentifier(token, input.Filename, IntegerType.Get()));
        }

        var baseType = begin.FindExpressionAfterToken("TYPE")?.GetFirstToken();
        var baseName = baseType?.GetStr();
        if (baseType != null && !string.IsNullOrEmpty(baseName))
        {
            var found = input.Scope.FindType(baseName);
            if (found != null)
            {
                input.Scope.AddReference(baseType, found, ReferenceType.TypeReference, input.Filename);
            }
        }

        var name = begin.FindFirstExpression<NamespaceSimpleName>();
        if (name != null)
        {
            string qualifiedName = name.ConcatTokens();
            if (input.Scope.GetType() == ScopeType.ClassDefinition || input.Scope.GetType() == ScopeType.Interface)
            {
                qualifiedName = input.Scope.GetName() + "=>" + qualifiedName;
            }
            var etype = new EnumType(new AbstractTypeData { QualifiedName = qualifiedName });
            var id = new TypedIdentifier(name.GetFirstToken(), input.Filename, etype, new List<string> { IdentifierMeta.Enum });
            input.Scope.AddType(id);
            types.Add(id);
        }

        var stru = begin.FindExpressionAfterToken("STRUCTURE");
        if (stru != null)
        {
            List<IStructureComponent> components = new List<IStructureComponent>();
            foreach (var r in values)
            {
                components.Add(new IStructureComponent { Name = r.GetName(), Type = r.GetType() });
            }
            values = new List<TypedIdentifier>();
            var id = new TypedIdentifier(stru.GetFirstToken(), input.Filename, new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components), new List<string> { IdentifierMeta.Enum });
            values.Add(id);
        }

        return (values, types);
    }
}
