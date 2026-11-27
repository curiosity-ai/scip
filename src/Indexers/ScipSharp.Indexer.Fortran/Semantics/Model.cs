using ScipSharp.Indexer.Fortran.Syntax;

namespace ScipSharp.Indexer.Fortran.Semantics;

internal enum EntityKind
{
    Program,
    Module,
    Submodule,
    BlockData,
    Subroutine,
    Function,
    Generic,
    AbstractInterface,
    DerivedType,
    TypeParam,
    Component,
    Binding,
    GenericBinding,
    Variable,
    NamedConstant,
    Dummy,
    CommonBlock,
    CommonMember,
    Namelist,
    Label,
    ConstructName,
    StatementFunction,
    Enumerator,
    ProcPointer,

    /// <summary>A name from an intrinsic module or declared INTRINSIC: never emitted.</summary>
    Intrinsic,

    /// <summary>A module, procedure or module member that is not defined in the project.</summary>
    Unknown,
}

/// <summary>A named Fortran entity (program unit, procedure, type, variable, label, ...).</summary>
internal sealed class Entity(string name, string displayName, EntityKind kind, Scope scope)
{
    public string Name { get; } = name;

    public string DisplayName { get; } = displayName;

    public EntityKind Kind { get; set; } = kind;

    /// <summary>The scope that declares the entity.</summary>
    public Scope Scope { get; set; } = scope;

    /// <summary>First declaring token (null for entities without a source position).</summary>
    public Token? DefToken { get; set; }

    /// <summary>The scope this entity opens (program units, procedures, derived types).</summary>
    public Scope? Body { get; set; }

    // ---- typing ----

    /// <summary>Declared type text, e.g. <c>real(dp)</c> or <c>type(node)</c>.</summary>
    public string? TypeText { get; set; }

    /// <summary>Lower-cased derived type name of <c>TYPE(t)</c>/<c>CLASS(t)</c>.</summary>
    public string? TypeName { get; set; }

    /// <summary>Scope where <see cref="TypeName"/> is resolved.</summary>
    public Scope? TypeScope { get; set; }

    public List<string> Attrs { get; } = [];

    public string? Dims { get; set; }

    public string? Init { get; set; }

    /// <summary>True when declared with an array shape (so <c>a(i)</c> is an array element, not a call).</summary>
    public bool IsArray { get; set; }

    /// <summary>True for external procedures (symbol at the package root even when declared in a module).</summary>
    public bool IsExternalProc { get; set; }

    /// <summary>True for interface-body declarations (emitted as forward definitions).</summary>
    public bool IsForward { get; set; }

    /// <summary>Declared by EXTERNAL / the EXTERNAL attribute: a reference to a global procedure, not a definition.</summary>
    public bool IsExternalDecl { get; set; }

    public bool Implicit { get; set; }

    // ---- procedures ----

    public List<Entity> Args { get; } = [];

    public Entity? Result { get; set; }

    /// <summary>Header text of a procedure / program unit statement.</summary>
    public string? Header { get; set; }

    /// <summary>Separate module procedure interface (<c>module subroutine</c> in an interface block).</summary>
    public bool IsSeparateInterface { get; set; }

    // ---- derived types ----

    public string? ParentTypeName { get; set; }

    public Entity? ParentType { get; set; }

    public bool ParentResolved { get; set; }

    // ---- generics / bindings ----

    /// <summary>Specific procedure names of a generic interface / generic binding (lower case) with their lookup scope.</summary>
    public List<(string Name, Scope Scope)> Specifics { get; } = [];

    /// <summary>Implementation procedure name of a type-bound procedure binding.</summary>
    public string? ImplName { get; set; }

    /// <summary>The generic's descriptor needs a disambiguator because a procedure of the same name exists.</summary>
    public bool GenericCollision { get; set; }

    // ---- misc ----

    /// <summary>The COMMON block of a common member.</summary>
    public Entity? Common { get; set; }

    /// <summary>When set, the entity is emitted with the symbol of this entity (separate module procedures, aliases).</summary>
    public Entity? Canonical { get; set; }

    /// <summary>Unknown module name for members of a module that is not part of the project.</summary>
    public string? UnknownModule { get; set; }

    /// <summary>For SELECT TYPE guards: the type currently in effect.</summary>
    public Entity? GuardType { get; set; }

