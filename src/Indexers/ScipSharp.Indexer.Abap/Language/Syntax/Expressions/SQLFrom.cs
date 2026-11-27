// Ported from abaplint packages/core/src/abap/5_syntax/expressions/sql_from.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SQLFrom
{

    public static List<IParseTypeObject?> RunSyntax(INode node, SyntaxInput input)
    {
        List<IParseTypeObject?> ret = new List<IParseTypeObject?>();
        var fromList = node.FindAllExpressions<SQLFromSource>();
        foreach (var from in fromList)
        {
            foreach (var d in from.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
            {
                Dynamic.RunSyntax(d, input);
            }

            var dbtab = from.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DatabaseTable>();
            if (dbtab != null)
            {
                ret.Add(DatabaseTable.RunSyntax(dbtab, input));
            }
        }
        return ret;
    }
}
