// Ported from abaplint packages/core/src/abap/5_syntax/statements/concatenate.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Concatenate : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var byteMode = node.FindDirectTokenByText("BYTE") != null;
        var linesMode = node.FindDirectTokenByText("LINES") != null;

        if (node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.TableExpression>() != null)
        {
            var message = "CONCATENATE with table expression not possible";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        var target = node.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            if (byteMode)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, XStringType.Get());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, StringType.Get());
            }
        }
        else if (target != null)
        {
            var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
            bool compatible = byteMode ? new TypeUtils(input.Scope).IsHexLike(type) : new TypeUtils(input.Scope).IsCharLikeStrict(type);
            if (compatible == false)
            {
                var message = "Target type not compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }

        if (linesMode)
        {
            foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
            {
                var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
                if (!((type is UnknownType)) && !((type is VoidType)) && !((type is TableType)))
                {
                    var message = "Source must be an internal table";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }

                if (linesMode == true && byteMode == true && type is TableType && (((TableType)type).GetRowType()) is StructureType)
                {
                    var message = "Source row type must not be a structure in BYTE mode";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
        }

        foreach (var s in node.FindDirectExpressions<SimpleSource3>())
        {
            var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            bool compatible = byteMode ? new TypeUtils(input.Scope).IsHexLike(type) : new TypeUtils(input.Scope).IsCharLikeStrict(type);
            if (compatible == false)
            {
                var message = "Source type not compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
    }
}
