using System.Runtime.CompilerServices;
using ScipSharp.Indexer.Sql.Lexing;
using ScipSharp.Indexer.Sql.Syntax;
using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Sql.Semantics;

/// <summary>An output column of a query, CTE or derived table.</summary>
internal sealed class OutCol(string key, string name, string? symbol)
{
    public string Key { get; } = key;

    public string Name { get; } = name;

    /// <summary>The symbol a reference to this column resolves to (alias local, view column, or the source column).</summary>
    public string? Symbol { get; set; } = symbol;

    /// <summary>The source column when the output is a plain column reference (for relationships).</summary>
    public string? Source { get; init; }

    /// <summary>Where a definition for a derived object's column can be placed when there is no alias.</summary>
    public Token? At { get; init; }

    public bool Aliased { get; init; }
}

/// <summary>The output shape of a query.</summary>
internal sealed class Shape(List<OutCol> columns, bool opaque)
{
    public List<OutCol> Columns { get; } = columns;

    public bool Opaque { get; set; } = opaque;

    public static Shape Unknown => new([], true);
}

/// <summary>A FROM-clause source visible in a scope.</summary>
internal sealed class Source
{
    /// <summary>Alias key, or the table name key when not aliased.</summary>
    public string? Key { get; init; }

    public string? TableKey { get; init; }

    public string? SchemaKey { get; init; }

    public string? AliasSymbol { get; init; }

    public DbObject? Object { get; init; }

    public List<OutCol>? Columns { get; init; }

    public bool Opaque { get; init; }

    /// <summary>NEW / OLD / inserted / deleted / excluded: qualifier occurrences are not emitted.</summary>
    public bool Pseudo { get; init; }
}

internal sealed class Scope(Scope? parent)
{
    public Scope? Parent { get; } = parent;

    public List<Source> Sources { get; } = [];

    public Dictionary<string, Source> Ctes { get; } = new(StringComparer.Ordinal);

    public List<OutCol> SelectAliases { get; } = [];
}

/// <summary>A procedural variable or parameter.</summary>
internal sealed class VarInfo(string symbol)
{
    public string Symbol { get; } = symbol;

    /// <summary>Fields of record / cursor / table variables.</summary>
    public List<OutCol>? Fields { get; set; }

    public DbObject? Row { get; set; }
}

/// <summary>Procedural context of a routine, trigger or anonymous block.</summary>
internal sealed class RoutineContext(RoutineContext? parent)
{
    public RoutineContext? Parent { get; } = parent;

    public Dictionary<string, VarInfo> Vars { get; } = new(StringComparer.Ordinal);

    public List<VarInfo> Positional { get; } = [];

    public DbObject? TriggerTable { get; set; }

    public Dictionary<string, bool> TriggerAliases { get; } = new(StringComparer.Ordinal);

    public DbObject? Routine { get; set; }

