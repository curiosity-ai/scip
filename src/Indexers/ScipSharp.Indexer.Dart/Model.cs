namespace ScipSharp.Indexer.Dart;

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
    LocalVariable,
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

    /// <summary>Set when the declaration belongs to a dependency or the SDK rather than the indexed package.</summary>
    public ExternalPackage? External { get; init; }

    /// <summary>For top-level declarations of an external library: the library that declares it.</summary>
    public ExtLibrary? Lib { get; set; }

    /// <summary>Descriptors below the file namespace, outermost first.</summary>
    public List<(string Name, SymbolDescriptor.Types.Suffix Suffix)> Path { get; init; } = [];

    public int EnclosingStart { get; set; }

    /// <summary>Start of the node including its doc comment (upstream enclosing ranges include documentation).</summary>
    public int RangeStart
    {
        get => _rangeStart >= 0 ? _rangeStart : EnclosingStart;
        set => _rangeStart = value;
    }

    private int _rangeStart = -1;

    public int EnclosingEnd { get; set; }

    public string? Doc { get; set; }

    public string Signature { get; set; } = "";

    /// <summary>When true, the symbol is a <c>local N</c> symbol (private names, positional parameters).</summary>
    public bool IsLocal { get; set; }

    public bool IsStatic { get; set; }

    /// <summary>A declaration that exists only so references can resolve to it; it is never defined in the document.</summary>
    public bool IsSynthetic { get; set; }

    /// <summary>Best-effort simple name of the declared (or obviously inferred) type; null when unknown.</summary>
    public string? TypeName { get; set; }

    /// <summary>Token index of the initializer of an untyped local (<c>var x = expr</c>), or -1.</summary>
    public int InitIndex { get; set; } = -1;

    public bool InitDone { get; set; }

    public Decl? InitType { get; set; }

    /// <summary>Names (best effort, without prefix/type arguments) of extends/with/implements/on types.</summary>
    public List<string> Supers { get; } = [];

    /// <summary>Members, for type-like declarations.</summary>
    public List<Decl> Members { get; } = [];

    private Dictionary<string, List<Decl>>? _membersByName;
    private int _membersIndexed;

    /// <summary>
    /// The members named <paramref name="name"/>, in declaration order. Member lookups run once per identifier, so a
    /// linear scan of a class with thousands of members (generated bindings) made indexing quadratic.
    /// </summary>
    public IReadOnlyList<Decl> MembersNamed(string name)
    {
        if (_membersByName is null || _membersIndexed != Members.Count)
        {
            _membersByName = new Dictionary<string, List<Decl>>(StringComparer.Ordinal);
            foreach (var m in Members)
            {
                if (!_membersByName.TryGetValue(m.Name, out var list))
                {
                    _membersByName[m.Name] = list = [];
                }

                list.Add(m);
            }

            _membersIndexed = Members.Count;
        }

        return _membersByName.TryGetValue(name, out var found) ? found : [];
    }

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
internal sealed record DirectiveRef(Token Uri, string Text)
{
    public string? Prefix { get; set; }

    public List<string>? Show { get; set; }

    public List<string>? Hide { get; set; }
}

/// <summary>A package other than the one being indexed (a dependency or an SDK library).</summary>
internal sealed record ExternalPackage(string Name, string Version);

/// <summary>A <c>this.x</c> field formal parameter: a reference to field <c>x</c> of the enclosing class.</summary>
internal sealed record ThisFieldRef(Token Name, Decl Class);

internal sealed class FileModel(string relativePath)
{
    public ExternalPackage? External { get; init; }

    public string RelativePath { get; } = relativePath;

    public List<Token> Tokens { get; set; } = [];

    /// <summary>Doc comment references: each entry is the dotted segments of one <c>[A.b]</c>.</summary>
    public List<List<Token>> DocRefs { get; set; } = [];

    public List<int> DocStarts { get; set; } = [];

    public List<Decl> TopLevel { get; } = [];

    /// <summary>Membership test for <see cref="TopLevel"/> (only valid once parsing is complete).</summary>
    public bool IsTopLevel(Decl d) => (_topLevelSet ??= [.. TopLevel]).Contains(d);

    private HashSet<Decl>? _topLevelSet;

    public List<Decl> AllDecls { get; } = [];

    public List<Decl> SyntheticParams { get; } = [];

    public List<DirectiveRef> Directives { get; } = [];

    public List<ThisFieldRef> ThisFieldRefs { get; } = [];

    public HashSet<string> ImportPrefixes { get; } = [];

    public Dictionary<string, Decl> PrefixDecls { get; } = [];

    /// <summary>Visible local declarations (variables, parameters) by name.</summary>
    public Dictionary<string, List<LocalScope>> Scopes { get; } = new(StringComparer.Ordinal);

    public void AddScope(Decl d, int start, int end)
    {
        if (!Scopes.TryGetValue(d.Name, out var list))
        {
            Scopes[d.Name] = list = [];
        }

        list.Add(new LocalScope(d, start, end));
        _sortedScopes.Remove(d.Name);
    }

    // Scopes per name sorted by start (stable), built on first lookup: a name such as `context` or `i` can have
    // thousands of scopes in one file, and scanning all of them for every reference was quadratic.
    private readonly Dictionary<string, (LocalScope[] Scopes, int[] MaxEnd)> _sortedScopes = new(StringComparer.Ordinal);

    /// <summary>Innermost local declaration named <paramref name="name"/> visible at <paramref name="offset"/>.</summary>
    public Decl? LookupLocal(string name, int offset)
    {
        if (!_sortedScopes.TryGetValue(name, out var index))
        {
            if (!Scopes.TryGetValue(name, out var list))
            {
                return null;
            }

            LocalScope[] ordered = [.. list.OrderBy(static s => s.Start)];
            var maxEnd = new int[ordered.Length];
            for (var i = 0; i < ordered.Length; i++)
            {
                maxEnd[i] = Math.Max(ordered[i].End, i > 0 ? maxEnd[i - 1] : int.MinValue);
            }

            index = (ordered, maxEnd);
            _sortedScopes[name] = index;
        }

        var (sorted, reach) = index;

        // The innermost scope is the containing one with the greatest start (the last in list order on ties): walk
        // back from the last scope starting at or before the offset.
        int lo = 0, hi = sorted.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (sorted[mid].Start <= offset)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        for (var i = lo - 1; i >= 0 && offset < reach[i]; i--)
        {
            if (offset < sorted[i].End)
            {
                return sorted[i].Decl;
            }
        }

        return null;
    }

    /// <summary>Offsets of identifier tokens that are declarations (never treated as references).</summary>
    public HashSet<int> DeclTokenOffsets { get; } = [];
}

internal readonly record struct LocalScope(Decl Decl, int Start, int End);
