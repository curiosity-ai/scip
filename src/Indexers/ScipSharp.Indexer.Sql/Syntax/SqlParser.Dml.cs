using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Syntax;

public sealed partial class SqlParser
{
    // ------------------------------------------------------------ INSERT / UPDATE / DELETE / MERGE

    private void SkipTop()
    {
        if (Is("TOP"))
        {
            _p++;
            SkipBalanced();
            Accept("PERCENT");
        }
    }

    private InsertStmt ParseInsert()
    {
        _p++; // INSERT / UPSERT / REPLACE
        while (Cur.IsWord && Cur.Upper is "LOW_PRIORITY" or "DELAYED" or "HIGH_PRIORITY" or "IGNORE" or "OVERWRITE" or "ALL" or "FIRST")
        {
            _p++;
        }

        if (Is("OR") && At(1).IsWord)
        {
            _p += 2; // INSERT OR REPLACE (SQLite)
        }

        SkipTop();
        Accept("INTO");
        Accept("TABLE");
        TableRef target;
        if (Cur.Kind == TokenKind.Variable)
        {
            target = new VariableTableRef(Cur);
            _p++;
        }
        else if (ParseQName(allowReserved: false) is { } name)
        {
            target = new NamedTableRef(name);
        }
        else
        {
            var g = new InsertStmt(new NamedTableRef(new QName([])));
            SkipToStatementEnd();
            return g;
        }

        ParseTableModifiers(target);
        if (Accept("AS"))
        {
            target.Alias = ParseIdent(allowReserved: true);
        }
        else if (IsAliasCandidate(Cur) && !Cur.Is("OUTPUT") && !(At(1).IsOp("(") && Cur.IsWord && Cur.Upper is "PARTITION"))
        {
            target.Alias = MakeIdent(Cur);
            _p++;
        }

        var ins = new InsertStmt(target);
        if (Is("PARTITION") && At(1).IsOp("("))
        {
            _p++;
            SkipBalanced();
        }

        if (IsOp("(") && !IsQueryStart(1))
        {
            ins.Columns = ParseIdentList();
        }

        while (!StmtEnd)
        {
            if (Is("OUTPUT"))
            {
                ParseOutput(ins.Returning);
                continue;
            }

            if (Is("OVERRIDING"))
            {
                _p += 3;
                continue;
            }

            if (AcceptSeq("DEFAULT", "VALUES"))
            {
                continue;
            }

            if (Is("VALUES") || Is("VALUE"))
            {
                ins.Source = ParseValues();
                if (Accept("AS") && ParseIdent() is { } rowAlias)
                {
                    ins.RowAlias = rowAlias; // MySQL 8 row alias
                }

                continue;
            }

            if (ins.Source is not null && (IsQueryStart() || AtTsqlStatementStart()))
            {
                break; // T-SQL scripts without semicolons
            }

            if (IsQueryStart())
            {
                ins.Source = ParseQuery();
                continue;
            }

            if (Accept("SET"))
            {
                ParseSetList(ins.Sets);
                continue;
            }

            if (AcceptSeq("ON", "CONFLICT"))
            {
                if (IsOp("("))
                {
                    _p++;
                    while (!StmtEnd && !IsOp(")"))
                    {
                        var e = ParseExpr();
                        ins.ConflictExprs.Add(e);
                        SkipClause();
                        if (!AcceptOp(","))
                        {
                            break;
                        }
                    }

                    AcceptOp(")");
                }
                else if (AcceptSeq("ON", "CONSTRAINT"))
                {
                    _p++;
                }

                if (Accept("WHERE"))
                {
                    ins.ConflictExprs.Add(ParseExpr());
                }

                if (Accept("DO"))
                {
                    if (AcceptSeq("UPDATE", "SET"))
                    {
                        ParseSetList(ins.ConflictSets);
                        if (Accept("WHERE"))
                        {
                            ins.ConflictExprs.Add(ParseExpr());
                        }
                    }
                    else
                    {
                        Accept("NOTHING");
                    }
                }

                continue;
            }

            if (AcceptSeq("ON", "DUPLICATE", "KEY", "UPDATE"))
            {
                ParseSetList(ins.ConflictSets);
                continue;
            }

            if (Is("RETURNING") || Is("RETURN"))
            {
                ParseReturning(ins.Returning);
                continue;
            }

            if (Is("EXEC") || Is("EXECUTE") || AtTsqlStatementStart())
            {
                break;
            }

            SkipBalanced();
        }

        return ins;
    }

