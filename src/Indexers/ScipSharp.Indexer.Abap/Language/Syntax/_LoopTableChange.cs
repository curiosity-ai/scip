// Ported from abaplint packages/core/src/abap/5_syntax/_loop_table_change.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax;

/** In a method, the body of the table an enclosing LOOP AT walks cannot be replaced or freed,
 *  "you cannot access the entire table body in change mode in a loop. If this is known statically,
 *  a syntax error occurs in classes", see the ABAP keyword documentation for LOOP AT itab.
 *  Only tables named directly are handled, a field symbol or a reference is a runtime matter. */
public class LoopTableChange
{
    private readonly List<TypedIdentifier> tables = new List<TypedIdentifier>();

    /** returns true if the LOOP structure was pushed, then pop() must be called after it */
    public bool Push(StructureNode node, CurrentScope scope)
    {
        if (scope.GetType() != ScopeType.Method)
        {
            return false;
        }
        var loop = node.FindDirectStatement<Loop>();
        if (loop == null || loop.FindDirectExpression<LoopGroupBy>() != null)
        {
            return false;
        }
        var chain = loop.FindDirectExpression<LoopSource>()?.FindDirectExpression<SimpleSource2>()?.FindDirectExpression<FieldChain>();
        var children = chain?.GetChildren() ?? new List<IExpressionOrTokenNode>();
        if (children.Count != 1 && !((children.Count == 2 && children.ElementAtOrDefault(1).Get() is TableBody)))
        {
            return false;
        }
        var found = LoopTableChange.FindTable(children.ElementAtOrDefault(0), scope);
        if (found == null)
        {
            return false;
        }
        this.tables.Add(found);
        return true;
    }

    public void Pop()
    {
        JsUtils.Pop(this.tables);
    }

    public void Check(StatementNode node, SyntaxInput input)
    {
        if (this.tables.Count == 0 || input.Scope.GetType() != ScopeType.Method)
        {
            return;
        }

        // DELETE itab WHERE is not refused, abapGit does it in zcl_abapgit_syntax_xml->order_matches,
        // SORT is left out, it is not certain that it is checked statically
        var s = node.Get();
        if (!((s is Clear)) && !((s is Free)) && !((s is Refresh)) && !((s is Move)))
        {
            return;
        }

        foreach (var target in node.FindDirectExpressions<Target>())
        {
            var children = target.GetChildren();
            var body = children.Count == 2 && children.ElementAtOrDefault(1).Get() is TableBody;
            if (children.Count != 1 && body == false)
            {
                continue;
            }
            else if (!((children.ElementAtOrDefault(0).Get() is TargetField)))
            {
                continue;
            }
            var found = LoopTableChange.FindTable(children.ElementAtOrDefault(0), input.Scope);
            if (found == null || this.tables.Contains(found) == false)
            {
                continue;
            }
            else if ((((TableType)found.GetType())).IsWithHeader() == true && body == false)
            {
                // without [] the statement works on the header line
                continue;
            }
            var message = $"\"{found.GetName()}\" cannot be changed as a whole inside a LOOP over it";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
            return;
        }
    }

    private static TypedIdentifier? FindTable(IExpressionOrTokenNode? node, CurrentScope scope)
    {
        if (!((node is ExpressionNode)) || ((ExpressionNode)node).GetChildren().Count != 1 || !(((((ExpressionNode)node).GetFirstChild()?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Field)))
        {
            return null;
        }
        var found = scope.FindVariable(((ExpressionNode)node).ConcatTokens());
        if (found == null || !((found.GetType() is TableType)))
        {
            return null;
        }
        return found;
    }
}
