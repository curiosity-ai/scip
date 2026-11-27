// Ported from abaplint packages/core/src/abap/5_syntax/expressions/sql_for_all_entries.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SQLForAllEntries
{

    public static void RunSyntax(ExpressionNode node, SyntaxInput input)
    {
        ExpressionNode? s = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (s == null)
        {
            s = node.FindFirstExpression<SimpleSource3>();
        }
        if (s != null)
        {
            var type = Source.RunSyntax(s, input);
            if (type is VoidType || type is UnknownType)
            {
                return;
            }
            if (!((type is TableType)))
            {
                var message = "FAE parameter must be table type, " + JsUtils.Str(type?.GetType().Name);
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }

            var name = JsUtils.ReplaceFirst(s.ConcatTokens(), "[]", "");
            input.Scope.SetAllowHeaderUse(name);
        }
    }
}
