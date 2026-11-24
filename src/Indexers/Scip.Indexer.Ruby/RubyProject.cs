namespace Scip.Indexer.Ruby;

/// <summary>A constant reference as written: a lexical chain of enclosing namespaces plus <c>A::B::C</c> segments.</summary>
internal sealed record RefSpec(string[][] Cref, string[] Segments, bool Absolute);

internal readonly record struct ConstTarget(string[] Path, bool IsType);

/// <summary>A (very) simple static type: an instance of <paramref name="Path"/>, or that class object when <paramref name="Singleton"/>.</summary>
internal readonly record struct RType(string[] Path, bool Singleton);

internal delegate RType? TypeFn(RubyProject project);

/// <summary>A class or module known to the project (possibly reopened in several files).</summary>
internal sealed class TypeInfo(string[] path)
{
    public string[] Path { get; } = path;

    public bool Defined { get; set; }

    /// <summary>True once a <c>class</c>/<c>module</c> statement of the project defines this type.</summary>
    public bool ProjectDefined { get; set; }

    /// <summary>Set when the type exists in the embedded core/stdlib payload.</summary>
    public CoreType? Core { get; set; }

    public bool IsModule { get; set; }

    /// <summary>An RSpec/Minitest <c>describe</c> group synthesized by the DSL rewriter.</summary>
    public bool IsSpec { get; set; }

    public RefSpec? Super { get; set; }

    public List<RefSpec> Includes { get; } = [];

    public List<RefSpec> Prepends { get; } = [];

    public List<RefSpec> Extends { get; } = [];

    public HashSet<string> Methods { get; } = [];

    /// <summary><c>alias</c>/<c>alias_method</c>: new name to original name (instance, then singleton).</summary>
    public Dictionary<string, string> Aliases { get; } = [];

    public HashSet<string> SingletonMethods { get; } = [];

    public HashSet<string> Constants { get; } = [];
}

/// <summary>Where a resolved method lives.</summary>
internal readonly record struct MethodTarget(string[] Owner, bool Singleton, string Name, bool Core = false);

/// <summary>Project-wide tables built while walking all files, used to resolve names in a second pass.</summary>
internal sealed class RubyProject(GemMetadata gem)
{
    private readonly Dictionary<string, TypeInfo> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<TypeInfo, List<TypeInfo>> _ancestors = [];
    private readonly HashSet<TypeInfo> _computing = [];

    public GemMetadata Gem { get; } = gem;

    public static string Key(string[] path) => string.Join("::", path);

    public TypeInfo Type(string[] path)
    {
        var key = Key(path);
        if (!_types.TryGetValue(key, out var t))
        {
            t = FindType(path);
            if (t is null)
            {
                _types[key] = t = new TypeInfo(path);
            }
        }

        return t;
    }

    public TypeInfo? FindType(string[] path)
    {
        var key = Key(path);
        if (_types.TryGetValue(key, out var t))
        {
            return t;
        }

        if (CoreIndex.Find(key) is not { } core)
        {
            return null;
        }

        t = _types[key] = new TypeInfo(path) { Defined = true, Core = core, IsModule = core.IsModule };
        MaterializeCore(t, core);
        return t;
    }

    private static RefSpec CoreRef(string name) => new([], name.Split("::"), true);

    private static void MaterializeCore(TypeInfo t, CoreType core)
    {
        if (core.Super is { } sup)
        {
            t.Super = CoreRef(sup);
        }

        foreach (var i in core.Includes)
        {
            t.Includes.Add(CoreRef(i));
        }

        foreach (var e in core.Extends)
        {
            t.Extends.Add(CoreRef(e));
        }
    }

    public void InvalidateAncestors() => _ancestors.Clear();

    private ConstTarget? Find(string[] ns, string seg)
    {
        string[] candidate = [.. ns, seg];
        if (FindType(candidate) is { Defined: true })
        {
            return new ConstTarget(candidate, true);
        }

        return FindType(ns) is { } owner && (owner.Constants.Contains(seg) || owner.Core?.Constants.Contains(seg) == true)
            ? new ConstTarget(candidate, false)
            : null;
    }

