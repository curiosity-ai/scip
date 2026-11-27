// Ported from abaplint packages/core/src/abap/5_syntax/statements/update_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Syntax.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class UpdateDatabase : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        IParseTypeObject? dbSource = null;
        var dbtab = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DatabaseTable>();
        if (dbtab != null)
        {
            dbSource = global::ScipSharp.Indexer.Abap.Syntax.Expressions.DatabaseTable.RunSyntax(dbtab, input);
            var fields = node.FindAllExpressions<SQLFieldAndValue>().SelectMany(fieldAndValue => fieldAndValue.FindDirectExpressions<SQLFieldName>()).ToList().Select(field => JsUtils.ToUpper(field.ConcatTokens())).ToList();
            fields.AddRange(node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLCompare>().Select(compare => JsUtils.ToUpper(compare.FindDirectExpression<SQLFieldName>()?.ConcatTokens())).ToList().Where(field => field != null).ToList());
            CheckDatabaseFieldsHelper.CheckDatabaseFields(fields, new List<IParseTypeObject?> { dbSource }, input, node.GetFirstToken());
        }

        var tableName = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DatabaseTable>();
        var tokenName = tableName?.GetFirstToken();
        if (tableName != null && tokenName != null)
        {
            // todo, this also finds structures, it should only find transparent tables
            var found = input.Scope.GetDDIC().LookupTable(tokenName.GetStr());
            if (found is StructureType)
            {
                input.Scope.Push(ScopeType.OpenSQL, "UPDATE", tokenName.GetStart(), input.Filename);
                foreach (var field in ((StructureType)found).GetComponents())
                {
                    var fieldToken = new Identifier(node.GetFirstToken().GetStart(), field.Name);
                    var id = new TypedIdentifier(fieldToken, input.Filename, field.Type);
                    input.Scope.AddIdentifier(id);
                }
            }
        }

        var sourceTypes = new Dictionary<ExpressionNode, AbstractType?>();
        foreach (var s in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            sourceTypes[s] = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindAllExpressions<SimpleSource3>())
        {
            sourceTypes[s] = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        foreach (var d in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(d, input);
        }

        CheckDatabaseWorkAreaHelper.CheckDatabaseWorkArea(node, dbSource, sourceTypes, input);

        if (input.Scope.GetType() == ScopeType.OpenSQL)
        {
            input.Scope.Pop(node.GetLastToken().GetEnd());
        }
    }
}
