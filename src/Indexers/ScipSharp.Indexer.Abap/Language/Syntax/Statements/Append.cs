// Ported from abaplint packages/core/src/abap/5_syntax/statements/append.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

// todo: issue error for short APPEND if the source is without header line
public class Append : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        AbstractType? targetType = null;

        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (target != null)
        {
            targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
        }

        // a hashed table has no index, APPEND adds at the end
        if (targetType is TableType && ((TableType)targetType).GetAccessType() == TableAccessType.hashed)
        {
            var message = "Implicit or explicit index operation on hashed table is not possible";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var fsTarget = node.FindExpressionAfterToken("ASSIGNING");
        if (fsTarget != null && fsTarget.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget)
        {
            if (!((targetType is TableType)) && !((targetType is VoidType)) && !((targetType is UnknownType)))
            {
                var message = "APPEND to non table type, " + JsUtils.Str(targetType?.GetType().Name);
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            AbstractType rowType = targetType is TableType ? ((TableType)targetType).GetRowType() : targetType;
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(fsTarget, input, rowType);
        }

        var dataTarget = node.FindExpressionAfterToken("INTO");
        if (dataTarget != null && JsUtils.ToUpper(node.ConcatTokens()).Contains(" REFERENCE INTO DATA(", StringComparison.Ordinal))
        {
            if (!((targetType is TableType)) && !((targetType is VoidType)) && !((targetType is UnknownType)))
            {
                var message = "APPEND to non table type, " + JsUtils.Str(targetType?.GetType().Name);
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
            AbstractType rowType = targetType is TableType ? ((TableType)targetType).GetRowType() : targetType;
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(dataTarget, input, new DataReference(rowType));
        }

        ExpressionNode? source = node.FindDirectExpression<SimpleSource4>();
        if (source == null)
        {
            source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        }
        if (source != null)
        {
            if (targetType != null && !((targetType is TableType)) && !((targetType is UnknownType)) && dataTarget != target && !((targetType is VoidType)))
            {
                var message = $"Append, target not a table type ({targetType.GetType().Name})";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }

            AbstractType? rowType = null;
            if (targetType is TableType)
            {
                rowType = ((TableType)targetType).GetRowType();
            }
            else if (targetType is VoidType)
            {
                rowType = ((VoidType)targetType);
            }
            AbstractType? sourceType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(source, input, rowType);

            if (node.FindDirectTokenByText("LINES") != null)
            {
                // hmm, checking only the row types are compatible will not check the table type, e.g. sorted or hashed
                if (sourceType is TableType)
                {
                    sourceType = ((TableType)sourceType).GetRowType();
                }
                else if (!((sourceType is VoidType)) && !((sourceType is UnknownType)))
                {
                    var message = "LINES OF must be a table type";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
                if (targetType is TableType)
                {
                    targetType = ((TableType)targetType).GetRowType();
                }
                if (new TypeUtils(input.Scope).IsAssignable(sourceType, targetType) == false)
                {
                    var message = "Incompatible types";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
            else
            {
                if (new TypeUtils(input.Scope).IsAssignable(sourceType, rowType) == false)
                {
                    var message = "Incompatible types";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
        }

        var from = node.FindExpressionAfterToken("FROM");
        if (from != null && from.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(from, input);
        }
        var to = node.FindExpressionAfterToken("TO");
        if (to != null && to.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Source)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(to, input);
        }
    }
}
