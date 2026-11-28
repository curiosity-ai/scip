using ScipSharp.Indexer.Sql.Lexing;
using ScipSharp.Indexer.Sql.Syntax;
using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Sql.Semantics;

internal sealed partial class Binder
{
    private void BindStatements(List<Stmt>? stmts)
    {
        if (stmts is null)
        {
            return;
        }

        var saved = _stmt;
        foreach (var s in stmts)
        {
            _stmt = s;
            BindStmt(s);
        }

        _stmt = saved;
    }

    private void BindStmt(Stmt s)
    {
        EnsureStack();
        switch (s)
        {
            case CreateSchemaStmt cs:
                BindCreateSchema(cs);
                break;
            case SetSchemaStmt ss:
                BindSetSchema(ss);
                break;
            case CreateTableStmt ct:
                BindCreateTable(ct);
                break;
            case CreateViewStmt cv:
                BindCreateView(cv);
                break;
            case CreateIndexStmt ci:
                BindCreateIndex(ci);
                break;
            case CreateSequenceStmt seq:
                {
                    CreateRelation(seq.Name, ObjectKind.Sequence, "SEQUENCE");
                    if (seq.OwnedBy is { } owner)
                    {
                        ResolveName(owner, new Scope(null));
                    }

                    break;
                }

            case CreateTypeStmt ty:
                BindCreateType(ty);
                break;
            case CreateRoutineStmt r:
                BindCreateRoutine(r, local: false);
                break;
            case CreateTriggerStmt tr:
                BindCreateTrigger(tr);
                break;
            case CreatePackageStmt p:
                BindCreatePackage(p);
                break;
            case AlterTableStmt at:
                BindAlterTable(at);
                break;
            case AlterObjectStmt ao:
                BindAlterObject(ao);
                break;
            case RenameTableStmt rt:
                foreach (var (from, to) in rt.Pairs)
                {
                    if (ResolveRelationRef(from) is { Unresolved: false } obj)
                    {
                        RenameRelation(obj, to);
                    }
                }

                break;
            case DropStmt d:
                BindDrop(d);
                break;
            case CommentOnStmt c:
                BindCommentOn(c);
                break;
            case QueryStmt q when _dbtModel is { } model && RoutineCtx() is null:
                {
                    // dbt: the model's (first top-level) SELECT defines a relation named after the file.
                    _dbtModel = null;
                    var key = _d.Normalize(model, quoted: false);
                    var descriptors = (SymbolDescriptor[])[.. SchemaDescriptors(_db, CurrentSchema), SymbolNames.Type(key)];
                    var obj = new DbObject(ObjectKind.View, _db, CurrentSchema, key, model, descriptors, Sym(descriptors));
                    _cat.Add(obj);
                    _ever.Add(obj);
                    DefineInfo(obj.Symbol, Kind.Class, model, [Code("MODEL " + Qualified(obj)), "dbt model defined by this file."]);
                    BindDerivedObject(obj, q.Query, null);
                    break;
                }

            case QueryStmt q:
                BindQuery(q.Query, new Scope(null), null);
                break;
            case InsertStmt or UpdateStmt or DeleteStmt or MergeStmt:
                BindStmtShape(s, null!);
                break;
            case CallStmt call:
                BindCallStmt(call);
                break;
            case ForLoopStmt loop:
                BindForLoop(loop);
                break;
            case DeclareStmt decl:
                BindDeclare(decl, packageLevel: false);
                break;
            case AnonymousBlockStmt block:
                {
                    var saved = _ctx;
                    _ctx = new RoutineContext(_ctx);
                    BindStatements(block.Body);
                    _ctx = saved;
                    break;
                }

            case TableRefsStmt tr:
                for (var i = 0; i < tr.Tables.Count; i++)
                {
                    var obj = ResolveRelationRef(tr.Tables[i]);
                    if (i < tr.Columns.Count && tr.Columns[i].Count > 0)
                    {
                        var src = TableSource(obj);
                        foreach (var col in tr.Columns[i])
                        {
                            EmitColumn(src, col);
                        }
                    }
                }

                BindExprs(tr.Exprs, new Scope(null));
                break;
            case GenericStmt g:
                {
                    var scope = new Scope(null);
                    BindExprs(g.Targets, scope);
                    BindExprs(g.Exprs, scope);
                    break;
                }
        }
    }

    private string CurrentSchema => _searchPath.FirstOrDefault(s => !s.StartsWith('$')) ?? _defaultSchema;

    private static Source TableSource(DbObject obj) => new() { Key = obj.NameKey, TableKey = obj.NameKey, SchemaKey = obj.SchemaKey, Object = obj };

    private static Scope TableScope(DbObject obj)
    {
        var scope = new Scope(null);
        scope.Sources.Add(TableSource(obj));
        return scope;
    }

