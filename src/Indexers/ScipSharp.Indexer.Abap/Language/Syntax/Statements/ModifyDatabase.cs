// Ported from abaplint packages/core/src/abap/5_syntax/statements/modify_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Syntax.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class ModifyDatabase : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        foreach (var d in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(d, input);
        }

        IParseTypeObject? dbSource = null;
        var dbtab = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.DatabaseTable>();
        if (dbtab != null)
        {
            if (node.GetChildren().Count == 5)
            {
                var found = input.Scope.FindVariable(dbtab.ConcatTokens());
                if (found != null)
                {
                    input.Scope.AddReference(dbtab.GetFirstToken(), found, ReferenceType.DataWriteReference, input.Filename);
                }
                else
                {
                    dbSource = global::ScipSharp.Indexer.Abap.Syntax.Expressions.DatabaseTable.RunSyntax(dbtab, input);
                }
            }
            else
            {
                dbSource = global::ScipSharp.Indexer.Abap.Syntax.Expressions.DatabaseTable.RunSyntax(dbtab, input);
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

        CheckDatabaseWorkAreaHelper.CheckDatabaseWorkArea(node, dbSource, sourceTypes, input);
    }
}
