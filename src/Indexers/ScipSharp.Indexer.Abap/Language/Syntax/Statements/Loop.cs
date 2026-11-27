// Ported from abaplint packages/core/src/abap/5_syntax/statements/loop.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Statements;

public class Loop : StatementSyntax
{
    public void RunSyntax(StatementNode node, SyntaxInput input)
    {
        var loopTarget = node.FindDirectExpression<LoopTarget>();

        ExpressionNode? target = loopTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Target>();
        AbstractType? targetType = target != null ? global::ScipSharp.Indexer.Abap.Syntax.Expressions.Target.RunSyntax(target, input) : (AbstractType?)null;
        if (target == null)
        {
            target = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget>();
        }

        var write = loopTarget?.FindDirectTokenByText("ASSIGNING") != null;

        var sources = node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        ExpressionNode? firstSource = ((ExpressionNode?)(node.FindDirectExpression<LoopSource>()?.GetFirstChild()));
        if (firstSource == null)
        {
            firstSource = sources.ElementAtOrDefault(0);
        }
        AbstractType? sourceType = firstSource != null ? global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(firstSource, input, targetType, write) : (AbstractType?)null;
        AbstractType? rowType = null;

        var concat = JsUtils.ToUpper(node.ConcatTokens());
        if (sourceType == null)
        {
            // if its a dynpro table control loop, then dont issue error
            if (concat != "LOOP.")
            {
                var message = "No source type determined";
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            }
        }
        else if (sourceType is UnknownType)
        {
            var message = "Loop, not a table type, " + ((UnknownType)sourceType).GetError();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            sourceType = VoidType.Get("Loop, not a table type");
        }
        else if (sourceType is TableType && target == null && ((TableType)sourceType).IsWithHeader() == false && node.GetChildren().Count == 4)
        {
            var message = "Loop, no header line";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }
        else if (!((sourceType is TableType)) && !((sourceType is AnyType)) && !((sourceType is DataType)) && !((sourceType is VoidType)) && concat.StartsWith("LOOP AT GROUP ", StringComparison.Ordinal) == false)
        {
            var message = "Loop, not a table type";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }
        else if (loopTarget == null && sourceType is TableType && ((TableType)sourceType).IsWithHeader() == false)
        {
            var message = "Loop, no header";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
        }

        var targetConcat = JsUtils.ToUpper(loopTarget?.ConcatTokens());
        var topType = sourceType;

        if (topType is TableType && ((TableType)topType).GetAccessType() == TableAccessType.hashed && node.FindDirectTokenByText("USING") == null && (node.FindDirectTokenByText("FROM") != null || node.FindDirectTokenByText("TO") != null))
        {
            var message = "Implicit or explicit index operation on hashed table is not possible";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        if (sourceType is TableType)
        {
            rowType = ((TableType)sourceType).GetRowType();
            sourceType = rowType;
            if ((targetConcat?.StartsWith("REFERENCE INTO ", StringComparison.Ordinal)) == true)
            {
                sourceType = new DataReference(sourceType);
            }
        }

        var cond = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCond>();
        if (cond != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.ComponentCond.RunSyntax(cond, input, rowType);
        }

        if (!string.IsNullOrEmpty(targetConcat) && targetConcat.StartsWith("TRANSPORTING ", StringComparison.Ordinal) && node.FindDirectTokenByText("WHERE") == null)
        {
            var message = "Loop, TRANSPORTING NO FIELDS only with WHERE";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }

        if (node.FindDirectTokenByText("USING") != null && cond != null && topType is TableType)
        {

            // https://github.com/abap2xlsx/abap2xlsx/issues/1341
            var keyName = node.FindExpressionAfterToken("KEY");
            ITableKey? key = null;
            string? name = (keyName?.Get()) is SimpleName ? JsUtils.ToUpper(keyName.GetFirstToken().GetStr()) : (string?)null;
            // it might be dynamic, in that case we cannot check anything; and
            // primary_key is the predefined name of the primary key every table has
            if (name != null && name != "PRIMARY_KEY")
            {
                key = ((TableType)topType).GetOptions().Secondary?.FirstOrDefault(k => JsUtils.ToUpper(k.Name) == name);
                if (key == null)
                {
                    var message = "Key " + JsUtils.Str(keyName?.ConcatTokens()) + " not found in table type";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }

                if (cond.FindDirectTokenByText("OR") != null)
                {
                    var message = "Loop, secondary key requires an optimizable WHERE, top-level OR not allowed";
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return;
                }

                if (!(Versions.ReleaseAtLeast(input.Scope.GetRelease(), Release.v740sp02)))
                {
                    var compares = cond.FindAllExpressionsRecursive<global::ScipSharp.Indexer.Abap.Statements.Expressions.ComponentCompare>().Select(c => JsUtils.ToUpper(c.ConcatTokens())).ToList();
                    foreach (var keyField in key.KeyFields)
                    {
                        if (compares.FirstOrDefault(c => c == JsUtils.ToUpper(keyField) + " IS INITIAL") != null)
                        {
                            var message = "Loop, key check with IS INITIAL cannot optimized before 7.40 SP02";
                            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                            return;
                        }
                    }
                }
            }
        }

        var inline = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineData>();
        if (inline != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineData.RunSyntax(inline, input, sourceType);
        }

        foreach (var s in sources)
        {
            if (s == firstSource)
            {
                continue;
            }
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Source.RunSyntax(s, input);
        }

        var inlinefs = target?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.InlineFS>();
        if (inlinefs != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.InlineFS.RunSyntax(inlinefs, input, sourceType);
        }
        else
        {
            var fstarget = loopTarget?.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FSTarget>();
            if (fstarget != null)
            {
                global::ScipSharp.Indexer.Abap.Syntax.Expressions.FSTarget.RunSyntax(fstarget, input, sourceType);
            }
        }

        foreach (var t in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic>())
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.Dynamic.RunSyntax(t, input);
        }

        var group = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.LoopGroupBy>();
        if (group != null)
        {
            global::ScipSharp.Indexer.Abap.Syntax.Expressions.LoopGroupBy.RunSyntax(group, input);
        }
    }
}
