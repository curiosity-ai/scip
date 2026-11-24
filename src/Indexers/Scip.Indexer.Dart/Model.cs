namespace Scip.Indexer.Dart;

internal enum DeclKind
{
    Class,
    Mixin,
    Enum,
    TypeAlias,
    Extension,
    Constructor,
    Method,
    Function,
    Variable,
    Field,
    Property,
    Parameter,
    TypeParameter,
    ImportPrefix,
}

/// <summary>One declaration found in a Dart source file.</summary>
internal sealed class Decl(DeclKind kind, string name, Decl? parent, Token nameToken, string file)
{
    public DeclKind Kind { get; } = kind;

    /// <summary>Declared name (for unnamed constructors the empty string).</summary>
    public string Name { get; } = name;

    public Decl? Parent { get; } = parent;

    public Token NameToken { get; } = nameToken;

    public string File { get; } = file;

    /// <summary>Descriptors below the file namespace, outermost first.</summary>
    public List<(string Name, SymbolDescriptor.Types.Suffix Suffix)> Path { get; init; } = [];

    public int EnclosingStart { get; set; }

    public int EnclosingEnd { get; set; }

    public string? Doc { get; set; }

    public string Signature { get; set; } = "";

    /// <summary>When true, the symbol is a <c>local N</c> symbol (private names, positional parameters).</summary>
    public bool IsLocal { get; init; }

    public bool IsStatic { get; set; }

    /// <summary>Names (best effort, without prefix/type arguments) of extends/with/implements/on types.</summary>
    public List<string> Supers { get; } = [];

    /// <summary>Members, for type-like declarations.</summary>
    public List<Decl> Members { get; } = [];

    /// <summary>Parameters (named and positional), for functions/methods/constructors.</summary>
    public List<Decl> Parameters { get; } = [];

    /// <summary>Type parameters declared by this declaration.</summary>
    public List<Decl> TypeParameters { get; } = [];

    /// <summary>True for getters (<see cref="DeclKind.Property"/>).</summary>
    public bool IsGetter { get; set; }

    /// <summary>Range of the body of a type-like declaration, used to find the enclosing class of a token.</summary>
    public int BodyStart { get; set; } = -1;

    public int BodyEnd { get; set; } = -1;

    public bool IsTypeLike => Kind is DeclKind.Class or DeclKind.Mixin or DeclKind.Enum or DeclKind.TypeAlias or DeclKind.Extension;
}

/// <summary>An import/export/part directive with a literal URI.</summary>
internal sealed record DirectiveRef(Token Uri, string Text);

/// <summary>A <c>this.x</c> field formal parameter: a reference to field <c>x</c> of the enclosing class.</summary>
internal sealed record ThisFieldRef(Token Name, Decl Class);

internal sealed class FileModel(string relativePath)
{
    public string RelativePath { get; } = relativePath;

    public List<Token> Tokens { get; set; } = [];

    public List<Decl> TopLevel { get; } = [];

    public List<Decl> AllDecls { get; } = [];

    public List<DirectiveRef> Directives { get; } = [];

    public List<ThisFieldRef> ThisFieldRefs { get; } = [];

    public HashSet<string> ImportPrefixes { get; } = [];

    public Dictionary<string, Decl> PrefixDecls { get; } = [];

    /// <summary>Offsets of identifier tokens that are declarations (never treated as references).</summary>
    public HashSet<int> DeclTokenOffsets { get; } = [];
}
