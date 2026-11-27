using ScipSharp.Indexer.Sql.Syntax;

namespace ScipSharp.Indexer.Sql.Semantics;

/// <summary>Builds canonical global symbols: <c>scip-sql . &lt;package&gt; &lt;version&gt; [db/]schema/object#column.</c></summary>
internal sealed class SymbolNames(string package, string version)
{
    public const string Scheme = "scip-sql";

    public string Format(IEnumerable<SymbolDescriptor> descriptors)
    {
        var symbol = new Symbol { Scheme = Scheme, Package = new Package { Manager = "", Name = package, Version = version } };
        symbol.Descriptors.AddRange(descriptors);
        return SymbolFormatter.Verbose.Format(symbol);
    }

    public static SymbolDescriptor Namespace(string name) => new() { Name = name, Suffix = SymbolDescriptor.Types.Suffix.Namespace };

    public static SymbolDescriptor Type(string name) => new() { Name = name, Suffix = SymbolDescriptor.Types.Suffix.Type };

    public static SymbolDescriptor Term(string name) => new() { Name = name, Suffix = SymbolDescriptor.Types.Suffix.Term };

    public static SymbolDescriptor Meta(string name) => new() { Name = name, Suffix = SymbolDescriptor.Types.Suffix.Meta };

    public static SymbolDescriptor Parameter(string name) => new() { Name = name, Suffix = SymbolDescriptor.Types.Suffix.Parameter };

    public static SymbolDescriptor Method(string name, string disambiguator) =>
        new() { Name = name, Disambiguator = disambiguator, Suffix = SymbolDescriptor.Types.Suffix.Method };
}

/// <summary>A column (or composite-type attribute, view output column, routine OUT column).</summary>
internal sealed class ColumnInfo(string key, string name, string symbol)
{
    public string Key { get; } = key;

    public string Name { get; } = name;

    public string Symbol { get; } = symbol;
}

/// <summary>A catalog object: schema, relation (table, view, sequence, type, ...), routine, package, index, trigger.</summary>
internal sealed class DbObject(ObjectKind kind, string dbKey, string schemaKey, string nameKey, string name, SymbolDescriptor[] descriptors, string symbol)
{
    public ObjectKind Kind { get; } = kind;

    public string DbKey { get; } = dbKey;

    public string SchemaKey { get; } = schemaKey;

    public string NameKey { get; } = nameKey;

    /// <summary>Display name as written at the definition.</summary>
    public string Name { get; } = name;

    public SymbolDescriptor[] Descriptors { get; } = descriptors;

    public string Symbol { get; } = symbol;

    /// <summary>True when the column list is unknown (unresolved table, failed parse, <c>SELECT *</c> from an unknown source).</summary>
    public bool Opaque { get; set; }

    /// <summary>True for objects synthesized for unresolved references.</summary>
    public bool Unresolved { get; init; }

    public List<ColumnInfo> Columns { get; } = [];

    /// <summary>Every column the object ever had (kept after DROP/RENAME COLUMN, for references in older code).</summary>
    public Dictionary<string, ColumnInfo> EverColumns { get; } = new(StringComparer.Ordinal);

    /// <summary>Named constraints, indexes and triggers of a table (name key → symbol).</summary>
    public Dictionary<string, string> Members { get; } = new(StringComparer.Ordinal);

    /// <summary>Primary-key column symbols (target of a FOREIGN KEY without a column list).</summary>
    public List<string> PrimaryKey { get; } = [];

    /// <summary>Routine overload signature (normalized parameter types) and arity.</summary>
    public string Signature { get; init; } = "";

    public int MinArgs { get; set; }

    public int MaxArgs { get; set; }

    /// <summary>Routine parameters (key → symbol), positional list and OUT/TABLE columns.</summary>
    public List<ColumnInfo> Params { get; } = [];

    /// <summary>For routines returning a set of rows: the result columns.</summary>
    public List<ColumnInfo>? ResultColumns { get; set; }

    /// <summary>For routines returning a relation type (<c>SETOF orders</c>) and table-typed variables.</summary>
    public string? ResultRelationSymbol { get; set; }

    public bool ReturnsTrigger { get; set; }

    /// <summary>Package that declares this routine or variable (its symbol).</summary>
    public string? Package { get; init; }

    /// <summary>Owning table of an index, constraint or trigger.</summary>
    public DbObject? Owner { get; init; }

    public ColumnInfo? FindColumn(string key)
    {
        foreach (var c in Columns)
        {
            if (c.Key == key)
            {
                return c;
            }
        }

        return null;
    }

    public void AddColumn(ColumnInfo c)
    {
        var i = Columns.FindIndex(x => x.Key == c.Key);
        if (i >= 0)
        {
            Columns[i] = c;
        }
        else
        {
            Columns.Add(c);
        }

        EverColumns[c.Key] = c;
    }

    public bool IsRelation => Kind is ObjectKind.Table or ObjectKind.View or ObjectKind.MaterializedView or ObjectKind.Sequence
        or ObjectKind.Type or ObjectKind.Domain or ObjectKind.Unknown;

    public override string ToString() => Symbol;
}