    /// <summary>For ASSOCIATE names: the entity whose type the associate name has.</summary>
    public Entity? TypeSource { get; set; }

    /// <summary>For an associate name aliasing a variable (SELECT TYPE without =>): the name of the outer variable.</summary>
    public string? AliasOf { get; set; }

    public List<string> Docs { get; } = [];

    /// <summary>Relationships to other entities (converted to SCIP relationships when the symbol is emitted).</summary>
    public List<(Entity Target, bool Implementation, bool Reference)> Rels { get; } = [];

    /// <summary>Separate module procedure implementation (<c>module procedure f</c> / <c>module subroutine f</c> in a submodule).</summary>
    public bool IsSeparateImpl { get; set; }

    public Entity? ResolvedType { get; set; }

    public bool TypeResolved { get; set; }

    /// <summary>Statement header text used as a signature for labels and statements that define the entity.</summary>
    public string? Signature { get; set; }

    public string? CachedSymbol { get; set; }

    public bool IsProcedureLike => Kind is EntityKind.Subroutine or EntityKind.Function or EntityKind.Generic or EntityKind.AbstractInterface
        or EntityKind.Binding or EntityKind.GenericBinding or EntityKind.StatementFunction;

    public override string ToString() => $"{Kind} {Name}";
}

internal enum ScopeKind
{
    File,
    Program,
    Module,
    Submodule,
    BlockData,
    Procedure,
    DerivedType,
    Interface,
    Block,
    Associate,
    SelectType,
}

/// <summary>A <c>USE</c> statement.</summary>
internal sealed class UseInfo(string module, Token token)
{
    public string Module { get; } = module;

    public Token Token { get; } = token;

    public bool Intrinsic { get; set; }

    /// <summary>Non-null when an ONLY list is present: local name → remote name.</summary>
    public Dictionary<string, string>? Only { get; set; }

    /// <summary>Renames outside an ONLY list: local name → remote name.</summary>
    public Dictionary<string, string> Renames { get; } = new(StringComparer.Ordinal);
}

/// <summary>A scoping unit (or a construct with its own names: BLOCK, ASSOCIATE, SELECT TYPE).</summary>
internal sealed class Scope(ScopeKind kind, Scope? parent, SourceDoc file)
{
    public ScopeKind Kind { get; } = kind;

    public Scope? Parent { get; set; } = parent;

    public SourceDoc File { get; } = file;

    public Entity? Owner { get; set; }

    public Dictionary<string, Entity> Entities { get; } = new(StringComparer.Ordinal);

    /// <summary>Generic interfaces (separate from <see cref="Entities"/>: a generic may share its name with a type or a specific).</summary>
    public Dictionary<string, Entity> Generics { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Entity> Labels { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Entity> Constructs { get; } = new(StringComparer.Ordinal);

    public List<UseInfo> Uses { get; } = [];

    public bool ImplicitNone { get; set; }

    /// <summary>IMPLICIT type for each letter (null: default I-N integer, otherwise real).</summary>
    public string?[]? ImplicitTypes { get; set; }

    public bool IsInterfaceBody { get; set; }

    public bool ImportAll { get; set; }

    public HashSet<string> Imports { get; } = new(StringComparer.Ordinal);

    public bool DefaultPrivate { get; set; }

    public Dictionary<string, bool> Access { get; } = new(StringComparer.Ordinal);

    /// <summary>Submodule: ancestor module and parent submodule names.</summary>
    public string? AncestorName { get; set; }

    public string? ParentSubmoduleName { get; set; }

    public bool HostResolved { get; set; }

    public Token? Start { get; set; }

    public Token? End { get; set; }

    public bool AfterContains { get; set; }

    public bool SeenExecutable { get; set; }

    /// <summary>For an interface block: its generic (null for a plain or abstract interface).</summary>
    public Entity? Generic { get; set; }

    public bool AbstractInterface { get; set; }

    /// <summary>The nearest enclosing scoping unit (labels, implicit variables, construct names live there).</summary>
    public Scope Unit
    {
        get
        {
            var s = this;
            while (s.Kind is ScopeKind.Block or ScopeKind.Associate or ScopeKind.SelectType or ScopeKind.Interface && s.Parent is not null)
            {
                s = s.Parent;
            }

            return s;
        }
    }

    public override string ToString() => $"{Kind} {Owner?.Name}";
}
