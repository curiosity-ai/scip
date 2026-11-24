namespace Scip.Indexer.Rust;

using Kind = SymbolInformation.Types.Kind;

/// <summary>Name-space membership of an item (a path segment is looked up in one or more of them).</summary>
[Flags]
internal enum NameSpaces
{
    None = 0,
    Type = 1,
    Value = 2,
    Macro = 4,
    Any = Type | Value | Macro,
}

/// <summary>A cargo target compiled as one crate (or one of the synthetic std crates).</summary>
internal sealed class CrateData(string name, PackageInformation package, string targetKind, bool isStd)
{
    public string Name { get; } = name;

    public PackageInformation Package { get; } = package;

    public string TargetKind { get; } = targetKind;

    public bool IsStd { get; } = isStd;

    public Item? Root { get; set; }

    /// <summary>True for crates whose sources lie outside the indexed project (path dependencies elsewhere).</summary>
    public bool External { get; set; }

    /// <summary>Extern crate names visible in this crate (from <c>[dependencies]</c>, the package's own lib, std).</summary>
    public Dictionary<string, CrateData> Deps { get; } = [];

    public List<SourceFileResult> Files { get; } = [];

    /// <summary>macro_rules macros by name (textual scoping is approximated crate-wide).</summary>
    public Dictionary<string, Item> Macros { get; } = [];
}

/// <summary>A nameable definition (module, type, function, ...).</summary>
internal sealed class Item(string name, string symbol, Kind kind, CrateData crate)
{
    public string Name { get; } = name;

    public string Symbol { get; } = symbol;

    public Kind Kind { get; } = kind;

    public CrateData Crate { get; } = crate;

    public Item? Parent { get; set; }

    /// <summary>Set when the item is a member of an impl block instead of a module/type/trait.</summary>
    public ImplData? ParentImpl { get; set; }

    public Dictionary<string, List<Item>> Members { get; } = [];

    /// <summary>Impl blocks whose self type resolves to this item (types only).</summary>
    public List<ImplData> Impls { get; } = [];

    // Type text: source tokens (file/from/to) or std text.
    public SourceFileResult? File { get; set; }

    public int TyFrom { get; set; }

    public int TyTo { get; set; }

    public string? StdTy { get; set; }

    public GenericScope? Generics { get; set; }

    public bool IsExternal => Crate.IsStd || Crate.External;

    public NameSpaces Spaces => Kind switch
    {
        Kind.Module => NameSpaces.Type,
        Kind.Struct or Kind.EnumMember => NameSpaces.Type | NameSpaces.Value,
        Kind.Enum or Kind.Union or Kind.Trait or Kind.TypeAlias or Kind.AssociatedType or Kind.Type => NameSpaces.Type,
        Kind.Macro or Kind.Attribute => NameSpaces.Macro,
        Kind.TypeParameter => NameSpaces.Type,
        _ => NameSpaces.Value,
    };

    public bool IsType => Kind is Kind.Struct or Kind.Enum or Kind.Union or Kind.Trait or Kind.TypeAlias or Kind.AssociatedType or Kind.Type;

    public void AddMember(Item child) => AddMemberAs(child.Name, child);

    public void AddMemberAs(string name, Item child)
    {
        if (!Members.TryGetValue(name, out var list))
        {
            Members[name] = list = [];
        }

        list.Add(child);
    }
}

/// <summary>An impl block (inherent or trait impl).</summary>
internal sealed class ImplData(string symbol, CrateData crate)
{
    public string Symbol { get; } = symbol;

    public CrateData Crate { get; } = crate;

    public SourceFileResult? File { get; set; }

    public ImplRange? Range { get; set; }

    public Item? Module { get; set; }

    public Dictionary<string, List<Item>> Members { get; } = [];

    public RType? SelfType { get; set; }

    public Item? TraitDef { get; set; }

    public bool Linked { get; set; }

    /// <summary>Std impls carry their self type as text.</summary>
    public string? StdSelfText { get; set; }

    public void AddMember(Item child)
    {
        if (!Members.TryGetValue(child.Name, out var list))
        {
            Members[child.Name] = list = [];
        }

        list.Add(child);
    }
}

/// <summary>A (partially known) type: a definition with generic arguments, or a generic parameter.</summary>
internal sealed record RType(Item? Def, string? Param, RType?[] Args)
{
    public static RType Of(Item def, params RType?[] args) => new(def, null, args);

    public static RType ParamOf(string name) => new(null, name, []);
}

/// <summary>One path segment of a <c>use</c> declaration or expression path.</summary>
internal readonly record struct Seg(string Name, int Tok);

/// <summary>A leaf of a use tree.</summary>
internal sealed class UseBinding(List<Seg> path, string? name, int aliasTok, bool glob, int selfTok)
{
    public List<Seg> Path { get; } = path;

    /// <summary>Binding name introduced into the module (alias, or the last segment); null for globs.</summary>
    public string? Name { get; } = name;

    public int AliasTok { get; } = aliasTok;

    public bool Glob { get; } = glob;

    /// <summary>Token of a <c>self</c> leaf inside braces, or -1.</summary>
    public int SelfTok { get; } = selfTok;

    public SourceFileResult? File { get; set; }

    public Item? Module { get; set; }

    public bool Resolving { get; set; }
}
