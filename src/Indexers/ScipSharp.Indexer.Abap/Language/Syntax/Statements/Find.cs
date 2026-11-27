// Ported from abaplint packages/core/src/abap/5_syntax/statements/find.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Find : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        var rfound = node.FindExpressionAfterToken("RESULTS");
        if (rfound != null && rfound.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            var sub = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "OFFSET", Type = IntegerType.Get() }, new IStructureComponent { Name = "LENGTH", Type = IntegerType.Get() } }, "SUBMATCH_RESULT", "SUBMATCH_RESULT");
            var type = new StructureType(new List<IStructureComponent> { new IStructureComponent { Name = "LINE", Type = IntegerType.Get() }, new IStructureComponent { Name = "OFFSET", Type = IntegerType.Get() }, new IStructureComponent { Name = "LENGTH", Type = IntegerType.Get() }, new IStructureComponent { Name = "SUBMATCHES", Type = new TableType(sub, new ITableOptions { WithHeader = false, KeyType = TableKeyType.@default }) } }, "MATCH_RESULT", "MATCH_RESULT");
            if (JsUtils.ToUpper(node.ConcatTokens()).StartsWith("FIND FIRST", StringComparison.Ordinal))
            {
                this.Inline(rfound, input, type);
            }
            else
            {
                this.Inline(rfound, input, new TableType(type, new ITableOptions { WithHeader = false, KeyType = TableKeyType.@default }, "MATCH_RESULT_TAB"));
            }
        }

        var ofound = node.FindExpressionsAfterToken("OFFSET");
        foreach (var o in ofound)
        {
            if (o.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
            {
                this.Inline(o, input, IntegerType.Get());
            }
        }

        var lfound = node.FindExpressionAfterToken("LINE");
        if (lfound != null && lfound.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            this.Inline(lfound, input, IntegerType.Get());
        }

        var cfound = node.FindExpressionAfterToken("COUNT");
        if (cfound != null && cfound.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            this.Inline(cfound, input, IntegerType.Get());
        }

        var lnfound = node.FindExpressionAfterToken("LENGTH");
        if (lnfound != null && lnfound.Get() is global::ScipSharp.Indexer.Abap.Statements.Expressions.Target)
        {
            this.Inline(lnfound, input, IntegerType.Get());
        }

        if (node.FindDirectTokenByText("SUBMATCHES") != null)
        {
            foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>())
            {
                if (t == rfound || t == lfound || t == cfound || t == lnfound)
                {
                    continue;
                }
                else if (ofound.IndexOf(t) >= 0)
                {
                    continue;
                }
                var inline = t?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
                if (inline != null)
                {
                    global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, StringType.Get());
                }
                else
                {
                    global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(t, input);
                }
            }
        }
    }

    /////////////////////
    private void Inline(ExpressionNode node, SyntaxInput input, AbstractType type)
    {
        var inline = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, type);
        }
        else
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(node, input);
        }
    }
}
