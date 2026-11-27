using ScipSharp.Indexer.Pli.Syntax;

namespace ScipSharp.Indexer.Pli.Semantics;

/// <summary>The files read while indexing (main sources and %INCLUDE members). Token <see cref="Token.File"/> ids index into it.</summary>
internal sealed class FileTable
{
    private readonly List<SourceFileEntry> _files = [];
    private readonly Dictionary<string, int> _byPath = new(StringComparer.Ordinal);

    public IReadOnlyList<SourceFileEntry> Files => _files;

    public SourceFileEntry this[int id] => _files[id];

    public bool TryGet(string relativePath, out SourceFileEntry entry)
    {
        if (_byPath.TryGetValue(relativePath, out var id))
        {
            entry = _files[id];
            return true;
        }

        entry = null!;
        return false;
    }

    public SourceFileEntry Add(string absolutePath, string relativePath, SourceText text, List<Token> tokens)
    {
        var entry = new SourceFileEntry(_files.Count, absolutePath, relativePath, text, tokens);
        _files.Add(entry);
        _byPath[relativePath] = entry.Id;
        return entry;
    }
}

/// <param name="Id">Index in the <see cref="FileTable"/>.</param>
/// <param name="AbsolutePath">Full path on disk.</param>
/// <param name="RelativePath">Path relative to the project root with forward slashes (the SCIP document path).</param>
/// <param name="Source">Decoded and margin-masked text.</param>
/// <param name="Tokens">Tokens of the whole file.</param>
internal sealed record SourceFileEntry(int Id, string AbsolutePath, string RelativePath, SourceText Source, List<Token> Tokens);

internal enum BlockKind
{
    /// <summary>The compilation unit itself (declarations outside any procedure, preprocessor scope).</summary>
    File,
    Package,
    Procedure,
    Begin,

    /// <summary>A preprocessor procedure (<c>%name: PROC</c> .. <c>%END</c>).</summary>
    PreprocessorProcedure,
}

/// <summary>A PL/I block (scope). Inner blocks see the declarations of enclosing blocks.</summary>
internal sealed class Block(BlockKind kind, Block? parent, Decl? decl)
{
    public BlockKind Kind { get; } = kind;

    public Block? Parent { get; } = parent;

    /// <summary>The declaration naming this block (procedure, labelled BEGIN, package), if any.</summary>
    public Decl? Decl { get; } = decl;

    /// <summary>Every declaration of the block (all structure levels), keyed by normalized name.</summary>
    public Dictionary<string, List<Decl>> ByName { get; } = new(StringComparer.Ordinal);

    public List<Block> Children { get; } = [];

    /// <summary>Descriptor name used for anonymous begin blocks.</summary>
    public string? AnonymousName { get; set; }

    /// <summary>True for a level-1 procedure that is not nested in another procedure (an external procedure).</summary>
    public bool IsExternalProcedure => Kind == BlockKind.Procedure && Parent is { Kind: BlockKind.File or BlockKind.Package };

    public void Add(Decl decl)
    {
        if (!ByName.TryGetValue(decl.Name, out var list))
        {
            ByName[decl.Name] = list = [];
        }

        list.Add(decl);
    }

    /// <summary>The outermost procedure (or package/file) containing this block: where implicit declarations go.</summary>
    public Block Outermost
    {
        get
        {
            var b = this;
            while (b.Parent is { Kind: not (BlockKind.File or BlockKind.Package) } p)
            {
                b = p;
            }

            return b;
        }
    }
}

internal enum DeclKind
{
    Variable,
    Member,
    Parameter,
    Procedure,

    /// <summary>A secondary entry point (ENTRY statement).</summary>
    Entry,

    /// <summary>An entry constant declared with <c>DCL x ENTRY</c> (external unless an internal procedure of that name exists).</summary>
    EntryDeclaration,
    Label,
    BeginBlock,
    Package,
    Builtin,
    Condition,
    File,

    /// <summary><c>DEFINE STRUCTURE</c>/<c>DEFINE ALIAS</c>/<c>DEFINE ORDINAL</c> types.</summary>
    Type,
    OrdinalValue,
    Generic,
    PreprocessorVariable,
    PreprocessorProcedure,

