using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Syntax;

public sealed partial class SqlParser
{
    private static readonly HashSet<string> CreateModifiers =
    [
        "TEMP", "TEMPORARY", "GLOBAL", "LOCAL", "UNLOGGED", "EXTERNAL", "VIRTUAL", "TRANSIENT", "VOLATILE", "EDITIONABLE",
        "NONEDITIONABLE", "EDITIONING", "FORCE", "NOFORCE", "RECURSIVE", "SECURE", "CLUSTERED", "NONCLUSTERED", "FULLTEXT",
        "SPATIAL", "BITMAP", "CONSTRAINT", "COLUMNSTORE", "MULTISET", "TRUSTED", "AGGREGATE", "SHARED", "XML", "ONLINE",
        "OFFLINE", "SQL", "SECURITY", "INVOKER", "DEFINER", "UNDEFINED", "MERGE", "TEMPTABLE", "CONCURRENTLY", "LARGE",
        "HYBRID", "DYNAMIC", "ICEBERG", "TRANSACTIONAL",
    ];

    private static readonly HashSet<string> ReferenceOptionWords =
    [
        "MATCH", "FULL", "PARTIAL", "SIMPLE", "ON", "DELETE", "UPDATE", "CASCADE", "SET", "NULL", "DEFAULT", "RESTRICT", "NO",
        "ACTION", "NOT", "DEFERRABLE", "INITIALLY", "DEFERRED", "IMMEDIATE", "ENABLE", "DISABLE", "VALIDATE", "NOVALIDATE",
        "FOR", "REPLICATION", "RELY", "NORELY", "ENFORCED",
    ];

    private bool StmtEnd => AtEnd || Cur.IsTerminator;

    /// <summary>
    /// True at a T-SQL statement-starting keyword (in scripts without semicolons a statement ends there). <c>WITH (</c>
    /// (table options) and <c>ON DELETE</c>/<c>ON UPDATE</c> are not statement starts.
    /// </summary>
    private bool AtTsqlStatementStart() =>
        _d == SqlDialect.TSql && Cur.IsWord && Keywords.StatementStarts.Contains(Cur.Upper)
        && !(Cur.Is("WITH") && At(1).IsOp("(")) && !(Prev.Is("ON") || Prev.Is("FOR") || Prev.Is("NOT"))
        && !(Cur.Is("SET") && Prev.Is("ON"));

    /// <summary>Skips to the next top-level <c>,</c> (when <paramref name="comma"/>), <c>)</c> or the statement end.</summary>
    private void SkipClause(bool comma = true)
    {
        while (!StmtEnd && !IsOp(")") && !(comma && IsOp(",")) && !AtTsqlStatementStart())
        {
            SkipBalanced();
        }
    }

    private Stmt? ParseCreate()
    {
        _p++; // CREATE
        if (AcceptSeq("OR", "REPLACE") || AcceptSeq("OR", "ALTER"))
        {
        }

        var temporary = false;
        var unique = false;
        var materialized = false;
        while (Cur.IsWord)
        {
            var u = Cur.Upper;
            if (u is "TEMP" or "TEMPORARY")
            {
                temporary = true;
                _p++;
            }
            else if (u == "UNIQUE")
            {
                unique = true;
                _p++;
            }
            else if (u == "MATERIALIZED")
            {
                materialized = true;
                _p++;
            }
            else if (u is "DEFINER" && At(1).IsOp("="))
            {
                _p += 2;
                SkipBalanced();
                if (Cur.Kind == TokenKind.Variable || IsOp("@"))
                {
                    _p++;
                    if (Cur.Kind is TokenKind.String or TokenKind.QuotedIdent || Cur.IsWord && !Keywords.IsReserved(Cur.Upper) && At(1).IsWord)
                    {
                        _p++;
                    }
                }

                if (IsOp("(") && At(1).IsOp(")"))
                {
                    _p += 2;
                }
            }
            else if (u is "ALGORITHM" && At(1).IsOp("="))
            {
                _p += 3;
            }
            else if (CreateModifiers.Contains(u) && !(u == "SQL" && !IsAt(1, "SECURITY")))
            {
                _p++;
            }
            else
            {
                break;
            }
        }

        switch (Cur.Upper)
        {
            case "TABLE":
                return ParseCreateTable(temporary);
            case "VIEW":
                return ParseCreateView(materialized);
            case "INDEX":
                return ParseCreateIndex(unique);
            case "SEQUENCE":
                return ParseCreateSequence();
            case "TYPE" when IsAt(1, "BODY"):
                SkipToStatementEnd();
                return null;
            case "TYPE":
                return ParseCreateType();
            case "DOMAIN":
                return ParseCreateDomain();
            case "FUNCTION":
                return ParseCreateRoutine(ObjectKind.Function);
            case "PROCEDURE" or "PROC":
                return ParseCreateRoutine(ObjectKind.Procedure);
            case "TRIGGER":
                return ParseCreateTrigger();
            case "PACKAGE":
                return ParseCreatePackage();
            case "SCHEMA" or "DATABASE":
                return ParseCreateSchema();
            default:
                SkipToStatementEnd();
                return null;
        }
    }

    // ------------------------------------------------------------ tables

    private CreateTableStmt? ParseCreateTable(bool temporary)
    {
        _p++; // TABLE
        AcceptSeq("IF", "NOT", "EXISTS");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        var ct = new CreateTableStmt(name) { Temporary = temporary };
        if (AcceptSeq("PARTITION", "OF") && ParseQName() is { } parent)
        {
            ct.Parents.Add(parent);
        }

        if (IsOp("(") && !IsQueryStart(1))
        {
            ParseTableElements(ct.Columns, ct.Constraints, ct.Parents);
        }

        while (!StmtEnd && !AtTsqlStatementStart())
        {
            if (IsQueryStart() || (Is("AS") && IsQueryStart(1)))
            {
                Accept("AS");
                ct.AsQuery = ParseQuery();
                continue;
            }

            if (Accept("INHERITS") && IsOp("("))
            {
                _p++;
                while (ParseQName() is { } p)
                {
                    ct.Parents.Add(p);
                    if (!AcceptOp(","))
                    {
                        break;
                    }
                }

                SkipToClose();
                continue;
            }

            if (Accept("LIKE"))
            {
                if (ParseQName() is { } like)
                {
                    ct.Parents.Add(like);
                }

                continue;
            }

            if (Is("COMMENT"))
            {
                _p++;
                AcceptOp("=");
                if (Cur.Kind == TokenKind.String)
                {
                    ct.Comment = Cur.Value;
                    _p++;
                }

                continue;
            }

            SkipBalanced();
        }

        return ct;
    }

    private bool IsTableConstraintStart()
    {
        var t = Cur;
        if (!t.IsWord)
        {
            return false;
        }

        switch (t.Upper)
        {
            case "CONSTRAINT" or "PRIMARY" or "FOREIGN" or "CHECK" or "EXCLUDE" or "PERIOD":
                return true;
            case "UNIQUE":
                return !At(1).IsOp(",") && !At(1).IsOp(")");
            case "KEY" or "INDEX" or "FULLTEXT" or "SPATIAL":
                {
                    var next = At(1);
                    if (next.IsOp("("))
                    {
                        return true;
                    }

                    return next.IsName && !Keywords.BuiltinTypes.Contains(next.Upper) && (At(2).IsOp("(") || At(2).Is("USING") || At(2).IsName);
                }

            default:
                return false;
        }
    }

