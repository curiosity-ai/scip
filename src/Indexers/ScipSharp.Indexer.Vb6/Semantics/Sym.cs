using ScipSharp.Indexer.Vb6.Syntax;

namespace ScipSharp.Indexer.Vb6.Semantics;

internal enum SymKind
{
    Project,
    StdModule,
    Class,
    Form,
    UserControl,
    UserDocument,
    PropertyPage,
    Designer,
    Sub,
    Function,
    Property,
    Declare,
    Variable,
    Constant,
    Event,
    Udt,
    UdtField,
    Enum,
    EnumMember,
    Control,
    Parameter,
    LocalVariable,
    LocalConstant,
    Label,

    /// <summary>A class of a referenced type library (e.g. <c>VB.CommandButton</c>), only known by name.</summary>
    ExternalClass,

    /// <summary>An event of an external class, named by an event handler (<c>Command1_Click</c>).</summary>
    ExternalEvent,
}

/// <summary>A VB6 entity: project, module, member, local, or a by-name external class.</summary>
internal sealed class Sym(string name, SymKind kind, string scip)
{
    public string Name { get; } = name;

    public SymKind Kind { get; } = kind;

    /// <summary>The SCIP symbol string (<c>local N</c> for locals).</summary>
    public string Scip { get; } = scip;

    public Sym? Parent { get; init; }

    /// <summary>Module whose scope resolves <see cref="DeclaredType"/>.</summary>
    public ModuleInfo? Module { get; init; }

    public bool IsPublic { get; set; } = true;

    public bool IsArray { get; set; }

    public bool IsWithEvents { get; set; }

    public TypeRefSyntax? DeclaredType { get; set; }

    /// <summary>For controls and external symbols: the external type, resolved up front.</summary>
    public Sym? FixedType { get; set; }

    public string Signature { get; set; } = "";

    public List<string> Documentation { get; } = [];

    public List<Relationship> Relationships { get; } = [];

    /// <summary>Case-insensitive member table of modules, UDTs, enums and projects (first declaration wins).</summary>
    public Dictionary<string, Sym> Members { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The document that defines this symbol (null for external symbols or files outside the project root).</summary>
    public string? DocumentPath { get; set; }

    public bool IsLocal => Scip.StartsWith("local ", StringComparison.Ordinal);

    public bool IsModuleLike => Kind is SymKind.StdModule or SymKind.Class or SymKind.Form or SymKind.UserControl
        or SymKind.UserDocument or SymKind.PropertyPage or SymKind.Designer;

    public bool IsType => IsModuleLike || Kind is SymKind.Udt or SymKind.Enum or SymKind.ExternalClass;

    internal bool TypeResolved { get; set; }

    internal Sym? ResolvedType { get; set; }

    public void AddMember(Sym member) => Members.TryAdd(member.Name, member);

    public override string ToString() => Scip;
}
