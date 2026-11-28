using ScipSharp.Indexer.Sql.Lexing;
using ScipSharp.Indexer.Sql.Syntax;
using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Sql.Semantics;

/// <summary>Defines the symbol of a select-list alias; receives the alias and the source column symbol (if any).</summary>
internal delegate string AliasDefiner(Ident alias, string? source);

internal sealed partial class Binder
{
    private string LocalAlias(Ident alias, string? source)
    {
        var local = NewLocal();
        Def(alias.Token, local, Kind.Variable, alias.Text, [Code("column alias " + alias.Text)]);
        return local;
    }

    private Shape BindQuery(Query q, Scope parent, AliasDefiner? definer)
    {
        EnsureStack();
        if (q is SetOpQuery top)
        {
            return BindSetOps(top, parent, definer);
        }

        var scope = new Scope(parent);
        BindWith(q.With, scope, q.Recursive);
        Shape shape;
        Scope inner;
        switch (q)
        {
            case SelectQuery s:
                shape = BindSelect(s, scope, definer ?? LocalAlias);
                inner = scope;
                break;
            case ValuesQuery v:
                BindExprs(v.Rows, scope);
                shape = new Shape([.. Enumerable.Range(1, v.Arity).Select(i => new OutCol($"column{i}", $"column{i}", null))], false);
                inner = scope;
                break;
            default:
                return Shape.Unknown;
        }

        BindExprs(q.OrderBy, inner);
        BindExprs(q.Tail, inner);
        return shape;
    }

    /// <summary>
    /// Binds a set-operation chain. The parser builds <c>a UNION b UNION c ...</c> left-deep, so the left spine is walked
    /// iteratively (recursion overflowed the stack on long chains); the bind order is that of the recursive walk.
    /// </summary>
    private Shape BindSetOps(SetOpQuery top, Scope parent, AliasDefiner? definer)
    {
        var spine = new List<(SetOpQuery Op, Scope Scope)>();
        var scope = parent;
        Query q = top;
        while (q is SetOpQuery so)
        {
            // A level without WITH adds nothing to its scope: share the enclosing one so that long chains do not build
            // equally long scope chains (every name lookup walks them).
            var levelScope = spine.Count == 0 || so.With.Count > 0 ? new Scope(scope) : scope;
            BindWith(so.With, levelScope, so.Recursive);
            spine.Add((so, levelScope));
            scope = levelScope;
            q = so.Left;
        }

        var shape = BindQuery(q, scope, definer);
        for (var i = spine.Count - 1; i >= 0; i--)
        {
            var (so, levelScope) = spine[i];
            var right = BindQuery(so.Right, levelScope, null);
            shape.Opaque &= right.Opaque || shape.Columns.Count == 0;
            var inner = new Scope(levelScope);
            inner.SelectAliases.AddRange(shape.Columns.Where(c => c.Symbol is not null));
            BindExprs(so.OrderBy, inner);
            BindExprs(so.Tail, inner);
        }

        return shape;
    }

    private static Source? FindCte(Scope scope, string key)
    {
        for (var s = scope; s is not null; s = s.Parent)
        {
            if (s.Ctes.TryGetValue(key, out var c))
            {
                return c;
            }
        }

        return null;
    }

    private void BindWith(List<Cte> ctes, Scope scope, bool recursive)
    {
        foreach (var cte in ctes)
        {
            var key = cte.Name.Key;
            var local = NewLocal();
            Def(cte.Name.Token, local, Kind.Class, cte.Name.Text, [Code("WITH " + cte.Name.Text + " AS (...)")]);
            List<OutCol>? declared = null;
            if (cte.Columns is { } names)
            {
                declared = DefineColumnAliases(names, null);
            }

            if (recursive || declared is not null)
            {
                scope.Ctes[key] = new Source { Key = key, TableKey = key, AliasSymbol = local, Columns = declared ?? [], Opaque = declared is null };
            }

            var shape = cte.Query is not null ? BindQuery(cte.Query, scope, null)
                : cte.Dml is not null ? BindStmtShape(cte.Dml, scope)
                : Shape.Unknown;
            if (declared is not null)
            {
                for (var i = 0; i < declared.Count && i < shape.Columns.Count; i++)
                {
                    declared[i] = new OutCol(declared[i].Key, declared[i].Name, declared[i].Symbol) { Source = shape.Columns[i].Symbol };
                }
            }

            scope.Ctes[key] = new Source
            {
                Key = key,
                TableKey = key,
                AliasSymbol = local,
                Columns = declared ?? shape.Columns,
                Opaque = declared is null && shape.Opaque,
            };
        }
    }

