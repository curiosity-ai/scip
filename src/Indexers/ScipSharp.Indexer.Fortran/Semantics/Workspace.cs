using ScipSharp.Indexer.Fortran.Syntax;

namespace ScipSharp.Indexer.Fortran.Semantics;

/// <summary>What kind of name a lookup is for.</summary>
internal enum LookupCtx
{
    /// <summary>A name in an expression: generics win over a specific or type of the same name.</summary>
    Expr,

    /// <summary>A derived type name (TYPE(t), CLASS(t), EXTENDS(t), TYPE IS (t)).</summary>
    Type,

    /// <summary>A CALL target / procedure name.</summary>
    Proc,

    /// <summary>A specific procedure of a generic: never the generic itself.</summary>
    Specific,
}

/// <summary>
/// Program-wide tables filled by the collector (pass 1) and the name lookup used by the linker and the resolver:
/// local names, use association (ONLY lists, renames, re-exports, PRIVATE), host association (internal and module
/// procedures, submodules, BLOCK), IMPORT in interface bodies and the global external-procedure namespace.
/// </summary>
internal sealed class Workspace
{
    public static readonly HashSet<string> IntrinsicModules = new(StringComparer.Ordinal)
    {
        "iso_fortran_env", "iso_c_binding", "ieee_arithmetic", "ieee_exceptions", "ieee_features",
        "omp_lib", "omp_lib_kinds", "openacc", "openacc_kinds",
    };

    public Dictionary<string, Entity> Modules { get; } = new(StringComparer.Ordinal);

    /// <summary>Submodules keyed by <c>ancestor:name</c>.</summary>
    public Dictionary<string, Entity> Submodules { get; } = new(StringComparer.Ordinal);

