using ScipSharp.Indexer.Cobol.Syntax;

namespace ScipSharp.Indexer.Cobol.Analysis;

internal enum UnitKind
{
    Program,
    Class,
    Method,
    Function,
}

internal enum Region : byte
{
    None,
    Identification,
    CommentEntry,
    Environment,
    Data,
    Procedure,
    Exec,
}

internal enum ExecKind : byte
{
    None,
    Sql,
    Cics,
    Other,
}

/// <summary>Something the indexer defines a symbol for.</summary>
internal abstract class Definition(string name, Token? nameToken)
{
    /// <summary>Canonical (upper-case) name.</summary>
    public string Name { get; } = name.ToUpperInvariant();

    /// <summary>Name as written.</summary>
    public string DisplayName { get; } = name;

    public Token? NameToken { get; } = nameToken;

    public string Symbol { get; set; } = "";

    /// <summary>Copybook file id owning the symbol's namespace, or -1 for the program.</summary>
    public int Owner => NameToken?.Origin ?? -1;

    /// <summary>First and last token index (inclusive) of the construct, for enclosing ranges.</summary>
    public int StartIndex { get; set; } = -1;

    public int EndIndex { get; set; } = -1;

    public string Documentation { get; set; } = "";

    public abstract SymbolInformation.Types.Kind Kind { get; }
}

internal sealed class ProgramUnit(string name, Token? nameToken, UnitKind unitKind, ProgramUnit? parent, int fileId) : Definition(name, nameToken)
{
    public UnitKind UnitKind { get; } = unitKind;

    public ProgramUnit? Parent { get; } = parent;

    /// <summary>The file being indexed that contains the unit.</summary>
    public int FileId { get; } = fileId;

    public List<ProgramUnit> Children { get; } = [];

    public bool IsCommon { get; set; }

    /// <summary>True for the anonymous unit that holds a copybook indexed on its own.</summary>
    public bool IsPseudo { get; init; }

    public List<DataItem> Items { get; } = [];

    public Dictionary<string, List<DataItem>> ItemsByName { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, FileDefinition> Files { get; } = new(StringComparer.Ordinal);

    public List<Procedure> Procedures { get; } = [];

    public Dictionary<string, List<Procedure>> ProceduresByName { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, SimpleDefinition> Specials { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, SimpleDefinition> Indexes { get; } = new(StringComparer.Ordinal);

    public List<SimpleDefinition> Entries { get; } = [];

    public override SymbolInformation.Types.Kind Kind => UnitKind switch
    {
        UnitKind.Class => SymbolInformation.Types.Kind.Class,
        UnitKind.Method => SymbolInformation.Types.Kind.Method,
        UnitKind.Function => SymbolInformation.Types.Kind.Function,
        _ => SymbolInformation.Types.Kind.Module,
    };

    public IEnumerable<ProgramUnit> SelfAndAncestors()
    {
        for (var u = this; u is not null; u = u.Parent)
        {
            yield return u;
        }
    }

    public void AddItem(DataItem item)
    {
        Items.Add(item);
        if (item.IsNamed)
        {
            if (!ItemsByName.TryGetValue(item.Name, out var list))
            {
                ItemsByName[item.Name] = list = [];
            }

            list.Add(item);
        }
    }

    public void AddProcedure(Procedure p)
    {
        Procedures.Add(p);
        if (!ProceduresByName.TryGetValue(p.Name, out var list))
        {
            ProceduresByName[p.Name] = list = [];
        }

        list.Add(p);
    }
}

internal sealed class DataItem(string name, Token? nameToken, int level, DataItem? parent) : Definition(name, nameToken)
{
    public int Level { get; } = level;

    public DataItem? Parent { get; } = parent;

    public List<DataItem> Children { get; } = [];

    /// <summary>The FD/SD this record (or its ancestor record) belongs to.</summary>
    public FileDefinition? File { get; set; }

    public bool IsGlobal { get; set; }

    public bool IsNamed => NameToken is not null;

    public string? Picture { get; set; }

    public DataItem Root
    {
        get
        {
            var r = this;
            while (r.Parent is not null)
            {
                r = r.Parent;
            }

            return r;
        }
    }

    public override SymbolInformation.Types.Kind Kind => Level switch
    {
        88 or 78 => SymbolInformation.Types.Kind.Constant,
        _ when Children.Any(c => c.Level != 88) => SymbolInformation.Types.Kind.Struct,
        1 or 77 => SymbolInformation.Types.Kind.Variable,
        _ => SymbolInformation.Types.Kind.Field,
    };
}

internal sealed class Procedure(string name, Token nameToken, bool isSection, Procedure? section) : Definition(name, nameToken)
{
    public bool IsSection { get; } = isSection;

    public Procedure? Section { get; } = section;

    public override SymbolInformation.Types.Kind Kind => IsSection ? SymbolInformation.Types.Kind.Namespace : SymbolInformation.Types.Kind.Function;
}

internal sealed class FileDefinition(string name, Token? nameToken) : Definition(name, nameToken)
{
    public bool IsGlobal { get; set; }

    public override SymbolInformation.Types.Kind Kind => SymbolInformation.Types.Kind.File;
}

internal enum SimpleKind
{
    Index,
    Special,
    Entry,
}

internal sealed class SimpleDefinition(string name, Token nameToken, SimpleKind simpleKind, DataItem? item) : Definition(name, nameToken)
{
    public SimpleKind SimpleKind { get; } = simpleKind;

    /// <summary>For an index name: the table item it indexes.</summary>
    public DataItem? Item { get; } = item;

    public override SymbolInformation.Types.Kind Kind => SimpleKind switch
    {
        SimpleKind.Index => SymbolInformation.Types.Kind.Variable,
        SimpleKind.Entry => SymbolInformation.Types.Kind.Function,
        _ => SymbolInformation.Types.Kind.Constant,
    };
}