    private List<OutCol> DefineColumnAliases(List<Ident> names, List<OutCol>? baseCols)
    {
        var result = new List<OutCol>();
        for (var i = 0; i < names.Count; i++)
        {
            var id = names[i];
            var local = NewLocal();
            Def(id.Token, local, Kind.Variable, id.Text, [Code("column alias " + id.Text)]);
            result.Add(new OutCol(id.Key, id.Text, local) { Source = baseCols is not null && i < baseCols.Count ? baseCols[i].Symbol : null });
        }

        return result;
    }

    private Shape BindSelect(SelectQuery s, Scope scope, AliasDefiner definer)
    {
        foreach (var r in s.From)
        {
            AddSource(r, scope);
        }

        BindExprs(s.DistinctOn, scope);
        BindExprs(s.Top, scope);
        var cols = new List<OutCol>();
        var opaque = false;
        foreach (var item in s.Items)
        {
            if (item.Star is { } star)
            {
                if (star.Qualifier is { } q)
                {
                    var schema = q.Parts.Count >= 2 ? q.Parts[^2].Key : null;
                    if (FindSource(scope, q.Last.Key, schema) is { } src)
                    {
                        EmitSourceQualifier(src, q.Last);
                        opaque |= Expand(src, cols, star.Star);
                    }
                    else
                    {
                        opaque = true;
                    }
                }
                else
                {
                    if (scope.Sources.Count == 0)
                    {
                        opaque = true;
                    }

                    foreach (var src in scope.Sources)
                    {
                        opaque |= Expand(src, cols, star.Star);
                    }
                }

                continue;
            }

            var sym = BindExpr(item.Expr, scope);
            if (item.AssignTarget is { Kind: TokenKind.Variable } target)
            {
                ResolveVarRef(new VarRef(target, null));
                continue;
            }

            if (item.Alias is { } alias)
            {
                var aliasSym = definer(alias, sym);
                var oc = new OutCol(alias.Key, alias.Text, aliasSym) { Source = sym, Aliased = true, At = alias.Token };
                cols.Add(oc);
                scope.SelectAliases.Add(oc);
            }
            else if (item.Expr.Items is [NameRef nr])
            {
                cols.Add(new OutCol(nr.Name.Last.Key, nr.Name.Last.Text, sym) { Source = sym, At = nr.Name.Last.Token });
            }
            else if (item.Expr.Items is [VarRef vr] && vr.Field is { } field)
            {
                cols.Add(new OutCol(field.Key, field.Text, sym) { Source = sym, At = field.Token });
            }
            else if (item.Expr.Items is [FuncCall fc])
            {
                cols.Add(new OutCol(fc.Name.Last.Key, fc.Name.Last.Text, null));
            }
            else
            {
                cols.Add(new OutCol("?column?", "?column?", null));
            }
        }

        var shape = new Shape(cols, opaque);
        foreach (var into in s.Into)
        {
            BindInto(into, shape);
        }

        BindExpr(s.Where, scope);
        BindExprs(s.GroupBy, scope);
        BindExpr(s.Having, scope);
        foreach (var (_, spec) in s.Windows)
        {
            BindExpr(spec, scope);
        }

        BindExprs(s.Misc, scope);
        return shape;
    }

    /// <summary>Appends the columns of <paramref name="src"/> (for <c>*</c>); returns true when they are not fully known.</summary>
    private bool Expand(Source src, List<OutCol> into, Token star)
    {
        if (src.Columns is { } cols)
        {
            into.AddRange(cols.Select(c => new OutCol(c.Key, c.Name, c.Symbol) { Source = c.Symbol, At = star }));
            return src.Opaque;
        }

        if (src.Object is { } o)
        {
            into.AddRange(ColumnsOf(o).Select(c => new OutCol(c.Key, c.Name, c.Symbol) { Source = c.Symbol, At = star }));
            return o.Opaque || o.Unresolved || o.Columns.Count == 0;
        }

        return true;
    }

