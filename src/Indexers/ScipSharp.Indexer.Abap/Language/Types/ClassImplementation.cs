// Ported from abaplint packages/core/src/abap/types/class_implementation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Structures.Structures;

namespace ScipSharp.Indexer.Abap.Types;

public class ClassImplementation : global::ScipSharp.Indexer.Abap.FileInformation.Identifier, IClassImplementation
{
    private readonly StructureNode node;

    public ClassImplementation(StructureNode node, string filename)
        : base(NameToken(node), filename)
    {
        this.node = node;
    }

    // TS checks the node and finds the name before calling super()
    private static AbstractToken NameToken(StructureNode node)
    {
        if (!((node.Get() is global::ScipSharp.Indexer.Abap.Structures.Structures.ClassImplementation)))
        {
            throw new Exception("ClassImplementation, unexpected node type");
        }
        return node.FindFirstStatement<global::ScipSharp.Indexer.Abap.Statements.Statements.ClassImplementation>()!.FindFirstExpression<ClassName>()!.GetFirstToken();
    }

    public List<MethodImplementation> GetMethodImplementations()
    {
        List<MethodImplementation> ret = new List<MethodImplementation>();
        foreach (var method in this.node.FindAllStructures<Method>())
        {
            ret.Add(new MethodImplementation(method, this.filename));
        }
        return ret;
    }

    public MethodImplementation? GetMethodImplementation(string name)
    {
        foreach (var impl in this.GetMethodImplementations())
        {
            if (JsUtils.ToUpper(impl.GetName()) == JsUtils.ToUpper(name))
            {
                return impl;
            }
        }
        return null;
    }
}
