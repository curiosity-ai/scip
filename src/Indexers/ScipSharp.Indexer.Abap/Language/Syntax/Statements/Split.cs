// Ported from abaplint packages/core/src/abap/5_syntax/statements/split.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Split : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        var intoTable = node.FindTokenSequencePosition("INTO", "TABLE") != null;
        AbstractType type = intoTable ? new TableType(StringType.Get(), new ITableOptions { WithHeader = false, KeyType = TableKeyType.@default }) : StringType.Get();

        foreach (var target in node.FindAllExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
        {
            var inline = target.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, type);
            }
            else
            {
                AbstractType? targetType = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
                if (intoTable)
                {
                    if (!((targetType is TableType)) && !((targetType is UnknownType)) && !((targetType is VoidType)))
                    {
                        var message = "Into must be table typed";
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, target.GetFirstToken(), message));
                        return;
                    }
                    if (targetType is TableType)
                    {
                        targetType = ((TableType)targetType).GetRowType();
                    }
                }
                if (new TypeUtils(input.Scope).IsCharLikeStrict(targetType) == false)
                {
                    var message = "Incompatible, target not character like";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }
            }
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
    }
}