    /// <summary><c>SELECT ... INTO target</c>: variables in procedural code, otherwise a new table.</summary>
    private void BindInto(Expr into, Shape shape)
    {
        switch (into.Items)
        {
            case [VarRef v]:
                ResolveVarRef(v);
                return;
            case [NameRef { Name.Parts.Count: 1 } n] when FindVar(n.Name.Last.Key) is { } var:
                Ref(n.Name.Last.Token, var.Symbol, Kind.Variable);
                return;
            case [NameRef n] when RoutineCtx() is null || _d == SqlDialect.TSql:
                {
                    var obj = CreateRelation(n.Name, ObjectKind.Table, "TABLE");
                    DefineShapeColumns(obj, shape, null);
                    return;
                }

            default:
                BindExpr(into, new Scope(null));
                return;
        }
    }

    private string? DefineAlias(TableRef r, string what)
    {
        if (r.Alias is not { } a || r is JoinRef)
        {
            return null;
        }

        var local = NewLocal();
        Def(a.Token, local, Kind.Variable, a.Text, [Code(a.Text + ": " + what)]);
        return local;
    }

    private void AddSource(TableRef r, Scope scope)
    {
        EnsureStack();
        if (r is JoinRef)
        {
            // Join chains are left-deep (a JOIN b JOIN c ...): walk the left spine iteratively, innermost join first.
            var joins = new Stack<JoinRef>();
            var leftmost = r;
            while (leftmost is JoinRef jr)
            {
                joins.Push(jr);
                leftmost = jr.Left;
            }

            AddSource(leftmost, scope);
            while (joins.TryPop(out var j))
            {
                AddSource(j.Right, scope);
                BindExpr(j.On, scope);
                if (j.Using is { } usingCols)
                {
                    foreach (var id in usingCols)
                    {
                        ResolveUnqualified(id, scope);
                    }
                }

                BindExprs(j.Misc, scope);
            }

            return;
        }

        switch (r)
        {
            case NamedTableRef n:
                {
                    var name = n.Name;
                    if (name.Parts.Count == 0)
                    {
                        return;
                    }

                    var key = name.Last.Key;
                    if (name.Parts.Count == 1 && !name.IsTemplate && FindCte(scope, key) is { } cte)
                    {
                        Ref(name.Last.Token, cte.AliasSymbol!, Kind.Class);
                        var alias = DefineAlias(r, "WITH " + name.Last.Text);
                        var cols = r.ColumnAliases is { } ca ? DefineColumnAliases(ca, cte.Columns) : cte.Columns;
                        scope.Sources.Add(new Source { Key = r.Alias?.Key ?? key, TableKey = key, AliasSymbol = alias ?? (r.Alias is null ? cte.AliasSymbol : null), Columns = cols, Opaque = cte.Opaque && r.ColumnAliases is null });
                        break;
                    }

                    if (name.Parts.Count == 1 && !name.Last.Quoted && name.Last.Text.Equals("dual", StringComparison.OrdinalIgnoreCase) && FindRelation(name) is null)
                    {
                        return;
                    }

                    if (name.Parts.Count == 1 && IsTriggerAlias(key) && TriggerTable() is { } triggerTable)
                    {
                        // T-SQL inserted/deleted, PostgreSQL REFERENCING NEW TABLE AS x transition tables.
                        var pseudoAlias = DefineAlias(r, name.Last.Text);
                        scope.Sources.Add(new Source { Key = r.Alias?.Key ?? key, TableKey = key, AliasSymbol = pseudoAlias, Object = triggerTable, Pseudo = pseudoAlias is null });
                        break;
                    }

                    var obj = ResolveRelationRef(name);
                    var aliasSym = DefineAlias(r, name.ToString());
                    var aliasCols = r.ColumnAliases is { } cas ? DefineColumnAliases(cas, ColumnsOf(obj)) : null;
                    scope.Sources.Add(new Source
                    {
                        Key = r.Alias?.Key ?? obj.NameKey,
                        TableKey = obj.NameKey,
                        SchemaKey = obj.SchemaKey,
                        AliasSymbol = aliasSym,
                        Object = obj,
                        Columns = aliasCols,
                    });
                    break;
                }

            case DerivedTableRef d:
                {
                    var shape = BindQuery(d.Query, scope, null);
                    var aliasSym = DefineAlias(r, "derived table");
                    var cols = r.ColumnAliases is { } ca ? DefineColumnAliases(ca, shape.Columns) : shape.Columns;
                    scope.Sources.Add(new Source { Key = r.Alias?.Key, AliasSymbol = aliasSym, Columns = cols, Opaque = shape.Opaque && r.ColumnAliases is null });
                    break;
                }

            case FunctionTableRef f:
                {
                    var routine = BindCall(f.Call, scope, returnRoutine: true);
                    var aliasSym = DefineAlias(r, f.Call.Name + "(...)");
                    List<OutCol>? cols = null;
                    var opaque = true;
                    if (routine?.ResultColumns is { } rc)
                    {
                        cols = [.. rc.Select(c => new OutCol(c.Key, c.Name, c.Symbol) { Source = c.Symbol })];
                        opaque = false;
                    }
                    else if (routine?.ResultRelationSymbol is { } rel && LookupBySymbol(rel) is { } relObj)
                    {
                        cols = ColumnsOf(relObj);
                        opaque = relObj.Opaque;
                    }

                    if (r.ColumnAliases is { } ca)
                    {
                        cols = DefineColumnAliases(ca, cols);
                        opaque = false;
                    }

                    scope.Sources.Add(new Source { Key = r.Alias?.Key ?? f.Call.Name.Last.Key, AliasSymbol = aliasSym, Columns = cols ?? [], Opaque = opaque });
                    break;
                }

            case VariableTableRef v:
                {
                    var info = ResolveVarRef(new VarRef(v.Token, null)) is not null ? FindVar(_d.Normalize(v.Token.Text, quoted: false)) : null;
                    var aliasSym = DefineAlias(r, v.Token.Text);
                    scope.Sources.Add(new Source
                    {
                        Key = r.Alias?.Key ?? _d.Normalize(v.Token.Text, quoted: false),
                        AliasSymbol = aliasSym ?? info?.Symbol,
                        Columns = info?.Fields ?? [],
                        Object = info?.Row,
                        Opaque = info?.Fields is null,
                    });
                    break;
                }

        }

        BindExprs(r.Misc, scope);
    }

