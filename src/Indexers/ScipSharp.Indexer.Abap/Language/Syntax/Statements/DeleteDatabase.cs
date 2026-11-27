// Ported from abaplint packages/core/src/abap/5_syntax/statements/delete_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Syntax.Expressions;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class DeleteDatabase : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindAllExpressions<SimpleSource3>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var d in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(d, input);
        }

        foreach (var compare in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLCompare>())
        {
            var sqlin = compare.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLIn>();
            if (sqlin != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.SQLIn.RunSyntax(sqlin, input);
            }
        }

        var dbtab = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DatabaseTable>();
        if (dbtab != null)
        {
            var dbSource = global::ScipSharp.Indexer.Abap.Syntax.Expressions.DatabaseTable.RunSyntax(dbtab, input);
            var fields = node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLCompare>().Select(compare => JsUtils.ToUpper(compare.FindDirectExpression<SQLFieldName>()?.ConcatTokens())).ToList().Where(field => field != null).ToList();
            foreach (var orderBy in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLOrderBy>())
            {
                fields.AddRange(orderBy.FindAllExpressions<SQLFieldName>().Select(field => JsUtils.ToUpper(field.ConcatTokens())).ToList());
            }
            CheckDatabaseFieldsHelper.CheckDatabaseFields(fields, new List<IParseTypeObject?> { dbSource }, input, node.GetFirstToken());
        }
    }
}
