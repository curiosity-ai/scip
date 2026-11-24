namespace Scip.Indexer.Ruby;

/// <summary>A constant reference as written: a lexical chain of enclosing namespaces plus <c>A::B::C</c> segments.</summary>
internal sealed record RefSpec(string[][] Cref, string[] Segments, bool Absolute);

internal readonly record struct ConstTarget(string[] Path, bool IsType);

/// <summary>A class or module known to the project (possibly reopened in several files).</summary>
internal sealed class TypeInfo(string[] path)
{
    public string[] Path { get; } = path;

    public bool Defined { get; set; }

    public bool IsModule { get; set; }

    public RefSpec? Super { get; set; }

    public List<RefSpec> Includes { get; } = [];

    public List<RefSpec> Prepends { get; } = [];

    public List<RefSpec> Extends { get; } = [];

    public HashSet<string> Methods { get; } = [];

    public HashSet<string> SingletonMethods { get; } = [];

    public HashSet<string> Constants { get; } = [];
}

/// <summary>Where a resolved method lives.</summary>
internal readonly record struct MethodTarget(string[] Owner, bool Singleton, string Name);

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
            _types[key] = t = new TypeInfo(path);
        }

        return t;
    }

    public TypeInfo? FindType(string[] path) => _types.GetValueOrDefault(Key(path));

    public void InvalidateAncestors() => _ancestors.Clear();

    private ConstTarget? Find(string[] ns, string seg)
    {
        string[] candidate = [.. ns, seg];
        if (FindType(candidate) is { Defined: true })
        {
            return new ConstTarget(candidate, true);
        }

        return FindType(ns) is { } owner && owner.Constants.Contains(seg) ? new ConstTarget(candidate, false) : null;
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

    public MethodTarget? LookupInstance(string[] owner, string name)
    {
        var t = FindType(owner);
        if (t is null)
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
            if (a.SingletonMethods.Contains(name))
            {
                return new MethodTarget(a.Path, true, name);
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

        // Modules used via `Module.function` style helpers still reach `Object`'s private top-level methods.
        return LookupInstance(["Object"], name);
    }

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

    public string TypeSymbol(string[] path) => Format(TypeDescriptors(path, false));

    public string ConstantSymbol(string[] path) =>
        Format(TypeDescriptors(path[..^1], false).Append(D(path[^1], SymbolDescriptor.Types.Suffix.Term)));

    public string MethodSymbol(string[] owner, bool singleton, string name) =>
        Format(TypeDescriptors(owner, singleton).Append(D(name, SymbolDescriptor.Types.Suffix.Method)));

    public string FieldSymbol(string[] owner, bool singleton, string name) =>
        Format(TypeDescriptors(owner, singleton).Append(D(name, SymbolDescriptor.Types.Suffix.Term)));

    public string GlobalSymbol(string name) =>
        Format([D("<Class:<root>>", SymbolDescriptor.Types.Suffix.Type), D(name, SymbolDescriptor.Types.Suffix.Term)], GemMetadata.GlobalPlaceholder);
}
