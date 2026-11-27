// Ported from abaplint packages/core/src/abap/5_syntax/structures/constants.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

public class Constants
{
    public (TypedIdentifier? type, Dictionary<string, object?> values) RunSyntax(StructureNode node, SyntaxInput input)
    {

        var name = node.FindFirstExpression<DefinitionName>()?.GetFirstToken();
        if (name == null)
        {
            throw new AssertError("Constants, structure, unexpected node");
        }

        List<IStructureComponent> components = new List<IStructureComponent>();
        Dictionary<string, object?> values = new Dictionary<string, object?>();
        foreach (var c in node.GetChildren())
        {
            var ctyp = c.Get();
            if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Constant)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Constant().RunSyntax(((StatementNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                    values[found.GetName()] = found.GetValue();
                }
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Constants)
            {
                var __d0 = new Constants().RunSyntax(((StructureNode)c), input);
                var found = __d0.type;
                var val = __d0.values;
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                    values[found.GetName()] = val;
                }
            }
        }

        if (components.Count == 0)
        {
            return (null, values);
        }

        var type = new TypedIdentifier(name, input.Filename, new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components), new List<string> { IdentifierMeta.ReadOnly, IdentifierMeta.Static });
        return (type, values);
    }
}
