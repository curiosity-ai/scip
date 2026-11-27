// Ported from abaplint packages/core/src/abap/5_syntax/expressions/dereference.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class Dereference
{
    public static AbstractType? RunSyntax(INode node, AbstractType? type, SyntaxInput input)
    {
        if (type is VoidType || type is AnyType || type is DataType || type == null || type is UnknownType)
        {
            return type;
        }

        if (!((type is DataReference)))
        {
            var message = "Not a data reference, cannot be dereferenced";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return VoidType.Get(SyntaxInput.CheckSyntaxKey);
        }

        return ((DataReference)type).GetType();
    }
}
