using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Syntax;

public sealed partial class SqlParser
{
    private static readonly HashSet<string> NotAliases =
    [
        "LIMIT", "OFFSET", "FETCH", "WINDOW", "QUALIFY", "PIVOT", "UNPIVOT", "SAMPLE", "TABLESAMPLE", "USE", "FORCE", "IGNORE",
        "PARTITION", "START", "CONNECT", "LOCK", "OPTION", "RETURNING", "NATURAL", "OUTER", "CROSS", "SEMI", "ANTI", "INNER",
        "LEFT", "RIGHT", "FULL", "JOIN", "APPLY", "ON", "USING", "WHERE", "GROUP", "ORDER", "HAVING", "UNION", "EXCEPT",
        "INTERSECT", "MINUS", "INTO", "VALUES", "SET", "FROM", "WITH", "FOR", "LOOP", "THEN", "OVERRIDING", "DEFAULT",
        "EXCEPTION", "MODEL", "MATCH_RECOGNIZE", "HASH", "MERGE", "ASOF", "GLOBAL", "LATERAL", "OUTPUT", "LANGUAGE",
        "RETURNS", "AS", "WHEN", "INCLUDE", "NOLOCK", "ONLY", "SEARCH", "CYCLE", "ASC", "DESC",
    ];

    private static bool IsAliasCandidate(Token t) =>
        t.Kind == TokenKind.QuotedIdent
        || (t.Kind == TokenKind.Word && !Keywords.IsReserved(t.Upper) && !Keywords.ClauseStops.Contains(t.Upper) && !NotAliases.Contains(t.Upper));

    private QueryStmt ParseQueryStatement(bool perform = false)
    {
        if (perform)
        {
            // PERFORM is SELECT without a result: parse it as one.
            return new QueryStmt(ParseSelect());
        }

        return new QueryStmt(ParseQuery());
    }

    private Stmt? ParseWithStatement()
    {
        var ctes = new List<Cte>();
        var recursive = ParseWithClause(ctes);
        List<Cte>? target = null;
        Stmt? stmt = null;
        switch (Cur.Upper)
        {
            case "INSERT":
                var ins = ParseInsert();
                target = ins.With;
                stmt = ins;
                break;
            case "UPDATE":
                var upd = ParseUpdate();
                target = upd.With;
                stmt = upd;
                break;
            case "DELETE":
                var del = ParseDelete();
                target = del.With;
                stmt = del;
                break;
            case "MERGE":
                var merge = ParseMerge();
                target = merge?.With;
                stmt = merge;
                break;
        }

        if (stmt is not null)
        {
            target!.InsertRange(0, ctes);
            return stmt;
        }

        var q = ParseSetOps();
        q.With.InsertRange(0, ctes);
        q.Recursive |= recursive;
        ParseOrderLimit(q);
        return new QueryStmt(q);
    }

    private Query ParseQuery()
    {
        var ctes = new List<Cte>();
        var recursive = Is("WITH") && ParseWithClause(ctes);
        var q = ParseSetOps();
        q.With.InsertRange(0, ctes);
        q.Recursive |= recursive;
        ParseOrderLimit(q);
        return q;
    }

    private bool ParseWithClause(List<Cte> ctes)
    {
        _p++; // WITH
        var recursive = Accept("RECURSIVE");
        while (!AtEnd)
        {
            var name = ParseIdent(allowReserved: false);
            if (name is null)
            {
                break;
            }

            List<Ident>? cols = IsOp("(") ? ParseIdentList() : null;
            Accept("AS");
            Accept("NOT");
            Accept("MATERIALIZED");
            Query? q = null;
            Stmt? dml = null;
            if (IsOp("("))
            {
                _p++;
                if (IsQueryStart())
                {
                    q = ParseQuery();
                }
                else if (Cur.Upper is "INSERT" or "UPDATE" or "DELETE" or "MERGE")
                {
                    var start = _p;
                    dml = ParseStatement();
                    if (dml is not null)
                    {
                        Finish(dml, start);
                    }
                }

                SkipToClose();
            }

            // PostgreSQL SEARCH / CYCLE clauses.
            while (Is("SEARCH") || Is("CYCLE"))
            {
                while (!AtEnd && !IsOp(",") && !IsQueryStart() && !Cur.IsTerminator && !(Cur.Upper is "INSERT" or "UPDATE" or "DELETE" or "MERGE"))
                {
                    SkipBalanced();
                }
            }

            ctes.Add(new Cte(name, cols, q, dml));
            if (!AcceptOp(","))
            {
                break;
            }
        }

        return recursive;
    }

