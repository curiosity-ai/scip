using ScipSharp.Indexer.Delphi.Syntax;

namespace ScipSharp.Indexer.Delphi.Semantics;

internal enum EntityKind
{
    Unit,
    Program,
    Library,
    Package,
    Class,
    Record,
    Interface,
    Object,
    ClassHelper,
    RecordHelper,
    Enum,
    EnumMember,
    Alias,
    ProcType,
    SetType,
    ArrayType,
    PointerType,
    ClassRef,
    Subrange,
    OtherType,
    TypeParam,
    Field,
    Property,
    Method,
    Routine,
    Const,
    Var,
    Param,
    Label,
}

/// <summary>The package a compilation unit's symbols belong to.</summary>
internal sealed record PackageInfo(string Manager, string Name, string Version);

/// <summary>A parsed source file (unit, program, package or stub) and the token list its syntax refers to.</summary>
internal sealed class SourceInfo(int fileId, string path, string relativePath, FileSyntax syntax)
{
    public int FileId { get; } = fileId;

    public string Path { get; } = path;

    public string RelativePath { get; } = relativePath;

    public FileSyntax Syntax { get; } = syntax;

    public IReadOnlyList<Token> Tokens => Syntax.Tokens;

    public Entity? Unit { get; set; }

    public bool IsStub { get; init; }
}

/// <summary>Case-insensitive, overload-aware member table that keeps declaration order.</summary>
internal sealed class MemberTable
{
    private readonly Dictionary<string, List<Entity>> _byName = new(StringComparer.OrdinalIgnoreCase);

    public List<Entity> InOrder { get; } = [];

    public void Add(Entity e)
    {
        if (e.Name.Length == 0)
        {
            return;
        }

        if (!_byName.TryGetValue(e.Name, out var list))
        {
            _byName[e.Name] = list = [];
        }

        list.Add(e);
        InOrder.Add(e);
    }

    public IReadOnlyList<Entity> Get(string name) => _byName.TryGetValue(name, out var l) ? l : [];

    public bool Contains(string name) => _byName.ContainsKey(name);

    public IEnumerable<string> Names => _byName.Keys;
}

/// <summary>A declared Delphi entity: unit, type, member, routine, variable, parameter, …</summary>
internal sealed class Entity(EntityKind kind, string name, Entity? parent)
{
    public EntityKind Kind { get; set; } = kind;

    public string Name { get; set; } = name;

    public Entity? Parent { get; set; } = parent;

    /// <summary>The compilation unit (unit/program/package entity) that declares this entity.</summary>
    public Entity? Unit { get; set; }

    public SourceInfo? Source { get; set; }

    public PackageInfo? Package { get; set; }

    public Decl? Decl { get; set; }

    /// <summary>Declared type (variables, fields, properties, constants, parameters) or definition (type declarations).</summary>
    public TypeNode? TypeSyntax { get; set; }

    public TypeNode? ReturnSyntax { get; set; }

    /// <summary>The scope in which <see cref="TypeSyntax"/>, <see cref="ReturnSyntax"/> and ancestors are resolved.</summary>
    public Scope? Scope { get; set; }

    public MemberTable Members { get; } = new();

    /// <summary>For units: declarations of the implementation section (interface ones live in <see cref="Members"/>).</summary>
    public MemberTable? ImplementationMembers { get; set; }

    public List<Entity> TypeParams { get; } = [];

    public List<Entity> Params { get; } = [];

    public List<NamedTypeNode> AncestorSyntax { get; } = [];

    public NamedTypeNode? HelperForSyntax { get; set; }

    public RoutineKind RoutineKind { get; set; }

    public bool IsClassMember { get; set; }

    public bool IsLocal { get; set; }

    public bool IsExternal { get; set; }

    /// <summary>Created on demand for a member/identifier of an external (unknown) declaration.</summary>
    public bool IsSynthetic { get; set; }

    /// <summary>For units: the unit could not be found (neither in the project nor in the built-in stubs).</summary>
    public bool IsUnknownUnit { get; set; }

    public bool IsDefaultProperty { get; set; }

    public bool IsScopedEnum { get; set; }

    public bool IsStrongAlias { get; set; }

    public HashSet<string> Directives { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Disambiguator { get; set; } = "";

    /// <summary>Added to the descriptor name of generic types that share their name with another type in the same scope.</summary>
    public string NameSuffix { get; set; } = "";

    /// <summary>Anonymous types (e.g. <c>var X: record … end</c>) take the descriptor of this entity.</summary>
    public Entity? AnonymousOwner { get; set; }

    /// <summary>The token of the defining name, if declared in source.</summary>
    public Token? DefToken { get; set; }

    /// <summary>Tokens of earlier forward declarations of the same entity.</summary>
    public List<Token> ForwardTokens { get; } = [];

    public string? Doc { get; set; }

    public string? Signature { get; set; }

    /// <summary>For units: the units listed in the interface / implementation <c>uses</c> clauses (resolved).</summary>
    public List<Entity> InterfaceUses { get; } = [];

    public List<Entity> ImplementationUses { get; } = [];

    /// <summary>For units: the dotted name segments.</summary>
    public IReadOnlyList<string> UnitSegments { get; set; } = [];

    // Resolution caches -----------------------------------------------------------------------------------------

    internal Ty? TypeCache { get; set; }

    internal bool TypeResolved { get; set; }

    internal List<Ty>? AncestorCache { get; set; }

    internal bool HelperTargetResolved { get; set; }

    internal Entity? HelperTarget { get; set; }

    internal string? SymbolCache { get; set; }

    public bool IsType => Kind is EntityKind.Class or EntityKind.Record or EntityKind.Interface or EntityKind.Object or
        EntityKind.ClassHelper or EntityKind.RecordHelper or EntityKind.Enum or EntityKind.Alias or EntityKind.ProcType or
        EntityKind.SetType or EntityKind.ArrayType or EntityKind.PointerType or EntityKind.ClassRef or EntityKind.Subrange or
        EntityKind.OtherType or EntityKind.TypeParam;

    public bool IsStructured => Kind is EntityKind.Class or EntityKind.Record or EntityKind.Interface or EntityKind.Object or
        EntityKind.ClassHelper or EntityKind.RecordHelper;

    public bool IsRoutine => Kind is EntityKind.Method or EntityKind.Routine;

    public bool IsUnitLike => Kind is EntityKind.Unit or EntityKind.Program or EntityKind.Library or EntityKind.Package;

    public bool IsCompilationUnitMember => Parent is { IsUnitLike: true };

    public int RequiredParamCount => Params.Count(static p => p.DefaultValue is null);

    public TokRange? DefaultValue { get; set; }

    public string? ParamModifier { get; set; }

    public override string ToString() => $"{Kind} {Name}";
}

/// <summary>A (possibly generic) type as seen by the resolver.</summary>
internal abstract record Ty;

/// <summary>A named type, with generic arguments when instantiated (<c>TList&lt;TFoo&gt;</c>).</summary>
internal sealed record NamedTy(Entity Decl, IReadOnlyList<Ty> Args) : Ty
{
    public static NamedTy Of(Entity e) => new(e, []);

    public bool Equals(NamedTy? other) => other is not null && ReferenceEquals(Decl, other.Decl) && Args.SequenceEqual(other.Args);

    public override int GetHashCode() => Decl.GetHashCode();
}

internal sealed record ArrayTy(Ty? Element) : Ty;

internal sealed record PointerTy(Ty? Target) : Ty;

internal sealed record ClassOfTy(Ty Instance) : Ty;

internal sealed record ProcTy(Ty? Return, bool IsFunction) : Ty;
