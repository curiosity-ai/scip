// Ported from abaplint packages/core/src/abap/5_syntax/statements/static.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Static
{
    public TypedIdentifier? RunSyntax(StatementNode node, SyntaxInput input)
    {
        var tt = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.TypeTable>();
        if (tt != null)
        {
            var ttfound = global::ScipSharp.Indexer.Abap.Syntax.Expressions.TypeTable.RunSyntax(node, input);
            if (ttfound != null)
            {
                return ttfound;
            }
        }

        var found = new BasicTypes(input).SimpleType(node);
        if (found != null)
        {
            return found;
        }

        var fallback = node.FindFirstExpression<NamespaceSimpleName>();
        if (fallback != null)
        {
            return new TypedIdentifier(fallback.GetFirstToken(), input.Filename, new UnknownType("Static, fallback"));
        }

        return null;
    }
}