    /// <summary>Routines declared inside a block (PL/SQL nested procedures).</summary>
    public Dictionary<string, DbObject> LocalRoutines { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Resolves names in parsed SQL against a project-wide catalog and emits SCIP occurrences. Run twice: a catalog-only
/// pass (no emission) collects the final state of every object, comments and relationships; the emitting pass replays
/// the statements in order and falls back to that final state for forward references.
/// </summary>
internal sealed partial class Binder
{
    private static readonly HashSet<string> PseudoColumns =
    [
        "LEVEL", "ROWID", "ROWNUM", "OID", "CTID", "XMIN", "XMAX", "CMIN", "CMAX", "TABLEOID", "SQLCODE", "SQLERRM", "SQLSTATE",
        "FOUND", "NEW", "OLD", "EXCLUDED", "INSERTED", "DELETED", "DUAL", "NEXTVAL", "CURRVAL", "ROW_COUNT", "SYSDATE",
        "USER", "UID", "NOW", "CURRENT", "VALUE", "ROWCOUNT", "NOTFOUND", "ISOPEN", "SQL", "NULL",
    ];

    private readonly SymbolNames _names;
    private readonly ProjectState _state;
    private readonly Catalog _cat = new();
    private readonly Catalog _ever = new();
    private readonly Catalog? _final;
    private readonly Catalog? _finalEver;
    private readonly bool _emit;
    private readonly string? _defaultSchemaOption;
    private readonly Dictionary<string, DbObject> _unresolved = new(StringComparer.Ordinal);

    private DocumentBuilder? _doc;
    private ParsedScript _script = null!;
    private SqlDialect _d;
    private List<string> _searchPath = [];
    private string _defaultSchema = "";
    private string _db = "";
    private RoutineContext _scriptVars = new(null);
    private RoutineContext _session = new(null);
    private RoutineContext _ctx = new(null);
    private DbObject? _package;
    private Stmt? _stmt;
    private int _lastBatch = -1;
    private int _lastSpan = -1;
    private string? _dbtModel;

    public Binder(SymbolNames names, ProjectState state, string? defaultSchema, Binder? catalogPass)
    {
        _names = names;
        _state = state;
        _defaultSchemaOption = defaultSchema;
        _final = catalogPass?._cat;
        _finalEver = catalogPass?._ever;
        _emit = catalogPass is not null;
    }

    public Catalog Catalog => _cat;

    /// <summary>Binds one file. <paramref name="doc"/> receives occurrences in the emitting pass.</summary>
    public void BindFile(ParsedScript script, SqlDialect dialect, DocumentBuilder? doc, string? dbtModel = null)
    {
        _dbtModel = dbtModel;
        _script = script;
        _d = dialect;
        _doc = doc;
        _defaultSchema = _defaultSchemaOption is { Length: > 0 } ds ? dialect.Normalize(ds, quoted: false) : dialect.DefaultSchema();
        _searchPath = [_defaultSchema];
        _db = "";
        _session = new RoutineContext(null);
        _lastBatch = -1;
        _lastSpan = -1;
        _package = null;
        foreach (var s in script.Statements)
        {
            var reset = _d == SqlDialect.TSql ? s.Batch != _lastBatch : s.Span != _lastSpan;
            if (reset)
            {
                _scriptVars = new RoutineContext(_session);
                _lastBatch = s.Batch;
                _lastSpan = s.Span;
            }

            _ctx = _scriptVars;
            _stmt = s;
            try
            {
                BindStmt(s);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException or KeyNotFoundException
                                           or InsufficientExecutionStackException)
            {
                // Tolerant: a statement that trips the binder (or nests too deeply, see EnsureStack) is skipped.
            }
        }
    }

    /// <summary>Guards the recursive walks over nested queries and expressions; caught per statement in <see cref="BindFile"/>.</summary>
    private static void EnsureStack()
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new InsufficientExecutionStackException("SQL nesting too deep");
        }
    }

    // ------------------------------------------------------------ emission helpers

    private static bool IsLocal(string symbol) => symbol.StartsWith("local ", StringComparison.Ordinal);

    private void Ref(Token t, string symbol, Kind kind = Kind.UnspecifiedKind, string? display = null) =>
        RefAt(t.Start, t.End, symbol, kind, display ?? t.Value);

    private void RefAt(int start, int end, string symbol, Kind kind, string display)
    {
        if (!_emit || _doc is null || symbol.Length == 0)
        {
            return;
        }

        _doc.Add(start, end, symbol, SymbolRole.UnspecifiedSymbolRole);
        if (!IsLocal(symbol))
        {
            _state.Referenced.TryAdd(symbol, (kind, display));
        }
    }

    private void Def(Token t, string symbol, Kind kind, string display, IEnumerable<string> docs, (int, int)? enclosing = null) =>
        DefAt(t.Start, t.End, symbol, kind, display, docs, enclosing);

    private void DefAt(int start, int end, string symbol, Kind kind, string display, IEnumerable<string> docs, (int, int)? enclosing = null)
    {
        if (!_emit || _doc is null)
        {
            return;
        }

        _doc.Add(start, end, symbol, SymbolRole.Definition, enclosing);
        DefineInfo(symbol, kind, display, docs);
    }

    /// <summary>Symbol information without an occurrence (columns inherited through LIKE, expanded <c>*</c> without a token).</summary>
    private void DefineInfo(string symbol, Kind kind, string display, IEnumerable<string> docs)
    {
        if (!_emit || _doc is null)
        {
            return;
        }

        var allDocs = docs.ToList();
        IEnumerable<Relationship>? rels = null;
        if (!IsLocal(symbol))
        {
            _state.Defined.Add(symbol);
            if (_state.ExtraDocs.TryGetValue(symbol, out var extra))
            {
                allDocs.AddRange(extra.Where(e => !allDocs.Contains(e)));
            }

            if (_state.Relationships.TryGetValue(symbol, out var r))
            {
                var fresh = r.Where(rel => _state.EmittedRelationships.Add((symbol, rel.Symbol))).ToList();
                rels = fresh;
                foreach (var rel in fresh)
                {
                    MarkReferenced(rel.Symbol);
                }
            }
        }

        _doc.AddInfo(symbol, kind, display, allDocs, rels);
    }