    private string StmtText(Stmt? s, int max = 300)
    {
        if (s?.First is null || s.Last is null || s.Last.End <= s.First.Start)
        {
            return "";
        }

        var raw = _script.Source.Text[s.First.Start..s.Last.End];
        var text = string.Join(' ', raw.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return text.Length > max ? text[..max] + " ..." : text;
    }

    private List<string> StmtDocs(string header)
    {
        var docs = new List<string> { Code(header) };
        if (_stmt?.First is { } first)
        {
            docs.AddRange(LeadingComments(first));
        }

        return docs;
    }

    private static string Qualified(DbObject o) =>
        (o.DbKey.Length > 0 ? o.DbKey + "." : "") + (o.SchemaKey.Length > 0 ? o.SchemaKey + "." : "") + o.Name;

    // ------------------------------------------------------------ schemas

    private void BindCreateSchema(CreateSchemaStmt cs)
    {
        var kind = cs.IsDatabase ? ObjectKind.Database : ObjectKind.Schema;
        var descriptors = cs.IsDatabase ? [SymbolNames.Namespace(cs.Name.Key)] : SchemaDescriptors(_db, cs.Name.Key);
        var obj = new DbObject(kind, cs.IsDatabase ? "" : _db, cs.IsDatabase ? "" : cs.Name.Key, cs.Name.Key, cs.Name.Text, descriptors, Sym(descriptors));
        _cat.Add(obj);
        _ever.Add(obj);
        Def(cs.Name.Token, obj.Symbol, Kind.Namespace, cs.Name.Text, StmtDocs((cs.IsDatabase ? "DATABASE " : "SCHEMA ") + cs.Name.Text), Range(_stmt));
        var saved = _searchPath;
        _searchPath = [cs.Name.Key, .. saved];
        BindStatements(cs.Elements);
        _searchPath = saved;
    }

    private void BindSetSchema(SetSchemaStmt ss)
    {
        if (ss.IsUse && _d == SqlDialect.TSql && ss.Schemas.Count > 0)
        {
            var db = ss.Schemas[0];
            _db = db.Key;
            var dbObj = Lookup(ObjectKind.Database, null, null, db.Key);
            Ref(db.Token, dbObj?.Symbol ?? Sym([SymbolNames.Namespace(db.Key)]), Kind.Namespace, db.Text);
            return;
        }

        var path = new List<string>();
        foreach (var s in ss.Schemas)
        {
            if (s.Text.StartsWith('$'))
            {
                path.Add(s.Key);
                continue;
            }

            path.Add(s.Key);
            if (s.Token.Kind != TokenKind.String)
            {
                var schemaObj = Lookup(ObjectKind.Schema, null, null, s.Key);
                Ref(s.Token, schemaObj?.Symbol ?? Sym(SchemaDescriptors(_db, s.Key)), Kind.Namespace, s.Text);
            }
        }

        if (path.Count > 0)
        {
            _searchPath = path;
        }
    }

    // ------------------------------------------------------------ relations

    private DbObject CreateRelation(QName name, ObjectKind kind, string what, string? comment = null)
    {
        var (dbId, schemaId, nameId) = Split(name);
        var db = dbId is not null && _d != SqlDialect.MySql ? dbId.Key : _db;
        var schema = schemaId?.Key ?? CurrentSchema;
        var descriptors = (SymbolDescriptor[])[.. SchemaDescriptors(db, schema), SymbolNames.Type(nameId.Key)];
        var obj = new DbObject(kind, db, schema, nameId.Key, nameId.Text, descriptors, Sym(descriptors));
        _cat.Add(obj);
        _ever.Add(obj);
        EmitQualifiers((dbId, schemaId, nameId), obj);
        var docs = StmtDocs(what + " " + Qualified(obj));
        if (comment is { Length: > 0 })
        {
            docs.Add(comment);
        }

        Def(nameId.Token, obj.Symbol, KindOf(obj), nameId.Text, docs, Range(_stmt));
        return obj;
    }

    private void BindCreateTable(CreateTableStmt ct)
    {
        var obj = CreateRelation(ct.Name, ObjectKind.Table, ct.Temporary ? "TEMPORARY TABLE" : "TABLE", ct.Comment);
        var scope = TableScope(obj);
        foreach (var p in ct.Parents)
        {
            var parent = ResolveRelationRef(p);
            foreach (var c in ColumnsOf(parent))
            {
                if (c.Symbol is { } sym)
                {
                    obj.AddColumn(new ColumnInfo(c.Key, c.Name, sym));
                }
            }

            if (parent.Opaque || parent.Unresolved)
            {
                obj.Opaque = true;
            }
        }

        foreach (var col in ct.Columns)
        {
            DefineColumn(obj, col, scope);
        }

        foreach (var c in ct.Constraints)
        {
            BindConstraint(obj, c, scope, null);
        }

        if (ct.AsQuery is { } q)
        {
            BindDerivedObject(obj, q, ct.AsColumns);
        }
        else if (ct.Columns.Count == 0 && ct.Parents.Count == 0)
        {
            obj.Opaque = true;
        }
    }

    private void DefineColumn(DbObject obj, ColumnDef col, Scope scope)
    {
        var sym = Sym(obj, SymbolNames.Term(col.Name.Key));
        obj.AddColumn(new ColumnInfo(col.Name.Key, col.Name.Text, sym));
        var docs = new List<string> { Code(StmtText(col)) };
        if (col.First is { } first)
        {
            docs.AddRange(LeadingComments(first));
        }

        if (col.Last is { } last && TrailingComment(last) is { Length: > 0 } trailing)
        {
            docs.Add(trailing);
        }

        if (col.Comment is { Length: > 0 } c)
        {
            docs.Add(c);
        }

        Def(col.Name.Token, sym, Kind.Field, col.Name.Text, docs, Range(col));
        if (col.TypeName is { } tn && FindRelation(tn) is { Kind: ObjectKind.Type or ObjectKind.Domain or ObjectKind.Table } type)
        {
            ResolveRelationRef(tn, Kind.Type);
            Relate(sym, type.Symbol, typeDefinition: true);
        }

        foreach (var cons in col.Constraints)
        {
            BindConstraint(obj, cons, scope, sym);
        }

        BindExprs(col.Exprs, scope);
    }

    private void BindConstraint(DbObject obj, ConstraintDef c, Scope scope, string? columnSym)
    {
        if (c.Name is { } n)
        {
            var sym = Sym(obj, SymbolNames.Meta(n.Key));
            obj.Members[n.Key] = sym;
            var kind = c.Kind is ConstraintKind.Index or ConstraintKind.PrimaryKey or ConstraintKind.Unique ? ObjectKind.Index : ObjectKind.Constraint;
            var member = new DbObject(kind, obj.DbKey, obj.SchemaKey, n.Key, n.Text, [.. obj.Descriptors, SymbolNames.Meta(n.Key)], sym) { Owner = obj };
            _cat.Add(member);
            _ever.Add(member);
            Def(n.Token, sym, Kind.Key, n.Text, [Code(StmtText(c))], Range(c));
        }

        var src = TableSource(obj);
        var fromSyms = new List<string>();
        if (columnSym is not null)
        {
            fromSyms.Add(columnSym);
        }
        else
        {
            foreach (var col in c.Columns)
            {
                if (EmitColumn(src, col) is { } s)
                {
                    fromSyms.Add(s);
                }
            }
        }

        if (c.Kind == ConstraintKind.PrimaryKey)
        {
            obj.PrimaryKey.Clear();
            obj.PrimaryKey.AddRange(fromSyms);
        }

        if (c.Kind == ConstraintKind.ForeignKey && c.RefTable is { } rt)
        {
            var target = ResolveRelationRef(rt);
            var refSyms = new List<string>();
            var targetSrc = TableSource(target);
            foreach (var rc in c.RefColumns)
            {
                if (EmitColumn(targetSrc, rc) is { } s)
                {
                    refSyms.Add(s);
                }
            }

            if (c.RefColumns.Count == 0)
            {
                refSyms.AddRange(PrimaryKeyOf(target));
            }

            for (var i = 0; i < fromSyms.Count && i < refSyms.Count; i++)
            {
                if (!target.Unresolved || _emit)
                {
                    Relate(fromSyms[i], refSyms[i], reference: true);
                }
            }
        }

        BindExprs(c.Exprs, scope);
    }

    private List<string> PrimaryKeyOf(DbObject o)
    {
        if (o.PrimaryKey.Count > 0)
        {
            return o.PrimaryKey;
        }

        foreach (var cat in Catalogs())
        {
            if (cat.BySymbol(o.Symbol) is { PrimaryKey.Count: > 0 } other)
            {
                return other.PrimaryKey;
            }
        }

        return [];
    }

    /// <summary>Defines the columns of a view / CTAS table from its query.</summary>
    private void BindDerivedObject(DbObject obj, Query q, List<Ident>? explicitColumns)
    {
        Shape shape;
        if (explicitColumns is { Count: > 0 })
        {
            shape = BindQuery(q, new Scope(null), null);
            for (var i = 0; i < explicitColumns.Count; i++)
            {
                var id = explicitColumns[i];
                var sym = Sym(obj, SymbolNames.Term(id.Key));
                obj.AddColumn(new ColumnInfo(id.Key, id.Text, sym));
                Def(id.Token, sym, Kind.Field, id.Text, [Code(id.Text + " (column of " + Qualified(obj) + ")")]);
                if (i < shape.Columns.Count && (shape.Columns[i].Source ?? shape.Columns[i].Symbol) is { } source)
                {
                    Relate(sym, source, reference: true);
                }
            }

            obj.Opaque = false;
            return;
        }

        var defined = new HashSet<string>(StringComparer.Ordinal);
        string Definer(Ident alias, string? source)
        {
            var sym = Sym(obj, SymbolNames.Term(alias.Key));
            if (defined.Add(alias.Key))
            {
                obj.AddColumn(new ColumnInfo(alias.Key, alias.Text, sym));
                Def(alias.Token, sym, Kind.Field, alias.Text, [Code(alias.Text + " (column of " + Qualified(obj) + ")")]);
                if (source is not null)
                {
                    Relate(sym, source, reference: true);
                }
            }

            return sym;
        }

        shape = BindQuery(q, new Scope(null), Definer);
        DefineShapeColumns(obj, shape, defined);
        obj.Opaque = shape.Opaque;
    }

    private void DefineShapeColumns(DbObject obj, Shape shape, HashSet<string>? defined)
    {
        var aliasesToo = defined is null;
        defined ??= new HashSet<string>(StringComparer.Ordinal);
        foreach (var col in shape.Columns)
        {
            if ((col.Aliased && !aliasesToo) || col.Key == "?column?" || !defined.Add(col.Key))
            {
                continue;
            }

            var sym = Sym(obj, SymbolNames.Term(col.Key));
            obj.AddColumn(new ColumnInfo(col.Key, col.Name, sym));
            var docs = (string[])[Code(col.Name + " (column of " + Qualified(obj) + ")")];
            if (col.At is { } at)
            {
                Def(at, sym, Kind.Field, col.Name, docs);
            }
            else
            {
                DefineInfo(sym, Kind.Field, col.Name, docs);
            }

            if ((col.Source ?? col.Symbol) is { } source)
            {
                Relate(sym, source, reference: true);
            }
        }

        obj.Opaque |= shape.Opaque;
    }

    private void BindCreateView(CreateViewStmt cv)
    {
        var obj = CreateRelation(cv.Name, cv.Materialized ? ObjectKind.MaterializedView : ObjectKind.View, cv.Materialized ? "MATERIALIZED VIEW" : "VIEW");
        if (cv.Query is { } q)
        {
            BindDerivedObject(obj, q, cv.Columns);
        }
        else
        {
            obj.Opaque = true;
        }
    }

    private DbObject DefineMember(DbObject? owner, Ident name, ObjectKind kind, QName? qualified, string what)
    {
        SymbolDescriptor[] descriptors;
        if (owner is not null)
        {
            descriptors = [.. owner.Descriptors, SymbolNames.Meta(name.Key)];
        }
        else
        {
            var parts = qualified is null ? (null, null, name) : Split(qualified);
            descriptors = [.. SchemaDescriptors(_db, parts.Item2?.Key ?? CurrentSchema), SymbolNames.Term(name.Key)];
        }

        var sym = Sym(descriptors);
        var member = new DbObject(kind, owner?.DbKey ?? _db, owner?.SchemaKey ?? CurrentSchema, name.Key, name.Text, descriptors, sym) { Owner = owner };
        owner?.Members.TryAdd(name.Key, sym);
        _cat.Add(member);
        _ever.Add(member);
        Def(name.Token, sym, KindOf(member), name.Text, StmtDocs(what + " " + name.Text + (owner is null ? "" : " ON " + Qualified(owner))), Range(_stmt));
        return member;
    }

    private void BindCreateIndex(CreateIndexStmt ix)
    {
        var table = ix.Table is { } t ? ResolveRelationRef(t) : null;
        if (ix.Name is { } n)
        {
            DefineMember(table, n, ObjectKind.Index, null, ix.Unique ? "UNIQUE INDEX" : "INDEX");
        }

        if (table is not null)
        {
            BindExprs(ix.Exprs, TableScope(table));
        }
    }

    private void BindCreateType(CreateTypeStmt ty)
    {
        var obj = CreateRelation(ty.Name, ty.Kind, ty.Kind == ObjectKind.Domain ? "DOMAIN" : ty.IsEnum ? "TYPE (ENUM)" : "TYPE");
        foreach (var label in ty.EnumLabels)
        {
            DefineEnumLabel(obj, label);
        }

        var scope = TableScope(obj);
        foreach (var attr in ty.Attributes)
        {
            DefineColumn(obj, attr, scope);
        }

        BindExprs(ty.Exprs, new Scope(null));
        if (!ty.IsEnum && ty.Attributes.Count == 0)
        {
            obj.Opaque = true;
        }
    }

    private void DefineEnumLabel(DbObject obj, Token label)
    {
        if (label.Text.Length < 2)
        {
            return;
        }

        var sym = Sym(obj, SymbolNames.Term(label.Value));
        DefAt(label.Start + 1, label.End - 1, sym, Kind.EnumMember, label.Value, [Code("'" + label.Value + "' (" + Qualified(obj) + ")")]);
    }

    // ------------------------------------------------------------ routines

    private static readonly Dictionary<string, string> TypeSynonyms = new(StringComparer.Ordinal)
    {
        ["int"] = "integer",
        ["int4"] = "integer",
        ["int8"] = "bigint",
        ["int2"] = "smallint",
        ["bool"] = "boolean",
        ["float8"] = "double precision",
        ["float4"] = "real",
        ["varchar"] = "character varying",
        ["char"] = "character",
        ["decimal"] = "numeric",
        ["timestamptz"] = "timestamp with time zone",
        ["timestamp without time zone"] = "timestamp",
        ["timetz"] = "time with time zone",
        ["time without time zone"] = "time",
    };

    internal static string NormalizeType(string type)
    {
        var t = type.ToLowerInvariant();
        var b = new System.Text.StringBuilder();
        var depth = 0;
        foreach (var ch in t)
        {
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0)
            {
                b.Append(ch);
            }
        }

        t = string.Join(' ', b.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Replace("\"", "", StringComparison.Ordinal);
        foreach (var prefix in (string[])["pg_catalog.", "sys."])
        {
            if (t.StartsWith(prefix, StringComparison.Ordinal))
            {
                t = t[prefix.Length..];
            }
        }

        var array = t.EndsWith("[]", StringComparison.Ordinal);
        var core = array ? t[..^2].TrimEnd() : t;
        core = TypeSynonyms.GetValueOrDefault(core, core);
        return array ? core + "[]" : core;
    }

    private bool Overloading => _d is SqlDialect.Postgres or SqlDialect.PlSql or SqlDialect.Ansi;

    private DbObject? BindCreateRoutine(CreateRoutineStmt r, bool local)
    {
        var (dbId, schemaId, nameId) = Split(r.Name);
        var inputs = r.Params.Where(p => p.Mode != "OUT").ToList();
        var signature = Overloading ? string.Join(",", inputs.Select(p => NormalizeType(p.TypeText))) : "";
        var isLocal = local && _package is null || local && RoutineCtx() is not null;
        var package = !isLocal && _package is not null && RoutineCtx() is null ? _package : null;
        string symbol;
        SymbolDescriptor[] descriptors;
        string db, schema;
        if (isLocal)
        {
            symbol = NewLocal();
            descriptors = [];
            db = _db;
            schema = CurrentSchema;
        }
        else if (package is not null)
        {
            descriptors = [.. package.Descriptors, SymbolNames.Method(nameId.Key, signature)];
            symbol = Sym(descriptors);
            db = package.DbKey;
            schema = package.SchemaKey;
        }
        else
        {
            db = dbId is not null && _d != SqlDialect.MySql ? dbId.Key : _db;
            schema = schemaId?.Key ?? CurrentSchema;
            descriptors = [.. SchemaDescriptors(db, schema), SymbolNames.Method(nameId.Key, signature)];
            symbol = Sym(descriptors);
        }

        var obj = new DbObject(r.Kind, db, schema, nameId.Key, nameId.Text, descriptors, symbol)
        {
            Signature = signature,
            Package = package?.Symbol,
            MinArgs = inputs.Count(p => p.Default is null),
            MaxArgs = inputs.Count,
            ReturnsTrigger = r.ReturnsText.Equals("trigger", StringComparison.OrdinalIgnoreCase),
        };
        if (r.ReturnsType is { } rt && FindRelation(rt) is { } relType)
        {
            obj.ResultRelationSymbol = relType.Symbol;
        }

        if (isLocal)
        {
            _ctx.LocalRoutines[nameId.Key] = obj;
        }
        else
        {
            _cat.Add(obj);
            _ever.Add(obj);
            if (package is null)
            {
                EmitQualifiers((dbId, schemaId, nameId), obj);
            }
        }

        var kindWord = r.Kind == ObjectKind.Procedure ? "PROCEDURE" : "FUNCTION";
        var header = kindWord + " " + (isLocal ? nameId.Text : package is not null ? package.Name + "." + nameId.Text : Qualified(obj))
            + "(" + string.Join(", ", r.Params.Select(p => string.Join(' ', new[] { p.Mode, p.Name?.Text ?? "", p.TypeText }.Where(x => x.Length > 0)))) + ")"
            + (r.ReturnsText.Length > 0 ? " RETURNS " + r.ReturnsText : "");
        Def(nameId.Token, symbol, KindOf(obj), nameId.Text, StmtDocs(header), Range(_stmt));

        var ctx = new RoutineContext(_ctx) { Routine = obj };
        foreach (var p in r.Params)
        {
            if (p.Name is not { } pn)
            {
                ctx.Positional.Add(new VarInfo(""));
                if (p.TypeName is { } ptn)
                {
                    BindAnchoredType(ptn, p.TypeText, new VarInfo(""));
                }

                continue;
            }

            var psym = isLocal ? NewLocal() : Sym(obj, SymbolNames.Parameter(pn.Key.TrimStart('@')));
            Def(pn.Token, psym, Kind.Parameter, pn.Text, [Code(string.Join(' ', new[] { p.Mode, pn.Text, p.TypeText }.Where(x => x.Length > 0)))]);
            obj.Params.Add(new ColumnInfo(pn.Key, pn.Text, psym));
            var vi = new VarInfo(psym);
            if (p.TypeName is { } tn)
            {
                BindAnchoredType(tn, p.TypeText, vi);
            }

            ctx.Vars[pn.Key] = vi;
            ctx.Positional.Add(vi);
            if (p.Default is { } def)
            {
                BindExpr(def, new Scope(null));
            }
        }

        if (r.ReturnsTable.Count > 0)
        {
            obj.ResultColumns = [];
            foreach (var col in r.ReturnsTable)
            {
                var csym = isLocal ? NewLocal() : Sym(obj, SymbolNames.Parameter(col.Name.Key));
                Def(col.Name.Token, csym, Kind.Parameter, col.Name.Text, [Code(StmtText(col))]);
                var ci = new ColumnInfo(col.Name.Key, col.Name.Text, csym);
                obj.ResultColumns.Add(ci);
                ctx.Vars.TryAdd(col.Name.Key, new VarInfo(csym));
                if (col.TypeName is { } ctn)
                {
                    ResolveTypeRef(ctn);
                }
            }

            if (r.ReturnsTableVariable is { } tv)
            {
                var local2 = NewLocal();
                Def(tv.Token, local2, Kind.Variable, tv.Text, [Code(tv.Text + " TABLE")]);
                ctx.Vars[tv.Key] = new VarInfo(local2) { Fields = [.. obj.ResultColumns.Select(c => new OutCol(c.Key, c.Name, c.Symbol))] };
            }
        }

        if (r.ReturnsType is { } rtn)
        {
            ResolveTypeRef(rtn);
        }

        if (obj.ReturnsTrigger && _state.TriggerTables.TryGetValue(symbol, out var tableSym))
        {
            ctx.TriggerTable = LookupBySymbol(tableSym);
        }

        var saved = _ctx;
        _ctx = ctx;
        BindStatements(r.Body);
        _ctx = saved;
        return obj;
    }

    /// <summary>A parameter type: <c>t.col%TYPE</c> references the column, <c>t%ROWTYPE</c> / a composite type gives the row.</summary>
    private void BindAnchoredType(QName tn, string typeText, VarInfo vi)
    {
        if (typeText.EndsWith("%TYPE", StringComparison.OrdinalIgnoreCase))
        {
            ResolveName(tn, new Scope(null));
            return;
        }

        if (typeText.EndsWith("%ROWTYPE", StringComparison.OrdinalIgnoreCase))
        {
            vi.Row = ResolveRelationRef(tn);
            return;
        }

        ResolveTypeRef(tn);
        if (FindRelation(tn) is { Kind: ObjectKind.Table or ObjectKind.Type or ObjectKind.View } row)
        {
            vi.Row = row;
        }
    }

    /// <summary>Resolves a routine reference (function call, CALL/EXEC, trigger EXECUTE FUNCTION).</summary>
    private DbObject? ResolveRoutineRef(QName name, int argCount)
    {
        var parts = name.Parts;
        if (parts.Count == 0)
        {
            return null;
        }

        var key = parts[^1].Key;
        bool Arity(DbObject o) => argCount < 0 || (argCount >= o.MinArgs && argCount <= o.MaxArgs);
        DbObject? Find(string? db, string? schema, string? package)
        {
            foreach (var cat in Catalogs())
            {
                var o = cat.Find(ObjectKind.Function, db, schema, key, SearchPath, _db, Arity, package)
                        ?? cat.Find(ObjectKind.Function, db, schema, key, SearchPath, _db, null, package);
                if (o is not null)
                {
                    return o;
                }
            }

            return null;
        }

        DbObject? found = null;
        DbObject? pkg = null;
        if (parts.Count == 1)
        {
            for (var c = _ctx; c is not null && found is null; c = c.Parent)
            {
                found = c.LocalRoutines.GetValueOrDefault(key);
            }

            if (found is null && _package is not null)
            {
                found = Find(null, null, _package.Symbol);
            }

            found ??= Find(null, null, null);
        }
        else
        {
            var q = parts[^2];
            var qSchema = parts.Count >= 3 ? parts[^3].Key : null;
            pkg = Lookup(ObjectKind.Package, null, qSchema, q.Key);
            if (pkg is not null)
            {
                found = Find(null, null, pkg.Symbol);
            }

            if (found is null)
            {
                pkg = null;
                found = Find(parts.Count >= 3 ? parts[^3].Key : null, q.Key, null);
            }
        }

        if (found is null)
        {
            return null;
        }

        if (pkg is not null)
        {
            Ref(parts[^2].Token, pkg.Symbol, Kind.Package, pkg.Name);
        }
        else if (parts.Count >= 2)
        {
            EmitQualifiers((parts.Count >= 3 ? parts[^3] : null, parts[^2], parts[^1]), found);
        }

        Ref(parts[^1].Token, found.Symbol, KindOf(found), found.Name);
        return found;
    }

    private void BindCallStmt(CallStmt call)
    {
        var routine = ResolveRoutineRef(call.Name, -1);
        foreach (var named in call.NamedArgs)
        {
            var k = _d.Normalize(named.Text, quoted: false);
            if (routine?.Params.FirstOrDefault(p => p.Key == k) is { } p)
            {
                Ref(named, p.Symbol, Kind.Parameter);
            }
        }

        BindExprs(call.Args, new Scope(null));
    }

    // ------------------------------------------------------------ triggers & packages

    private void BindCreateTrigger(CreateTriggerStmt tr)
    {
        var table = tr.Table is { } t ? ResolveRelationRef(t) : null;
        var (_, _, nameId) = Split(tr.Name);
        DefineMember(table, nameId, ObjectKind.Trigger, tr.Name, "TRIGGER");
        var ctx = new RoutineContext(_ctx) { TriggerTable = table };
        foreach (var (alias, isNew) in tr.Referencing)
        {
            ctx.TriggerAliases[alias.Key] = isNew;
            var local = NewLocal();
            Def(alias.Token, local, Kind.Variable, alias.Text, [Code((isNew ? "NEW" : "OLD") + " AS " + alias.Text)]);
        }

        if (table is not null)
        {
            var src = TableSource(table);
            foreach (var col in tr.OfColumns)
            {
                EmitColumn(src, col);
            }
        }

        var saved = _ctx;
        _ctx = ctx;
        BindExpr(tr.When, new Scope(null));
        if (tr.Executes is { } exec)
        {
            var routine = ResolveRoutineRef(exec.Name, -1);
            if (routine is not null && table is { Unresolved: false })
            {
                _state.TriggerTables.TryAdd(routine.Symbol, table.Symbol);
            }

            BindExprs(exec.Args, new Scope(null));
        }

        BindStatements(tr.Body);
        _ctx = saved;
    }

    private void BindCreatePackage(CreatePackageStmt p)
    {
        var (dbId, schemaId, nameId) = Split(p.Name);
        var db = dbId?.Key ?? _db;
        var schema = schemaId?.Key ?? CurrentSchema;
        var descriptors = (SymbolDescriptor[])[.. SchemaDescriptors(db, schema), SymbolNames.Namespace(nameId.Key)];
        var obj = new DbObject(ObjectKind.Package, db, schema, nameId.Key, nameId.Text, descriptors, Sym(descriptors));
        _cat.Add(obj);
        _ever.Add(obj);
        EmitQualifiers((dbId, schemaId, nameId), obj);
        Def(nameId.Token, obj.Symbol, Kind.Package, nameId.Text, StmtDocs((p.IsBody ? "PACKAGE BODY " : "PACKAGE ") + Qualified(obj)), Range(_stmt));
        var savedPkg = _package;
        var savedCtx = _ctx;
        _package = obj;
        _ctx = new RoutineContext(_ctx);
        var saved = _stmt;
        foreach (var m in p.Members)
        {
            _stmt = m;
            if (m is DeclareStmt d)
            {
                BindDeclare(d, packageLevel: true);
            }
            else
            {
                BindStmt(m);
            }
        }

        _stmt = saved;
        _ctx = savedCtx;
        _package = savedPkg;
    }

    // ------------------------------------------------------------ ALTER / DROP / COMMENT

    private void BindAlterTable(AlterTableStmt at)
    {
        var obj = ResolveRelationRef(at.Name);
        var scope = TableScope(obj);
        var src = TableSource(obj);
        foreach (var a in at.Actions)
        {
            var saved = _stmt;
            switch (a.Kind)
            {
                case AlterKind.AddColumn when a.Column is { } col:
                    DefineColumn(obj, col, scope);
                    break;
                case AlterKind.DropColumn when a.Name is { } name:
                    EmitColumn(src, name);
                    obj.Columns.RemoveAll(c => c.Key == name.Key);
                    break;
                case AlterKind.RenameColumn when a.Name is { } old && a.NewName is { } nn:
                    {
                        var oldSym = EmitColumn(src, old);
                        var sym = Sym(obj, SymbolNames.Term(nn.Key));
                        obj.Columns.RemoveAll(c => c.Key == old.Key);
                        obj.AddColumn(new ColumnInfo(nn.Key, nn.Text, sym));
                        var docs = new List<string> { Code(StmtText(a.Column ?? (Stmt)a)), "Renamed from " + old.Text + "." };
                        Def(nn.Token, sym, Kind.Field, nn.Text, docs, Range(a));
                        if (oldSym is not null)
                        {
                            Relate(sym, oldSym, reference: true);
                        }

                        if (a.Column?.TypeName is { } tn)
                        {
                            ResolveTypeRef(tn);
                        }

                        break;
                    }

                case AlterKind.RenameTable:
                    if (a.NewTable is { } to)
                    {
                        RenameRelation(obj, to);
                    }
                    else if (a.Name is { } schema)
                    {
                        RenameRelation(obj, new QName([schema, new Ident(schema.Token, obj.Name, obj.NameKey, false)]), emitDefinition: false);
                    }

                    break;
                case AlterKind.AddConstraint when a.Constraint is { } c:
                    _stmt = a;
                    BindConstraint(obj, c, scope, null);
                    break;
                case AlterKind.DropConstraint when a.Name is { } cn:
                    if (obj.Members.TryGetValue(cn.Key, out var csym) || LookupBySymbol(Sym(obj, SymbolNames.Meta(cn.Key))) is not null)
                    {
                        Ref(cn.Token, csym ?? Sym(obj, SymbolNames.Meta(cn.Key)), Kind.Key, cn.Text);
                        obj.Members.Remove(cn.Key);
                    }

                    break;
                case AlterKind.RenameConstraint when a.Name is { } from && a.NewName is { } nn2:
                    {
                        if (obj.Members.TryGetValue(from.Key, out var fsym))
                        {
                            Ref(from.Token, fsym, Kind.Key, from.Text);
                            obj.Members.Remove(from.Key);
                        }

                        var sym = Sym(obj, SymbolNames.Meta(nn2.Key));
                        obj.Members[nn2.Key] = sym;
                        Def(nn2.Token, sym, Kind.Key, nn2.Text, [Code(StmtText(a))], Range(a));
                        break;
                    }

                case AlterKind.AlterColumn when a.Name is { } colName:
                    if (a.Column is { } redefined && FindColumn(obj, colName.Key) is null)
                    {
                        DefineColumn(obj, redefined, scope);
                        break;
                    }

                    EmitColumn(src, colName);
                    if (a.Column?.TypeName is { } atn)
                    {
                        ResolveTypeRef(atn);
                    }

                    if (a.Column is { } c2)
                    {
                        BindExprs(c2.Exprs, scope);
                        foreach (var cons in c2.Constraints)
                        {
                            BindConstraint(obj, cons, scope, FindColumn(obj, colName.Key)?.Symbol);
                        }
                    }

                    BindExprs(a.Exprs, scope);
                    break;
            }

            _stmt = saved;
        }
    }

    private void RenameRelation(DbObject obj, QName to, bool emitDefinition = true)
    {
        var (dbId, schemaId, nameId) = Split(to);
        var db = dbId?.Key ?? obj.DbKey;
        var schema = schemaId?.Key ?? obj.SchemaKey;
        var descriptors = (SymbolDescriptor[])[.. SchemaDescriptors(db, schema), SymbolNames.Type(nameId.Key)];
        var renamed = new DbObject(obj.Kind, db, schema, nameId.Key, nameId.Text, descriptors, Sym(descriptors)) { Opaque = obj.Opaque };
        foreach (var c in obj.Columns)
        {
            renamed.AddColumn(c);
        }

        foreach (var (k, v) in obj.Members)
        {
            renamed.Members[k] = v;
        }

        renamed.PrimaryKey.AddRange(obj.PrimaryKey);
        _cat.Remove(obj);
        _cat.Add(renamed);
        _ever.Add(renamed);
        var docs = StmtDocs(obj.Kind.ToString().ToUpperInvariant() + " " + Qualified(renamed));
        docs.Add("Renamed from " + Qualified(obj) + ".");
        if (emitDefinition)
        {
            EmitQualifiers((dbId, schemaId, nameId), renamed);
            Def(nameId.Token, renamed.Symbol, KindOf(renamed), nameId.Text, docs, Range(_stmt));
        }
        else
        {
            EmitQualifiers((null, schemaId, nameId), renamed);
            DefineInfo(renamed.Symbol, KindOf(renamed), nameId.Text, docs);
        }

        Relate(renamed.Symbol, obj.Symbol, reference: true);
    }

    private void BindAlterObject(AlterObjectStmt ao)
    {
        DbObject? obj;
        switch (ao.Kind)
        {
            case ObjectKind.Function or ObjectKind.Procedure:
                obj = ResolveRoutineRef(ao.Name, -1);
                break;
            case ObjectKind.Schema or ObjectKind.Database:
                obj = Lookup(ao.Kind, null, null, ao.Name.Last.Key);
                if (obj is not null)
                {
                    Ref(ao.Name.Last.Token, obj.Symbol, Kind.Namespace, obj.Name);
                }

                break;
            case ObjectKind.Index or ObjectKind.Trigger:
                obj = ResolveMemberRef(ao.Name, null, ao.Kind);
                break;
            case ObjectKind.Package:
                obj = Lookup(ObjectKind.Package, null, Split(ao.Name).Schema?.Key, ao.Name.Last.Key);
                if (obj is not null)
                {
                    Ref(ao.Name.Last.Token, obj.Symbol, Kind.Package, obj.Name);
                }

                break;
            default:
                obj = ResolveRelationRef(ao.Name);
                break;
        }

        if (obj is null)
        {
            return;
        }

        foreach (var label in ao.AddedLabels)
        {
            DefineEnumLabel(obj, label);
        }

        if (ao.RenameTo is { } to && obj.IsRelation && !obj.Unresolved)
        {
            RenameRelation(obj, to);
        }

        BindExprs(ao.Exprs, new Scope(null));
    }

    /// <summary>Resolves an index / trigger / constraint by name (<c>[schema.]name</c>, <c>table.name</c> or with an ON table).</summary>
    private DbObject? ResolveMemberRef(QName name, QName? onTable, ObjectKind kind)
    {
        var last = name.Last;
        DbObject? table = null;
        if (onTable is not null)
        {
            table = ResolveRelationRef(onTable);
        }
        else if (name.Parts.Count >= 2 && FindRelation(new QName(name.Parts.Take(name.Parts.Count - 1).ToList())) is { } t)
        {
            table = ResolveRelationRef(new QName(name.Parts.Take(name.Parts.Count - 1).ToList()));
            _ = t;
        }

        DbObject? member = null;
        foreach (var cat in Catalogs())
        {
            foreach (var m in cat.Candidates(kind, last.Key))
            {
                if (m.Kind != kind && !(kind == ObjectKind.Index && m.Kind == ObjectKind.Constraint))
                {
                    continue;
                }

                if (table is not null && m.Owner?.Symbol != table.Symbol)
                {
                    continue;
                }

                if (table is null && name.Parts.Count >= 2 && m.SchemaKey != name.Parts[^2].Key)
                {
                    continue;
                }

                member = m;
                break;
            }

            if (member is not null)
            {
                break;
            }
        }

        if (member is not null)
        {
            if (table is null && name.Parts.Count >= 2)
            {
                EmitQualifiers((null, name.Parts[^2], last), member);
            }

            Ref(last.Token, member.Symbol, KindOf(member), member.Name);
        }

        return member;
    }

    private void BindDrop(DropStmt d)
    {
        for (var i = 0; i < d.Names.Count; i++)
        {
            var name = d.Names[i];
            switch (d.Kind)
            {
                case ObjectKind.Function or ObjectKind.Procedure:
                    {
                        var sig = i < d.Signatures.Count ? d.Signatures[i] : null;
                        var routine = sig is not null && Overloading
                            ? ResolveRoutineBySignature(name, string.Join(",", sig.Select(NormalizeType)))
                            : ResolveRoutineRef(name, -1);
                        if (routine is not null && _cat.BySymbol(routine.Symbol) is { } current)
                        {
                            _cat.Remove(current);
                        }

                        break;
                    }

                case ObjectKind.Index or ObjectKind.Trigger:
                    {
                        var m = ResolveMemberRef(name, d.OnTable, d.Kind);
                        if (m is not null)
                        {
                            _cat.Remove(m);
                            m.Owner?.Members.Remove(m.NameKey);
                        }

                        break;
                    }

                case ObjectKind.Schema or ObjectKind.Database:
                    if (Lookup(d.Kind, null, null, name.Last.Key) is { } s)
                    {
                        Ref(name.Last.Token, s.Symbol, Kind.Namespace, s.Name);
                    }

                    break;
                case ObjectKind.Package:
                    if (Lookup(ObjectKind.Package, null, Split(name).Schema?.Key, name.Last.Key) is { } p)
                    {
                        Ref(name.Last.Token, p.Symbol, Kind.Package, p.Name);
                        _cat.Remove(p);
                    }

                    break;
                case ObjectKind.Unknown:
                    break;
                default:
                    {
                        var found = FindRelation(name);
                        if (found is null)
                        {
                            break; // DROP ... IF EXISTS of something never created
                        }

                        ResolveRelationRef(name);
                        if (_cat.BySymbol(found.Symbol) is { } current)
                        {
                            _cat.Remove(current);
                        }

                        break;
                    }
            }
        }
    }

    private DbObject? ResolveRoutineBySignature(QName name, string signature)
    {
        var parts = Split(name);
        var found = Lookup(ObjectKind.Function, parts.Db?.Key, parts.Schema?.Key, parts.Name.Key, o => o.Signature == signature)
                    ?? Lookup(ObjectKind.Function, parts.Db?.Key, parts.Schema?.Key, parts.Name.Key);
        if (found is null)
        {
            return null;
        }

        EmitQualifiers(parts, found);
        Ref(parts.Name.Token, found.Symbol, KindOf(found), found.Name);
        return found;
    }

    private void BindCommentOn(CommentOnStmt c)
    {
        string? target = null;
        var parts = c.Target.Parts;
        switch (c.Kind)
        {
            case ObjectKind.Column when parts.Count >= 2:
                {
                    var table = ResolveRelationRef(new QName(parts.Take(parts.Count - 1).ToList()));
                    target = EmitColumn(TableSource(table), parts[^1]);
                    break;
                }

            case ObjectKind.Function or ObjectKind.Procedure:
                target = ResolveRoutineRef(c.Target, -1)?.Symbol;
                break;
            case ObjectKind.Trigger or ObjectKind.Constraint or ObjectKind.Index when parts.Count >= 2 && c.Kind != ObjectKind.Index:
                {
                    var table = ResolveRelationRef(new QName(parts.Take(parts.Count - 1).ToList()));
                    if (table.Members.TryGetValue(parts[^1].Key, out var msym) || FindMemberSymbol(table, parts[^1].Key) is { } found && (msym = found) is not null)
                    {
                        Ref(parts[^1].Token, msym, Kind.Key, parts[^1].Text);
                        target = msym;
                    }

                    break;
                }

            case ObjectKind.Index:
                target = ResolveMemberRef(c.Target, null, ObjectKind.Index)?.Symbol;
                break;
            case ObjectKind.Schema or ObjectKind.Database:
                if (Lookup(c.Kind, null, null, parts[^1].Key) is { } s)
                {
                    Ref(parts[^1].Token, s.Symbol, Kind.Namespace, s.Name);
                    target = s.Symbol;
                }

                break;
            case ObjectKind.Table or ObjectKind.View or ObjectKind.MaterializedView or ObjectKind.Sequence or ObjectKind.Type or ObjectKind.Domain:
                target = ResolveRelationRef(c.Target).Symbol;
                break;
        }

        if (target is not null && !_emit)
        {
            _state.AddDoc(target, c.Text);
        }
    }

    private string? FindMemberSymbol(DbObject table, string key)
    {
        foreach (var cat in Catalogs())
        {
            if (cat.BySymbol(table.Symbol) is { } t && t.Members.TryGetValue(key, out var s))
            {
                return s;
            }
        }

        return null;
    }
}
