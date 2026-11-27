// Ported from abaplint packages/core/src/abap/5_syntax/structures/type_mesh.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

public class TypeMesh
{
    public TypedIdentifier? RunSyntax(StructureNode node, SyntaxInput input)
    {
        var begin = node.FindDirectStatement<TypeMeshBegin>();
        if (begin == null)
        {
            return null;
        }
        var name = begin.FindFirstExpression<NamespaceSimpleName>()?.GetFirstToken();
        if (name == null)
        {
            return null;
        }

        List<IStructureComponent> components = new List<IStructureComponent>();
        foreach (var c in node.GetChildren())
        {
            if (!((c is StatementNode)))
            {
                continue;
            }
            var ctyp = ((StatementNode)c).Get();
            if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Type)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Type().RunSyntax(((StatementNode)c), input, name.GetStr() + "-");
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                }
            }
            else if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.TypeMesh)
            {
                var found = this.RunMeshNode(((StatementNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                }
            }
        }

        string qualifiedName = name.GetStr();
        if (input.Scope.GetType() == ScopeType.ClassDefinition || input.Scope.GetType() == ScopeType.Interface)
        {
            qualifiedName = input.Scope.GetName() + "=>" + qualifiedName;
        }

        return new TypedIdentifier(name, input.Filename, new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components, qualifiedName));
    }

    ////////////////////
    private TypedIdentifier? RunMeshNode(StatementNode node, SyntaxInput input)
    {
        var nameToken = node.FindFirstExpression<NamespaceSimpleName>()?.GetFirstToken();
        if (nameToken == null)
        {
            return null;
        }

        var typeName = node.FindFirstExpression<TypeName>();
        AbstractType? type = new BasicTypes(input).ResolveTypeName(typeName);
        if (type == null)
        {
            type = new UnknownType("Mesh node, unknown type " + JsUtils.Str(typeName?.ConcatTokens()));
        }
        else if (JsUtils.ToUpper(node.ConcatTokens()).Contains(" TYPE REF TO ", StringComparison.Ordinal))
        {
            type = new DataReference(type);
        }

        return new TypedIdentifier(nameToken, input.Filename, type);
    }
}
