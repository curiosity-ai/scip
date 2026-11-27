// Ported from abaplint packages/core/src/abap/5_syntax/expressions/sql_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SQLSource
{

    public static AbstractType? RunSyntax(INode node, SyntaxInput input)
    {
        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            return Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindAllExpressions<SimpleSource3>())
        {
            return Source.RunSyntax(s, input);
        }
        return null;
    }
}