    // ------------------------------------------------------------ DML

    /// <summary>Binds a data-modifying statement and returns the shape of its RETURNING / OUTPUT list.</summary>
    private Shape BindStmtShape(Stmt s, Scope parent) => s switch
    {
        InsertStmt i => BindInsert(i, parent),
        UpdateStmt u => BindUpdate(u, parent),
        DeleteStmt d => BindDelete(d, parent),
        MergeStmt m => BindMerge(m, parent),
        _ => Shape.Unknown,
    };

    private Source? AddTarget(TableRef r, Scope scope)
    {
        var before = scope.Sources.Count;
        AddSource(r, scope);
        return scope.Sources.Count > before ? scope.Sources[before] : null;
    }

    private Shape BindInsert(InsertStmt ins, Scope? parent)
    {
        var scope = new Scope(parent);
        BindWith(ins.With, scope, false);
        if (ins.Source is { } source)
        {
            BindQuery(source, scope, null);
        }

        var targetScope = new Scope(scope);
        var target = AddTarget(ins.Target, targetScope);
        if (ins.Columns is { } cols && target is not null)
        {
            foreach (var id in cols)
            {
                EmitColumn(target, id);
            }
        }

        BindSets(ins.Sets, targetScope, target);
        var conflictScope = new Scope(targetScope);
        if (target?.Object is { } obj)
        {
            conflictScope.Sources.Add(new Source { Key = "excluded", TableKey = "excluded", Object = obj, Pseudo = true });
            if (ins.RowAlias is { } rowAlias)
            {
                conflictScope.Sources.Add(new Source { Key = rowAlias.Key, Object = obj, Pseudo = true });
            }
        }

        BindExprs(ins.ConflictExprs, conflictScope);
        BindSets(ins.ConflictSets, conflictScope, target);
        return BindReturning(ins.Returning, targetScope, target);
    }

