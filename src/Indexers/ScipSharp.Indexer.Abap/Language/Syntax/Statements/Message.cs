// Ported from abaplint packages/core/src/abap/5_syntax/statements/message.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Message : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var found = node.FindExpressionAfterToken("INTO");
        var inline = found?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();

        if (inline != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, StringType.Get());
        }
        else if (found != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(found, input);
        }

        foreach (var mss in node.FindDirectExpressions<MessageSourceSource>())
        {
            foreach (var s in mss.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            }
            foreach (var s in mss.FindDirectExpressions<SimpleSource3>())
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
            }
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindDirectExpressions<SimpleSource3>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.MessageSource>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.MessageSource.RunSyntax(s, input);
        }
    }
}
