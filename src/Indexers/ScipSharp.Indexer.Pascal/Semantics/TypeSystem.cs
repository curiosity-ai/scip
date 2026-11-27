namespace ScipSharp.Indexer.Pascal.Semantics;

/// <summary>Member lookup (ancestors, generics, helpers) and the type of index/dereference/member expressions.</summary>
internal sealed class TypeSystem
{
    private readonly Dictionary<PType, List<PType>> _helpers = new(ReferenceEqualityComparer.Instance);

    public PType CharType { get; set; } = new(TypeKind.Simple);

    public PType StringType { get; set; } = new(TypeKind.String);

    public PType BooleanType { get; set; } = new(TypeKind.Simple);

    public PType IntegerType { get; set; } = new(TypeKind.Simple);

    public PType RealType { get; set; } = new(TypeKind.Simple);

    public PType? ObjectType { get; set; }

    public void RegisterHelper(PType helper, PType target)
    {
        target = Structural(target);
        if (!_helpers.TryGetValue(target, out var list))
        {
            _helpers[target] = list = [];
        }

        list.Add(helper);
    }

    /// <summary>The generic type behind a specialization (repeatedly).</summary>
    public static PType Structural(PType t)
    {
        var guard = 0;
        while (t.Kind == TypeKind.Specialization && t.Generic is { } g && guard++ < 16)
        {
            t = g;
        }

        return t;
    }

    public List<Sym>? FindMember(PType type, string name)
    {
        var visited = new HashSet<PType>(ReferenceEqualityComparer.Instance);
        return Find(type, name, visited);
    }

    private List<Sym>? Find(PType type, string name, HashSet<PType> visited)
    {
        var t = Structural(type);
        if (t.Kind is TypeKind.Pointer or TypeKind.ClassRef && t.Elem is { } target)
        {
            t = Structural(target);
        }

        if (!visited.Add(t))
        {
            return null;
        }

        if (t.Members?.Find(name) is { } found)
        {
            return found;
        }

        foreach (var ancestor in t.Ancestors)
        {
            if (Find(ancestor, name, visited) is { } inherited)
            {
                return inherited;
            }
        }

        if (t.Kind == TypeKind.Helper && t.Elem is { } extended && Find(extended, name, visited) is { } fromTarget)
        {
            return fromTarget;
        }

        if (t.Kind is TypeKind.Class && t.Ancestors.Count == 0 && ObjectType is { } obj && !ReferenceEquals(obj, t))
        {
            if (Find(obj, name, visited) is { } fromObject)
            {
                return fromObject;
            }
        }

        if (_helpers.TryGetValue(t, out var helpers))
        {
            foreach (var helper in helpers)
            {
                if (Find(helper, name, visited) is { } fromHelper)
                {
                    return fromHelper;
                }
            }
        }

        return null;
    }

    /// <summary>Walks ancestors of <paramref name="type"/> (excluding itself) breadth-first.</summary>
    public IEnumerable<PType> AncestorsOf(PType type)
    {
        var visited = new HashSet<PType>(ReferenceEqualityComparer.Instance) { Structural(type) };
        var queue = new Queue<PType>(Structural(type).Ancestors);
        if (Structural(type).Kind == TypeKind.Class && Structural(type).Ancestors.Count == 0 && ObjectType is { } obj)
        {
            queue.Enqueue(obj);
        }

        while (queue.Count > 0)
        {
            var a = Structural(queue.Dequeue());
            if (!visited.Add(a))
            {
                continue;
            }

            yield return a;
            foreach (var next in a.Ancestors)
            {
                queue.Enqueue(next);
            }
        }
    }

    /// <summary>Element type produced by <c>x[i]</c>.</summary>
    public PType? IndexResult(PType? type)
    {
        if (type is null)
        {
            return null;
        }

        var t = Structural(type);
        switch (t.Kind)
        {
            case TypeKind.Array:
                return Substitute(t.Elem, type);
            case TypeKind.String:
                return CharType;
            case TypeKind.Pointer:
                return t.Elem;
        }

        if (t.IsStructured && FindDefaultProperty(t) is { } prop)
        {
            return Substitute(prop.Type, type);
        }

        return null;
    }

    private Sym? FindDefaultProperty(PType t)
    {
        if (t.DefaultProperty is { } p)
        {
            return p;
        }

        foreach (var a in AncestorsOf(t))
        {
            if (a.DefaultProperty is { } ap)
            {
                return ap;
            }
        }

        return null;
    }

    public static PType? Deref(PType? type) => type is null ? null
        : Structural(type) is { Kind: TypeKind.Pointer or TypeKind.File } t ? t.Elem
        : null;

    /// <summary>Replaces a generic type parameter by the matching argument of <paramref name="receiver"/>.</summary>
    public static PType? Substitute(PType? memberType, PType? receiver)
    {
        if (memberType is not { Kind: TypeKind.TypeParam, Decl: { } param } || receiver is null)
        {
            return memberType;
        }

        var r = receiver;
        var guard = 0;
        while (r is { Kind: TypeKind.Specialization } && guard++ < 16)
        {
            if (r.Generic?.Decl is { } g && ReferenceEquals(param.Owner, g) && r.Args is { } args && param.Index < args.Count)
            {
                return args[param.Index];
            }

            r = r.Generic;
        }

        return memberType;
    }

    /// <summary>The value type of <paramref name="sym"/> when accessed through <paramref name="receiver"/>.</summary>
    public static PType? ValueType(Sym sym, PType? receiver)
    {
        var t = sym.Kind == SymKind.Routine ? sym.Routine?.Result : sym.Type;
        return Substitute(t, receiver);
    }
}
