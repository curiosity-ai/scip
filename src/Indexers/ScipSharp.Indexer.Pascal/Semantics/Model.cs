using ScipSharp.Indexer.Pascal.Syntax;
using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

internal enum SymKind
{
    Unit,
    Program,
    Type,
    TypeParam,
    Const,
    Var,
    Field,
    Routine,
    Param,
    Property,
    EnumMember,
    Label,
    Result,
    Self,
}

internal enum RoutineKind
{
    Procedure,
    Function,
    Constructor,
    Destructor,
    Operator,
}

/// <summary>Package coordinates of a SCIP symbol (<c>scip-pascal &lt;manager&gt; &lt;name&gt; &lt;version&gt;</c>).</summary>
internal sealed record PackageInfo(string Manager, string Name, string Version)
{
    public string Prefix => $"scip-pascal {Escape(Manager)} {Escape(Name)} {Escape(Version)} ";

    private static string Escape(string s) => s.Length == 0 ? "." : s.Replace(" ", "  ", StringComparison.Ordinal);
}

/// <summary>A declared entity (unit, type, routine, variable, ...).</summary>
internal sealed class Sym(string name, SymKind kind, Sym? owner)
{
    public string Name { get; } = name;

    public SymKind Kind { get; set; } = kind;

    public Sym? Owner { get; } = owner;

    /// <summary>The global SCIP symbol, or <c>null</c> for a document-local symbol.</summary>
    public string? Symbol { get; set; }

    public bool IsLocal => Symbol is null;

    /// <summary>Value type (variables, fields, constants, parameters, function results via <see cref="Routine"/>) or the declared type (type symbols).</summary>
    public PType? Type { get; set; }

    public ScipKind ScipKind { get; set; }

    public string? Signature { get; set; }

    public string? Documentation { get; set; }

    public RoutineInfo? Routine { get; set; }

    /// <summary>For units/programs: the compile unit.</summary>
    public CompileUnit? Unit { get; set; }

    /// <summary>Declared outside the indexed project (built-in table, search path unit, or inferred).</summary>
    public bool External { get; set; }

    /// <summary>Created on the fly for an unresolved name: its kind is a guess.</summary>
    public bool Inferred { get; set; }

    /// <summary>A forward class declaration (<c>TFoo = class;</c>) awaiting its full declaration.</summary>
    public bool IsForwardType { get; set; }

    /// <summary>Static member (<c>class var</c>, <c>class procedure</c>, ...).</summary>
    public bool IsStatic { get; set; }

    /// <summary>Generic type parameters of a type or routine.</summary>
    public List<Sym>? TypeParams { get; set; }

    /// <summary>The document holding the definition (locals only).</summary>
    public SourceText? HomeSource { get; set; }

    /// <summary>SymbolInformation of global symbols, attached to the defining document (or emitted as external).</summary>
    public SymbolInformation? Info { get; set; }

    /// <summary>A definition occurrence was emitted into a project document.</summary>
    public bool DefinedInDocument { get; set; }

    public List<Relationship> Relationships { get; } = [];

    /// <summary>Type parameter index (for <see cref="SymKind.TypeParam"/>).</summary>
    public int Index { get; set; }

    public override string ToString() => Symbol ?? $"local {Name}";
}

internal sealed class RoutineInfo(RoutineKind kind)
{
    public RoutineKind Kind { get; set; } = kind;

    public List<Sym> Params { get; set; } = [];

    public PType? Result { get; set; }

    public bool HasBody { get; set; }

    /// <summary>Lower-cased parameter type list, used to match bodies with their forward/interface declarations.</summary>
    public string SignatureKey { get; set; } = "";

    public int MinArgs { get; set; }

    public int MaxArgs { get; set; }

    public bool IsOverride { get; set; }

    public bool IsOverload { get; set; }

    public bool IsForward { get; set; }

    /// <summary>Declaration occurrences emitted as definitions; turned into forward definitions once a body appears.</summary>
    public List<Occurrence> DeclarationOccurrences { get; } = [];

    /// <summary>The document holding the parameter locals of the declaration.</summary>
    public SourceText? ParamSource { get; set; }
}

internal enum TypeKind
{
    Unknown,
    Simple,
    String,
    Record,
    Class,
    Object,
    Interface,
    Helper,
    Pointer,
    Array,
    Set,
    File,
    Enum,
    Subrange,
    Proc,
    ClassRef,
    TypeParam,
    Specialization,
}

/// <summary>A (structural) Pascal type. Named types share the instance of their declaration.</summary>
internal sealed class PType(TypeKind kind)
{
    public TypeKind Kind { get; set; } = kind;

    /// <summary>The type symbol that declared this type (if any).</summary>
    public Sym? Decl { get; set; }

    /// <summary>Members of records, classes, objects, interfaces, helpers.</summary>
    public Scope? Members { get; set; }

    public List<PType> Ancestors { get; } = [];

    /// <summary>Pointer target, array/set/file element, class-ref target, helper target, subrange base.</summary>
    public PType? Elem { get; set; }

    /// <summary>Procedural type result.</summary>
    public PType? Result { get; set; }

    /// <summary>Generic specialization: the generic type and its arguments.</summary>
    public PType? Generic { get; set; }

    public List<PType>? Args { get; set; }

    public Sym? DefaultProperty { get; set; }

    public bool IsStructured => Kind is TypeKind.Record or TypeKind.Class or TypeKind.Object or TypeKind.Interface or TypeKind.Helper;

    public static PType Unknown => new(TypeKind.Unknown);

    public override string ToString() => $"{Kind} {Decl?.Name}";
}

/// <summary>A name-lookup layer.</summary>
internal abstract class Layer
{
    public abstract List<Sym>? Find(string name);
}

/// <summary>A declaration scope (case-insensitive table).</summary>
internal sealed class Scope(Sym? owner) : Layer
{
    private readonly Dictionary<string, List<Sym>> _table = new(StringComparer.OrdinalIgnoreCase);

    public Sym? Owner { get; } = owner;

    public IEnumerable<Sym> All => _table.Values.SelectMany(static v => v);

    public void Add(Sym sym)
    {
        if (!_table.TryGetValue(sym.Name, out var list))
        {
            _table[sym.Name] = list = [];
        }

        if (!list.Contains(sym))
        {
            list.Add(sym);
        }
    }

    public override List<Sym>? Find(string name) => _table.TryGetValue(name, out var list) && list.Count > 0 ? list : null;
}

/// <summary>Members of a type (with ancestors and helpers) visible inside methods and <c>with</c> statements.</summary>
internal sealed class MemberLayer(PType type, TypeSystem types) : Layer
{
    public PType Type { get; } = type;

    public override List<Sym>? Find(string name) => types.FindMember(Type, name);
}
