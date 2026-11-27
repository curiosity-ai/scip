// Ported from abaplint packages/core/src/abap/5_syntax/statements/get_time.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class GetTime : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        var stamp = node.FindDirectTokenByText("STAMP") != null;

        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, new PackedType(8, 0));
        }
        else if (target != null)
        {
            var type = global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input);
            if (stamp == true && this.CompatibleTimeStamp(type) == false)
            {
                var message = "GET TIME STAMP FIELD, target type not compatible";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                return;
            }
        }
    }

    // the target must be a timestamp, ie. packed(timestamp and timestampl) or utclong
    private bool CompatibleTimeStamp(AbstractType? type)
    {
        return type == null || type is PackedType || type is UTCLongType || type is VoidType || type is AnyType || type is DataType || type is UnknownType || type.IsGeneric();
    }
}