    private void MarkReferenced(string symbol)
    {
        if (!IsLocal(symbol))
        {
            _state.Referenced.TryAdd(symbol, (Kind.UnspecifiedKind, ""));
        }
    }

    private void Relate(string from, string to, bool reference = false, bool typeDefinition = false)
    {
        if (from == to || IsLocal(from) || IsLocal(to))
        {
            return;
        }

        var rel = new Relationship { Symbol = to, IsReference = reference, IsTypeDefinition = typeDefinition };
        _state.AddRelationship(from, rel);
        if (_emit && _doc?.Info(from) is { } info && !info.Relationships.Any(x => x.Symbol == to) && _state.EmittedRelationships.Add((from, to)))
        {
            info.Relationships.Add(rel.Clone());
            MarkReferenced(to);
        }
    }

    private string NewLocal() => _doc?.NewLocal() ?? "local 0";

    private static (int, int)? Range(Stmt? s) => s?.First is null || s.Last is null ? null : (s.First.Start, s.Last.End);

    private static string Code(string text) => "```sql\n" + text + "\n```";

    // ------------------------------------------------------------ comments as documentation

    /// <summary>Comments directly above <paramref name="first"/> (no blank line in between).</summary>
    private List<string> LeadingComments(Token first)
    {
        var result = new List<string>();
        var comments = _script.Lex.Comments;
        var text = _script.Source.Text;
        var i = LowerBound(comments, first.Start) - 1;
        var limit = first.Start;
        while (i >= 0)
        {
            var c = comments[i];
            if (c.End > limit || !OnlyBlank(text, c.End, limit, allowOneNewline: true))
            {
                break;
            }

            if (c.Text.StartsWith("--", StringComparison.Ordinal) || c.Text.StartsWith("/*", StringComparison.Ordinal) || c.Text.StartsWith('#'))
            {
                result.Insert(0, CleanComment(c.Text));
            }

            limit = c.Start;
            i--;
        }

        var joined = string.Join('\n', result.Where(r => r.Length > 0));
        return joined.Length == 0 ? [] : [joined];
    }

    /// <summary>A line comment on the same line after <paramref name="last"/> (only <c>,</c> and spaces in between).</summary>
    private string? TrailingComment(Token last)
    {
        var comments = _script.Lex.Comments;
        var text = _script.Source.Text;
        var i = LowerBound(comments, last.End);
        if (i >= comments.Count)
        {
            return null;
        }

        var c = comments[i];
        for (var k = last.End; k < c.Start; k++)
        {
            if (text[k] is not (' ' or '\t' or ','))
            {
                return null;
            }
        }

        return c.IsLineComment ? CleanComment(c.Text) : null;
    }

