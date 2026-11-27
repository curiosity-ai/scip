// Ported from abaplint packages/core/src/abap/5_syntax/statements/modify_internal.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class ModifyInternal : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        // there is only one
        var target = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var targetExpression = target;
        if (targetExpression != null)
        {
            // it might be a dynamic target
            var targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(targetExpression, input);
            if (targetType is VoidType || targetType is AnyType || targetType is UnknownType)
            {
                // ok
            }
            else if (targetType is TableType)
            {
                if (node.FindDirectTokenByText("TABLE") == null && node.FindDirectTokenByText("INDEX") != null && ((TableType)targetType).GetAccessType() == TableAccessType.hashed)
                {
                    var message = "Implicit or explicit index operation on hashed table is not possible";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                if (node.FindDirectTokenByText("TABLE") != null && node.FindDirectTokenByText("INDEX") != null && ((TableType)targetType).IsWithHeader() == false)
                {
                    // MODIFY TABLE INDEX
                    var message = "Table does not have header line";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
            else if (targetType is StructureType)
            {
                // it might originate from a TABLES statement
                if (JsUtils.ToUpper(target.ConcatTokens()) != ((StructureType)targetType).GetDDICName())
                {
                    var message = "Not an internal table";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
            else
            {
                var message = "Not an internal table";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        var fstarget = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget>();
        if (fstarget != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(fstarget, input, null);
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCond>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.ComponentCond.RunSyntax(t, input);
        }
    }
}
