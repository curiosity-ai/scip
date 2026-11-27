// Ported from abaplint packages/core/src/abap/types/method_implementation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Structures.Structures;

namespace ScipSharp.Indexer.Abap.Types;

using AbstractToken = ScipSharp.Indexer.Abap.Lexer.Tokens.AbstractToken;

public class MethodImplementation : Identifier
{

    public MethodImplementation(StructureNode node, string filename)
        : base(NameToken(node), filename)
    {
    }

    // TS checks the node and finds the name before calling super()
    private static AbstractToken NameToken(StructureNode node)
    {
        if (!((node.Get() is Method)))
        {
            throw new Exception("MethodImplementation, expected Method as part of input node");
        }
        var found = node.FindFirstExpression<MethodName>();
        if (found == null)
        {
            throw new Exception("MethodImplementation, expected MethodName as part of input node");
        }
        return found.GetFirstToken();
    }
}