    private void BindSets(List<SetItem> sets, Scope scope, Source? primary)
    {
        foreach (var item in sets)
        {
            foreach (var t in item.Targets)
            {
                if (primary is not null && t.Items is [NameRef { Name.Parts.Count: 1 } n])
                {
                    if (EmitColumn(primary, n.Name.Last) is null)
                    {
                        BindExpr(t, scope);
                    }
                }
                else
                {
                    BindExpr(t, scope);
                }
            }

            BindExpr(item.Value, scope);
        }
    }

    private Shape BindReturning(List<Expr> returning, Scope scope, Source? primary)
    {
        if (returning.Count == 0)
        {
            return Shape.Unknown;
        }

        var outScope = new Scope(scope);
        if (primary?.Object is { } obj)
        {
            outScope.Sources.Add(new Source { Key = "inserted", TableKey = "inserted", Object = obj, Pseudo = true });
            outScope.Sources.Add(new Source { Key = "deleted", TableKey = "deleted", Object = obj, Pseudo = true });
        }

        var cols = new List<OutCol>();
        var opaque = false;
        foreach (var e in returning)
        {
            if (e.Items is [StarRef s] && primary is not null)
            {
                opaque |= Expand(primary, cols, s.Star);
                continue;
            }

            var sym = BindExpr(e, outScope);
            if (e.Items is [NameRef nr])
            {
                cols.Add(new OutCol(nr.Name.Last.Key, nr.Name.Last.Text, sym) { Source = sym });
            }
        }

        return new Shape(cols, opaque);
    }

    private Shape BindUpdate(UpdateStmt u, Scope? parent)
    {
        var scope = new Scope(parent);
        BindWith(u.With, scope, false);
        foreach (var f in u.From)
        {
            AddSource(f, scope);
        }

        Source? primary = null;
        foreach (var t in u.Targets)
        {
            if (u.From.Count > 0 && t is NamedTableRef { Alias: null } n && n.Name.Parts.Count == 1 && FindSource(scope, n.Name.Last.Key) is { } existing)
            {
                EmitSourceQualifier(existing, n.Name.Last);
                primary ??= existing;
                continue;
            }

            var src = AddTarget(t, scope);
            primary ??= src;
        }

        BindSets(u.Sets, scope, primary);
        BindExpr(u.Where, scope);
        return BindReturning(u.Returning, scope, primary);
    }

    private Shape BindDelete(DeleteStmt d, Scope? parent)
    {
        var scope = new Scope(parent);
        BindWith(d.With, scope, false);
        foreach (var f in d.From)
        {
            AddSource(f, scope);
        }

        foreach (var t in d.Targets)
        {
            if (t.Parts.Count == 1 && FindSource(scope, t.Last.Key) is { } existing)
            {
                EmitSourceQualifier(existing, t.Last);
            }
            else
            {
                ResolveRelationRef(t);
            }
        }

        BindExpr(d.Where, scope);
        return BindReturning(d.Returning, scope, scope.Sources.FirstOrDefault());
    }

    private Shape BindMerge(MergeStmt m, Scope? parent)
    {
        var scope = new Scope(parent);
        BindWith(m.With, scope, false);
        var target = AddTarget(m.Target, scope);
        if (m.Source is { } source)
        {
            AddSource(source, scope);
        }

        BindExpr(m.On, scope);
        foreach (var c in m.Clauses)
        {
            BindExpr(c.Condition, scope);
            BindSets(c.Sets, scope, target);
            if (c.InsertColumns is { } cols && target is not null)
            {
                foreach (var id in cols)
                {
                    EmitColumn(target, id);
                }
            }

            BindExpr(c.Values, scope);
        }

        return BindReturning(m.Returning, scope, target);
    }
}