    private static int LowerBound(List<Comment> comments, int offset)
    {
        int lo = 0, hi = comments.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (comments[mid].Start < offset)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private static bool OnlyBlank(string text, int from, int to, bool allowOneNewline)
    {
        var newlines = 0;
        for (var k = from; k < to; k++)
        {
            if (text[k] == '\n')
            {
                newlines++;
            }
            else if (!char.IsWhiteSpace(text[k]))
            {
                return false;
            }
        }

        return newlines <= (allowOneNewline ? 1 : 0);
    }

    private static string CleanComment(string c)
    {
        if (c.StartsWith("--", StringComparison.Ordinal))
        {
            return c.TrimStart('-').Trim();
        }

        if (c.StartsWith('#'))
        {
            return c.TrimStart('#').Trim();
        }

        if (c.StartsWith("/*", StringComparison.Ordinal))
        {
            var inner = c.Length >= 4 ? c[2..^2] : "";
            var lines = inner.Split('\n').Select(l => l.Trim().TrimStart('*').Trim()).Where(l => l.Length > 0);
            return string.Join('\n', lines);
        }

        return c.Trim();
    }

    // ------------------------------------------------------------ catalog helpers

    private static SymbolDescriptor[] SchemaDescriptors(string db, string schema) =>
        db.Length > 0 ? [SymbolNames.Namespace(db), SymbolNames.Namespace(schema)] : [SymbolNames.Namespace(schema)];

    private string Sym(IEnumerable<SymbolDescriptor> descriptors) => _names.Format(descriptors);

    private string Sym(DbObject parent, SymbolDescriptor leaf) => _names.Format([.. parent.Descriptors, leaf]);

    /// <summary>Splits a qualified object name into (database, schema, name) identifiers.</summary>
    private static (Ident? Db, Ident? Schema, Ident Name) Split(QName q)
    {
        var p = q.Parts;
        var name = p[^1];
        if (p.Count == 1)
        {
            return (null, null, name);
        }

        if (p.Count == 2)
        {
            return (null, p[0], name);
        }

        return (p[^3], p[^2], name);
    }

    private IReadOnlyList<string> SearchPath => _searchPath;

    private DbObject? Lookup(ObjectKind kind, string? db, string? schema, string name, Func<DbObject, bool>? filter = null, string? package = null)
    {
        foreach (var cat in Catalogs())
        {
            var o = cat.Find(kind, db, schema, name, SearchPath, _db, filter, package);
            if (o is not null)
            {
                return o;
            }
        }

        if (schema is not null && schema != _defaultSchema)
        {
            // Oracle-style scripts mix unqualified (default schema) and owner-qualified names.
            foreach (var cat in Catalogs())
            {
                var o = cat.Find(kind, db, _defaultSchema, name, SearchPath, _db, filter, package);
                if (o is not null)
                {
                    return o;
                }
            }
        }

        return null;
    }

    private IEnumerable<Catalog> Catalogs()
    {
        yield return _cat;
        if (_final is not null)
        {
            yield return _final;
        }

        if (_finalEver is not null)
        {
            yield return _finalEver;
        }

        yield return _ever;
    }

    private DbObject? FindRelation(QName q) => FindRelation(q, out _);

    private DbObject? FindRelation(QName q, out (Ident? Db, Ident? Schema, Ident Name) parts)
    {
        parts = Split(q);
        return Lookup(ObjectKind.Table, parts.Db?.Key, parts.Schema?.Key, parts.Name.Key);
    }

    /// <summary>Resolves a relation reference, emitting occurrences for its qualifiers; unknown relations get an unresolved symbol.</summary>
    private DbObject ResolveRelationRef(QName q, Kind kind = Kind.Class)
    {
        var found = FindRelation(q, out var parts);
        var obj = found ?? Unresolved(parts);
        EmitQualifiers(parts, obj);
        Ref(parts.Name.Token, obj.Symbol, obj.Unresolved ? kind : KindOf(obj), obj.Name);
        return obj;
    }

    private void EmitQualifiers((Ident? Db, Ident? Schema, Ident Name) parts, DbObject? obj)
    {
        if (parts.Schema is { } s)
        {
            var dbKey = parts.Db?.Key ?? obj?.DbKey ?? _db;
            var schemaObj = Lookup(ObjectKind.Schema, null, null, s.Key, o => o.DbKey == dbKey || dbKey.Length == 0);
            var schemaSym = schemaObj?.Symbol ?? Sym(SchemaDescriptors(obj is not null && obj.SchemaKey == s.Key ? obj.DbKey : dbKey, s.Key));
            Ref(s.Token, schemaSym, Kind.Namespace, s.Text);
        }

        if (parts.Db is { } d && _d != SqlDialect.MySql)
        {
            var dbObj = Lookup(ObjectKind.Database, null, null, d.Key);
            Ref(d.Token, dbObj?.Symbol ?? Sym([SymbolNames.Namespace(d.Key)]), Kind.Namespace, d.Text);
        }
    }

    private DbObject Unresolved((Ident? Db, Ident? Schema, Ident Name) parts)
    {
        var db = parts.Db?.Key ?? _db;
        var schema = parts.Schema?.Key ?? _searchPath.FirstOrDefault(s => !s.StartsWith('$')) ?? _defaultSchema;
        var name = parts.Name.Key;
        var descriptors = (SymbolDescriptor[])[.. SchemaDescriptors(db, schema), SymbolNames.Type(name)];
        var symbol = Sym(descriptors);
        if (!_unresolved.TryGetValue(symbol, out var obj))
        {
            obj = new DbObject(ObjectKind.Unknown, db, schema, name, parts.Name.Text, descriptors, symbol) { Opaque = true, Unresolved = true };
            _unresolved[symbol] = obj;
        }

        return obj;
    }

    private static Kind KindOf(DbObject o) => o.Kind switch
    {
        ObjectKind.Table => Kind.Class,
        ObjectKind.View or ObjectKind.MaterializedView => Kind.Class,
        ObjectKind.Sequence => Kind.Object,
        ObjectKind.Type => Kind.Type,
        ObjectKind.Domain => Kind.TypeAlias,
        ObjectKind.Function => Kind.Function,
        ObjectKind.Procedure => Kind.Method,
        ObjectKind.Trigger => Kind.Event,
        ObjectKind.Package => Kind.Package,
        ObjectKind.Schema or ObjectKind.Database => Kind.Namespace,
        ObjectKind.Index or ObjectKind.Constraint => Kind.Key,
        _ => Kind.Class,
    };

    /// <summary>Finds a column of <paramref name="o"/> (falling back to the object's final and historical columns).</summary>
    private ColumnInfo? FindColumn(DbObject o, string key)
    {
        if (o.FindColumn(key) is { } c)
        {
            return c;
        }

        foreach (var cat in Catalogs())
        {
            var other = cat.BySymbol(o.Symbol);
            if (other is null)
            {
                continue;
            }

            if (other.FindColumn(key) is { } c2)
            {
                return c2;
            }

            if (other.EverColumns.TryGetValue(key, out var c3))
            {
                return c3;
            }
        }

        if (o.EverColumns.TryGetValue(key, out var c4))
        {
            return c4;
        }

        if (o.ResultRelationSymbol is { } rel && LookupBySymbol(rel) is { } relObj && relObj != o)
        {
            return FindColumn(relObj, key);
        }

        return null;
    }

    private DbObject? LookupBySymbol(string symbol)
    {
        foreach (var cat in Catalogs())
        {
            if (cat.BySymbol(symbol) is { } o)
            {
                return o;
            }
        }

        return null;
    }

    private List<OutCol> ColumnsOf(DbObject o)
    {
        var cols = o.Columns;
        if (cols.Count == 0 && LookupBySymbol(o.Symbol) is { } latest && latest != o)
        {
            cols = latest.Columns;
        }

        if (cols.Count == 0 && o.ResultRelationSymbol is { } rel && LookupBySymbol(rel) is { } relObj && relObj != o)
        {
            return ColumnsOf(relObj);
        }

        return [.. cols.Select(c => new OutCol(c.Key, c.Name, c.Symbol) { Source = c.Symbol })];
    }

    /// <summary>Column of a table source; unknown columns of tables get a symbol under the table (external when never defined).</summary>
    private string? ColumnSymbol(DbObject o, Ident col, bool synthesize)
    {
        if (FindColumn(o, col.Key) is { } c)
        {
            return c.Symbol;
        }

        if (!synthesize || o.Kind is ObjectKind.Function or ObjectKind.Procedure)
        {
            return null;
        }

        return Sym(o, SymbolNames.Term(col.Key));
    }

    // ------------------------------------------------------------ variables

    private VarInfo? FindVar(string key)
    {
        for (var c = _ctx; c is not null; c = c.Parent)
        {
            if (c.Vars.TryGetValue(key, out var v))
            {
                return v;
            }
        }

        if (_package is not null && Lookup(ObjectKind.Table, null, null, key, null, _package.Symbol) is { } pv)
        {
            return new VarInfo(pv.Symbol);
        }

        return null;
    }

    private DbObject? TriggerTable()
    {
        for (var c = _ctx; c is not null; c = c.Parent)
        {
            if (c.TriggerTable is not null)
            {
                return c.TriggerTable;
            }
        }

        return null;
    }

    private bool IsTriggerAlias(string key)
    {
        for (var c = _ctx; c is not null; c = c.Parent)
        {
            if (c.TriggerTable is not null)
            {
                return c.TriggerAliases.ContainsKey(key) || key.ToUpperInvariant() is "NEW" or "OLD" or "INSERTED" or "DELETED";
            }
        }

        return false;
    }

    private RoutineContext? RoutineCtx()
    {
        for (var c = _ctx; c is not null; c = c.Parent)
        {
            if (c.Routine is not null)
            {
                return c;
            }
        }

        return null;
    }

    private static void DeclareVar(RoutineContext ctx, Ident name, VarInfo v) => ctx.Vars[name.Key] = v;

    // ------------------------------------------------------------ expressions

    /// <summary>Binds an expression; returns the column symbol when the expression is a single resolved name.</summary>
    private string? BindExpr(Expr? e, Scope scope)
    {
        if (e is null)
        {
            return null;
        }

        string? single = null;
        foreach (var item in e.Items)
        {
            var s = BindItem(item, scope);
            if (e.Items.Count == 1)
            {
                single = s;
            }
        }

        return single;
    }

    private void BindExprs(IEnumerable<Expr> exprs, Scope scope)
    {
        foreach (var e in exprs)
        {
            BindExpr(e, scope);
        }
    }

    private string? BindItem(ExprItem item, Scope scope)
    {
        EnsureStack();
        switch (item)
        {
            case NameRef n:
                return ResolveName(n.Name, scope);
            case StarRef s:
                if (s.Qualifier is { } q)
                {
                    ResolveQualifier(q, scope);
                }

                return null;
            case FuncCall f:
                BindCall(f, scope, returnRoutine: false);
                return null;
            case SubqueryExpr sq:
                BindQuery(sq.Query, new Scope(scope), definer: null);
                return null;
            case VarRef v:
                return ResolveVarRef(v);
            case TypeRef t:
                ResolveTypeRef(t.Name);
                return null;
            case ObjectRef o:
                ResolveRelationRef(o.Name, Kind.Object);
                return null;
            default:
                return null;
        }
    }

    private DbObject? BindCall(FuncCall f, Scope scope, bool returnRoutine)
    {
        var routine = ResolveRoutineRef(f.Name, f.ArgCount);
        var upper = f.Name.Parts.Count == 1 ? f.Name.Last.Text.ToUpperInvariant() : "";
        if (routine is null && upper is "NEXTVAL" or "CURRVAL" or "SETVAL" && f.FirstStringArg is { } str)
        {
            ResolveStringObject(str, ObjectKind.Sequence);
        }

        foreach (var a in f.Args)
        {
            BindExpr(a, scope);
        }

        foreach (var a in f.Extra)
        {
            BindExpr(a, scope);
        }

        return returnRoutine ? routine : null;
    }

    /// <summary>Resolves an object named in a string literal (<c>nextval('public.seq')</c>).</summary>
    private void ResolveStringObject(Token str, ObjectKind kind)
    {
        if (str.Text.Length < 3 || str.Text[0] != '\'')
        {
            return;
        }

        var content = str.Text[1..^1];
        var parts = new List<Ident>();
        var offset = str.Start + 1;
        foreach (var piece in content.Split('.'))
        {
            var quoted = piece.Length >= 2 && piece[0] == '"' && piece[^1] == '"';
            var text = quoted ? piece[1..^1] : piece;
            if (text.Length == 0 || text.Any(ch => char.IsWhiteSpace(ch) || ch is '(' or ')' or '\''))
            {
                return;
            }

            var tok = new Token(TokenKind.Word, offset, offset + piece.Length, piece, text);
            parts.Add(new Ident(tok, text, _d.Normalize(text, quoted), quoted));
            offset += piece.Length + 1;
        }

        if (parts.Count is 0 or > 3)
        {
            return;
        }

        var q = new QName(parts);
        if (FindRelation(q) is not null || kind == ObjectKind.Sequence)
        {
            ResolveRelationRef(q, Kind.Object);
        }
    }

    private void ResolveTypeRef(QName q)
    {
        if (FindRelation(q) is { } t && t.Kind is ObjectKind.Type or ObjectKind.Domain or ObjectKind.Table)
        {
            ResolveRelationRef(q);
        }
    }

    private string? ResolveVarRef(VarRef v)
    {
        var text = v.Token.Text;
        if (text.StartsWith('$') && int.TryParse(text.AsSpan(1), out var n))
        {
            var r = RoutineCtx();
            if (r is not null && n >= 1 && n <= r.Positional.Count && r.Positional[n - 1].Symbol.Length > 0)
            {
                Ref(v.Token, r.Positional[n - 1].Symbol, Kind.Parameter);
                return r.Positional[n - 1].Symbol;
            }

            return null;
        }

        if (text.StartsWith(':'))
        {
            var key = _d.Normalize(text[1..], quoted: false);
            if (IsTriggerAlias(key) && TriggerTable() is { } table)
            {
                if (v.Field is { } f && ColumnSymbol(table, f, synthesize: !table.Opaque || table.Unresolved) is { } cs)
                {
                    Ref(f.Token, cs, Kind.Field, f.Text);
                    return cs;
                }

                return null;
            }

            if (FindVar(key) is { } bind)
            {
                Ref(v.Token, bind.Symbol, Kind.Variable);
                return bind.Symbol;
            }

            return null;
        }

        if (text.StartsWith("@@", StringComparison.Ordinal))
        {
            return null;
        }

        var vk = _d.Normalize(text, quoted: false);
        var info = FindVar(vk);
        if (info is null && _d is SqlDialect.MySql or SqlDialect.Ansi && text.StartsWith('@'))
        {
            // MySQL user variables need no declaration: the first occurrence defines it for the file.
            var local = NewLocal();
            info = new VarInfo(local);
            _session.Vars[vk] = info;
            Def(v.Token, local, Kind.Variable, text, [Code("user variable " + text)]);
            return local;
        }

        if (info is null)
        {
            return null;
        }

        Ref(v.Token, info.Symbol, Kind.Variable);
        if (v.Field is { } field && info.Fields?.FirstOrDefault(c => c.Key == field.Key) is { Symbol: { } fs })
        {
            Ref(field.Token, fs, Kind.Field, field.Text);
        }

        return info.Symbol;
    }

    // ------------------------------------------------------------ names

    /// <summary>Resolves a source by its qualifier name (alias, table name or CTE) in the scope chain.</summary>
    private static Source? FindSource(Scope scope, string key, string? schemaKey = null)
    {
        for (var s = scope; s is not null; s = s.Parent)
        {
            foreach (var src in s.Sources)
            {
                if (schemaKey is null && src.Key == key)
                {
                    return src;
                }
            }

            foreach (var src in s.Sources)
            {
                if (src.TableKey == key && (schemaKey is null || src.SchemaKey == schemaKey))
                {
                    return src;
                }
            }
        }

        return null;
    }

    private void EmitSourceQualifier(Source src, Ident qualifier)
    {
        if (src.Pseudo)
        {
            return;
        }

        if (src.AliasSymbol is { } a && src.Key == qualifier.Key)
        {
            Ref(qualifier.Token, a, Kind.Variable);
        }
        else if (src.Object is { } o)
        {
            Ref(qualifier.Token, o.Symbol, KindOf(o), o.Name);
        }
    }

    private string? SourceColumn(Source src, Ident col, bool qualified)
    {
        if (src.Columns is { } cols)
        {
            if (cols.FirstOrDefault(c => c.Key == col.Key) is { } oc)
            {
                return oc.Symbol;
            }

            if (src.Object is { } baseObj && src.Opaque)
            {
                return ColumnSymbol(baseObj, col, synthesize: qualified);
            }

            return null;
        }

        if (src.Object is { } o)
        {
            return ColumnSymbol(o, col, synthesize: qualified);
        }

        return null;
    }

    /// <summary>Resolves a qualifier of <c>q.*</c>.</summary>
    private void ResolveQualifier(QName q, Scope scope)
    {
        var last = q.Last;
        var schema = q.Parts.Count >= 2 ? q.Parts[^2].Key : null;
        if (FindSource(scope, last.Key, schema) is { } src)
        {
            EmitSourceQualifier(src, last);
        }
    }

    private string? ResolveName(QName q, Scope scope)
    {
        var parts = q.Parts;
        if (parts.Count == 1)
        {
            return ResolveUnqualified(parts[0], scope);
        }

        // [schema.]table.column or alias.column
        if (parts.Count >= 3 && FindSource(scope, parts[^2].Key, parts[^3].Key) is { } s3)
        {
            if (!s3.Pseudo && s3.Object is { } o3)
            {
                EmitQualifiers((parts.Count >= 4 ? parts[^4] : null, parts[^3], parts[^2]), o3);
            }

            EmitSourceQualifier(s3, parts[^2]);
            return EmitColumn(s3, parts[^1]);
        }

        var qual = parts[0];
        var col = parts[1];
        if (FindSource(scope, qual.Key) is { } src)
        {
            EmitSourceQualifier(src, qual);
            var sym = EmitColumn(src, col);
            if (parts.Count > 2 && sym is not null)
            {
                return null; // composite field access: col.field
            }

            return sym;
        }

        if (IsTriggerAlias(qual.Key) && TriggerTable() is { } table)
        {
            if (ColumnSymbol(table, col, synthesize: true) is { } cs)
            {
                Ref(col.Token, cs, Kind.Field, col.Text);
                return cs;
            }

            return null;
        }

        if (FindVar(qual.Key) is { } v)
        {
            Ref(qual.Token, v.Symbol, Kind.Variable);
            var field = v.Fields?.FirstOrDefault(c => c.Key == col.Key)?.Symbol
                        ?? (v.Row is { } row ? ColumnSymbol(row, col, synthesize: !row.Opaque) : null);
            if (field is not null)
            {
                Ref(col.Token, field, Kind.Field, col.Text);
            }

            return field;
        }

        // Routine parameter qualified by the routine name (PL/pgSQL fn.param).
        if (RoutineCtx() is { Routine: { } routine } rc && routine.NameKey == qual.Key && rc.Vars.TryGetValue(col.Key, out var pv))
        {
            Ref(qual.Token, routine.Symbol, KindOf(routine), routine.Name);
            Ref(col.Token, pv.Symbol, Kind.Parameter, col.Text);
            return pv.Symbol;
        }

        // Package variable: pkg.var
        if (Lookup(ObjectKind.Package, null, parts.Count >= 3 ? parts[^3].Key : null, parts[^2].Key) is { } pkg
            && Lookup(ObjectKind.Table, null, null, parts[^1].Key, null, pkg.Symbol) is { } member)
        {
            Ref(parts[^2].Token, pkg.Symbol, Kind.Package, pkg.Name);
            Ref(parts[^1].Token, member.Symbol, Kind.Variable, member.Name);
            return member.Symbol;
        }

        // Oracle sequences (seq.NEXTVAL) and tables referenced without FROM (e.g. in COMMENT/procedural code).
        var lastUpper = parts[^1].Text.ToUpperInvariant();
        var relationName = new QName(parts.Take(parts.Count - 1).ToList());
        if (FindRelation(relationName) is { } rel)
        {
            if (lastUpper is "NEXTVAL" or "CURRVAL")
            {
                ResolveRelationRef(relationName, Kind.Object);
                return null;
            }

            if (rel.Kind is ObjectKind.Table or ObjectKind.View or ObjectKind.MaterializedView)
            {
                ResolveRelationRef(relationName);
                if (ColumnSymbol(rel, parts[^1], synthesize: true) is { } cs)
                {
                    Ref(parts[^1].Token, cs, Kind.Field, parts[^1].Text);
                    return cs;
                }
            }
        }

        return null;
    }

    private string? EmitColumn(Source src, Ident col)
    {
        var sym = SourceColumn(src, col, qualified: true);
        if (sym is not null)
        {
            Ref(col.Token, sym, Kind.Field, col.Text);
        }

        return sym;
    }

    private string? ResolveUnqualified(Ident id, Scope scope)
    {
        // 1. Columns of the sources in scope (innermost scope first: correlated subqueries see outer sources).
        for (var s = scope; s is not null; s = s.Parent)
        {
            foreach (var src in s.Sources)
            {
                if (SourceColumn(src, id, qualified: false) is { } sym)
                {
                    Ref(id.Token, sym, Kind.Field, id.Text);
                    return sym;
                }
            }
        }

        // 2. Procedural variables and parameters.
        if (FindVar(id.Key) is { } v)
        {
            Ref(id.Token, v.Symbol, Kind.Variable);
            return v.Symbol;
        }

        // 3. Select-list aliases (ORDER BY / GROUP BY / HAVING).
        for (var s = scope; s is not null; s = s.Parent)
        {
            if (s.SelectAliases.FirstOrDefault(a => a.Key == id.Key) is { Symbol: { } aliasSym })
            {
                Ref(id.Token, aliasSym, Kind.Variable);
                return aliasSym;
            }
        }

        if (PseudoColumns.Contains(id.Text.ToUpperInvariant()) && !id.Quoted)
        {
            return null;
        }

        // 4. A sole opaque source (unknown table, SELECT * of an unknown table) takes the column.
        for (var s = scope; s is not null; s = s.Parent)
        {
            if (s.Sources.Count == 0)
            {
                continue;
            }

            if (s.Sources.Count == 1 && s.Sources[0] is { Pseudo: false } only)
            {
                var obj = only.Object;
                if (obj is not null && (only.Columns is null || only.Opaque) && (obj.Opaque || obj.Unresolved || RoutineCtx() is null))
                {
                    var sym = ColumnSymbol(obj, id, synthesize: true);
                    if (sym is not null)
                    {
                        Ref(id.Token, sym, Kind.Field, id.Text);
                        return sym;
                    }
                }
            }

            break;
        }

        return null;
    }
}
