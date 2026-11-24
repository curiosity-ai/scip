using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Semantics;

/// <summary>Semantic types. Class names are JVM internal names (<c>java/util/Map$Entry</c>).</summary>
public abstract record JType
{
    public static readonly JType Unknown = new UnknownT();
    public static readonly JType Void = new PrimT("void");
    public static readonly JType Null = new NullT();
    public static ClassT Object { get; } = new("java/lang/Object", []);
    public static ClassT String { get; } = new("java/lang/String", []);

    public virtual bool IsKnown => true;
}

public sealed record UnknownT : JType { public override bool IsKnown => false; }

public sealed record NullT : JType;

public sealed record PrimT(string Name) : JType;

public sealed record VarT(string Name) : JType;

public sealed record ArrayT(JType Elem) : JType;

public sealed record WildT(WildKind Kind, JType? Bound) : JType;

public sealed record ClassT(string Name, IReadOnlyList<JType> Args) : JType
{
    public bool Equals(ClassT? other) => other is not null && Name == other.Name && Args.SequenceEqual(other.Args);

    public override int GetHashCode() => Name.GetHashCode();
}

public sealed class FieldInfo
{
    public required string Name { get; init; }
    public JType Type { get; set; } = JType.Unknown;
    public Mod Flags { get; set; }
    public TypeInfo Owner { get; set; } = null!;
    public Node? Decl { get; set; }
    public bool IsEnumConstant { get; set; }
    public bool IsStatic => (Flags & Mod.Static) != 0;
}

public sealed class MethodInfo
{
    public required string Name { get; init; }
    public List<JType> Params { get; set; } = [];
    public List<string> ParamNames { get; set; } = [];
    public JType Return { get; set; } = JType.Void;
    public Mod Flags { get; set; }
    public List<string> TypeParams { get; set; } = [];

    /// <summary>First (erased) bound of each entry of <see cref="TypeParams"/>; empty when unknown (treated as Object).</summary>
    public List<JType> TypeParamBounds { get; set; } = [];
    public List<JType> Throws { get; set; } = [];
    public bool Varargs { get; set; }
    public TypeInfo Owner { get; set; } = null!;
    public MethodDecl? Decl { get; set; }
    public bool IsCtor => Name == "<init>";
    public bool IsStatic => (Flags & Mod.Static) != 0;
    public bool Synthetic { get; set; }
}

/// <summary>A class, interface, enum, record or annotation type, from source or from a class file.</summary>
public sealed class TypeInfo
{
    private Action<TypeInfo>? _completer;

    public required string Name { get; init; }
    public required string Simple { get; set; }
    public TypeInfo? Outer { get; set; }
    public string? OuterName { get; set; }
    public string Package => Name.Contains('/') ? Name[..Name.LastIndexOf('/')] : "";
    public TypeKind Kind { get; set; }
    public Mod Flags { get; set; }
    public List<string> TypeParams { get; set; } = [];
    public List<JType> TypeParamBounds { get; set; } = [];
    public JType? Super { get; set; }
    public List<JType> Interfaces { get; set; } = [];
    public List<FieldInfo> Fields { get; set; } = [];
    public List<MethodInfo> Methods { get; set; } = [];
    public Dictionary<string, string> MemberTypes { get; set; } = new(StringComparer.Ordinal);
    public TypeDecl? Decl { get; set; }
    public JavaPackage Origin { get; set; } = JavaPackage.Empty;
    public bool IsAnonymous { get; set; }
    public bool IsLocal { get; set; }

    /// <summary>The method a named local class is declared in; its symbol is then a global descriptor under that method (as semanticdb-javac does).</summary>
    public MethodInfo? LocalOwner { get; set; }
    public bool FromSource => Decl is not null;
    public bool IsInterface => Kind is TypeKind.Interface or TypeKind.Annotation;

    public void SetCompleter(Action<TypeInfo> completer) => _completer = completer;

    /// <summary>Resolves lazily computed parts (supertypes, member signatures) of source types.</summary>
    public void Complete()
    {
        var c = _completer;
        if (c is null) return;
        _completer = null;
        c(this);
    }

    public override string ToString() => Name;
}