    /// <summary>External procedures (and ENTRY points outside modules).</summary>
    public Dictionary<string, Entity> Procs { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Entity> Commons { get; } = new(StringComparer.Ordinal);

    public List<Entity> AllEntities { get; } = [];

    public List<Scope> SubmoduleScopes { get; } = [];

    private readonly Dictionary<string, Entity> _unknownProcs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entity> _unknownModules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entity> _unknownMembers = new(StringComparer.Ordinal);
    private readonly Scope _nowhere;

    public Workspace()
    {
        _nowhere = new Scope(ScopeKind.File, null, new SourceDoc("", "", [], SourceForm.Free));
        IntrinsicEntity = new Entity("", "", EntityKind.Intrinsic, _nowhere);
    }

    /// <summary>Shared marker for names that must not be emitted (intrinsic module members, INTRINSIC procedures).</summary>
    public Entity IntrinsicEntity { get; }

    public Entity Track(Entity e)
    {
        AllEntities.Add(e);
        return e;
    }

    /// <summary>The external procedure <paramref name="name"/>: the project's definition, or a placeholder for an undefined one.</summary>
    public Entity ExternalProc(string name, string display)
    {
        if (Procs.TryGetValue(name, out var p))
        {
            return p;
        }

        if (!_unknownProcs.TryGetValue(name, out var u))
        {
            u = new Entity(name, display, EntityKind.Unknown, _nowhere) { IsExternalProc = true };
            _unknownProcs[name] = u;
        }

        return u;
    }

    public Entity UnknownModule(string name, string display)
    {
        if (!_unknownModules.TryGetValue(name, out var u))
        {
            u = new Entity(name, display, EntityKind.Unknown, _nowhere) { UnknownModule = "" };
            _unknownModules[name] = u;
        }

        return u;
    }

    public Entity UnknownMember(string module, string name)
    {
        var key = module + "/" + name;
        if (!_unknownMembers.TryGetValue(key, out var u))
        {
            u = new Entity(name, name, EntityKind.Unknown, _nowhere) { UnknownModule = module };
            _unknownMembers[key] = u;
        }

        return u;
    }

    // ---------------- lookup ----------------

    public Entity? Lookup(Scope? scope, string name, LookupCtx ctx)
    {
        var cur = scope;
        var guard = 0;
        Dictionary<(Scope, string), int>? seen = null;
        while (cur is not null && guard++ < 64)
        {
            var e = Local(cur, name, ctx) ?? FromUses(cur, name, ctx, 0, ref seen);
            if (e is not null)
            {
                return e;
            }

            if (cur.IsInterfaceBody)
            {
                // Interface bodies do not host-associate, except for IMPORTed names.
                var enclosing = cur.Parent?.Kind == ScopeKind.Interface ? cur.Parent.Parent : cur.Parent;
                cur = cur.ImportAll || cur.Imports.Contains(name) ? enclosing : FileOf(cur);
                continue;
            }

            if (cur.Kind == ScopeKind.File)
            {
                break;
            }

            cur = cur.Parent;
        }

        return ctx is LookupCtx.Proc or LookupCtx.Expr or LookupCtx.Specific && Procs.TryGetValue(name, out var proc) ? proc : null;
    }

    private static Scope? FileOf(Scope s)
    {
        var cur = s;
        while (cur.Parent is not null && cur.Kind != ScopeKind.File)
        {
            cur = cur.Parent;
        }

        return cur.Kind == ScopeKind.File ? cur : null;
    }

    public static Entity? Local(Scope s, string name, LookupCtx ctx)
    {
        switch (ctx)
        {
            case LookupCtx.Type:
                return s.Entities.TryGetValue(name, out var t) && t.Kind is EntityKind.DerivedType or EntityKind.Unknown ? t : null;
            case LookupCtx.Specific:
                return s.Entities.GetValueOrDefault(name);
            default:
                if (s.Generics.TryGetValue(name, out var g))
                {
                    return g;
                }

                return s.Entities.GetValueOrDefault(name);
        }
    }

    private Entity? FromUses(Scope s, string name, LookupCtx ctx, int depth, ref Dictionary<(Scope, string), int>? seen)
    {
        if (s.Uses.Count == 0 || depth > 24)
        {
            return null;
        }

        foreach (var u in s.Uses)
        {
            string remote;
            if (u.Only is not null)
            {
                if (!u.Only.TryGetValue(name, out var r))
                {
                    continue;
                }

                remote = r;
            }
            else if (u.Renames.TryGetValue(name, out var r2))
            {
                remote = r2;
            }
            else if (u.Renames.ContainsValue(name))
            {
                continue;
            }
            else
            {
                remote = name;
            }

            var e = Export(u, remote, ctx, depth, ref seen);
            if (e is not null)
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>The entity <paramref name="name"/> made accessible by a USE statement.</summary>
    public Entity? Export(UseInfo u, string name, LookupCtx ctx)
    {
        Dictionary<(Scope, string), int>? seen = null;
        return Export(u, name, ctx, 0, ref seen);
    }

    private Entity? Export(UseInfo u, string name, LookupCtx ctx, int depth, ref Dictionary<(Scope, string), int>? seen)
    {
        if (!Modules.TryGetValue(u.Module, out var module) || u.Intrinsic)
        {
            if (u.Only is null)
            {
                return null;
            }

            return u.Intrinsic || IntrinsicModules.Contains(u.Module) ? IntrinsicEntity : UnknownMember(u.Module, name);
        }

        return ModuleExport(module.Body!, name, ctx, depth + 1, ref seen);
    }

    private Entity? ModuleExport(Scope m, string name, LookupCtx ctx, int depth, ref Dictionary<(Scope, string), int>? seen)
    {
        if (depth > 24 || IsPrivate(m, name))
        {
            return null;
        }

        if (Local(m, name, ctx) is { } local)
        {
            return local;
        }

        // A diamond-shaped USE graph would otherwise be re-walked once per path (exponential in its depth). A miss
        // for (module, name) at some depth is a miss at any greater depth, so only shallower revisits are explored.
        seen ??= [];
        if (seen.TryGetValue((m, name), out var prev) && prev <= depth)
        {
            return null;
        }

        seen[(m, name)] = depth;
        return FromUses(m, name, ctx, depth, ref seen);
    }

    public static bool IsPrivate(Scope m, string name) => m.Access.TryGetValue(name, out var pub) ? !pub : m.DefaultPrivate;

    /// <summary>True when some USE in scope (or its hosts) imports an unknown or intrinsic module wholesale: undeclared names may come from it.</summary>
    public bool HasOpaqueUse(Scope scope)
    {
        Dictionary<Scope, int>? seen = null;
        for (var cur = scope; cur is not null; cur = cur.IsInterfaceBody ? null : cur.Parent)
        {
            foreach (var u in cur.Uses)
            {
                if (u.Only is null && (u.Intrinsic || !Modules.TryGetValue(u.Module, out var m) || ModuleHasOpaqueUse(m.Body!, 0, seen ??= [])))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ModuleHasOpaqueUse(Scope m, int depth, Dictionary<Scope, int> seen)
    {
        // Visited modules (at the same or a shallower depth) already answered false: keeps diamonds linear.
        if (depth > 16 || (seen.TryGetValue(m, out var prev) && prev <= depth))
        {
            return false;
        }

        seen[m] = depth;

        foreach (var u in m.Uses)
        {
            if (u.Only is null && (u.Intrinsic || !Modules.TryGetValue(u.Module, out var inner) || ModuleHasOpaqueUse(inner.Body!, depth + 1, seen)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when IMPLICIT NONE applies in <paramref name="scope"/> (inherited from hosts, not into interface bodies).</summary>
    public static bool ImplicitNone(Scope scope)
    {
        for (var cur = scope; cur is not null; cur = cur.IsInterfaceBody ? null : cur.Parent)
        {
            if (cur.ImplicitNone)
            {
                return true;
            }

            if (cur.ImplicitTypes is not null)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Type text implied for an undeclared name by the IMPLICIT rules in effect.</summary>
    public static string ImplicitType(Scope scope, string name)
    {
        var letter = name.Length > 0 ? name[0] : 'a';
        for (var cur = scope; cur is not null; cur = cur.IsInterfaceBody ? null : cur.Parent)
        {
            if (cur.ImplicitTypes is { } map && letter is >= 'a' and <= 'z' && map[letter - 'a'] is { } t)
            {
                return t;
            }
        }

        return letter is >= 'i' and <= 'n' ? "integer" : "real";
    }

    // ---------------- types ----------------

    /// <summary>The derived type of a variable, component, function result, binding or associate name (best effort).</summary>
    public Entity? TypeOf(Entity? e, int depth = 0)
    {
        if (e is null || depth > 16)
        {
            return null;
        }

        if (e.GuardType is not null)
        {
            return e.GuardType;
        }

        if (e.TypeSource is not null)
        {
            return TypeOf(e.TypeSource, depth + 1);
        }

        switch (e.Kind)
        {
            case EntityKind.DerivedType:
                return e;
            case EntityKind.Binding when e.ImplName is not null:
                var impl = Lookup(e.Scope.Parent, e.ImplName, LookupCtx.Specific);
                return impl == e ? null : TypeOf(impl, depth + 1);
            case EntityKind.Function when e.Result is not null:
                return TypeOf(e.Result, depth + 1);
        }

        if (e.Canonical is not null && e.Canonical != e && e.TypeName is null)
        {
            return TypeOf(e.Canonical, depth + 1);
        }

        if (!e.TypeResolved)
        {
            e.TypeResolved = true;
            e.ResolvedType = e.TypeName is null ? null : Lookup(e.TypeScope ?? e.Scope, e.TypeName, LookupCtx.Type);
        }

        return e.ResolvedType;
    }

    public Entity? ParentOf(Entity type)
    {
        if (!type.ParentResolved)
        {
            type.ParentResolved = true;
            if (type.ParentTypeName is not null)
            {
                var p = Lookup(type.Scope, type.ParentTypeName, LookupCtx.Type);
                type.ParentType = p == type ? null : p;
            }
        }

        return type.ParentType;
    }

    /// <summary>A component, binding or the parent component of <paramref name="type"/> (searching the EXTENDS chain).</summary>
    public Entity? FindMember(Entity type, string name)
    {
        var cur = type;
        for (var depth = 0; cur is not null && depth < 32; depth++)
        {
            if (cur.Body is { } body && (body.Entities.TryGetValue(name, out var m) || body.Generics.TryGetValue(name, out m)))
            {
                return m;
            }

            var parent = ParentOf(cur);
            if (parent is not null && parent.Name == name)
            {
                return parent;
            }

            cur = parent;
        }

        return null;
    }
}
