// Ported from abaplint packages/core/src/abap/5_syntax/statements/convert.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Convert : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        // todo, the source must be of a specific type
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        var timeTargets = node.FindExpressionsAfterToken("TIME");
        foreach (var timeTarget in timeTargets)
        {
            var concat = JsUtils.ToUpper(node.ConcatTokens());
            if ((timeTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
            {
                var inline = timeTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
                if (inline != null)
                {
                    AbstractType targetType = new TimeType();
                    if (concat.Contains("DAYLIGHT SAVING TIME " + JsUtils.ToUpper(inline.ConcatTokens()), StringComparison.Ordinal))
                    {
                        targetType = new CharacterType(1);
                    }
                    global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, targetType);
                }
                else
                {
                    global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(timeTarget, input);
                }
            }
        }

        var dateTarget = node.FindExpressionAfterToken("DATE");
        if ((dateTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var inline = dateTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, new DateType());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(dateTarget, input);
            }
        }

        var stampTarget = node.FindExpressionAfterToken("STAMP");
        if ((stampTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var inline = stampTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, new PackedType(8, 0));
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(stampTarget, input);
            }
        }

        var utclongTarget = node.FindExpressionAfterToken("UTCLONG");
        if ((utclongTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var inline = utclongTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, new UTCLongType());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(utclongTarget, input);
            }
        }
    }
}