    /// <summary>Parses <c>( column | constraint | LIKE t, ... )</c>.</summary>
    private void ParseTableElements(List<ColumnDef> columns, List<ConstraintDef> constraints, List<QName>? parents)
    {
        _p++; // (
        while (!AtEnd && !IsOp(")") && !Cur.IsTerminator)
        {
            var start = _p;
            if (IsTableConstraintStart())
            {
                var c = ParseConstraint();
                if (c is not null)
                {
                    Finish(c, start);
                    constraints.Add(c);
                }
            }
            else if (Is("LIKE") && At(1).IsName)
            {
                _p++;
                if (ParseQName() is { } like)
                {
                    parents?.Add(like);
                }
            }
            else if (Cur.IsName)
            {
                var col = ParseColumnDef();
                if (col is not null)
                {
                    Finish(col, start);
                    columns.Add(col);
                }
            }

            SkipClause();
            if (!AcceptOp(","))
            {
                break;
            }
        }

        AcceptOp(")");
    }

    private bool IsColumnConstraintStart(Token t) =>
        t.IsWord && (Keywords.ColumnConstraintStarts.Contains(t.Upper) || t.Upper is "ON" or "CONFLICT" or "NOT" or "NULL");

    private ColumnDef? ParseColumnDef()
    {
        var name = ParseIdent(allowReserved: true);
        if (name is null)
        {
            return null;
        }

        var col = new ColumnDef(name);
        if (Is("AS") && (_d == SqlDialect.TSql || At(1).IsOp("(")))
        {
            _p++;
            col.Exprs.Add(ParseExprStopping());
            col.TypeText = "computed";
        }
        else if (!StmtEnd && !IsOp(",") && !IsOp(")") && !IsColumnConstraintStart(Cur) && (Cur.IsName || Cur.Kind == TokenKind.Variable))
        {
            var items = new List<ExprItem>();
            col.TypeText = ParseType(items, IsColumnConstraintStart);
            col.TypeName = items.OfType<TypeRef>().FirstOrDefault()?.Name;
        }

        ParseColumnConstraints(col);
        return col;
    }

    private Expr ParseExprStopping()
    {
        var save = _stop;
        _stop = IsColumnConstraintStart;
        try
        {
            return ParseExpr();
        }
        finally
        {
            _stop = save;
        }
    }

    private void ParseColumnConstraints(ColumnDef col)
    {
        while (!StmtEnd && !IsOp(",") && !IsOp(")"))
        {
            var start = _p;
            Ident? cname = null;
            if (Accept("CONSTRAINT"))
            {
                cname = ParseIdent(allowReserved: true);
            }

            var u = Cur.Upper;
            if (AcceptSeq("PRIMARY", "KEY"))
            {
                var c = Finish(new ConstraintDef(cname, ConstraintKind.PrimaryKey), start);
                c.Columns.Add(col.Name);
                col.Constraints.Add(c);
            }
            else if (u == "UNIQUE")
            {
                _p++;
                Accept("KEY");
                var c = Finish(new ConstraintDef(cname, ConstraintKind.Unique), start);
                c.Columns.Add(col.Name);
                col.Constraints.Add(c);
            }
            else if (u == "REFERENCES")
            {
                var c = new ConstraintDef(cname, ConstraintKind.ForeignKey);
                c.Columns.Add(col.Name);
                ParseReferences(c);
                col.Constraints.Add(Finish(c, start));
            }
            else if (u == "CHECK" && At(1).IsOp("("))
            {
                _p++;
                var c = new ConstraintDef(cname, ConstraintKind.Check);
                var items = new List<ExprItem>();
                ParseParenGroup(items);
                c.Exprs.Add(new Expr(items));
                col.Constraints.Add(Finish(c, start));
            }
            else if (u == "DEFAULT")
            {
                _p++;
                col.Exprs.Add(ParseExprStopping());
            }
            else if (u is "GENERATED" or "AS")
            {
                while (Cur.IsWord && Cur.Upper is "GENERATED" or "ALWAYS" or "BY" or "DEFAULT" or "AS" or "ON" or "NULL")
                {
                    _p++;
                }

                if (Accept("IDENTITY"))
                {
                    if (IsOp("("))
                    {
                        SkipBalanced();
                    }
                }
                else if (IsOp("("))
                {
                    var items = new List<ExprItem>();
                    ParseParenGroup(items);
                    col.Exprs.Add(new Expr(items));
                }
            }
            else if (u == "COMMENT" && At(1).Kind == TokenKind.String)
            {
                col.Comment = At(1).Value;
                _p += 2;
            }
            else if (u == "COLLATE")
            {
                _p += 2;
            }
            else if (u == "ON" && IsAt(1, "UPDATE"))
            {
                _p += 2;
                col.Exprs.Add(ParseExprStopping());
            }
            else if (cname is null)
            {
                SkipBalanced();
            }
        }
    }

    private void ParseReferences(ConstraintDef c)
    {
        _p++; // REFERENCES
        c.RefTable = ParseQName(allowReserved: true);
        if (IsOp("("))
        {
            c.RefColumns.AddRange(ParseIdentList());
        }

        while (Cur.IsWord && ReferenceOptionWords.Contains(Cur.Upper))
        {
            if (Is("NOT") && IsAt(1, "NULL"))
            {
                break;
            }

            if (Is("NULL") && !Prev.Is("SET"))
            {
                break;
            }

            if (Is("DEFAULT") && !Prev.Is("SET"))
            {
                break;
            }

            if (Is("ON") && !(At(1).Upper is "DELETE" or "UPDATE"))
            {
                break;
            }

            _p++;
        }
    }

