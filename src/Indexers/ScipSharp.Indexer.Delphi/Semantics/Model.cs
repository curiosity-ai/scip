using ScipSharp.Indexer.Delphi.Syntax;

namespace ScipSharp.Indexer.Delphi.Semantics;

/// <summary>Result of a member lookup: the overload set found and the generic substitution of the type it was found in.</summary>
internal readonly record struct MemberResult(IReadOnlyList<Entity> Found, IReadOnlyDictionary<Entity, Ty> Env)
{
    public static MemberResult None { get; } = new([], Model.EmptyEnv);
}

/// <summary>
/// The program-wide semantic model: units, the built-in <c>System</c> unit, type resolution, generic substitution and
/// member lookup through ancestors and class/record helpers.
/// </summary>
internal sealed class Model
{
    public static readonly IReadOnlyDictionary<Entity, Ty> EmptyEnv = new Dictionary<Entity, Ty>();

    private static readonly HashSet<string> StringTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "AnsiString", "WideString", "UnicodeString", "ShortString", "RawByteString", "UTF8String",
    };

    private Dictionary<Entity, List<Entity>>? _helpersByTarget;

    /// <summary>The <c>System</c> unit (implicitly used by every unit). Replaced once the System stub is declared.</summary>
    public Entity SystemUnit { get; set; } = new(EntityKind.Unit, "System", null) { UnitSegments = ["System"], IsExternal = true };

    /// <summary>All units known by full name (project files and stubs; several files may declare the same unit).</summary>
    public Dictionary<string, List<Entity>> UnitsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<Entity> Helpers { get; } = [];

    /// <summary>Entities for anonymous structured/enum types declared inline (<c>var X: record … end</c>).</summary>
    public Dictionary<TypeNode, Entity> AnonymousTypes { get; } = new(ReferenceEqualityComparer.Instance);

    public Entity? SystemType(string name) => SystemUnit.Members.Get(name).FirstOrDefault(e => e.IsType);

    // ---------------------------------------------------------------------------------------------------------
    // Type resolution

    /// <summary>Resolves the entity named by a dotted, possibly unit-qualified path. Returns one entity per segment group.</summary>
    public List<(int FirstPart, int LastPart, Entity Entity)> ResolvePath(
        IReadOnlyList<Token> tokens, IReadOnlyList<NamePart> parts, Scope scope, bool typeContext)
    {
        var result = new List<(int, int, Entity)>();
        if (parts.Count == 0)
        {
            return result;
        }

        var index = 0;
        Entity? current = null;
        var unit = MatchUnitPrefix(tokens, parts.Select(p => p.Tok).ToList(), scope, out var consumed);
        if (unit is not null)
        {
            result.Add((0, consumed - 1, unit));
            current = unit;
            index = consumed;
        }

        if (current is null)
        {
            var name = tokens[parts[0].Tok].Text;
            var arity = parts[0].Args?.Count ?? 0;
            var isLast = parts.Count == 1;
            var found = arity > 0 ? scope.Lookup(name, e => e.IsType && e.TypeParams.Count == arity) : [];
            if (found.Count == 0)
            {
                found = typeContext && isLast
                    ? scope.Lookup(name, static e => e.IsType)
                    : scope.Lookup(name, e => !typeContext || e.IsType || e.IsUnitLike);
            }

            if (found.Count == 0 && typeContext)
            {
                found = scope.Lookup(name);
            }

            var e = PickByArity(found, arity);
            if (e is null)
            {
                e = MatchSingleUnit(name, scope);
            }

            if (e is null && SingleUnknownUnit(scope) is { } unknown)
            {
                // Declared in the only used unit we could not find: attribute it there.
                e = Synthesize(unknown, name, isCall: false, isType: typeContext || parts.Count > 1);
            }

            if (e is null)
            {
                return result;
            }

            result.Add((0, 0, e));
            current = e;
            index = 1;
        }

        for (; index < parts.Count; index++)
        {
            var name = tokens[parts[index].Tok].Text;
            var arity = parts[index].Args?.Count ?? 0;
            IReadOnlyList<Entity> found = current.IsUnitLike
                ? current.Members.Get(name)
                : FindMember(NamedTy.Of(current), name).Found;
            var e = PickByArity(found, arity);
            if (e is null && current.IsExternal && !current.IsUnitLike && !ReferenceEquals(current.Unit, SystemUnit))
            {
                e = Synthesize(current, name, isCall: false, isType: typeContext);
            }
            else if (e is null && current.IsUnknownUnit)
            {
                e = Synthesize(current, name, isCall: false, isType: typeContext || index < parts.Count - 1);
            }

            if (e is null)
            {
                return result;
            }

            result.Add((index, index, e));
            current = e;
        }

        return result;
    }

    private static Entity? PickByArity(IReadOnlyList<Entity> found, int arity)
    {
        if (found.Count == 0)
        {
            return null;
        }

        foreach (var e in found)
        {
            if (e.IsType && e.TypeParams.Count == arity)
            {
                return e;
            }
        }

        return found[0];
    }

    /// <summary>Longest dotted unit name (≥ 2 segments) visible from <paramref name="scope"/> that prefixes <paramref name="partToks"/>.</summary>
    public Entity? MatchUnitPrefix(IReadOnlyList<Token> tokens, IReadOnlyList<int> partToks, Scope scope, out int consumed)
    {
        consumed = 0;
        var unitScope = scope.UnitScope;
        if (unitScope is null || partToks.Count < 3)
        {
            return null;
        }

        var visible = VisibleUnits(unitScope);
        for (var k = partToks.Count - 1; k >= 2; k--)
        {
            var name = string.Join(".", partToks.Take(k).Select(t => tokens[t].Text));
            if (visible.TryGetValue(name, out var u))
            {
                consumed = k;
                return u;
            }
        }

        return null;
    }

    /// <summary>The used unit that was found neither in the project nor in the stubs, if there is exactly one.</summary>
    public static Entity? SingleUnknownUnit(Scope scope)
    {
        if (scope.UnitScope is not { } us)
        {
            return null;
        }

        Entity? only = null;
        foreach (var u in us.UnknownUnits)
        {
            if (only is not null && !ReferenceEquals(only, u))
            {
                return null;
            }

            only = u;
        }

        return only;
    }

    public Entity? MatchSingleUnit(string name, Scope scope)
    {
        var unitScope = scope.UnitScope;
        if (unitScope is null)
        {
            return null;
        }

        return VisibleUnits(unitScope).GetValueOrDefault(name);
    }

    private readonly Dictionary<UnitScope, Dictionary<string, Entity>> _visibleUnits = [];

    public Dictionary<string, Entity> VisibleUnits(UnitScope scope)
    {
        if (_visibleUnits.TryGetValue(scope, out var map))
        {
            return map;
        }

        map = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
        void Add(Entity u)
        {
            map.TryAdd(string.Join(".", u.UnitSegments), u);
            foreach (var alias in UnitAliases.GetValueOrDefault(u) ?? [])
            {
                map.TryAdd(alias, u);
            }
        }

        Add(scope.Unit);
        foreach (var u in scope.Unit.InterfaceUses)
        {
            Add(u);
        }

        if (scope.Implementation)
        {
            foreach (var u in scope.Unit.ImplementationUses)
            {
                Add(u);
            }
        }

        Add(SystemUnit);
        _visibleUnits[scope] = map;
        return map;
    }

    /// <summary>Names a unit was written as in <c>uses</c> clauses (e.g. <c>SysUtils</c> for <c>System.SysUtils</c>).</summary>
    public Dictionary<Entity, HashSet<string>> UnitAliases { get; } = new(ReferenceEqualityComparer.Instance);

    public Ty? ResolveTypeNode(TypeNode? node, Scope? scope, IReadOnlyList<Token>? tokens, int depth = 0)
    {
        if (node is null || scope is null || tokens is null || depth > 16)
        {
            return null;
        }

        switch (node)
        {
            case NamedTypeNode n:
            {
                var path = ResolvePath(tokens, n.Parts, scope, typeContext: true);
                if (path.Count == 0 || path[^1].LastPart != n.Parts.Count - 1)
                {
                    return null;
                }

                var e = path[^1].Entity;
                if (!e.IsType)
                {
                    return null;
                }

                var args = n.Parts[^1].Args;
                if (args is null || args.Count == 0)
                {
                    return NamedTy.Of(e);
                }

                return new NamedTy(e, args.Select(a => ResolveTypeNode(a, scope, tokens, depth + 1) ?? Unknown).ToList());
            }

            case ArrayTypeNode a:
                return new ArrayTy(ResolveTypeNode(a.Element, scope, tokens, depth + 1));
            case PointerTypeNode p:
                return new PointerTy(ResolveTypeNode(p.Target, scope, tokens, depth + 1));
            case ClassOfTypeNode c:
                return ResolveTypeNode(c.Target, scope, tokens, depth + 1) is { } inst ? new ClassOfTy(inst) : null;
            case ProcTypeNode pr:
                return new ProcTy(ResolveTypeNode(pr.Return, scope, tokens, depth + 1), pr.Kind == RoutineKind.Function);
            case StructTypeNode or EnumTypeNode:
                return AnonymousTypes.TryGetValue(node, out var anon) ? NamedTy.Of(anon) : null;
            default:
                return null;
        }
    }

    /// <summary>A placeholder for types that could not be resolved (keeps generic argument positions).</summary>
    public static readonly Ty Unknown = new ArrayTy(null);

    /// <summary>The instance type of a structured type inside its own declaration: <c>TFoo&lt;T&gt;</c> with its own parameters.</summary>
    public static NamedTy SelfType(Entity type) =>
        type.TypeParams.Count == 0 ? NamedTy.Of(type) : new NamedTy(type, type.TypeParams.Select(tp => (Ty)NamedTy.Of(tp)).ToList());

    /// <summary>The declared type of a variable, field, parameter, property or constant; the result type of a routine.</summary>
    public Ty? TypeOf(Entity e)
    {
        if (e.TypeResolved)
        {
            return e.TypeCache;
        }

        e.TypeResolved = true;
        Ty? t = null;
        switch (e.Kind)
        {
            case EntityKind.Field or EntityKind.Var or EntityKind.Const or EntityKind.Param:
                t = ResolveTypeNode(e.TypeSyntax, e.Scope, e.Source?.Tokens);
                break;
            case EntityKind.Property:
                t = ResolveTypeNode(e.TypeSyntax, e.Scope, e.Source?.Tokens);
                if (t is null && e.TypeSyntax is null && e.Parent is { } owner)
                {
                    // `property Foo;` re-publishes an inherited property.
                    foreach (var anc in Ancestors(owner))
                    {
                        if (anc is NamedTy an && FindMember(an, e.Name).Found.FirstOrDefault(x => x.Kind == EntityKind.Property && !ReferenceEquals(x, e)) is { } inherited)
                        {
                            t = TypeOf(inherited);
                            break;
                        }
                    }
                }

                break;
            case EntityKind.Method or EntityKind.Routine:
                t = e.RoutineKind == RoutineKind.Constructor && e.Parent is { IsType: true } cls
                    ? SelfType(cls)
                    : ResolveTypeNode(e.ReturnSyntax, e.Scope, e.Source?.Tokens);
                break;
            case EntityKind.EnumMember when e.Parent is not null:
                t = NamedTy.Of(e.Parent);
                break;
        }

        e.TypeCache = t;
        return t;
    }

    /// <summary>Resolves aliases (<c>TFoo = TBar</c>, <c>TArr = array of X</c>, <c>PFoo = ^TFoo</c>) to their structure.</summary>
    public Ty? Normalize(Ty? ty)
    {
        for (var i = 0; i < 16 && ty is NamedTy n; i++)
        {
            var d = n.Decl;
            if (d.Kind is not (EntityKind.Alias or EntityKind.ArrayType or EntityKind.PointerType or EntityKind.ClassRef or EntityKind.ProcType))
            {
                return ty;
            }

            if (d.TypeSyntax is null)
            {
                return ty;
            }

            var under = ResolveTypeNode(d.TypeSyntax, d.Scope, d.Source?.Tokens);
            if (under is null || under is NamedTy u && ReferenceEquals(u.Decl, d))
            {
                return ty;
            }

            ty = Subst(under, Bind(d, n.Args));
        }

        return ty;
    }

    public static IReadOnlyDictionary<Entity, Ty> Bind(Entity decl, IReadOnlyList<Ty> args)
    {
        if (decl.TypeParams.Count == 0 || args.Count == 0)
        {
            return EmptyEnv;
        }

        var env = new Dictionary<Entity, Ty>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < decl.TypeParams.Count && i < args.Count; i++)
        {
            if (!(args[i] is NamedTy a && ReferenceEquals(a.Decl, decl.TypeParams[i])))
            {
                env[decl.TypeParams[i]] = args[i];
            }
        }

        return env;
    }

    public static Ty? Subst(Ty? ty, IReadOnlyDictionary<Entity, Ty> env, int depth = 0)
    {
        if (ty is null || env.Count == 0 || depth > 16)
        {
            return ty;
        }

        return ty switch
        {
            NamedTy { Decl.Kind: EntityKind.TypeParam } n when env.TryGetValue(n.Decl, out var s) => s,
            NamedTy n when n.Args.Count > 0 => new NamedTy(n.Decl, n.Args.Select(a => Subst(a, env, depth + 1) ?? Unknown).ToList()),
            ArrayTy a => new ArrayTy(Subst(a.Element, env, depth + 1)),
            PointerTy p => new PointerTy(Subst(p.Target, env, depth + 1)),
            ClassOfTy c => new ClassOfTy(Subst(c.Instance, env, depth + 1) ?? c.Instance),
            ProcTy pr => new ProcTy(Subst(pr.Return, env, depth + 1), pr.IsFunction),
            _ => ty,
        };
    }

    public List<Ty> Ancestors(Entity decl)
    {
        if (decl.AncestorCache is not null)
        {
            return decl.AncestorCache;
        }

        decl.AncestorCache = [];
        var list = new List<Ty>();
        foreach (var a in decl.AncestorSyntax)
        {
            if (ResolveTypeNode(a, decl.Scope, decl.Source?.Tokens) is NamedTy t && !ReferenceEquals(t.Decl, decl))
            {
                list.Add(t);
            }
        }

        if (list.Count == 0 && decl.Name is not ("TObject" or "IInterface" or "IUnknown"))
        {
            var implicitBase = decl.Kind switch
            {
                EntityKind.Class => SystemType("TObject"),
                EntityKind.Interface => SystemType("IInterface"),
                _ => null,
            };
            if (implicitBase is not null && !ReferenceEquals(implicitBase, decl))
            {
                list.Add(NamedTy.Of(implicitBase));
            }
        }

        decl.AncestorCache = list;
        return list;
    }

    public Entity? HelperTarget(Entity helper)
    {
        if (helper.HelperTargetResolved)
        {
            return helper.HelperTarget;
        }

        helper.HelperTargetResolved = true;
        if (ResolveTypeNode(helper.HelperForSyntax, helper.Scope, helper.Source?.Tokens) is NamedTy t)
        {
            helper.HelperTarget = Normalize(t) is NamedTy n ? n.Decl : t.Decl;
        }

        return helper.HelperTarget;
    }

    private List<Entity> HelpersFor(Entity decl)
    {
        if (_helpersByTarget is null)
        {
            _helpersByTarget = new Dictionary<Entity, List<Entity>>(ReferenceEqualityComparer.Instance);
            foreach (var h in Helpers)
            {
                if (HelperTarget(h) is { } target)
                {
                    if (!_helpersByTarget.TryGetValue(target, out var l))
                    {
                        _helpersByTarget[target] = l = [];
                    }

                    l.Add(h);
                }
            }
        }

        return _helpersByTarget.TryGetValue(decl, out var list) ? list : [];
    }

    /// <summary>Strips class references and auto-dereferences pointers to reach the type whose members apply.</summary>
    public NamedTy? MemberContainer(Ty? receiver)
    {
        var t = Normalize(receiver);
        for (var i = 0; i < 4; i++)
        {
            switch (t)
            {
                case ClassOfTy c:
                    t = Normalize(c.Instance);
                    continue;
                case PointerTy p when p.Target is not null:
                    t = Normalize(p.Target);
                    continue;
                case NamedTy n:
                    return n;
            }

            break;
        }

        return null;
    }

    public MemberResult FindMember(Ty? receiver, string name)
    {
        var n = MemberContainer(receiver);
        if (n is null)
        {
            return MemberResult.None;
        }

        return SearchType(n.Decl, n.Args, name, new HashSet<Entity>(ReferenceEqualityComparer.Instance));
    }

    private MemberResult SearchType(Entity decl, IReadOnlyList<Ty> args, string name, HashSet<Entity> visited)
    {
        if (!visited.Add(decl) || visited.Count > 64)
        {
            return MemberResult.None;
        }

        var env = Bind(decl, args);
        var found = decl.Members.Get(name);
        if (found.Count > 0)
        {
            return new MemberResult(found, env);
        }

        foreach (var h in HelpersFor(decl))
        {
            var r = SearchType(h, [], name, visited);
            if (r.Found.Count > 0)
            {
                return r with { Env = env.Count > 0 ? env : r.Env };
            }
        }

        if (decl.Kind is EntityKind.ClassHelper or EntityKind.RecordHelper && HelperTarget(decl) is { } target)
        {
            var r = SearchType(target, [], name, visited);
            if (r.Found.Count > 0)
            {
                return r;
            }
        }

        foreach (var anc in Ancestors(decl))
        {
            if (Subst(anc, env) is NamedTy a)
            {
                var r = SearchType(a.Decl, a.Args, name, visited);
                if (r.Found.Count > 0)
                {
                    return r;
                }
            }
        }

        if (StringTypeNames.Contains(decl.Name) && ReferenceEquals(decl.Unit, SystemUnit) && SystemType("string") is { } str && !ReferenceEquals(str, decl))
        {
            return SearchType(str, [], name, visited);
        }

        return MemberResult.None;
    }

    /// <summary>The first external structured type (not from <c>System</c>) reached from <paramref name="receiver"/>.</summary>
    public Entity? ExternalBase(Ty? receiver)
    {
        var n = MemberContainer(receiver);
        var visited = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        var current = n?.Decl;
        while (current is not null && visited.Add(current))
        {
            if (current.IsExternal)
            {
                return ReferenceEquals(current.Unit, SystemUnit) || !current.IsStructured && current.Kind != EntityKind.OtherType ? null : current;
            }

            current = Ancestors(current).OfType<NamedTy>().Select(a => a.Decl).FirstOrDefault(a => a.Kind != EntityKind.Interface);
        }

        return null;
    }

    /// <summary>Creates (once) a member of an external declaration that the stubs do not describe.</summary>
    public static Entity Synthesize(Entity owner, string name, bool isCall, bool isType)
    {
        var existing = owner.Members.Get(name);
        if (existing.Count > 0)
        {
            return existing[0];
        }

        var kind = isType ? EntityKind.OtherType
            : owner.IsUnitLike ? (isCall ? EntityKind.Routine : EntityKind.Var)
            : isCall ? EntityKind.Method : EntityKind.Property;
        var e = new Entity(kind, name, owner)
        {
            Unit = owner.IsUnitLike ? owner : owner.Unit,
            Package = owner.Package,
            IsExternal = true,
            IsSynthetic = true,
        };
        owner.Members.Add(e);
        return e;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Expression typing helpers

    /// <summary>The element type obtained by indexing <paramref name="ty"/>: arrays, strings, pointers, default properties.</summary>
    public Ty? IndexType(Ty? ty, out Entity? defaultProperty)
    {
        defaultProperty = null;
        var t = Normalize(ty);
        switch (t)
        {
            case ArrayTy a:
                return a.Element;
            case PointerTy p:
                return p.Target;
            case NamedTy n when StringTypeNames.Contains(n.Decl.Name) && ReferenceEquals(n.Decl.Unit, SystemUnit):
                return SystemType("Char") is { } ch ? NamedTy.Of(ch) : null;
            case NamedTy n:
            {
                var r = FindDefaultProperty(n.Decl, n.Args, new HashSet<Entity>(ReferenceEqualityComparer.Instance));
                if (r is { } found)
                {
                    defaultProperty = found.Property;
                    return Subst(TypeOf(found.Property), found.Env);
                }

                return null;
            }

            default:
                return null;
        }
    }

    private (Entity Property, IReadOnlyDictionary<Entity, Ty> Env)? FindDefaultProperty(Entity decl, IReadOnlyList<Ty> args, HashSet<Entity> visited)
    {
        if (!visited.Add(decl))
        {
            return null;
        }

        var env = Bind(decl, args);
        foreach (var m in decl.Members.InOrder)
        {
            if (m.Kind == EntityKind.Property && m.IsDefaultProperty)
            {
                return (m, env);
            }
        }

        foreach (var h in HelpersFor(decl))
        {
            if (FindDefaultProperty(h, [], visited) is { } r)
            {
                return r;
            }
        }

        foreach (var anc in Ancestors(decl))
        {
            if (Subst(anc, env) is NamedTy a && FindDefaultProperty(a.Decl, a.Args, visited) is { } r)
            {
                return r;
            }
        }

        return null;
    }

    /// <summary>Element type of a <c>for … in</c> loop over <paramref name="collection"/>.</summary>
    public Ty? EnumeratedType(Ty? collection)
    {
        var t = Normalize(collection);
        if (t is ArrayTy a)
        {
            return a.Element;
        }

        if (MemberContainer(t) is { } n)
        {
            var ge = FindMember(n, "GetEnumerator");
            if (ge.Found.FirstOrDefault(e => e.IsRoutine) is { } getEnum &&
                Subst(TypeOf(getEnum), ge.Env) is { } enumTy &&
                FindMember(enumTy, "Current") is { Found.Count: > 0 } cur)
            {
                return Subst(Subst(TypeOf(cur.Found[0]), cur.Env), ge.Env);
            }

            if (n.Decl.Kind == EntityKind.SetType)
            {
                return null;
            }

            return IndexType(t, out _);
        }

        return null;
    }

    /// <summary>Ancestor method that <paramref name="method"/> overrides or <c>inherited</c> calls.</summary>
    public Entity? InheritedMember(Entity ownerType, string name, int? argCount)
    {
        foreach (var anc in Ancestors(ownerType))
        {
            var r = FindMember(anc, name);
            if (r.Found.Count > 0)
            {
                return ChooseOverload(r.Found, argCount);
            }
        }

        return null;
    }

    public static Entity ChooseOverload(IReadOnlyList<Entity> candidates, int? argCount)
    {
        if (candidates.Count == 1 || argCount is null)
        {
            return candidates[0];
        }

        foreach (var c in candidates)
        {
            if (c.IsRoutine && argCount >= c.RequiredParamCount && argCount <= c.Params.Count)
            {
                return c;
            }
        }

        return candidates[0];
    }
}