    private Query ParseSetOps()
    {
        var left = ParseQueryPrimary();
        while (Cur.IsWord && Cur.Upper is "UNION" or "INTERSECT" or "EXCEPT" or "MINUS")
        {
            var op = Cur.Upper;
            _p++;
            if (!Accept("ALL"))
            {
                Accept("DISTINCT");
            }

            Accept("CORRESPONDING");
            var right = ParseQueryPrimary();
            left = new SetOpQuery(left, right, op);
        }

        return left;
    }

    private Query ParseQueryPrimary()
    {
        if (IsOp("(") && IsQueryStart(1))
        {
            _p++;
            var q = ParseQuery();
            SkipToClose();
            return q;
        }

        if (Is("SELECT"))
        {
            return ParseSelect();
        }

        if (Is("VALUES"))
        {
            return ParseValues();
        }

        if (Is("WITH"))
        {
            return ParseQuery();
        }

        if (Is("TABLE") && At(1).IsName)
        {
            _p++;
            var s = new SelectQuery();
            if (ParseQName() is { } name)
            {
                s.Items.Add(new SelectItem(Expr.Empty, null, new StarRef(null, Prev)));
                s.From.Add(new NamedTableRef(name));
            }

            return s;
        }

        return new SelectQuery();
    }

    private ValuesQuery ParseValues()
    {
        _p++; // VALUES
        var rows = new List<Expr>();
        var arity = 0;
        while (!AtEnd)
        {
            Accept("ROW");
            if (!IsOp("("))
            {
                break;
            }

            var items = new List<ExprItem>();
            _p++;
            var n = 0;
            while (!AtEnd && !IsOp(")") && !Cur.IsTerminator)
            {
                var before = _p;
                items.AddRange(ParseExpr(paren: true).Items);
                n++;
                if (!AcceptOp(",") && _p == before)
                {
                    _p++;
                }
            }

            AcceptOp(")");
            if (rows.Count == 0)
            {
                arity = n;
            }

            rows.Add(new Expr(items));
            if (!AcceptOp(","))
            {
                break;
            }
        }

        return new ValuesQuery(rows) { Arity = arity };
    }