    private ConstraintDef? ParseConstraint()
    {
        Ident? name = null;
        if (Accept("CONSTRAINT"))
        {
            name = ParseIdent(allowReserved: true);
        }

        ConstraintDef c;
        if (AcceptSeq("PRIMARY", "KEY"))
        {
            c = new ConstraintDef(name, ConstraintKind.PrimaryKey);
        }
        else if (Accept("UNIQUE"))
        {
            c = new ConstraintDef(name, ConstraintKind.Unique);
            if (!Accept("KEY"))
            {
                Accept("INDEX");
            }
        }
        else if (AcceptSeq("FOREIGN", "KEY"))
        {
            c = new ConstraintDef(name, ConstraintKind.ForeignKey);
        }
        else if (Is("CHECK"))
        {
            _p++;
            c = new ConstraintDef(name, ConstraintKind.Check);
            var items = new List<ExprItem>();
            if (IsOp("("))
            {
                ParseParenGroup(items);
            }

            c.Exprs.Add(new Expr(items));
            return c;
        }
        else if (Accept("EXCLUDE"))
        {
            c = new ConstraintDef(name, ConstraintKind.Exclude);
            if (Accept("USING"))
            {
                _p++;
            }

            if (IsOp("("))
            {
                _p++;
                while (!StmtEnd && !IsOp(")"))
                {
                    var e = ParseExpr();
                    c.Exprs.Add(e);
                    if (Accept("WITH"))
                    {
                        _p++;
                    }

                    SkipClause();
                    if (!AcceptOp(","))
                    {
                        break;
                    }
                }

                AcceptOp(")");
            }

            return c;
        }
        else if (Cur.IsWord && Cur.Upper is "KEY" or "INDEX" or "FULLTEXT" or "SPATIAL")
        {
            _p++;
            Accept("KEY");
            Accept("INDEX");
            c = new ConstraintDef(name, ConstraintKind.Index);
        }
        else
        {
            return name is null ? null : new ConstraintDef(name, ConstraintKind.Other);
        }

        // Optional index name (MySQL UNIQUE KEY name (..), FOREIGN KEY name (..)) and index options.
        if (c.Name is null && IsNameToken(Cur) && !(Cur.Upper is "CLUSTERED" or "NONCLUSTERED" or "USING") && (At(1).IsOp("(") || At(1).Is("USING")))
        {
            c = new ConstraintDef(ParseIdent(), c.Kind);
        }
        else if (c.Kind == ConstraintKind.Index && name is null && IsNameToken(Cur) && !(Cur.Upper is "CLUSTERED" or "NONCLUSTERED"))
        {
            c = new ConstraintDef(ParseIdent(), c.Kind);
        }

        while (Cur.IsWord && Cur.Upper is "CLUSTERED" or "NONCLUSTERED" or "USING" or "BTREE" or "HASH" or "NULLS" or "NOT" or "DISTINCT")
        {
            _p++;
        }

        if (IsOp("("))
        {
            c.Columns.AddRange(ParseIdentList());
        }

        if (c.Kind == ConstraintKind.ForeignKey && Is("REFERENCES"))
        {
            ParseReferences(c);
        }

        if (Accept("INCLUDE") && IsOp("("))
        {
            c.Columns.AddRange(ParseIdentList());
        }

        return c;
    }

    // ------------------------------------------------------------ views, indexes, sequences, types

    private CreateViewStmt? ParseCreateView(bool materialized)
    {
        _p++; // VIEW
        AcceptSeq("IF", "NOT", "EXISTS");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        var v = new CreateViewStmt(name, materialized);
        if (IsOp("(") && !IsQueryStart(1))
        {
            v.Columns = ParseIdentList();
        }

        while (!StmtEnd && !(Is("AS") && (IsQueryStart(1) || At(1).IsOp("("))) && !IsQueryStart())
        {
            SkipBalanced();
        }

        Accept("AS");
        if (IsQueryStart())
        {
            v.Query = ParseQuery();
        }

        SkipToStatementEnd();
        return v;
    }

    private CreateIndexStmt? ParseCreateIndex(bool unique)
    {
        _p++; // INDEX
        Accept("CONCURRENTLY");
        AcceptSeq("IF", "NOT", "EXISTS");
        Ident? name = null;
        if (!Is("ON") && ParseQName(allowReserved: true) is { } n)
        {
            name = n.Last;
        }

        while (Cur.IsWord && Cur.Upper is "USING" or "BTREE" or "HASH")
        {
            _p++;
        }

        if (!Accept("ON"))
        {
            SkipToStatementEnd();
            return name is null ? null : new CreateIndexStmt(name, null) { Unique = unique };
        }

        Accept("ONLY");
        var table = ParseQName(allowReserved: true);
        var ix = new CreateIndexStmt(name, table) { Unique = unique };
        if (Accept("USING"))
        {
            _p++;
        }

        if (IsOp("("))
        {
            ParseIndexColumns(ix.Exprs);
        }

        while (!StmtEnd && !AtTsqlStatementStart())
        {
            if (Accept("INCLUDE") && IsOp("("))
            {
                ParseIndexColumns(ix.Exprs);
            }
            else if (Accept("WHERE"))
            {
                ix.Exprs.Add(ParseExpr());
            }
            else
            {
                SkipBalanced();
            }
        }

        return ix;
    }

    private void ParseIndexColumns(List<Expr> into)
    {
        _p++; // (
        while (!StmtEnd && !IsOp(")"))
        {
            var e = ParseExpr();
            if (e.Items.Count > 0)
            {
                into.Add(e);
            }

            SkipClause();
            if (!AcceptOp(","))
            {
                break;
            }
        }

        AcceptOp(")");
    }

    private CreateSequenceStmt? ParseCreateSequence()
    {
        _p++; // SEQUENCE
        AcceptSeq("IF", "NOT", "EXISTS");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        var seq = new CreateSequenceStmt(name);
        while (!StmtEnd && !AtTsqlStatementStart())
        {
            if (AcceptSeq("OWNED", "BY"))
            {
                seq.OwnedBy = ParseQName();
                continue;
            }

            SkipBalanced();
        }

        return seq;
    }

    private CreateTypeStmt? ParseCreateType()
    {
        _p++; // TYPE
        AcceptSeq("IF", "NOT", "EXISTS");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        Accept("FORCE");
        var ty = new CreateTypeStmt(name, ObjectKind.Type);
        if (Accept("FROM"))
        {
            ty.BaseType = ParseType(ty.Exprs.Count == 0 ? null : null);
            SkipToStatementEnd();
            return ty;
        }

        if (Accept("AS") || Accept("IS") || Accept("UNDER"))
        {
            if (Accept("ENUM"))
            {
                ty.IsEnum = true;
                if (AcceptOp("("))
                {
                    while (!StmtEnd && !IsOp(")"))
                    {
                        if (Cur.Kind == TokenKind.String)
                        {
                            ty.EnumLabels.Add(Cur);
                        }

                        _p++;
                    }

                    AcceptOp(")");
                }
            }
            else if (Accept("OBJECT") || IsOp("(") || (Is("TABLE") && At(1).IsOp("(")))
            {
                Accept("TABLE");
                if (IsOp("("))
                {
                    var constraints = new List<ConstraintDef>();
                    ParseTypeAttributes(ty.Attributes, constraints);
                }
            }
            else if (Cur.IsWord && Cur.Upper is "TABLE" or "VARRAY" or "VARYING" or "RANGE")
            {
                var start = _p;
                while (!StmtEnd && !Is("OF") && !IsOp("("))
                {
                    _p++;
                }

                if (IsOp("("))
                {
                    SkipBalanced();
                }

                if (Accept("OF"))
                {
                    var items = new List<ExprItem>();
                    ty.BaseType = TextOf(start, _p) + " " + ParseType(items);
                    ty.Exprs.Add(new Expr(items));
                }
            }
        }

        SkipToStatementEnd();
        return ty;
    }

    /// <summary>Composite / object type attributes; Oracle member declarations are skipped.</summary>
    private void ParseTypeAttributes(List<ColumnDef> columns, List<ConstraintDef> constraints)
    {
        _p++; // (
        while (!AtEnd && !IsOp(")") && !Cur.IsTerminator)
        {
            var start = _p;
            if (Cur.IsWord && Cur.Upper is "MEMBER" or "STATIC" or "CONSTRUCTOR" or "MAP" or "ORDER" or "OVERRIDING" or "FINAL" or "INSTANTIABLE" or "NOT")
            {
                // method spec: skip
            }
            else if (IsTableConstraintStart())
            {
                if (ParseConstraint() is { } c)
                {
                    constraints.Add(Finish(c, start));
                }
            }
            else if (Cur.IsName && ParseColumnDef() is { } col)
            {
                columns.Add(Finish(col, start));
            }

            SkipClause();
            if (!AcceptOp(","))
            {
                break;
            }
        }

        AcceptOp(")");
    }