    /// <summary>An undeclared name that PL/I declares implicitly (emitted as a local symbol).</summary>
    Implicit,
}

/// <summary>A declared name.</summary>
internal sealed class Decl(string name, string displayName, DeclKind kind, Block block)
{
    /// <summary>Normalized name (upper case unless case-sensitive).</summary>
    public string Name { get; } = name;

    public string DisplayName { get; } = displayName;

    public DeclKind Kind { get; set; } = kind;

    /// <summary>The block whose scope contains the name.</summary>
    public Block Block { get; } = block;

    /// <summary>Structure level (1 for non-structure names).</summary>
    public int Level { get; set; } = 1;

    public Decl? Parent { get; set; }

    public List<Decl> Children { get; } = [];

    /// <summary>Tokens where the name is defined (usually one).</summary>
    public List<Token> Definitions { get; } = [];

    /// <summary>Tokens of declaration sites that are references (e.g. DCL of a parameter, BUILTIN declarations).</summary>
    public List<Token> DeclarationReferences { get; } = [];

    /// <summary>Attribute text shown as documentation.</summary>
    public string? Signature { get; set; }

    public bool IsExternal { get; set; }

    /// <summary>True once a DECLARE statement has supplied attributes (for parameters).</summary>
    public bool Declared { get; set; }

    /// <summary>Reference chain of a <c>LIKE</c> attribute (structure template), resolved lazily.</summary>
    public List<Token>? LikeTarget { get; set; }

    /// <summary>Reference of a <c>TYPE(t)</c>/<c>HANDLE(t)</c> attribute, resolved lazily.</summary>
    public List<Token>? TypeTarget { get; set; }

    public Decl? ResolvedTemplate { get; set; }

    public bool TemplateResolved { get; set; }

    /// <summary>The block a procedure/package/begin declaration names.</summary>
    public Block? OwnBlock { get; set; }

    /// <summary>Range enclosing the whole construct (procedure through END).</summary>
    public (Token Start, Token End)? Extent { get; set; }

    /// <summary>Global symbol (null for locals). Assigned by <see cref="SymbolNamer"/>.</summary>
    public string? Symbol { get; set; }

    public override string ToString() => $"{Kind} {Name}";
}

internal enum RefMode
{
    /// <summary>An expression: names are references, unresolved names are builtins, keywords or implicit declarations.</summary>
    Expression,

    /// <summary>A DECLARE attribute list: attribute keywords at depth 0 are skipped.</summary>
    Attributes,

    /// <summary>The target of CALL.</summary>
    Call,

    /// <summary>EXPORTS list of a package: only declared names.</summary>
    Exports,

    /// <summary>Host variables in EXEC SQL (names after a colon).</summary>
    Sql,

    /// <summary>EXEC CICS and similar: only names that resolve.</summary>
    DeclaredOnly,
}

/// <summary>A token range to resolve in the second pass.</summary>
/// <param name="Block">Scope of the range.</param>
/// <param name="Tokens">Statement tokens.</param>
/// <param name="Start">First token index (inclusive).</param>
/// <param name="End">Last token index (exclusive).</param>
/// <param name="Mode">How names are interpreted.</param>
/// <param name="Options">Statement option keywords recognised at depth 0.</param>
/// <param name="Preprocessor">True in preprocessor statements and procedures.</param>
/// <param name="Write">True when the first reference is assigned to.</param>
internal sealed record RefSite(
    Block Block,
    IReadOnlyList<Token> Tokens,
    int Start,
    int End,
    RefMode Mode,
    HashSet<string>? Options = null,
    bool Preprocessor = false,
    bool Write = false,
    bool FormatItems = false);

/// <summary>Result of parsing one compilation unit (a main file and its includes).</summary>
internal sealed class Unit(SourceFileEntry file, Block root)
{
    public SourceFileEntry File { get; } = file;

    public Block Root { get; } = root;

    /// <summary>Preprocessor variables and procedures (one scope per compilation).</summary>
    public Dictionary<string, Decl> Preprocessor { get; } = new(StringComparer.Ordinal);

    public List<RefSite> References { get; } = [];

    public List<Decl> Decls { get; } = [];

    /// <summary>Ids of every file read for this unit (main file first).</summary>
    public List<int> Files { get; } = [];

    public List<string> Warnings { get; } = [];
}
