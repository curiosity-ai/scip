// Ported from abaplint packages/core/src/abap/5_syntax/statements/insert_internal.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class InsertInternal : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        AbstractType? targetType = null;
        var t = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        if (t != null)
        {
            targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
        }
        if (!((targetType is TableType)) && !((targetType is VoidType)) && !((targetType is AnyType)) && !((targetType is UnknownType)) && targetType != null)
        {
            var message = "INSERT target must be a table";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
        else if (targetType is TableType && node.FindDirectTokenByText("LINES") == null)
        {
            targetType = ((TableType)targetType).GetRowType();
        }

        var initial = node.FindDirectTokenByText("INITIAL") != null;
        ExpressionNode? source = null;
        if (initial == false)
        {
            source = node.FindDirectExpression<SimpleSource4>();
            if (source == null)
            {
                source = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
            }
        }
        AbstractType? sourceType = source != null ? global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(source, input, targetType) : targetType;

        if (targetType == null && !((sourceType is TableType)) && !((sourceType is VoidType)) && !((sourceType is AnyType)) && !((sourceType is UnknownType)))
        {
            var message = "INSERT target must be a table";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var afterAssigning = node.FindExpressionAfterToken("ASSIGNING");
        if ((afterAssigning?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget)
        {
            var inlinefs = afterAssigning?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineFS>();
            if (inlinefs != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineFS.RunSyntax(inlinefs, input, sourceType);
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(afterAssigning, input, sourceType);
            }
        }

        if (initial == false)
        {
            bool error = false;
            if (sourceType is IntegerType && targetType is Integer8Type)
            {
                error = true;
            }
            else if (new TypeUtils(input.Scope).IsAssignable(sourceType, targetType) == false)
            {
                error = true;
            }
            else if (sourceType is CharacterType && targetType is StringType)
            {
                // yea, well, INSERT doesnt convert the values automatically, like everything else?
                error = true;
            }

            if (error == true)
            {
                var message = "Types not compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        var afterInto = node.FindExpressionAfterToken("INTO");
        if ((afterInto?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target && sourceType != null)
        {
            var inline = afterInto.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(afterInto, input, new DataReference(sourceType));
            }
            else
            {
                var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(afterInto, input);
                if (type is TableType && ((TableType)type).GetAccessType() == TableAccessType.hashed)
                {
                    var message = "Implicit or explicit index operation on hashed table is not possible";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            if (s == source)
            {
                continue;
            }
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input, targetType);
        }
    }
}