    private CreateTypeStmt? ParseCreateDomain()
    {
        _p++; // DOMAIN
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        var ty = new CreateTypeStmt(name, ObjectKind.Domain);
        Accept("AS");
        var items = new List<ExprItem>();
        ty.BaseType = ParseType(items, IsColumnConstraintStart);
        ty.Exprs.Add(new Expr(items));
        SkipToStatementEnd();
        return ty;
    }

    private CreateSchemaStmt? ParseCreateSchema()
    {
        var isDatabase = Is("DATABASE");
        _p++;
        AcceptSeq("IF", "NOT", "EXISTS");
        Ident? name;
        if (Accept("AUTHORIZATION"))
        {
            name = ParseIdent(allowReserved: true);
        }
        else
        {
            name = ParseIdent(allowReserved: true);
            if (Accept("AUTHORIZATION"))
            {
                _p++;
            }
        }

        if (name is null)
        {
            SkipToStatementEnd();
            return null;
        }

        var s = new CreateSchemaStmt(name, isDatabase && _d != SqlDialect.MySql);
        while (!StmtEnd)
        {
            if (Is("CREATE") || Is("GRANT"))
            {
                var start = _p;
                var inner = Is("CREATE") ? ParseCreate() : ParseGrant();
                if (inner is not null)
                {
                    s.Elements.Add(Finish(inner, start));
                }

                if (_p == start)
                {
                    _p++;
                }

                continue;
            }

            if (AtTsqlStatementStart())
            {
                break;
            }

            SkipBalanced();
        }

        return s;
    }

    // ------------------------------------------------------------ routines

    /// <summary>Parses <c>FUNCTION|PROCEDURE name (params) ... body</c> (the current token is FUNCTION/PROCEDURE).</summary>
    private CreateRoutineStmt? ParseCreateRoutine(ObjectKind kind, bool nested = false)
    {
        _p++; // FUNCTION / PROCEDURE / PROC
        AcceptSeq("IF", "NOT", "EXISTS");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            if (!nested)
            {
                SkipToStatementEnd();
            }

            return null;
        }

        var r = new CreateRoutineStmt(name, kind);
        if (IsOp("("))
        {
            ParseParams(r.Params);
        }
        else if (_d == SqlDialect.TSql)
        {
            while (Cur.Kind == TokenKind.Variable)
            {
                ParseParam(r.Params);
                if (!AcceptOp(","))
                {
                    break;
                }
            }
        }

        Token? bodyString = null;
        while (!AtEnd)
        {
            if (Cur.IsTerminator)
            {
                if (nested || _d == SqlDialect.PlSql)
                {
                    r.SpecOnly = true;
                }

                break;
            }

            var u = Cur.IsWord ? Cur.Upper : "";
            if (u == "RETURNS")
            {
                _p++;
                ParseReturns(r);
                continue;
            }

            if (u == "RETURN" && _d == SqlDialect.PlSql)
            {
                _p++;
                var items = new List<ExprItem>();
                r.ReturnsText = ParseType(items);
                r.ReturnsType = items.OfType<TypeRef>().FirstOrDefault()?.Name;
                continue;
            }

            if (u == "LANGUAGE")
            {
                _p++;
                r.Language = Cur.Kind == TokenKind.String ? Cur.Value.ToLowerInvariant() : Cur.Text.ToLowerInvariant();
                _p++;
                continue;
            }

            if (u is "AS" or "IS")
            {
                _p++;
                if (Cur.Kind == TokenKind.String && _d != SqlDialect.TSql)
                {
                    bodyString = Cur;
                    _p++;
                    if (AcceptOp(","))
                    {
                        _p++; // AS 'obj_file', 'link_symbol'
                        bodyString = null;
                    }

                    continue;
                }

                if (Is("LANGUAGE") || Is("EXTERNAL"))
                {
                    SkipToStatementEnd(); // call spec
                    break;
                }

                if (_d == SqlDialect.PlSql || nested)
                {
                    r.Body = ParsePlSqlBlockAfterIs();
                    break;
                }

                r.Body = ParseStatements();
                break;
            }

            if (u == "BEGIN" || (u == "RETURN" && _d != SqlDialect.TSql)
                || (_d is SqlDialect.MySql or SqlDialect.Ansi && u.Length > 0 && Keywords.StatementStarts.Contains(u) && u is not ("SET" or "WITH"))
                || (_d == SqlDialect.MySql && Cur.IsName && At(1).IsOp(":")))
            {
                r.Body = ParseStatements();
                break;
            }

            if (u == "WITH" && _d == SqlDialect.TSql)
            {
                _p++;
                while (!StmtEnd && !Is("AS"))
                {
                    _p++;
                }

                continue;
            }

            if (u == "COMMENT" && At(1).Kind == TokenKind.String)
            {
                _p += 2;
                continue;
            }

            SkipBalanced();
        }

        if (bodyString is not null && r.Language is "" or "plpgsql" or "sql" or "edbspl" or "spl")
        {
            r.Body = ParseEmbedded(bodyString);
        }

