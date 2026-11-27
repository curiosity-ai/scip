// Ported from abaplint packages/core/src/abap/5_syntax/statements/describe.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Describe : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }
        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.FieldChain.RunSyntax(s, input, ReferenceType.DataReadReference);
        }

        var linesTarget = node.FindExpressionAfterToken("LINES");
        if ((linesTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var inline = linesTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, IntegerType.Get());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(linesTarget, input);
            }
        }

        var typeTarget = node.FindExpressionAfterToken("TYPE");
        if ((typeTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var inline = typeTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, new CharacterType(1));
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(typeTarget, input);
            }
        }

        var lengthTarget = node.FindExpressionAfterToken("LENGTH");
        if ((lengthTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var inline = lengthTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, IntegerType.Get());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(lengthTarget, input);
            }
        }

        var componentsTarget = node.FindExpressionAfterToken("COMPONENTS");
        if ((componentsTarget?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var inline = componentsTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
            if (inline != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, IntegerType.Get());
            }
            else
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(componentsTarget, input);
            }
        }
    }
}