    private ConstTarget? FindInAncestors(TypeInfo ns, string seg)
    {
        foreach (var a in Ancestors(ns))
        {
            if (Find(a.Path, seg) is { } t)
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>Resolves the first <paramref name="count"/> segments of a constant reference.</summary>
    public ConstTarget? Resolve(RefSpec spec, int count)
    {
        ConstTarget? cur = null;
        for (var k = 0; k < count; k++)
        {
            var seg = spec.Segments[k];
            if (k == 0)
            {
                if (!spec.Absolute)
                {
                    foreach (var cref in spec.Cref)
                    {
                        cur ??= Find(cref, seg);
                    }

                    if (cur is null && spec.Cref.Length > 0 && FindType(spec.Cref[0]) is { } inner)
                    {
                        cur = FindInAncestors(inner, seg);
                    }
                }

                cur ??= Find([], seg);
            }
            else
            {
                if (cur is not { IsType: true } c || FindType(c.Path) is not { } ns)
                {
                    return null;
                }

                cur = Find(ns.Path, seg) ?? FindInAncestors(ns, seg);
            }

            if (cur is null)
            {
                return null;
            }
        }

        return cur;
    }

    public TypeInfo? ResolveType(RefSpec spec) =>
        Resolve(spec, spec.Segments.Length) is { IsType: true } t ? FindType(t.Path) : null;

    /// <summary>Method resolution order: prepends, the type, includes, then the superclass chain, then <c>Object</c>.</summary>
    public List<TypeInfo> Ancestors(TypeInfo start)
    {
        if (_ancestors.TryGetValue(start, out var cached))
        {
            return cached;
        }

        if (!_computing.Add(start))
        {
            // Re-entrant lookup while resolving this type's own ancestry: be conservative.
            return [start];
        }

        var result = new List<TypeInfo>();
        var seen = new HashSet<TypeInfo>();
        void Add(TypeInfo t)
        {
            if (!seen.Add(t))
            {
                return;
            }

            foreach (var p in t.Prepends)
            {
                if (ResolveType(p) is { } pt) Add(pt);
            }

            result.Add(t);
            for (var i = t.Includes.Count - 1; i >= 0; i--)
            {
                if (ResolveType(t.Includes[i]) is { } it) Add(it);
            }

            if (t.Super is { } s && ResolveType(s) is { } st) Add(st);
        }

        Add(start);
        if (!start.IsModule && FindType(["Object"]) is { } obj)
        {
            Add(obj);
        }

        _computing.Remove(start);
        return _ancestors[start] = result;
    }

    private static bool HasInstance(TypeInfo a, string name, out bool core)
    {
        core = false;
        if (a.Methods.Contains(name))
        {
            return true;
        }

        return core = a.Core?.Instance.ContainsKey(name) == true;
    }

    private static bool HasSingleton(TypeInfo a, string name, out bool core)
    {
        core = false;
        if (a.SingletonMethods.Contains(name))
        {
            return true;
        }

        return core = a.Core?.Singleton.ContainsKey(name) == true;
    }

    public MethodTarget? LookupInstance(string[] owner, string name)
    {
        var t = FindType(owner);
        if (t is null)
        {
            return null;
        }

        foreach (var a in Ancestors(t))
        {
            if (a.Aliases.TryGetValue(name, out var original) && original != name)
            {
                return LookupInstance(a.Path, original);
            }

            if (HasInstance(a, name, out var core))
            {
                return new MethodTarget(a.Path, false, name, core);
            }
        }

        // Module bodies and methods still see Kernel/Object (they are mixed into some class eventually).
        return t.IsModule && owner is not ["Object"] ? LookupInstance(["Object"], name) : null;
    }

    /// <summary>Instance method defined by the project (used to resolve <c>Foo.new</c> to <c>initialize</c>).</summary>
    public MethodTarget? LookupProjectInstance(string[] owner, string name)
    {
        if (FindType(owner) is not { } t)
        {
            return null;
        }

        foreach (var a in Ancestors(t))
        {
            if (a.Methods.Contains(name))
            {
                return new MethodTarget(a.Path, false, name);
            }
        }

        return null;
    }

    /// <summary>Singleton method defined in the type's ancestry (no <c>extend</c> or <c>Object</c> fallbacks).</summary>
    public MethodTarget? LookupExplicitSingleton(string[] owner, string name)
    {
        if (FindType(owner) is not { } t)
        {
            return null;
        }

        foreach (var a in Ancestors(t))
        {
            if (a.SingletonMethods.Contains(name))
            {
                return new MethodTarget(a.Path, true, name);
            }
        }

        return null;
    }

    public MethodTarget? LookupSingleton(string[] owner, string name)
    {
        var t = FindType(owner);
        if (t is null)
        {
            return null;
        }

        var chain = Ancestors(t);
        foreach (var a in chain)
        {
            if (HasSingleton(a, name, out var core))
            {
                return new MethodTarget(a.Path, true, name, core);
            }
        }

        foreach (var a in chain)
        {
            foreach (var e in a.Extends)
            {
                if (ResolveType(e) is { } et && LookupInstance(et.Path, name) is { } m)
                {
                    return m;
                }
            }
        }

        // Class/Module methods (`new`, `attr_accessor`, `include`, ...), then Object's (Kernel's) private helpers.
        return LookupInstance([t.IsModule ? "Module" : "Class"], name) ?? LookupInstance(["Object"], name);
    }

    public MethodTarget? LookupMethod(RType r, string name)
    {
        if (!r.Singleton)
        {
            return LookupInstance(r.Path, name);
        }

        if (name == "new")
        {
            return LookupExplicitSingleton(r.Path, name) ?? LookupProjectInstance(r.Path, "initialize") ?? LookupSingleton(r.Path, name);
        }

        return LookupSingleton(r.Path, name);
    }

    /// <summary>The declared or payload return type of a resolved call.</summary>
    public RType? ReturnOf(MethodTarget? m, RType? recv, string name)
    {
        if (recv is { Singleton: true } r && name == "new")
        {
            return new RType(r.Path, false);
        }

        if (recv is { Singleton: false } inst && name == "class")
        {
            return new RType(inst.Path, true);
        }

        if (m is not { } t)
        {
            return null;
        }

        if (!t.Core)
        {
            return MethodRets.TryGetValue(RetKey(t.Owner, t.Singleton, t.Name), out var fn)
                ? (ReferenceEquals(fn, RubyWalker.AttachedClass) ? (recv is { } ar ? new RType(ar.Path, false) : null) : fn(this))
                : null;
        }

        var core = FindType(t.Owner)?.Core;
        var ret = core is null ? null : (t.Singleton ? core.Singleton : core.Instance).GetValueOrDefault(t.Name);
        return ret switch
        {
            null => null,
            "@" => recv,
            "^" => recv is { } rr ? new RType(rr.Path, false) : null,
            _ => FindType(ret.Split("::")) is not null ? new RType(ret.Split("::"), false) : null,
        };
    }

    /// <summary>True when <paramref name="path"/> looks like a spec group: <c>Minitest::Spec</c> or a class with a project-defined <c>it</c>.</summary>
    public bool IsSpecGroup(string[] path)
    {
        if (FindType(path) is not { } t)
        {
            return false;
        }

        foreach (var a in Ancestors(t))
        {
            if (a.IsSpec || a.SingletonMethods.Contains("it") || a.SingletonMethods.Contains("describe") || a.Super is { Segments: ["Minitest", "Spec"] })
            {
                return true;
            }
        }

        return false;
    }

    public Dictionary<string, TypeFn> MethodRets { get; } = [];

    public HashSet<string> KwParams { get; } = [];

    private int _typeDepth;

    /// <summary>Evaluates a lazily-resolved type, cutting self-referential field assignments (<c>@a = @a</c>).</summary>
    public RType? Guarded(Func<RType?> f)
    {
        if (_typeDepth > 12)
        {
            return null;
        }

        _typeDepth++;
        try
        {
            return f();
        }
        finally
        {
            _typeDepth--;
        }
    }

    public Dictionary<string, TypeFn> FieldTypes { get; } = [];

    public static string RetKey(string[] owner, bool singleton, string name) => Key(owner) + (singleton ? "." : "#") + name;

    // ---- symbol construction -------------------------------------------------------------------------------

    private static SymbolDescriptor D(string name, SymbolDescriptor.Types.Suffix suffix) => new() { Name = name, Suffix = suffix };

    public static IEnumerable<SymbolDescriptor> TypeDescriptors(string[] path, bool singleton)
    {
        // As in Sorbet, the singleton class <Class:Foo> is a sibling of Foo (owned by Foo's owner), not a child.
        for (var i = 0; i < path.Length; i++)
        {
            var last = i == path.Length - 1;
            yield return D(singleton && last ? $"<Class:{path[i]}>" : path[i], SymbolDescriptor.Types.Suffix.Type);
        }
    }

    public string Format(IEnumerable<SymbolDescriptor> descriptors, GemMetadata? gem = null)
    {
        gem ??= Gem;
        var symbol = new Symbol { Scheme = "scip-ruby", Package = new Package { Manager = "gem", Name = gem.Name, Version = gem.Version } };
        symbol.Descriptors.AddRange(descriptors);
        return SymbolFormatter.Verbose.Format(symbol);
    }

    private GemMetadata? CoreGem(string[] path) =>
        FindType(path) is { Core: not null, ProjectDefined: false } ? GemMetadata.Stdlib : null;

    public string TypeSymbol(string[] path) => Format(TypeDescriptors(path, false), CoreGem(path));

    public string ConstantSymbol(string[] path)
    {
        var owner = FindType(path[..^1]);
        var core = owner is { Core: not null } && !owner.Constants.Contains(path[^1]) && owner.Core.Constants.Contains(path[^1]);
        return Format(TypeDescriptors(path[..^1], false).Append(D(path[^1], SymbolDescriptor.Types.Suffix.Term)), core ? GemMetadata.Stdlib : null);
    }

    public string MethodSymbol(string[] owner, bool singleton, string name, bool core = false) =>
        Format(TypeDescriptors(owner, singleton).Append(D(name, SymbolDescriptor.Types.Suffix.Method)), core ? GemMetadata.Stdlib : null);

    public string ParamSymbol(string[] owner, bool singleton, string method, string param) =>
        Format(TypeDescriptors(owner, singleton)
            .Append(D(method, SymbolDescriptor.Types.Suffix.Method))
            .Append(D(param, SymbolDescriptor.Types.Suffix.Parameter)));

    public string FieldSymbol(string[] owner, bool singleton, string name) =>
        Format(TypeDescriptors(owner, singleton).Append(D(name, SymbolDescriptor.Types.Suffix.Term)));

    public string GlobalSymbol(string name) =>
        Format([D("<Class:<root>>", SymbolDescriptor.Types.Suffix.Type), D(name, SymbolDescriptor.Types.Suffix.Term)], GemMetadata.GlobalPlaceholder);
}