    private void ParseOrderLimit(Query q)
    {
        while (!AtEnd)
        {
            if (Is("ORDER") && IsAt(1, "BY"))
            {
                _p += 2;
                Accept("SIBLINGS");
                ParseOrderItems(q.OrderBy);
            }
            else if (Is("LIMIT"))
            {
                _p++;
                q.Tail.Add(ParseExpr());
                if (AcceptOp(","))
                {
                    q.Tail.Add(ParseExpr());
                }
            }
            else if (Is("OFFSET"))
            {
                _p++;
                q.Tail.Add(ParseLimitExpr());
                if (!Accept("ROWS"))
                {
                    Accept("ROW");
                }
            }
            else if (Is("FETCH") && At(1).Upper is "FIRST" or "NEXT")
            {
                _p += 2;
                q.Tail.Add(ParseLimitExpr());
                while (Cur.IsWord && Cur.Upper is "ROW" or "ROWS" or "ONLY" or "WITH" or "TIES" or "PERCENT")
                {
                    _p++;
                }
            }
            else if (Is("FOR") && At(1).Upper is "UPDATE" or "SHARE" or "NO" or "KEY" or "XML" or "JSON" or "BROWSE" or "READ")
            {
                _p++;
                while (!AtEnd && !Cur.IsTerminator && !IsOp(")") && !(Cur.IsWord && Cur.Upper is "UNION" or "INTERSECT" or "EXCEPT" or "OPTION" or "INTO" or "LOOP"))
                {
                    if (Cur.IsWord && Keywords.StatementStarts.Contains(Cur.Upper) && !Cur.Is("UPDATE"))
                    {
                        break;
                    }

                    SkipBalanced();
                }
            }
            else if (Is("OPTION") && At(1).IsOp("("))
            {
                _p++;
                SkipBalanced();
            }
            else if (Is("LOCK") && IsAt(1, "IN"))
            {
                _p += 4; // LOCK IN SHARE MODE
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>An OFFSET / FETCH count, which is followed by <c>ROW[S] [ONLY]</c> keywords rather than an alias.</summary>
    private Expr ParseLimitExpr()
    {
        var save = _stop;
        _stop = static t => t.IsWord && t.Upper is "ROW" or "ROWS" or "ONLY" or "PERCENT" or "WITH" or "FETCH";
        try
        {
            return ParseExpr();
        }
        finally
        {
            _stop = save;
        }
    }

    private void ParseOrderItems(List<Expr> into)
    {
        while (!AtEnd)
        {
            var e = ParseExpr();
            into.Add(e);
            while (Cur.IsWord && Cur.Upper is "ASC" or "DESC" or "NULLS" or "FIRST" or "LAST")
            {
                _p++;
            }

            if (Is("USING"))
            {
                _p += 2;
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }
    }

    private SelectQuery ParseSelect()
    {
        var s = new SelectQuery();
        _p++; // SELECT / PERFORM
        while (Cur.IsWord)
        {
            if (Is("DISTINCT") && IsAt(1, "ON") && At(2).IsOp("("))
            {
                _p += 2;
                var items = new List<ExprItem>();
                ParseParenGroup(items);
                s.DistinctOn.Add(new Expr(items));
                continue;
            }

            if (Is("TOP"))
            {
                _p++;
                if (IsOp("("))
                {
                    var items = new List<ExprItem>();
                    ParseParenGroup(items);
                    s.Top.Add(new Expr(items));
                }
                else
                {
                    _p++;
                }

                Accept("PERCENT");
                AcceptSeq("WITH", "TIES");
                continue;
            }

            if (Cur.Upper is "ALL" or "DISTINCT" or "DISTINCTROW" or "UNIQUE" or "HIGH_PRIORITY" or "STRAIGHT_JOIN" or "SQL_CALC_FOUND_ROWS"
                or "SQL_NO_CACHE" or "SQL_CACHE" or "SQL_SMALL_RESULT" or "SQL_BIG_RESULT" or "SQL_BUFFER_RESULT")
            {
                _p++;
                continue;
            }

            break;
        }

        while (!AtEnd)
        {
            if (Is("FROM") || Is("INTO") || Cur.IsTerminator)
            {
                break;
            }

            if (ParseSelectItem() is { } item)
            {
                s.Items.Add(item);
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }

        while (!AtEnd)
        {
            if (Is("INTO") || AcceptSeq("BULK", "COLLECT") && Is("INTO"))
            {
                _p++;
                while (Cur.IsWord && Cur.Upper is "TEMP" or "TEMPORARY" or "UNLOGGED" or "TABLE" or "OUTFILE" or "DUMPFILE" or "LOCAL")
                {
                    _p++;
                }

                while (!AtEnd)
                {
                    var before = _p;
                    var e = ParseExpr();
                    if (e.Items.Count > 0)
                    {
                        s.Into.Add(e);
                    }

                    if (!AcceptOp(",") || _p == before)
                    {
                        break;
                    }
                }
            }
            else if (Accept("FROM"))
            {
                s.From.AddRange(ParseFromList());
            }
            else if (Accept("WHERE"))
            {
                if (AcceptSeq("CURRENT", "OF"))
                {
                    _p++;
                }
                else
                {
                    s.Where = ParseExpr();
                }
            }
            else if (Is("GROUP") && IsAt(1, "BY"))
            {
                _p += 2;
                if (!Accept("ALL"))
                {
                    Accept("DISTINCT");
                }

                while (!AtEnd)
                {
                    s.GroupBy.Add(ParseExpr());
                    if (!AcceptOp(","))
                    {
                        break;
                    }
                }

                if (Is("WITH") && At(1).Upper is "ROLLUP" or "CUBE")
                {
                    _p += 2;
                }
            }
            else if (Accept("HAVING"))
            {
                s.Having = ParseExpr();
            }
            else if (Is("WINDOW") && At(1).IsName)
            {
                _p++;
                while (ParseIdent() is { } w)
                {
                    Accept("AS");
                    var items = new List<ExprItem>();
                    if (IsOp("("))
                    {
                        ParseParenGroup(items);
                    }

                    s.Windows.Add((w, new Expr(items)));
                    if (!AcceptOp(","))
                    {
                        break;
                    }
                }
            }
            else if (Accept("QUALIFY"))
            {
                s.Misc.Add(ParseExpr());
            }
            else if (AcceptSeq("START", "WITH") || AcceptSeq("CONNECT", "BY"))
            {
                Accept("NOCYCLE");
                Accept("PRIOR");
                s.Misc.Add(ParseExpr());
            }
            else
            {
                break;
            }
        }

        return s;
    }

    private SelectItem? ParseSelectItem()
    {
        if (IsOp("*"))
        {
            var star = Cur;
            _p++;
            SkipStarModifiers();
            return new SelectItem(Expr.Empty, null, new StarRef(null, star));
        }

        Token? assign = null;
        if (_d == SqlDialect.TSql && (Cur.Kind == TokenKind.Variable || IsNameToken(Cur)) && At(1).IsOp("=") && !At(2).IsOp("="))
        {
            assign = Cur;
            _p += 2;
        }

        var e = ParseExpr();
        if (e.Items is [StarRef { Qualifier: not null } qs])
        {
            SkipStarModifiers();
            return new SelectItem(e, null, qs);
        }

        Ident? alias = null;
        if (Accept("AS"))
        {
            if (Cur.Kind == TokenKind.String)
            {
                alias = new Ident(Cur, Cur.Value, _d.Normalize(Cur.Value, quoted: true), true);
                _p++;
            }
            else
            {
                alias = ParseIdent(allowReserved: true);
            }
        }
        else if (IsAliasCandidate(Cur))
        {
            alias = MakeIdent(Cur);
            _p++;
        }
        else if (Cur.Kind == TokenKind.String && _d == SqlDialect.TSql)
        {
            alias = new Ident(Cur, Cur.Value, _d.Normalize(Cur.Value, quoted: true), true);
            _p++;
        }

        if (assign is not null && assign.Kind != TokenKind.Variable && alias is null)
        {
            alias = MakeIdent(assign);
            assign = null;
        }

        return new SelectItem(e, alias, null) { AssignTarget = assign };
    }

    private void SkipStarModifiers()
    {
        while (Cur.IsWord && Cur.Upper is "EXCEPT" or "EXCLUDE" or "REPLACE" or "RENAME" && At(1).IsOp("("))
        {
            _p++;
            SkipBalanced();
        }
    }

    private List<TableRef> ParseFromList()
    {
        var list = new List<TableRef>();
        while (!AtEnd)
        {
            var r = ParseJoinChain();
            if (r is null)
            {
                break;
            }

            list.Add(r);
            if (!AcceptOp(","))
            {
                break;
            }
        }

        return list;
    }

    private TableRef? ParseJoinChain()
    {
        var left = ParseTablePrimary();
        if (left is null)
        {
            return null;
        }

        while (!AtEnd)
        {
            var save = _p;
            var sawJoinWord = false;
            while (Cur.IsWord && Cur.Upper is "NATURAL" or "INNER" or "LEFT" or "RIGHT" or "FULL" or "OUTER" or "CROSS" or "SEMI" or "ANTI" or "HASH" or "LOOP" or "MERGE" or "REMOTE" or "ASOF" or "GLOBAL" or "ANY" or "POSITIONAL" or "REDUCE")
            {
                _p++;
                sawJoinWord = true;
            }

            if ((!sawJoinWord || !Accept("APPLY")) && !Accept("JOIN") && !Accept("STRAIGHT_JOIN"))
            {
                _p = save;
                break;
            }

            var right = ParseTablePrimary();
            if (right is null)
            {
                break;
            }

            Expr? on = null;
            List<Ident>? usingCols = null;
            if (Accept("ON"))
            {
                on = ParseExpr();
            }
            else if (Accept("USING"))
            {
                usingCols = ParseIdentList();
            }

            left = new JoinRef(left, right, on, usingCols);
        }

        return left;
    }

    private TableRef? ParseTablePrimary()
    {
        var lateral = Accept("LATERAL");
        Accept("ONLY");
        TableRef? r;
        if (IsOp("("))
        {
            if (IsQueryStart(1))
            {
                _p++;
                var q = ParseQuery();
                SkipToClose();
                r = new DerivedTableRef(q, lateral);
            }
            else if (IsAt(1, "VALUES"))
            {
                _p++;
                var v = ParseValues();
                SkipToClose();
                r = new DerivedTableRef(v, lateral);
            }
            else
            {
                var save = _p;
                _p++;
                var inner = ParseJoinChain();
                if (inner is null || !IsOp(")"))
                {
                    _p = save;
                    SkipBalanced();
                    return null;
                }

                _p++;
                r = inner;
            }
        }
        else if (Is("VALUES"))
        {
            r = new DerivedTableRef(ParseValues(), false);
        }
        else if (Cur.Kind == TokenKind.Variable)
        {
            r = new VariableTableRef(Cur);
            _p++;
        }
        else if (Is("TABLE") && At(1).IsOp("("))
        {
            var tableTok = Cur;
            _p++;
            r = new FunctionTableRef(ParseFunctionCall(new QName([MakeIdent(tableTok)])));
        }
        else if ((Cur.Kind == TokenKind.Template && Cur.Value.Length > 0) || IsNameToken(Cur) || (Cur.IsWord && Keywords.FunctionLike.Contains(Cur.Upper) && At(1).IsOp("(")))
        {
            var name = ParseQName(allowReserved: true)!;
            if (IsOp("(") && !name.IsTemplate)
            {
                r = new FunctionTableRef(ParseFunctionCall(name));
            }
            else
            {
                r = new NamedTableRef(name);
            }

            if (IsOp("@") && At(1).IsName)
            {
                _p += 2; // Oracle database link
            }
        }
        else
        {
            return null;
        }

        ParseTableModifiers(r);
        if (Accept("AS"))
        {
            r.Alias = ParseIdent(allowReserved: true);
        }
        else if (IsAliasCandidate(Cur))
        {
            r.Alias = MakeIdent(Cur);
            _p++;
        }

        if (r.Alias is not null && IsOp("(") && At(1).IsName)
        {
            r.ColumnAliases = ParseIdentList();
        }

        ParseTableModifiers(r);
        while (Cur.IsWord && Cur.Upper is "PIVOT" or "UNPIVOT" && (At(1).IsOp("(") || At(2).IsOp("(")))
        {
            _p++;
            if (Cur.IsWord)
            {
                _p++; // INCLUDE/EXCLUDE NULLS
            }

            var items = new List<ExprItem>();
            ParseParenGroup(items);
            r.Misc.Add(new Expr(items));
            if (Accept("AS"))
            {
                r.Alias = ParseIdent(allowReserved: true);
            }
            else if (IsAliasCandidate(Cur))
            {
                r.Alias = MakeIdent(Cur);
                _p++;
            }
        }

        return r;
    }

    private void ParseTableModifiers(TableRef r)
    {
        while (!AtEnd)
        {
            if (Is("WITH") && At(1).IsOp("("))
            {
                _p++;
                SkipBalanced();
            }
            else if (AcceptSeq("WITH", "ORDINALITY") || AcceptOp("*"))
            {
            }
            else if (Is("TABLESAMPLE") || Is("SAMPLE"))
            {
                _p++;
                while (Cur.IsWord && !IsAliasCandidate(Cur) && !Keywords.ClauseStops.Contains(Cur.Upper))
                {
                    _p++;
                }

                if (Cur.IsName && At(1).IsOp("("))
                {
                    _p++;
                }

                if (IsOp("("))
                {
                    var items = new List<ExprItem>();
                    ParseParenGroup(items);
                    r.Misc.Add(new Expr(items));
                }

                if (Accept("REPEATABLE"))
                {
                    SkipBalanced();
                }
            }
            else if (Is("PARTITION") && At(1).IsOp("("))
            {
                _p++;
                SkipBalanced();
            }
            else if (Cur.IsWord && Cur.Upper is "USE" or "FORCE" or "IGNORE" && At(1).Upper is "INDEX" or "KEY")
            {
                _p += 2;
                if (AcceptSeq("FOR", "JOIN") || AcceptSeq("FOR", "GROUP", "BY") || AcceptSeq("FOR", "ORDER", "BY"))
                {
                }

                SkipBalanced();
            }
            else if (Is("FOR") && At(1).Upper is "SYSTEM_TIME" or "PORTION")
            {
                _p += 2;
                while (!AtEnd && !Cur.IsTerminator && !(Cur.IsWord && (Keywords.ClauseStops.Contains(Cur.Upper) && !Cur.Is("AS") && !Cur.Is("FROM")) || IsAliasCandidate(Cur) && Prev.Kind != TokenKind.Word))
                {
                    if (Is("AS") && IsAt(1, "OF"))
                    {
                        _p += 2;
                        continue;
                    }

                    if (Is("AS"))
                    {
                        break;
                    }

                    _p++;
                }
            }
            else if (Is("AS") && IsAt(1, "OF"))
            {
                _p += 2;
                ParseExpr();
            }
            else
            {
                break;
            }
        }
    }
}