    /// <summary><c>RETURNING exprs [INTO targets]</c>.</summary>
    private void ParseReturning(List<Expr> into)
    {
        _p++;
        while (!StmtEnd)
        {
            if (IsOp("*"))
            {
                into.Add(new Expr([new StarRef(null, Cur)]));
                _p++;
            }
            else
            {
                into.Add(ParseExpr());
            }

            if (Accept("AS"))
            {
                _p++;
            }
            else if (IsAliasCandidate(Cur))
            {
                _p++;
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }

        if ((Accept("INTO") || (AcceptSeq("BULK", "COLLECT") && Accept("INTO"))) && !StmtEnd)
        {
            while (!StmtEnd)
            {
                into.Add(ParseExpr());
                if (!AcceptOp(","))
                {
                    break;
                }
            }
        }
    }

    /// <summary>T-SQL <c>OUTPUT inserted.x, deleted.y [INTO target (cols)]</c>.</summary>
    private void ParseOutput(List<Expr> into)
    {
        _p++; // OUTPUT
        while (!StmtEnd)
        {
            if (IsOp("*"))
            {
                _p++;
            }
            else
            {
                into.Add(ParseExpr());
            }

            if (Accept("AS"))
            {
                _p++;
            }
            else if (IsAliasCandidate(Cur))
            {
                _p++;
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }

        if (Accept("INTO"))
        {
            if (Cur.Kind == TokenKind.Variable)
            {
                _p++;
            }
            else
            {
                ParseQName();
            }

            if (IsOp("(") && !IsQueryStart(1))
            {
                SkipBalanced();
            }
        }
    }

    /// <summary><c>a = expr, (b, c) = (..), t.d = expr</c>.</summary>
    private void ParseSetList(List<SetItem> into)
    {
        while (!StmtEnd)
        {
            var targets = new List<Expr>();
            if (IsOp("("))
            {
                _p++;
                while (!StmtEnd && !IsOp(")"))
                {
                    if (Cur.IsName)
                    {
                        targets.Add(new Expr([new NameRef(ParseNameChain(out _))]));
                    }
                    else
                    {
                        _p++;
                    }

                    AcceptOp(",");
                }

                AcceptOp(")");
            }
            else if (Cur.Kind == TokenKind.Variable)
            {
                targets.Add(new Expr([ParseVarRef()]));
                if (AcceptOp("=") && IsNameToken(Cur) && At(1).IsOp("="))
                {
                    targets.Add(new Expr([new NameRef(ParseNameChain(out _))])); // SET @v = col = expr
                }
                else if (Prev.IsOp("="))
                {
                    _p--;
                }
            }
            else if (Cur.IsName)
            {
                targets.Add(new Expr([new NameRef(ParseNameChain(out _))]));
                if (IsOp("["))
                {
                    SkipBalancedBrackets();
                }
            }
            else
            {
                break;
            }

            if (!(AcceptOp("=") || AcceptOp(":=") || AcceptOp("+=") || AcceptOp("-=") || AcceptOp("*=") || AcceptOp("/=") || AcceptOp("%=") || Accept("TO")))
            {
                into.Add(new SetItem(targets, Expr.Empty));
                break;
            }

            Expr value;
            if (IsQueryStart() || (IsOp("(") && IsQueryStart(1)))
            {
                value = new Expr([new SubqueryExpr(ParseQuery())]);
            }
            else
            {
                value = ParseExpr();
            }

            into.Add(new SetItem(targets, value));
            if (!AcceptOp(","))
            {
                break;
            }
        }
    }

    private void SkipBalancedBrackets()
    {
        while (!StmtEnd && !IsOp("]"))
        {
            _p++;
        }

        AcceptOp("]");
    }

    private UpdateStmt ParseUpdate()
    {
        _p++; // UPDATE
        var u = new UpdateStmt();
        while (Cur.IsWord && Cur.Upper is "LOW_PRIORITY" or "IGNORE" or "ONLY")
        {
            _p++;
        }

        if (Is("OR") && At(1).IsWord)
        {
            _p += 2;
        }

        SkipTop();
        u.Targets.AddRange(ParseFromList());
        while (!StmtEnd)
        {
            if (Accept("SET"))
            {
                ParseSetList(u.Sets);
            }
            else if (Is("OUTPUT"))
            {
                ParseOutput(u.Returning);
            }
            else if (Accept("FROM"))
            {
                u.From.AddRange(ParseFromList());
            }
            else if (Accept("WHERE"))
            {
                if (AcceptSeq("CURRENT", "OF"))
                {
                    _p++;
                }
                else
                {
                    u.Where = ParseExpr();
                }
            }
            else if (Is("RETURNING") || Is("RETURN"))
            {
                ParseReturning(u.Returning);
            }
            else if (AcceptSeq("ORDER", "BY"))
            {
                ParseOrderItems(u.Returning);
            }
            else if (Accept("LIMIT") || Is("OPTION"))
            {
                SkipBalanced();
            }
            else if (AtTsqlStatementStart())
            {
                break;
            }
            else
            {
                SkipBalanced();
            }
        }

        return u;
    }

    private DeleteStmt ParseDelete()
    {
        _p++; // DELETE
        var d = new DeleteStmt();
        while (Cur.IsWord && Cur.Upper is "LOW_PRIORITY" or "QUICK" or "IGNORE")
        {
            _p++;
        }

        SkipTop();
        if (Accept("FROM"))
        {
            d.From.AddRange(ParseFromList());
            if (Accept("FROM"))
            {
                // T-SQL: DELETE FROM a FROM t a JOIN ...
                d.Targets.AddRange(d.From.OfType<NamedTableRef>().Select(t => t.Name));
                d.From.Clear();
                d.From.AddRange(ParseFromList());
            }
            else if (Accept("USING"))
            {
                if (d.From.All(f => f is NamedTableRef { Alias: null }) && _d == SqlDialect.MySql)
                {
                    d.Targets.AddRange(d.From.OfType<NamedTableRef>().Select(t => t.Name));
                    d.From.Clear();
                }

                d.From.AddRange(ParseFromList());
            }
        }
        else
        {
            var save = _p;
            var names = new List<QName>();
            while (ParseQName() is { } n)
            {
                names.Add(n);
                AcceptOp(".");
                AcceptOp("*");
                if (!AcceptOp(","))
                {
                    break;
                }
            }

            if (names.Count > 0 && Accept("FROM"))
            {
                d.Targets.AddRange(names);
                d.From.AddRange(ParseFromList());
            }
            else
            {
                _p = save;
                d.From.AddRange(ParseFromList());
            }
        }

        while (!StmtEnd)
        {
            if (Is("OUTPUT"))
            {
                ParseOutput(d.Returning);
            }
            else if (Accept("WHERE"))
            {
                if (AcceptSeq("CURRENT", "OF"))
                {
                    _p++;
                }
                else
                {
                    d.Where = ParseExpr();
                }
            }
            else if (Is("RETURNING") || Is("RETURN"))
            {
                ParseReturning(d.Returning);
            }
            else if (AcceptSeq("ORDER", "BY"))
            {
                ParseOrderItems(d.Returning);
            }
            else if (AtTsqlStatementStart())
            {
                break;
            }
            else
            {
                SkipBalanced();
            }
        }

        return d;
    }

    private MergeStmt? ParseMerge()
    {
        _p++; // MERGE
        SkipTop();
        Accept("INTO");
        var target = ParseTablePrimary();
        if (target is null || !Accept("USING"))
        {
            SkipToStatementEnd();
            return null;
        }

        var source = ParseTablePrimary();
        Expr? on = null;
        if (Accept("ON"))
        {
            on = ParseExpr();
        }

        var m = new MergeStmt(target, source, on);
        while (!StmtEnd)
        {
            if (Accept("WHEN"))
            {
                Accept("NOT");
                Accept("MATCHED");
                if (Accept("BY"))
                {
                    _p++;
                }

                Expr? cond = null;
                if (Accept("AND"))
                {
                    cond = ParseExpr();
                }

                Accept("THEN");
                var start = _p;
                if (AcceptSeq("UPDATE", "SET"))
                {
                    var c = new MergeClause(cond, "UPDATE");
                    ParseSetList(c.Sets);
                    if (Accept("WHERE"))
                    {
                        c.Values = ParseExpr();
                    }

                    if (AcceptSeq("DELETE", "WHERE"))
                    {
                        ParseExpr();
                    }

                    m.Clauses.Add(Finish(c, start));
                }
                else if (Accept("DELETE"))
                {
                    m.Clauses.Add(Finish(new MergeClause(cond, "DELETE"), start));
                }
                else if (Accept("INSERT"))
                {
                    var c = new MergeClause(cond, "INSERT");
                    if (IsOp("("))
                    {
                        c.InsertColumns = ParseIdentList();
                    }

                    if (Accept("VALUES") && IsOp("("))
                    {
                        var items = new List<ExprItem>();
                        ParseParenGroup(items);
                        c.Values = new Expr(items);
                    }

                    AcceptSeq("DEFAULT", "VALUES");
                    if (Accept("WHERE"))
                    {
                        ParseExpr();
                    }

                    m.Clauses.Add(Finish(c, start));
                }
                else
                {
                    AcceptSeq("DO", "NOTHING");
                }

                continue;
            }

            if (Is("OUTPUT"))
            {
                ParseOutput(m.Returning);
                continue;
            }

            if (Is("RETURNING"))
            {
                ParseReturning(m.Returning);
                continue;
            }

            if (AtTsqlStatementStart())
            {
                break;
            }

            SkipBalanced();
        }

        return m;
    }

    // ------------------------------------------------------------ table-only statements

    private static readonly HashSet<string> TableRefModifiers =
    [
        "TABLE", "TABLES", "ONLY", "VERBOSE", "FULL", "FREEZE", "ANALYZE", "MATERIALIZED", "VIEW", "CONCURRENTLY", "STATISTICS",
        "NO_WRITE_TO_BINLOG", "LOCAL", "SKIP_LOCKED", "QUICK", "EXTENDED", "USE_FRM", "IF", "EXISTS", "BINARY",
    ];

    private TableRefsStmt ParseTableRefs()
    {
        var kw = Cur.Upper;
        _p++;
        var s = new TableRefsStmt();
        if (kw == "REINDEX" || kw == "CLUSTER" && !Cur.IsName)
        {
            SkipToStatementEnd();
            return s;
        }

        if (kw == "COPY" && IsOp("(") && IsQueryStart(1))
        {
            _p++;
            s.Exprs.Add(new Expr([new SubqueryExpr(ParseQuery())]));
            SkipToStatementEnd();
            return s;
        }

        while (!StmtEnd)
        {
            if (Cur.IsWord && TableRefModifiers.Contains(Cur.Upper))
            {
                _p++;
                continue;
            }

            if (IsOp("("))
            {
                SkipBalanced();
                continue;
            }

            break;
        }

        while (!StmtEnd && !AtTsqlStatementStart())
        {
            var name = ParseQName(allowReserved: false);
            if (name is null)
            {
                break;
            }

            s.Tables.Add(name);
            AcceptOp("*");
            s.Columns.Add(IsOp("(") && !IsQueryStart(1) ? ParseIdentList() : []);
            if (Accept("AS") || IsAliasCandidate(Cur) && kw == "LOCK" && _d == SqlDialect.MySql)
            {
                _p++;
            }

            if (Cur.IsWord && Cur.Upper is "READ" or "WRITE")
            {
                _p++;
                Accept("LOCAL");
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }

        SkipToStatementEnd();
        return s;
    }

    private TableRefsStmt? ParseGrant()
    {
        _p++; // GRANT / REVOKE / DENY
        var s = new TableRefsStmt();
        var columns = new List<Ident>();
        while (!StmtEnd && !Is("ON") && !Is("TO") && !AtTsqlStatementStart())
        {
            if (IsOp("("))
            {
                columns.AddRange(ParseIdentList());
                continue;
            }

            _p++;
        }

        if (!Accept("ON"))
        {
            SkipToStatementEnd();
            return null;
        }

        var tableLike = true;
        if (Cur.IsWord && At(1).IsOp("::"))
        {
            tableLike = Cur.Upper is "OBJECT";
            _p += 2;
        }
        else if (AcceptSeq("ALL", "TABLES", "IN", "SCHEMA") || AcceptSeq("ALL", "SEQUENCES", "IN", "SCHEMA") || AcceptSeq("ALL", "FUNCTIONS", "IN", "SCHEMA"))
        {
            tableLike = false;
        }
        else if (Cur.IsWord && Cur.Upper is "TABLE" or "VIEW" or "SEQUENCE")
        {
            _p++;
        }
        else if (Cur.IsWord && Cur.Upper is "FUNCTION" or "PROCEDURE" or "SCHEMA" or "DATABASE" or "TYPE" or "DOMAIN" or "LANGUAGE"
                     or "TABLESPACE" or "FOREIGN" or "LARGE" or "ROUTINE" or "PACKAGE" or "DIRECTORY" or "JAVA")
        {
            tableLike = false;
        }

        if (tableLike)
        {
            while (!StmtEnd && ParseQName() is { } name)
            {
                s.Tables.Add(name);
                s.Columns.Add(columns);
                AcceptOp(".");
                AcceptOp("*");
                if (!AcceptOp(","))
                {
                    break;
                }
            }
        }

        SkipToStatementEnd();
        return s.Tables.Count > 0 ? s : null;
    }

    // ------------------------------------------------------------ procedural statements

    private Stmt? ParseSet()
    {
        _p++; // SET
        while (Cur.IsWord && Cur.Upper is "SESSION" or "LOCAL" or "GLOBAL" or "PERSIST" or "PERSIST_ONLY")
        {
            _p++;
        }

        if ((Is("search_path") || Is("SEARCH_PATH")) && (At(1).IsOp("=") || At(1).Is("TO")))
        {
            _p += 2;
            var schemas = new List<Ident>();
            while (!StmtEnd)
            {
                if (Cur.Kind == TokenKind.String)
                {
                    foreach (var part in Cur.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        schemas.Add(new Ident(Cur, part, _d.Normalize(part.Trim('"'), quoted: part.StartsWith('"')), false));
                    }

                    _p++;
                }
                else if (ParseIdent(allowReserved: true) is { } id)
                {
                    schemas.Add(id);
                }
                else
                {
                    _p++;
                }

                if (!AcceptOp(","))
                {
                    break;
                }
            }

            SkipToStatementEnd();
            return new SetSchemaStmt(schemas, isUse: false);
        }

        if (Is("SCHEMA") && At(1).Kind == TokenKind.String)
        {
            var t = At(1);
            _p += 2;
            return new SetSchemaStmt([new Ident(t, t.Value, _d.Normalize(t.Value, quoted: false), false)], isUse: false);
        }

        if (_d == SqlDialect.TSql && Cur.IsWord && !Cur.Is("TRANSACTION") && Cur.Kind != TokenKind.Variable && !At(1).IsOp("="))
        {
            SkipToStatementEnd(); // SET NOCOUNT ON, SET IDENTITY_INSERT t ON, ...
            return null;
        }

        if (Cur.IsWord && Cur.Upper is "NAMES" or "CHARACTER" or "TRANSACTION" or "CONSTRAINTS" or "ROLE" or "TIME" or "SESSION" or "PASSWORD" or "DEFINE")
        {
            SkipToStatementEnd();
            return null;
        }

        var g = new GenericStmt();
        var sets = new List<SetItem>();
        ParseSetList(sets);
        foreach (var item in sets)
        {
            g.Targets.AddRange(item.Targets);
            g.Exprs.Add(item.Value);
        }

        SkipToStatementEnd();
        return g;
    }

    private Stmt? ParseDeclare()
    {
        var start = _p;
        _p++; // DECLARE
        if (_d == SqlDialect.TSql || Cur.Kind == TokenKind.Variable)
        {
            return ParseTsqlDeclare();
        }

        if (_d == SqlDialect.MySql)
        {
            return ParseMySqlDeclare();
        }

        var decl = ParseDeclSection();
        return decl.Vars.Count == 0 && decl.Members.Count == 0 && _p == start + 1 ? null : decl;
    }

    private DeclareStmt? ParseTsqlDeclare()
    {
        var decl = new DeclareStmt([]);
        while (!StmtEnd)
        {
            var start = _p;
            if (Cur.Kind == TokenKind.Variable)
            {
                var v = new VarDecl(new Ident(Cur, Cur.Text, _d.Normalize(Cur.Text, quoted: false), false));
                _p++;
                Accept("AS");
                if (Is("TABLE") && At(1).IsOp("("))
                {
                    _p++;
                    ParseTableElements(v.TableColumns, [], null);
                    v.TypeText = "TABLE";
                }
                else if (Accept("CURSOR"))
                {
                    v.TypeText = "CURSOR";
                }
                else
                {
                    var items = new List<ExprItem>();
                    v.TypeText = ParseType(items);
                    v.TypeName = items.OfType<TypeRef>().FirstOrDefault()?.Name;
                    if (AcceptOp("="))
                    {
                        v.Default = ParseExpr();
                    }
                }

                decl.Vars.Add(Finish(v, start));
            }
            else if (Cur.IsName && (IsAt(1, "CURSOR") || IsAt(1, "INSENSITIVE") || IsAt(1, "SCROLL")))
            {
                var v = new VarDecl(MakeIdent(Cur)) { TypeText = "CURSOR" };
                _p++;
                while (!StmtEnd && !Is("FOR"))
                {
                    _p++;
                }

                if (Accept("FOR") && IsQueryStart())
                {
                    v.Cursor = ParseQuery();
                }

                decl.Vars.Add(Finish(v, start));
                break;
            }
            else
            {
                break;
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }

        return decl;
    }

    private Stmt? ParseMySqlDeclare()
    {
        if (Cur.IsWord && Cur.Upper is "CONTINUE" or "EXIT" or "UNDO" && IsAt(1, "HANDLER"))
        {
            _p += 2;
            Accept("FOR");
            while (!StmtEnd)
            {
                if (Accept("SQLSTATE"))
                {
                    Accept("VALUE");
                    _p++;
                }
                else if (AcceptSeq("NOT", "FOUND") || Accept("SQLEXCEPTION") || Accept("SQLWARNING") || Cur.Kind == TokenKind.Number && Accept(Cur.Upper))
                {
                }
                else if (Cur.Kind == TokenKind.Number)
                {
                    _p++;
                }
                else if (Cur.IsName && (At(1).IsOp(",") || !At(1).IsOp("=")) && !Keywords.StatementStarts.Contains(Cur.Upper))
                {
                    _p++;
                }

                if (!AcceptOp(","))
                {
                    break;
                }
            }

            return StmtEnd ? null : ParseStatement();
        }

        var names = new List<(Ident Name, int Start)>();
        while (Cur.IsName)
        {
            names.Add((MakeIdent(Cur), _p));
            _p++;
            if (!AcceptOp(","))
            {
                break;
            }
        }

        var decl = new DeclareStmt([]);
        if (names.Count == 0)
        {
            SkipToStatementEnd();
            return null;
        }

        if (Accept("CONDITION"))
        {
            SkipToStatementEnd();
            return null;
        }

        Query? cursor = null;
        var typeText = "";
        QName? typeName = null;
        Expr? def = null;
        if (Accept("CURSOR"))
        {
            Accept("FOR");
            cursor = IsQueryStart() ? ParseQuery() : null;
            typeText = "CURSOR";
        }
        else
        {
            var items = new List<ExprItem>();
            typeText = ParseType(items, t => t.Is("DEFAULT"));
            typeName = items.OfType<TypeRef>().FirstOrDefault()?.Name;
            if (Accept("DEFAULT"))
            {
                def = ParseExpr();
            }
        }

        foreach (var (name, start) in names)
        {
            var v = new VarDecl(name) { TypeText = typeText, TypeName = typeName, Default = def, Cursor = cursor };
            decl.Vars.Add(Finish(v, start));
        }

        return decl;
    }

    private Stmt? ParseExec()
    {
        if (_d != SqlDialect.TSql || At(1).IsOp("(") || At(1).Kind == TokenKind.String)
        {
            return ParseGeneric();
        }

        _p++; // EXEC / EXECUTE
        if (Cur.Kind == TokenKind.Variable && At(1).IsOp("="))
        {
            _p += 2;
        }

        if (Cur.Kind == TokenKind.Variable || ParseQName() is not { } name)
        {
            return ParseGeneric();
        }

        if (IsOp(";") && At(1).Kind == TokenKind.Number)
        {
            _p += 2;
        }

        var call = new CallStmt(name, []);
        while (!StmtEnd && !AtTsqlStatementStart())
        {
            if (Cur.Kind == TokenKind.Variable && At(1).IsOp("="))
            {
                call.NamedArgs.Add(Cur);
                _p += 2;
            }

            var before = _p;
            var e = ParseExpr();
            if (e.Items.Count > 0)
            {
                call.Args.Add(e);
            }

            while (Cur.IsWord && Cur.Upper is "OUTPUT" or "OUT" or "DEFAULT")
            {
                _p++;
            }

            if (!AcceptOp(","))
            {
                if (_p == before)
                {
                    _p++;
                }

                if (!Cur.IsOp(","))
                {
                    break;
                }
            }
        }

        if (Accept("WITH"))
        {
            SkipToStatementEnd();
        }

        return call;
    }

    private Stmt? ParseCall()
    {
        _p++; // CALL
        if (ParseQName() is not { } name)
        {
            return ParseGeneric();
        }

        var args = new List<Expr>();
        if (IsOp("("))
        {
            var fc = ParseFunctionCall(name);
            args.AddRange(fc.Args);
        }

        SkipToStatementEnd();
        return new CallStmt(name, args);
    }

    private Stmt? ParseFor()
    {
        _p++; // FOR / FOREACH
        if (ParseIdent() is not { } variable)
        {
            return ParseGeneric();
        }

        while (IsOp(",") && At(1).IsName)
        {
            _p += 2;
        }

        var loop = new ForLoopStmt(variable);
        if (Accept("SLICE"))
        {
            _p++;
        }

        if (!Accept("IN"))
        {
            return ParseGeneric();
        }

        Accept("REVERSE");
        Accept("ARRAY");
        if (IsQueryStart())
        {
            loop.Query = ParseQuery();
        }
        else
        {
            var items = new List<ExprItem>();
            while (!StmtEnd && !Is("LOOP"))
            {
                var before = _p;
                items.AddRange(ParseExpr().Items);
                if (_p == before)
                {
                    _p++;
                }
            }

            loop.Range = new Expr(items);
        }

        Accept("LOOP");
        return loop;
    }
}