/// <summary>The project-wide catalog: name-keyed namespaces of catalog objects (several objects may share a name).</summary>
internal sealed class Catalog
{
    private readonly Dictionary<string, List<DbObject>> _relations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DbObject>> _routines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DbObject>> _schemas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DbObject>> _packages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DbObject>> _members = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DbObject> _bySymbol = new(StringComparer.Ordinal);

    public DbObject? BySymbol(string symbol) => _bySymbol.GetValueOrDefault(symbol);

    private Dictionary<string, List<DbObject>> Namespace(ObjectKind kind) => kind switch
    {
        ObjectKind.Function or ObjectKind.Procedure => _routines,
        ObjectKind.Schema or ObjectKind.Database => _schemas,
        ObjectKind.Package => _packages,
        ObjectKind.Index or ObjectKind.Trigger or ObjectKind.Constraint => _members,
        _ => _relations,
    };

    /// <summary>Adds (or replaces the object with the same symbol).</summary>
    public void Add(DbObject o)
    {
        var ns = Namespace(o.Kind);
        if (!ns.TryGetValue(o.NameKey, out var list))
        {
            ns[o.NameKey] = list = [];
        }

        var i = list.FindIndex(x => x.Symbol == o.Symbol);
        if (i >= 0)
        {
            list[i] = o;
        }
        else
        {
            list.Add(o);
        }

        _bySymbol[o.Symbol] = o;
    }

    public void Remove(DbObject o)
    {
        if (Namespace(o.Kind).TryGetValue(o.NameKey, out var list))
        {
            list.RemoveAll(x => x.Symbol == o.Symbol);
        }

        _bySymbol.Remove(o.Symbol);
    }

    public IReadOnlyList<DbObject> Candidates(ObjectKind kind, string nameKey) =>
        Namespace(kind).TryGetValue(nameKey, out var list) ? list : [];

    /// <summary>
    /// Finds an object by (optional) database and schema; without a schema the search path is tried in order, then
    /// any schema (so that objects of a project that uses several schemas resolve without a search path).
    /// </summary>
    public DbObject? Find(ObjectKind kind, string? db, string? schema, string name, IReadOnlyList<string> searchPath, string currentDb, Func<DbObject, bool>? filter = null, string? package = null)
    {
        var list = Candidates(kind, name);
        if (list.Count == 0)
        {
            return null;
        }

        DbObject? best = null;
        var bestScore = int.MinValue;
        foreach (var o in list)
        {
            if (filter is not null && !filter(o))
            {
                continue;
            }

            if (o.Package != package)
            {
                continue;
            }

            if (schema is not null && o.SchemaKey != schema)
            {
                continue;
            }

            if (db is not null && o.DbKey != db && !(db.Length > 0 && o.DbKey.Length == 0))
            {
                continue;
            }

            var score = 0;
            if (schema is null)
            {
                var idx = IndexOf(searchPath, o.SchemaKey);
                score += idx >= 0 ? 100 - idx : 0;
            }

            if (o.DbKey == (db ?? currentDb))
            {
                score += 10;
            }

            if (score > bestScore)
            {
                best = o;
                bestScore = score;
            }
        }

        return best;
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>Information gathered in the first (catalog-only) pass and used when emitting in the second.</summary>
internal sealed class ProjectState
{
    /// <summary><c>COMMENT ON</c> texts by symbol.</summary>
    public Dictionary<string, List<string>> ExtraDocs { get; } = new(StringComparer.Ordinal);

    /// <summary>Relationships (FK → referenced column, view column → source column, column → type) by symbol.</summary>
    public Dictionary<string, List<Relationship>> Relationships { get; } = new(StringComparer.Ordinal);

    /// <summary>Trigger functions (PostgreSQL) → the table of the trigger that executes them.</summary>
    public Dictionary<string, string> TriggerTables { get; } = new(StringComparer.Ordinal);

    /// <summary>Relationships already written to some document (each is emitted once per index).</summary>
    public HashSet<(string From, string To)> EmittedRelationships { get; } = [];

    /// <summary>Global symbols that got a definition (and SymbolInformation) in some document.</summary>
    public HashSet<string> Defined { get; } = new(StringComparer.Ordinal);

    /// <summary>Global symbols referenced anywhere, with a kind and display name for external symbol information.</summary>
    public Dictionary<string, (SymbolInformation.Types.Kind Kind, string Display)> Referenced { get; } = new(StringComparer.Ordinal);

    public void AddDoc(string symbol, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (!ExtraDocs.TryGetValue(symbol, out var list))
        {
            ExtraDocs[symbol] = list = [];
        }

        if (!list.Contains(text))
        {
            list.Add(text);
        }
    }

    public void AddRelationship(string symbol, Relationship rel)
    {
        if (symbol == rel.Symbol)
        {
            return;
        }

        if (!Relationships.TryGetValue(symbol, out var list))
        {
            Relationships[symbol] = list = [];
        }

        var existing = list.FindIndex(r => r.Symbol == rel.Symbol);
        if (existing >= 0)
        {
            var e = list[existing];
            e.IsReference |= rel.IsReference;
            e.IsTypeDefinition |= rel.IsTypeDefinition;
            e.IsImplementation |= rel.IsImplementation;
            e.IsDefinition |= rel.IsDefinition;
            return;
        }

        list.Add(rel);
    }
}
