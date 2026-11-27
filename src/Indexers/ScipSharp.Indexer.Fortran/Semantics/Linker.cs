namespace ScipSharp.Indexer.Fortran.Semantics;

/// <summary>
/// Runs between the passes, once every file has been collected: attaches submodules to their parents, connects
/// separate module procedures to their interfaces, and records relationships (EXTENDS, type-bound procedure
/// implementations and overrides, specific procedures of generic interfaces and generic bindings).
/// </summary>
internal static class Linker
{
    public static void Link(Workspace ws)
    {
        foreach (var s in ws.SubmoduleScopes)
        {
            Scope? host = null;
            if (s.ParentSubmoduleName is { } parent && ws.Submodules.TryGetValue(s.AncestorName + ":" + parent, out var p))
            {
                host = p.Body;
            }
            else if (s.AncestorName is { } anc && ws.Modules.TryGetValue(anc, out var m))
            {
                host = m.Body;
            }

            if (host is not null && host != s)
            {
                s.Parent = host;
            }

            s.HostResolved = true;
        }

        foreach (var e in ws.AllEntities.ToList())
        {
            switch (e.Kind)
            {
                case EntityKind.Subroutine or EntityKind.Function when e.IsSeparateImpl:
                    LinkSeparate(ws, e);
                    break;
                case EntityKind.DerivedType when e.ParentTypeName is not null:
                    if (ws.ParentOf(e) is { } parentType)
                    {
                        e.Rels.Add((parentType, true, false));
                    }

                    break;
                case EntityKind.Binding:
                    LinkBinding(ws, e);
                    break;
                case EntityKind.Generic:
                    if (e.Scope.Entities.TryGetValue(e.Name, out var same) && same.IsProcedureLike)
                    {
                        e.GenericCollision = true;
                    }

                    foreach (var (name, scope) in e.Specifics)
                    {
                        var sp = ws.Lookup(scope, name, LookupCtx.Specific);
                        if (sp is not null && sp != e && sp.IsProcedureLike)
                        {
                            sp.Rels.Add((e, true, false));
                        }
                    }

                    break;
                case EntityKind.GenericBinding when e.Scope.Owner is { } type:
                    foreach (var (name, _) in e.Specifics)
                    {
                        if (ws.FindMember(type, name) is { } m && m != e)
                        {
                            m.Rels.Add((e, true, false));
                        }
                    }

                    break;
            }
        }
    }

    private static void LinkSeparate(Workspace ws, Entity e)
    {
        var iface = e.Scope.Parent is { } host ? ws.Lookup(host, e.Name, LookupCtx.Specific) : null;
        if (iface is null || iface == e || !iface.IsSeparateInterface)
        {
            return;
        }

        e.Canonical = iface;
        e.Kind = iface.Kind;
        if (e.Body is null || e.Args.Count > 0)
        {
            return;
        }

        // `module procedure f`: the dummy arguments come from the interface.
        foreach (var a in iface.Args)
        {
            var d = ws.Track(new Entity(a.Name, a.DisplayName, EntityKind.Dummy, e.Body)
            {
                TypeText = a.TypeText, TypeName = a.TypeName, TypeScope = a.TypeScope, Dims = a.Dims, IsArray = a.IsArray,
            });
            d.Attrs.AddRange(a.Attrs);
            e.Body.Entities.TryAdd(d.Name, d);
            e.Args.Add(d);
        }

        if (iface.Kind == EntityKind.Function)
        {
            if (iface.Result is { } r)
            {
                var rv = ws.Track(new Entity(r.Name, r.DisplayName, EntityKind.Variable, e.Body)
                {
                    TypeText = r.TypeText, TypeName = r.TypeName, TypeScope = r.TypeScope, Dims = r.Dims, IsArray = r.IsArray,
                });
                e.Body.Entities.TryAdd(rv.Name, rv);
                e.Result = rv;
            }
            else
            {
                e.Body.Entities.TryAdd(e.Name, e);
            }
        }
    }

    private static void LinkBinding(Workspace ws, Entity b)
    {
        var type = b.Scope.Owner;
        if (b.ImplName is { } implName && b.Scope.Parent is { } host)
        {
            var impl = ws.Lookup(host, implName, LookupCtx.Specific);
            if (impl is not null && impl != b && impl.Kind is EntityKind.Subroutine or EntityKind.Function)
            {
                impl.Rels.Add((b, true, false));
            }
        }

        if (type is not null && ws.ParentOf(type) is { } parent && ws.FindMember(parent, b.Name) is { Kind: EntityKind.Binding } overridden)
        {
            b.Rels.Add((overridden, true, false));
        }
    }
}