        return r;
    }

    private void ParseReturns(CreateRoutineStmt r)
    {
        if (Cur.Kind == TokenKind.Variable && IsAt(1, "TABLE"))
        {
            r.ReturnsTableVariable = new Ident(Cur, Cur.Text, _d.Normalize(Cur.Text, quoted: false), false);
            _p += 2;
            if (IsOp("("))
            {
                ParseTableElements(r.ReturnsTable, [], null);
            }

            r.ReturnsText = "TABLE";
            return;
        }

        if (Is("TABLE") && At(1).IsOp("("))
        {
            _p++;
            ParseTableElements(r.ReturnsTable, [], null);
            r.ReturnsText = "TABLE";
            return;
        }

        var start = _p;
        Accept("SETOF");
        var items = new List<ExprItem>();
        ParseType(items);
        r.ReturnsText = TextOf(start, _p);
        r.ReturnsType = items.OfType<TypeRef>().FirstOrDefault()?.Name;
    }

    private void ParseParams(List<ParamDef> into)
    {
        _p++; // (
        while (!StmtEnd && !IsOp(")"))
        {
            ParseParam(into);
            SkipClause();
            if (!AcceptOp(","))
            {
                break;
            }
        }

        AcceptOp(")");
    }

    private static bool StartsMultiWordType(Token t, Token next) =>
        t.IsWord && next.IsWord && (Keywords.TypeContinuations.Contains(next.Upper) || (next.Upper is "WITH" or "WITHOUT" && t.Upper is "TIMESTAMP" or "TIME"));

    private void ParseParam(List<ParamDef> into)
    {
        var mode = "";
        if (Cur.IsWord && Cur.Upper is "IN" or "OUT" or "INOUT" or "VARIADIC" && !(At(1).IsOp(",") || At(1).IsOp(")")) && _d != SqlDialect.PlSql)
        {
            mode = Cur.Upper;
            _p++;
            if (mode == "IN" && Accept("OUT"))
            {
                mode = "INOUT";
            }
        }

        Ident? name = null;
        if (Cur.Kind == TokenKind.Variable)
        {
            name = new Ident(Cur, Cur.Text, _d.Normalize(Cur.Text, quoted: false), false);
            _p++;
        }
        else if (Cur.IsName)
        {
            var next = At(1);
            var typeOnly = next.IsOp(",") || next.IsOp(")") || next.IsOp("(") || next.IsOp(".") || next.IsOp("[") || next.IsOp("%TYPE")
                || next.IsOp("%ROWTYPE") || next.IsOp("=") || next.Is("DEFAULT") || next.IsTerminator || StartsMultiWordType(Cur, next)
                || (next.Is("ARRAY") && _d == SqlDialect.Postgres);
            if (!typeOnly)
            {
                name = MakeIdent(Cur);
                _p++;
            }
        }

        if (_d == SqlDialect.PlSql)
        {
            while (Cur.IsWord && Cur.Upper is "IN" or "OUT" or "NOCOPY")
            {
                mode = mode.Length == 0 ? Cur.Upper : mode + " " + Cur.Upper;
                _p++;
            }
        }

        Accept("AS");
        var items = new List<ExprItem>();
        var typeText = ParseType(items, t => t.Is("DEFAULT"));
        var p = new ParamDef(name, typeText, mode) { TypeName = items.OfType<TypeRef>().FirstOrDefault()?.Name };
        while (Cur.IsWord && Cur.Upper is "VARYING" or "NULL" or "NOT")
        {
            _p++;
        }

        if (Accept("DEFAULT") || AcceptOp("=") || AcceptOp(":="))
        {
            p.Default = ParseExpr();
        }

        if (Cur.IsWord && Cur.Upper is "OUT" or "OUTPUT" or "READONLY")
        {
            p = new ParamDef(name, typeText, Cur.Upper == "READONLY" ? mode : "OUT") { TypeName = p.TypeName, Default = p.Default };
            _p++;
        }

        into.Add(p);
    }

    /// <summary>
    /// Parses a PL/SQL <c>IS|AS</c> section: declarations, then <c>BEGIN .. END</c>. Returns the declarations (as a
    /// <see cref="DeclareStmt"/>) followed by the body statements.
    /// </summary>
    private List<Stmt> ParsePlSqlBlockAfterIs()
    {
        var result = new List<Stmt>();
        var start = _p;
        var decls = ParseDeclSection();
        result.Add(Finish(decls, start));
        if (Is("BEGIN"))
        {
            var end = MatchEnd(_p);
            result.AddRange(ParseRangeNested(_p + 1, end));
            _p = end;
            if (Accept("END") && Cur.IsName && !Cur.IsTerminator)
            {
                _p++;
            }
        }
        else if (Accept("END") && Cur.IsName)
        {
            _p++;
        }

        return result;
    }

    /// <summary>Index of the <c>END</c> that closes the <c>BEGIN</c> at <paramref name="begin"/> (or the range end).</summary>
    private int MatchEnd(int begin)
    {
        var depth = 0;
        for (var k = begin; k < _end; k++)
        {
            var t = _t[k];
            if (!t.IsWord || (k > 0 && _t[k - 1].IsOp(".")))
            {
                continue;
            }

            var next = k + 1 < _end ? _t[k + 1] : _eof;
            switch (t.Upper)
            {
                case "BEGIN" when !(next.Upper is "TRAN" or "TRANSACTION" or "DISTRIBUTED" or "WORK"):
                    depth++;
                    break;
                case "CASE" when !(k > 0 && _t[k - 1].Is("END")):
                    depth++;
                    break;
                case "END":
                    if (next.Upper is "IF" or "LOOP" or "WHILE" or "REPEAT" or "FOR")
                    {
                        break;
                    }

                    depth--;
                    if (depth <= 0)
                    {
                        return k;
                    }

                    break;
            }
        }

        return _end;
    }

    /// <summary>Parses statements in tokens <c>[from, to)</c> and continues at <paramref name="to"/>.</summary>
    private List<Stmt> ParseRangeNested(int from, int to)
    {
        var saveEnd = _end;
        _p = from;
        _end = Math.Min(to, saveEnd);
        var list = ParseStatements();
        _end = saveEnd;
        _p = Math.Min(to, saveEnd);
        return list;
    }

    // ------------------------------------------------------------ triggers & packages

    private CreateTriggerStmt? ParseCreateTrigger()
    {
        _p++; // TRIGGER
        AcceptSeq("IF", "NOT", "EXISTS");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        var tr = new CreateTriggerStmt(name);
        var expectEvent = false;
        while (!StmtEnd)
        {
            var u = Cur.IsWord ? Cur.Upper : "";
            if (u is "BEFORE" or "AFTER" || (u == "FOR" && _d == SqlDialect.TSql && tr.Table is not null))
            {
                _p++;
                expectEvent = true;
                continue;
            }

            if (AcceptSeq("INSTEAD", "OF"))
            {
                expectEvent = true;
                continue;
            }

            if (expectEvent && u is "INSERT" or "UPDATE" or "DELETE" or "TRUNCATE")
            {
                _p++;
                if (u == "UPDATE" && Accept("OF"))
                {
                    while (ParseIdent() is { } col)
                    {
                        tr.OfColumns.Add(col);
                        if (!AcceptOp(",") || !Cur.IsName || Cur.Upper is "INSERT" or "UPDATE" or "DELETE")
                        {
                            break;
                        }
                    }
                }

                expectEvent = Is("OR") || IsOp(",");
                if (expectEvent)
                {
                    _p++;
                }

                continue;
            }

            if (u == "ON" && tr.Table is null)
            {
                _p++;
                if (Cur.Upper is "DATABASE" or "SCHEMA" or "ALL")
                {
                    _p++;
                    AcceptSeq("SERVER");
                    continue;
                }

                tr.Table = ParseQName(allowReserved: true);
                continue;
            }

            if (u == "REFERENCING")
            {
                _p++;
                while (Cur.IsWord && Cur.Upper is "OLD" or "NEW")
                {
                    var isNew = Cur.Upper == "NEW";
                    _p++;
                    Accept("TABLE");
                    Accept("ROW");
                    Accept("AS");
                    if (ParseIdent(allowReserved: true) is { } alias)
                    {
                        tr.Referencing.Add((alias, isNew));
                    }
                }

                continue;
            }

            if (u == "WHEN")
            {
                _p++;
                var items = new List<ExprItem>();
                if (IsOp("("))
                {
                    ParseParenGroup(items);
                }
                else
                {
                    items.AddRange(ParseExpr().Items);
                }

                tr.When = new Expr(items);
                continue;
            }

            if (u is "EXECUTE" or "EXEC" && _d != SqlDialect.TSql)
            {
                _p++;
                Accept("FUNCTION");
                Accept("PROCEDURE");
                if (ParseQName() is { } f && IsOp("("))
                {
                    tr.Executes = ParseFunctionCall(f);
                }

                continue;
            }

            if (u is "FOLLOWS" or "PRECEDES")
            {
                _p++;
                ParseQName();
                continue;
            }

            if (u == "COMPOUND")
            {
                SkipToStatementEnd();
                break;
            }

            if (u == "AS" && _d == SqlDialect.TSql)
            {
                _p++;
                tr.Body = ParseStatements();
                break;
            }

            if (u is "BEGIN" or "DECLARE" || (u.Length > 0 && Keywords.StatementStarts.Contains(u) && u is not ("FOR" or "EXECUTE" or "WITH")))
            {
                tr.Body = ParseStatements();
                break;
            }

            if (u == "CALL" && _d == SqlDialect.PlSql)
            {
                tr.Body = ParseStatements();
                break;
            }

            SkipBalanced();
        }

        return tr;
    }

    private CreatePackageStmt? ParseCreatePackage()
    {
        _p++; // PACKAGE
        var isBody = Accept("BODY");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        var pkg = new CreatePackageStmt(name, isBody);
        while (!StmtEnd && !Is("IS") && !Is("AS"))
        {
            SkipBalanced();
        }

        if (Accept("IS") || Accept("AS"))
        {
            pkg.Members.AddRange(ParsePlSqlBlockAfterIs());
        }

        SkipToStatementEnd();
        return pkg;
    }

    // ------------------------------------------------------------ declarations (PL/SQL, PL/pgSQL)

    /// <summary>Parses declarations until <c>BEGIN</c>/<c>END</c> (PL/SQL and PL/pgSQL declaration sections).</summary>
    private DeclareStmt ParseDeclSection()
    {
        var decl = new DeclareStmt([]);
        while (!AtEnd)
        {
            if (Cur.IsSemi)
            {
                _p++;
                continue;
            }

            if (Cur.Kind is TokenKind.BatchSeparator or TokenKind.EndOfFile || Is("BEGIN") || Is("END"))
            {
                break;
            }

            var start = _p;
            if (IsOp("<<"))
            {
                while (!AtEnd && !IsOp(">>"))
                {
                    _p++;
                }

                _p++;
                continue;
            }

            if (Is("PROCEDURE") || Is("FUNCTION"))
            {
                var kind = Is("PROCEDURE") ? ObjectKind.Procedure : ObjectKind.Function;
                if (ParseCreateRoutine(kind, nested: true) is { } r)
                {
                    decl.Members.Add(Finish(r, start));
                }

                SkipToSemi();
                continue;
            }

            if (Is("CURSOR") && At(1).IsName)
            {
                _p++;
                var v = new VarDecl(MakeIdent(Cur));
                _p++;
                ParseCursorRest(v);
                decl.Vars.Add(Finish(v, start));
                SkipToSemi();
                continue;
            }

            if (Cur.IsWord && Cur.Upper is "TYPE" or "SUBTYPE" && At(1).IsName)
            {
                _p++;
                var v = new VarDecl(MakeIdent(Cur)) { TypeText = "type" };
                _p++;
                Accept("IS");
                Accept("AS");
                if (Accept("RECORD") && IsOp("("))
                {
                    ParseTypeAttributes(v.TableColumns, []);
                }

                decl.Vars.Add(Finish(v, start));
                SkipToSemi();
                continue;
            }

            if (Is("PRAGMA"))
            {
                SkipToSemi();
                continue;
            }

            if (Cur.IsName)
            {
                var v = new VarDecl(MakeIdent(Cur));
                _p++;
                ParseVarDeclRest(v);
                decl.Vars.Add(Finish(v, start));
            }

            SkipToSemi();
        }

        return decl;
    }

    private void SkipToSemi()
    {
        while (!AtEnd && !Cur.IsSemi && Cur.Kind is not (TokenKind.BatchSeparator or TokenKind.EndOfFile))
        {
            if (Is("BEGIN") || Is("END"))
            {
                return;
            }

            SkipBalanced();
        }
    }

    private void ParseCursorRest(VarDecl v)
    {
        if (IsOp("("))
        {
            ParseParams(v.CursorParams);
        }

        if (Accept("RETURN"))
        {
            ParseType(null);
        }

        while (!StmtEnd && !Is("IS") && !Is("FOR"))
        {
            _p++;
        }

        if ((Accept("IS") || Accept("FOR")) && IsQueryStart())
        {
            v.Cursor = ParseQuery();
        }

        v.TypeText = "cursor";
    }

    /// <summary>After the variable name: <c>[CONSTANT] type [NOT NULL] [:= | DEFAULT expr]</c>, cursors, aliases, exceptions.</summary>
    private void ParseVarDeclRest(VarDecl v)
    {
        Accept("CONSTANT");
        if (Accept("EXCEPTION"))
        {
            v.IsException = true;
            v.TypeText = "exception";
            return;
        }

        if (AcceptSeq("ALIAS", "FOR"))
        {
            v.TypeText = "alias for " + Cur.Text;
            _p++;
            return;
        }

        Accept("NO");
        Accept("SCROLL");
        if (Accept("CURSOR"))
        {
            ParseCursorRest(v);
            return;
        }

        var start = _p;
        var items = new List<ExprItem>();
        if (Cur.IsName)
        {
            var q = ParseQName(allowReserved: true);
            if (IsOp("%TYPE") || IsOp("%ROWTYPE"))
            {
                v.Anchor = q;
                _p++;
            }
            else
            {
                _p = start;
                ParseType(items, t => t.Is("DEFAULT") || t.Is("NOT") || t.Is("COLLATE"));
                v.TypeName = items.OfType<TypeRef>().FirstOrDefault()?.Name;
            }
        }

        v.TypeText = TextOf(start, _p);
        while (Cur.IsWord && Cur.Upper is "NOT" or "NULL" or "COLLATE")
        {
            if (Accept("COLLATE"))
            {
                _p++;
                continue;
            }

            _p++;
        }

        if (Accept("DEFAULT") || AcceptOp(":=") || AcceptOp("="))
        {
            v.Default = ParseExpr();
        }
    }

    // ------------------------------------------------------------ ALTER / DROP / COMMENT / RENAME

    private static ObjectKind KindOf(string upper) => upper switch
    {
        "TABLE" => ObjectKind.Table,
        "VIEW" => ObjectKind.View,
        "MATERIALIZED" => ObjectKind.MaterializedView,
        "INDEX" => ObjectKind.Index,
        "SEQUENCE" => ObjectKind.Sequence,
        "TYPE" => ObjectKind.Type,
        "DOMAIN" => ObjectKind.Domain,
        "FUNCTION" => ObjectKind.Function,
        "PROCEDURE" or "PROC" => ObjectKind.Procedure,
        "TRIGGER" => ObjectKind.Trigger,
        "SCHEMA" => ObjectKind.Schema,
        "DATABASE" => ObjectKind.Database,
        "PACKAGE" => ObjectKind.Package,
        "COLUMN" => ObjectKind.Column,
        "CONSTRAINT" => ObjectKind.Constraint,
        _ => ObjectKind.Unknown,
    };

    private Stmt? ParseAlter()
    {
        _p++; // ALTER
        if (AcceptSeq("SESSION", "SET") && (AcceptSeq("CURRENT_SCHEMA") || AcceptSeq("CURRENT_SCHEMA")))
        {
            AcceptOp("=");
            var s = ParseIdent(allowReserved: true);
            SkipToStatementEnd();
            return s is null ? null : new SetSchemaStmt([s], isUse: false);
        }

        Accept("ONLINE");
        Accept("IGNORE");
        var u = Cur.Upper;
        if (u is "VIEW" or "PROCEDURE" or "PROC" or "FUNCTION" or "TRIGGER" && (_d == SqlDialect.TSql || (_d == SqlDialect.MySql && u == "VIEW")))
        {
            // T-SQL ALTER PROCEDURE/VIEW/... redefine the object (like CREATE OR ALTER).
            var save = _p;
            var probe = _p + 1;
            while (probe < _end && !_t[probe].IsTerminator && !_t[probe].Is("AS") && !_t[probe].Is("RENAME"))
            {
                probe++;
            }

            if (probe < _end && _t[probe].Is("AS"))
            {
                return u switch
                {
                    "VIEW" => ParseCreateView(materialized: false),
                    "TRIGGER" => ParseCreateTrigger(),
                    "FUNCTION" => ParseCreateRoutine(ObjectKind.Function),
                    _ => ParseCreateRoutine(ObjectKind.Procedure),
                };
            }

            _p = save;
        }

        if (u == "TABLE")
        {
            return ParseAlterTable();
        }

        var kind = KindOf(u);
        if (kind is ObjectKind.Unknown or ObjectKind.Column or ObjectKind.Constraint)
        {
            SkipToStatementEnd();
            return null;
        }

        _p++;
        if (kind == ObjectKind.MaterializedView)
        {
            Accept("VIEW");
        }

        if (kind == ObjectKind.Package)
        {
            Accept("BODY");
        }

        AcceptSeq("IF", "EXISTS");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        var a = new AlterObjectStmt(kind, name);
        if (IsOp("(") && kind is ObjectKind.Function or ObjectKind.Procedure)
        {
            SkipBalanced();
        }

        while (!StmtEnd && !AtTsqlStatementStart())
        {
            if (AcceptSeq("RENAME", "TO"))
            {
                if (ParseQName(allowReserved: true) is { } to)
                {
                    a.RenameTo = to;
                }

                continue;
            }

            if (AcceptSeq("ADD", "VALUE"))
            {
                AcceptSeq("IF", "NOT", "EXISTS");
                if (Cur.Kind == TokenKind.String)
                {
                    a.AddedLabels.Add(Cur);
                    _p++;
                }

                continue;
            }

            if (Accept("OWNED") && Accept("BY"))
            {
                if (ParseQName() is { } owner)
                {
                    a.Exprs.Add(new Expr([new NameRef(owner)]));
                }

                continue;
            }

            SkipBalanced();
        }

        return a;
    }

    private static bool IsAlterActionWord(Token t) =>
        t.IsWord && t.Upper is "ADD" or "DROP" or "ALTER" or "RENAME" or "MODIFY" or "CHANGE" or "OWNER" or "SET" or "ENABLE"
            or "DISABLE" or "VALIDATE" or "ATTACH" or "DETACH" or "INHERIT" or "NO" or "CLUSTER" or "REPLICA" or "FORCE"
            or "RESET" or "OF" or "NOT" or "WITH" or "CHECK" or "NOCHECK" or "SWITCH" or "REBUILD" or "TRUNCATE" or "ORDER"
            or "ENGINE" or "AUTO_INCREMENT" or "CONVERT" or "DEFAULT" or "LOCK" or "ALGORITHM" or "PARTITION" or "COMMENT"
            or "COALESCE" or "REORGANIZE" or "EXCHANGE" or "ANALYZE" or "OPTIMIZE" or "REPAIR" or "REMOVE" or "UPGRADE"
            or "IMPORT" or "DISCARD" or "MOVE" or "SHRINK" or "ROW" or "LOGGING" or "NOLOGGING" or "PARALLEL" or "NOPARALLEL";

    private AlterTableStmt? ParseAlterTable()
    {
        _p++; // TABLE
        AcceptSeq("IF", "EXISTS");
        Accept("ONLY");
        if (ParseQName(allowReserved: true) is not { } name)
        {
            SkipToStatementEnd();
            return null;
        }

        AcceptOp("*");
        var at = new AlterTableStmt(name);
        var last = AlterKind.Other;
        while (!StmtEnd && !AtTsqlStatementStart() || (Is("ALTER") && last != AlterKind.Other && !IsAt(1, "TABLE")))
        {
            var start = _p;
            List<AlterAction> actions;
            if (!IsAlterActionWord(Cur) && last == AlterKind.AddColumn && Cur.IsName)
            {
                actions = ParseColumnDef() is { } more ? [new AlterAction(AlterKind.AddColumn) { Column = Finish(more, start) }] : [];
            }
            else
            {
                actions = ParseAlterAction();
            }

            foreach (var action in actions)
            {
                at.Actions.Add(Finish(action, start));
                last = action.Kind;
            }

            SkipClause();
            if (_p == start)
            {
                _p++;
            }

            if (!AcceptOp(","))
            {
                if (IsOp(")"))
                {
                    _p++;
                    continue;
                }

                break;
            }
        }

        return at;
    }

    private List<AlterAction> ParseAlterAction()
    {
        var u = Cur.Upper;
        switch (u)
        {
            case "ADD":
                {
                    _p++;
                    if (IsTableConstraintStart() && !(Cur.Upper is "KEY" or "INDEX" && At(1).IsName && At(2).IsName && !At(2).Is("USING")))
                    {
                        var start = _p;
                        return ParseConstraint() is { } c ? [new AlterAction(AlterKind.AddConstraint) { Constraint = Finish(c, start) }] : [];
                    }

                    Accept("COLUMN");
                    AcceptSeq("IF", "NOT", "EXISTS");
                    if (IsOp("("))
                    {
                        var cols = new List<ColumnDef>();
                        var cons = new List<ConstraintDef>();
                        ParseTableElements(cols, cons, null);
                        return
                        [
                            .. cols.Select(c => new AlterAction(AlterKind.AddColumn) { Column = c }),
                            .. cons.Select(c => new AlterAction(AlterKind.AddConstraint) { Constraint = c }),
                        ];
                    }

                    var colStart = _p;
                    var col = ParseColumnDef();
                    return col is null ? [] : [new AlterAction(AlterKind.AddColumn) { Column = Finish(col, colStart) }];
                }

            case "DROP":
                {
                    _p++;
                    if (Accept("CONSTRAINT") || (Cur.Upper is "FOREIGN" or "CHECK" or "INDEX" or "KEY" && !At(1).IsOp(",")))
                    {
                        Accept("FOREIGN");
                        Accept("KEY");
                        Accept("INDEX");
                        Accept("CHECK");
                        AcceptSeq("IF", "EXISTS");
                        return [new AlterAction(AlterKind.DropConstraint) { Name = ParseIdent(allowReserved: true) }];
                    }

                    if (AcceptSeq("PRIMARY", "KEY"))
                    {
                        return [new AlterAction(AlterKind.DropConstraint)];
                    }

                    if (Cur.IsWord && Cur.Upper is "PARTITION" or "DEFAULT" or "PERIOD" or "SYSTEM" or "ROW" or "UNUSED")
                    {
                        return [];
                    }

                    Accept("COLUMN");
                    AcceptSeq("IF", "EXISTS");
                    if (IsOp("("))
                    {
                        return [.. ParseIdentList().Select(id => new AlterAction(AlterKind.DropColumn) { Name = id })];
                    }

                    var drops = new List<AlterAction> { new(AlterKind.DropColumn) { Name = ParseIdent(allowReserved: true) } };
                    while (_d == SqlDialect.TSql && IsOp(",") && At(1).IsName && !IsAlterActionWord(At(1)))
                    {
                        _p++;
                        if (ParseIdent(allowReserved: true) is { } more)
                        {
                            drops.Add(new AlterAction(AlterKind.DropColumn) { Name = more });
                        }
                    }

                    return drops;
                }

            case "RENAME":
                {
                    _p++;
                    if (Accept("TO") || Accept("AS"))
                    {
                        return [new AlterAction(AlterKind.RenameTable) { NewTable = ParseQName(allowReserved: true) }];
                    }

                    if (Accept("CONSTRAINT") || Accept("INDEX") || Accept("KEY"))
                    {
                        var from = ParseIdent(allowReserved: true);
                        Accept("TO");
                        return [new AlterAction(AlterKind.RenameConstraint) { Name = from, NewName = ParseIdent(allowReserved: true) }];
                    }

                    var hadColumn = Accept("COLUMN");
                    var old = ParseIdent(allowReserved: true);
                    if (Accept("TO"))
                    {
                        return [new AlterAction(AlterKind.RenameColumn) { Name = old, NewName = ParseIdent(allowReserved: true) }];
                    }

                    if (!hadColumn && old is not null)
                    {
                        // MySQL: RENAME new_name
                        return [new AlterAction(AlterKind.RenameTable) { NewTable = new QName([old]) }];
                    }

                    return [];
                }

            case "ALTER" or "MODIFY":
                {
                    _p++;
                    if (Cur.IsWord && Cur.Upper is "CONSTRAINT" or "INDEX" or "CHECK")
                    {
                        return [];
                    }

                    Accept("COLUMN");
                    if (u == "MODIFY" && IsOp("("))
                    {
                        var cols = new List<ColumnDef>();
                        ParseTableElements(cols, [], null);
                        return [.. cols.Select(c => new AlterAction(AlterKind.AlterColumn) { Column = c, Name = c.Name })];
                    }

                    var start = _p;
                    var name = ParseIdent(allowReserved: true);
                    if (name is null)
                    {
                        return [];
                    }

                    var action = new AlterAction(AlterKind.AlterColumn) { Name = name };
                    if (u == "MODIFY" || (_d == SqlDialect.TSql && Cur.IsName && !Cur.Is("ADD") && !Cur.Is("DROP")))
                    {
                        _p = start;
                        action.Column = ParseColumnDef() is { } redefined ? Finish(redefined, start) : null;
                        return [action];
                    }

                    while (!StmtEnd && !IsOp(",") && !AtTsqlStatementStart())
                    {
                        if (Accept("USING"))
                        {
                            action.Exprs.Add(ParseExpr());
                            continue;
                        }

                        if (Is("DEFAULT") && Prev.Is("SET"))
                        {
                            _p++;
                            action.Exprs.Add(ParseExpr());
                            continue;
                        }

                        SkipBalanced();
                    }

                    return [action];
                }

            case "CHANGE":
                {
                    _p++;
                    Accept("COLUMN");
                    var old = ParseIdent(allowReserved: true);
                    var start = _p;
                    var col = ParseColumnDef();
                    if (old is null || col is null)
                    {
                        return [];
                    }

                    return [new AlterAction(AlterKind.RenameColumn) { Name = old, NewName = col.Name, Column = Finish(col, start) }];
                }

            case "SET" when IsAt(1, "SCHEMA"):
                {
                    _p += 2;
                    return ParseIdent(allowReserved: true) is { } schema
                        ? [new AlterAction(AlterKind.RenameTable) { Name = schema }]
                        : [];
                }

            default:
                return [];
        }
    }

    private DropStmt? ParseDrop()
    {
        _p++; // DROP
        var u = Cur.Upper;
        var kind = KindOf(u);
        if (kind is ObjectKind.Unknown or ObjectKind.Column or ObjectKind.Constraint)
        {
            SkipToStatementEnd();
            return null;
        }

        _p++;
        if (kind == ObjectKind.MaterializedView)
        {
            Accept("VIEW");
        }

        var drop = new DropStmt(kind);
        if (kind == ObjectKind.Package && Accept("BODY"))
        {
            drop = new DropStmt(ObjectKind.Unknown); // dropping a package body keeps the specification
        }

        Accept("CONCURRENTLY");
        AcceptSeq("IF", "EXISTS");
        while (!StmtEnd)
        {
            if (ParseQName(allowReserved: true) is not { } name)
            {
                break;
            }

            drop.Names.Add(name);
            List<string>? sig = null;
            if (IsOp("(") && kind is ObjectKind.Function or ObjectKind.Procedure)
            {
                var ps = new List<ParamDef>();
                ParseParams(ps);
                sig = [.. ps.Where(p => p.Mode != "OUT").Select(p => p.TypeText)];
            }

            drop.Signatures.Add(sig);
            if (!AcceptOp(","))
            {
                break;
            }
        }

        if (Accept("ON"))
        {
            drop.OnTable = ParseQName(allowReserved: true);
        }

        SkipToStatementEnd();
        return drop;
    }

    private CommentOnStmt? ParseCommentOn()
    {
        _p += 2; // COMMENT ON
        var u = Cur.Upper;
        var kind = KindOf(u);
        _p++;
        if (kind == ObjectKind.MaterializedView)
        {
            Accept("VIEW");
        }

        if (ParseQName(allowReserved: true) is not { } target)
        {
            SkipToStatementEnd();
            return null;
        }

        if (IsOp("("))
        {
            SkipBalanced();
        }

        if (kind is ObjectKind.Trigger or ObjectKind.Constraint && Accept("ON") && ParseQName(allowReserved: true) is { } table)
        {
            target = new QName([.. table.Parts, target.Last]);
        }

        string text = "";
        if (Accept("IS"))
        {
            if (Cur.Kind == TokenKind.String)
            {
                text = Cur.Value;
                _p++;
            }
        }

        SkipToStatementEnd();
        return new CommentOnStmt(kind, target, text);
    }

    private RenameTableStmt? ParseRenameTable()
    {
        _p += 2; // RENAME TABLE
        var r = new RenameTableStmt();
        while (!StmtEnd)
        {
            var from = ParseQName(allowReserved: true);
            if (from is null || !Accept("TO"))
            {
                break;
            }

            if (ParseQName(allowReserved: true) is { } to)
            {
                r.Pairs.Add((from, to));
            }

            if (!AcceptOp(","))
            {
                break;
            }
        }

        SkipToStatementEnd();
        return r;
    }
}
