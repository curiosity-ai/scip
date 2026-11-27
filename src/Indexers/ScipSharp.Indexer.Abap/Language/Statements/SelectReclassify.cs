// Ported from abaplint packages/core/src/abap/2_statements/_select_reclassify.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Statements;


namespace ScipSharp.Indexer.Abap.Statements;

using E = ScipSharp.Indexer.Abap.Statements.Expressions;
using S = ScipSharp.Indexer.Abap.Statements.Statements;
using Type = System.Type;

internal static class SelectReclassify
{
    private static bool ContainsAggregation(IReadOnlyList<IExpressionOrTokenNode> children)
    {
        foreach (var c in children)
        {
            if (c is not ExpressionNode child)
            {
                continue;
            }
            if (child.Get() is E.SQLAggregation)
            {
                if (child.FindDirectExpression<E.SQLOver>() == null)
                {
                    return true;
                }
                continue;
            }
            if (child.Get() is E.SQLSetOpGroup)
            {
                continue;
            }
            if (child.Get() is E.SQLFrom)
            {
                continue;
            }
            if (ContainsAggregation(child.GetChildren()))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsCountAllPattern(IReadOnlyList<IExpressionOrTokenNode> children)
    {
        foreach (var c in children)
        {
            if (c is not ExpressionNode child) { continue; }
            var e = child.Get();
            if (e is E.SQLGroupBy || e is E.SQLHaving || e is E.SQLOrderBy)
            {
                return false;
            }
        }
        return ContainsAggregation(children);
    }

    private static void ScanForExprTypes(IReadOnlyList<IExpressionOrTokenNode> children, HashSet<Type> found, Type[] targets)
    {
        foreach (var c in children)
        {
            if (found.Count == targets.Length) { return; }
            if (c is not ExpressionNode child) { continue; }
            var e = child.Get();
            if (e is E.SQLSetOpGroup) { continue; }
            foreach (var t in targets)
            {
                if (t.IsInstanceOfType(e)) { found.Add(t); break; }
            }
            ScanForExprTypes(child.GetChildren(), found, targets);
        }
    }

    private static bool IsSelectLoop(StatementNode node)
    {
        var selectExpr = node.FindDirectExpression<E.Select>();
        IReadOnlyList<IExpressionOrTokenNode> top = selectExpr != null ? selectExpr.GetChildren() : [];

        if (top.Any(c => c is TokenNode tn && JsString.ToUpper(tn.Get().GetStr()) == "SINGLE"))
        {
            return false;
        }

        Type[] targets =
        [
            typeof(E.SQLSetOp), typeof(E.SQLPackageSize), typeof(E.SQLIntoTable),
            typeof(E.SQLIntoStructure), typeof(E.SQLIntoList), typeof(E.SQLGroupBy),
        ];
        var found = new HashSet<Type>();
        ScanForExprTypes(top, found, targets);

        bool has(Type t) => found.Contains(t);

        if (has(typeof(E.SQLPackageSize))) { return true; }
        if (has(typeof(E.SQLIntoTable))) { return false; }

        var hasInto = has(typeof(E.SQLIntoTable)) || has(typeof(E.SQLIntoStructure)) || has(typeof(E.SQLIntoList));
        if (!hasInto && IsCountAllPattern(top)) { return false; }
        if (has(typeof(E.SQLSetOp))) { return true; }
        if (!has(typeof(E.SQLGroupBy)) && ContainsAggregation(top)) { return false; }

        return true;
    }

    private static bool IsWithLoop(StatementNode node)
    {
        var selectExpr = node.FindDirectExpression<E.Select>();
        if (selectExpr == null) { return true; }

        Type[] targets = [typeof(E.SQLPackageSize), typeof(E.SQLIntoTable)];
        var found = new HashSet<Type>();
        ScanForExprTypes(selectExpr.GetChildren(), found, targets);

        if (found.Contains(typeof(E.SQLPackageSize))) { return true; }
        if (found.Contains(typeof(E.SQLIntoTable))) { return false; }

        return true;
    }

    public static StatementNode ReclassifySelect(
        StatementNode node,
        IStatement stmt,
        IReadOnlyList<AbstractToken> pragmas)
    {
        var children = node.GetChildren().ToList();
        if (stmt is S.Select || stmt is SelectLoop)
        {
            var loop = IsSelectLoop(node);
            if (loop && stmt is S.Select)
            {
                return new StatementNode(new SelectLoop(), node.GetColon(), pragmas).SetChildren(children);
            }
            else if (!loop && stmt is SelectLoop)
            {
                return new StatementNode(new S.Select(), node.GetColon(), pragmas).SetChildren(children);
            }
        }
        else if (stmt is S.With || stmt is WithLoop)
        {
            var loop = IsWithLoop(node);
            if (loop && stmt is S.With)
            {
                return new StatementNode(new WithLoop(), node.GetColon(), pragmas).SetChildren(children);
            }
            else if (!loop && stmt is WithLoop)
            {
                return new StatementNode(new S.With(), node.GetColon(), pragmas).SetChildren(children);
            }
        }
        return node;
    }
}
