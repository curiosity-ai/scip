// Ported from abaplint packages/core/src/abap/5_syntax/statements/delete_internal.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class DeleteInternal : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        AbstractType? targetType = null;
        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (target != null)
        {
            var targetName = target.ConcatTokens();
            ILookupResult? tabl = null;
            var localVariable = input.Scope.FindVariable(targetName);
            if (localVariable == null && node.GetChildren().Count == 5 && JsUtils.ToUpper(node.GetChildren().ElementAtOrDefault(2).ConcatTokens()) == "FROM")
            {
                // it might be a database table
                var found = input.Scope.GetDDIC()?.LookupTableOrView(targetName);
                if (found?.Object != null)
                {
                    tabl = found;
                    input.Scope.GetDDICReferences().AddUsing(input.Scope.GetParentObj(), new IObjectAndToken { Object = tabl.Object });
                }
            }
            if (tabl == null)
            {
                var ambigiousVoids = input.Scope.GetRegistry().GetConfig().GetSyntaxSetttings().AmbigiousVoids ?? new List<string>();
                if (ambigiousVoids.Any(name => JsUtils.ToUpper(name) == JsUtils.ToUpper(targetName)))
                {
                    targetType = VoidType.Get(targetName);
                }
                else
                {
                    targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
                }
                if (node.FindDirectTokenByText("TABLE") == null && node.FindDirectTokenByText("ADJACENT") == null && (node.FindDirectTokenByText("FROM") != null || node.FindDirectTokenByText("INDEX") != null) && targetType is TableType && ((TableType)targetType).GetAccessType() == TableAccessType.hashed)
                {
                    var message = "Implicit or explicit index operation on hashed table is not possible";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                if (targetType is TableType)
                {
                    targetType = ((TableType)targetType).GetRowType();
                }
            }
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCompare>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.ComponentCompare.RunSyntax(t, input, targetType);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCond>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.ComponentCond.RunSyntax(t, input, targetType);
        }
    }
}
