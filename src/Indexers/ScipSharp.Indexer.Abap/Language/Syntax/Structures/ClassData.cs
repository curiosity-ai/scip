// Ported from abaplint packages/core/src/abap/5_syntax/structures/class_data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

public class ClassData
{
    public TypedIdentifier? RunSyntax(StructureNode node, SyntaxInput input)
    {
        var name = node.FindFirstExpression<NamespaceSimpleName>()!.GetFirstToken();
        Dictionary<string, object?> values = new Dictionary<string, object?>();

        List<IStructureComponent> components = new List<IStructureComponent>();
        foreach (var c in node.GetChildren())
        {
            var ctyp = c.Get();
            if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.ClassData)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.ClassData().RunSyntax(((StatementNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                    values[found.GetName()] = found.GetValue();
                }
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.ClassData)
            {
                var found = new ClassData().RunSyntax(((StructureNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                    values[found.GetName()] = found.GetValue();
                }
            }
            // todo, INCLUDES
        }

        return new TypedIdentifier(name, input.Filename, new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components), new List<string> { IdentifierMeta.Static }, values);
    }
}
