using ScipSharp.Indexer.Delphi.Syntax;

namespace ScipSharp.Indexer.Delphi.Semantics;

/// <summary>What the walker needs to know about a routine declaration.</summary>
/// <param name="Entity">The routine (for an implementation: the declaration it implements).</param>
/// <param name="OwnerType">The class/record for methods.</param>
/// <param name="HeaderScope">Scope for parameter and result types of this header.</param>
/// <param name="IsImplementationOfDeclaration">The header implements an earlier declaration (interface section, class, forward).</param>
internal sealed record RoutineInfo(Entity Entity, Entity? OwnerType, Scope HeaderScope, bool IsImplementationOfDeclaration);

/// <summary>
/// Creates entities for the declarations of every compilation unit, merges forward declarations, links
/// implementation-section routine bodies (<c>TFoo.Bar</c>, interface routines) to their declarations and assigns
/// overload disambiguators.
/// </summary>
internal sealed class Binder(Model model)
{
    private readonly List<(RoutineDecl Decl, Scope Scope, SourceInfo Source, Entity Unit)> _pendingImpls = [];
    private readonly List<MemberTable> _tables = [];

    public Dictionary<RoutineDecl, RoutineInfo> Routines { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Entities by (file id, name token index) for every declared name.</summary>
    public Dictionary<(int File, int Tok), Entity> ByNameToken { get; } = [];

    public Dictionary<Entity, (UnitScope Interface, UnitScope Implementation)> UnitScopes { get; } = new(ReferenceEqualityComparer.Instance);

    public Dictionary<Entity, Scope> BodyScopes { get; } = new(ReferenceEqualityComparer.Instance);

    public Entity DeclareUnit(SourceInfo src, PackageInfo package, bool external)
    {
        var fs = src.Syntax;
        var kind = fs.Kind switch
        {
            FileKind.Program => EntityKind.Program,
            FileKind.Library => EntityKind.Library,
            FileKind.Package => EntityKind.Package,
            _ => EntityKind.Unit,
        };
        var name = fs.Name.Length > 0 ? fs.Name : Path.GetFileNameWithoutExtension(src.Path);
        var unit = new Entity(kind, name, null)
        {
            Source = src,
            Package = package,
            UnitSegments = name.Split('.'),
            IsExternal = external,
            Doc = fs.Doc,
            DefToken = fs.NameToks.Count > 0 ? fs.Tokens[fs.NameToks[0]] : null,
        };
        unit.Unit = unit;
        if (kind == EntityKind.Unit)
        {
            unit.ImplementationMembers = new MemberTable();
        }

        src.Unit = unit;
        var intf = new UnitScope(unit, false, model);
        var impl = new UnitScope(unit, true, model);
        UnitScopes[unit] = (intf, impl);
        if (!model.UnitsByName.TryGetValue(name, out var list))
        {
            model.UnitsByName[name] = list = [];
        }

        list.Add(unit);
        _tables.Add(unit.Members);
        Declare(fs.InterfaceDecls, unit, unit.Members, intf, src, implementation: false);
        Declare(fs.ImplementationDecls, unit, unit.ImplementationMembers ?? unit.Members, kind == EntityKind.Unit ? impl : intf, src, implementation: true);
        return unit;
    }

    private Entity New(EntityKind kind, int nameTok, Entity parent, SourceInfo src, Decl? decl)
    {
        var tok = src.Tokens[nameTok];
        var e = new Entity(kind, tok.Text, parent)
        {
            Unit = parent.Unit,
            Package = parent.Package,
            Source = src,
            Decl = decl,
            DefToken = tok,
            Doc = decl?.Doc,
            IsLocal = parent.IsLocal,
            IsExternal = parent.IsExternal,
        };
        ByNameToken[(tok.File, nameTok)] = e;
        return e;
    }

    public void Declare(List<Decl> decls, Entity parent, MemberTable table, Scope scope, SourceInfo src, bool implementation)
    {
        foreach (var d in decls)
        {
            switch (d)
            {
                case TypeDecl td:
                    DeclareType(td, parent, table, scope, src);
                    break;
                case ConstDecl cd:
                {
                    var e = New(EntityKind.Const, cd.NameTok, parent, src, cd);
                    e.TypeSyntax = cd.Type;
                    e.Scope = scope;
                    e.IsClassMember = cd.IsClassMember;
                    DeclareAnonymousType(cd.Type, e, scope, src);
                    table.Add(e);
                    break;
                }

                case VarDecl vd:
                {
                    var kind = parent.IsType ? EntityKind.Field : EntityKind.Var;
                    Entity? first = null;
                    foreach (var nt in vd.NameToks)
                    {
                        var e = New(kind, nt, parent, src, vd);
                        e.TypeSyntax = vd.Type;
                        e.Scope = scope;
                        e.IsClassMember = vd.Kind == VarKind.ClassVar;
                        table.Add(e);
                        first ??= e;
                    }

                    if (first is not null)
                    {
                        DeclareAnonymousType(vd.Type, first, scope, src);
                    }

                    break;
                }

                case RoutineDecl rd:
                    DeclareRoutine(rd, parent, table, scope, src, implementation);
                    break;
                case PropertyDecl pd:
                {
                    var e = New(EntityKind.Property, pd.NameTok, parent, src, pd);
                    e.TypeSyntax = pd.Type;
                    e.Scope = scope;
                    e.IsDefaultProperty = pd.IsDefault;
                    e.IsClassMember = pd.IsClassProperty;
                    foreach (var g in pd.IndexParams)
                    {
                        foreach (var nt in g.NameToks)
                        {
                            var p = New(EntityKind.Param, nt, e, src, null);
                            p.TypeSyntax = g.Type;
                            p.Scope = scope;
                            e.Params.Add(p);
                        }
                    }

                    table.Add(e);
                    break;
                }

                case LabelDecl ld:
                    foreach (var nt in ld.NameToks)
                    {
                        table.Add(New(EntityKind.Label, nt, parent, src, ld));
                    }

                    break;
            }
        }
    }

    private void DeclareAnonymousType(TypeNode? type, Entity owner, Scope scope, SourceInfo src)
    {
        switch (type)
        {
            case StructTypeNode s:
            {
                var anon = new Entity(s.Kind == StructKind.Record ? EntityKind.Record : EntityKind.Class, "", owner.Parent)
                {
                    Unit = owner.Unit, Package = owner.Package, Source = src, Scope = scope, AnonymousOwner = owner,
                    IsLocal = owner.IsLocal, IsExternal = owner.IsExternal, TypeSyntax = s,
                };
                anon.AncestorSyntax.AddRange(s.Ancestors);
                model.AnonymousTypes[s] = anon;
                var body = new TypeBodyScope(anon, model, scope);
                BodyScopes[anon] = body;
                _tables.Add(anon.Members);
                Declare(s.Members, anon, anon.Members, body, src, implementation: false);
                break;
            }

            case EnumTypeNode en:
            {
                var anon = new Entity(EntityKind.Enum, "", owner.Parent)
                {
                    Unit = owner.Unit, Package = owner.Package, Source = src, Scope = scope, AnonymousOwner = owner,
                    IsLocal = owner.IsLocal, IsExternal = owner.IsExternal, TypeSyntax = en,
                };
                model.AnonymousTypes[en] = anon;
                DeclareEnumMembers(en, anon, null, src);
                break;
            }

            case ArrayTypeNode a:
                DeclareAnonymousType(a.Element, owner, scope, src);
                break;
            case SetTypeNode st:
                DeclareAnonymousType(st.Element, owner, scope, src);
                break;
        }
    }

    private readonly Dictionary<(MemberTable, string), Entity> _forwards = [];

    private void DeclareType(TypeDecl td, Entity parent, MemberTable table, Scope scope, SourceInfo src)
    {
        var name = src.Tokens[td.NameTok].Text;
        var isForward = td.Type is StructTypeNode { IsForward: true };
        var key = (table, name.ToUpperInvariant());
        if (!isForward && _forwards.Remove(key, out var fwd))
        {
            // The full declaration of a forward-declared class or interface: same entity.
            fwd.ForwardTokens.Add(fwd.DefToken!.Value);
            fwd.DefToken = src.Tokens[td.NameTok];
            fwd.Decl = td;
            fwd.Doc = td.Doc ?? fwd.Doc;
            ByNameToken[(src.Tokens[td.NameTok].File, td.NameTok)] = fwd;
            fwd.TypeParams.Clear();
            FillType(fwd, td, scope, src, table);
            return;
        }

        var kind = KindOf(td.Type);
        var e = New(kind, td.NameTok, parent, src, td);
        FillType(e, td, scope, src, table);
        table.Add(e);
        if (isForward)
        {
            _forwards[key] = e;
        }
    }

    private static EntityKind KindOf(TypeNode? t) => t switch
    {
        StructTypeNode s => s.Kind switch
        {
            StructKind.Class => EntityKind.Class,
            StructKind.Record => EntityKind.Record,
            StructKind.Interface or StructKind.DispInterface => EntityKind.Interface,
            StructKind.Object => EntityKind.Object,
            StructKind.ClassHelper => EntityKind.ClassHelper,
            _ => EntityKind.RecordHelper,
        },
        EnumTypeNode => EntityKind.Enum,
        ProcTypeNode => EntityKind.ProcType,
        SetTypeNode => EntityKind.SetType,
        ArrayTypeNode => EntityKind.ArrayType,
        PointerTypeNode => EntityKind.PointerType,
        ClassOfTypeNode => EntityKind.ClassRef,
        ExprTypeNode => EntityKind.Subrange,
        NamedTypeNode => EntityKind.Alias,
        _ => EntityKind.OtherType,
    };

    private void FillType(Entity e, TypeDecl td, Scope scope, SourceInfo src, MemberTable enclosingTable)
    {
        e.Kind = KindOf(td.Type);
        e.TypeSyntax = td.Type;
        e.IsStrongAlias = td.IsStrongAlias;
        var tpScope = scope;
        if (td.TypeParams.Count > 0)
        {
            var tpTable = new MemberTable();
            foreach (var tp in td.TypeParams)
            {
                var p = New(EntityKind.TypeParam, tp.NameTok, e, src, null);
                p.Scope = scope;
                p.AncestorSyntax.AddRange(tp.Constraints.OfType<NamedTypeNode>());
                e.TypeParams.Add(p);
                tpTable.Add(p);
            }

            tpScope = new TableScope(tpTable, scope);
        }

        e.Scope = tpScope;
        switch (td.Type)
        {
            case StructTypeNode s:
            {
                e.AncestorSyntax.Clear();
                e.AncestorSyntax.AddRange(s.Ancestors);
                e.HelperForSyntax = s.HelperFor;
                var body = new TypeBodyScope(e, model, tpScope);
                BodyScopes[e] = body;
                _tables.Add(e.Members);
                Declare(s.Members, e, e.Members, body, src, implementation: false);
                if (s.Kind is StructKind.ClassHelper or StructKind.RecordHelper)
                {
                    model.Helpers.Add(e);
                }

                break;
            }

            case EnumTypeNode en:
                e.IsScopedEnum = en.Scoped;
                DeclareEnumMembers(en, e, en.Scoped ? null : enclosingTable, src);
                break;
            case SetTypeNode { Element: EnumTypeNode inner }:
                DeclareInlineEnum(inner, e, enclosingTable, src);
                break;
            case ArrayTypeNode { Element: EnumTypeNode inner }:
                DeclareInlineEnum(inner, e, enclosingTable, src);
                break;
            case NamedTypeNode { Parts.Count: 1 } self when string.Equals(src.Tokens[self.Parts[0].Tok].Text, e.Name, StringComparison.OrdinalIgnoreCase) && self.Parts[0].Args is null:
                // `Integer = Integer;` in the System stub: an intrinsic type.
                e.Kind = EntityKind.OtherType;
                e.TypeSyntax = null;
                break;
            case ProcTypeNode pt:
                e.ReturnSyntax = pt.Return;
                DeclareParams(pt.Params, e, tpScope, src);
                break;
        }
    }

    private void DeclareInlineEnum(EnumTypeNode en, Entity owner, MemberTable enclosingTable, SourceInfo src)
    {
        var anon = new Entity(EntityKind.Enum, "", owner.Parent)
        {
            Unit = owner.Unit, Package = owner.Package, Source = src, Scope = owner.Scope, AnonymousOwner = owner,
            IsLocal = owner.IsLocal, IsExternal = owner.IsExternal, TypeSyntax = en, IsScopedEnum = en.Scoped,
        };
        model.AnonymousTypes[en] = anon;
        DeclareEnumMembers(en, anon, en.Scoped ? null : enclosingTable, src);
    }

    private void DeclareEnumMembers(EnumTypeNode en, Entity enumType, MemberTable? enclosingTable, SourceInfo src)
    {
        foreach (var item in en.Items)
        {
            var m = New(EntityKind.EnumMember, item.NameTok, enumType, src, null);
            m.Scope = enumType.Scope;
            enumType.Members.Add(m);
            enclosingTable?.Add(m);
        }
    }

    private void DeclareParams(List<ParamGroup> groups, Entity routine, Scope scope, SourceInfo src)
    {
        foreach (var g in groups)
        {
            foreach (var nt in g.NameToks)
            {
                var p = New(EntityKind.Param, nt, routine, src, null);
                p.TypeSyntax = g.Type;
                p.Scope = scope;
                p.DefaultValue = g.Default;
                p.ParamModifier = g.Modifier;
                routine.Params.Add(p);
            }
        }
    }

    /// <summary>Creates the entity for a routine header (with type parameters and parameters).</summary>
    public Entity CreateRoutine(RoutineDecl rd, Entity parent, Scope scope, SourceInfo src)
    {
        var kind = parent.IsType ? EntityKind.Method : EntityKind.Routine;
        var e = New(kind, rd.NameTok, parent, src, rd);
        e.RoutineKind = rd.Kind;
        e.IsClassMember = rd.IsClassMethod;
        e.ReturnSyntax = rd.Return;
        e.Directives.UnionWith(rd.Directives);
        var headerScope = scope;
        if (rd.TypeParams.Count > 0)
        {
            var tpTable = new MemberTable();
            foreach (var tp in rd.TypeParams)
            {
                var p = New(EntityKind.TypeParam, tp.NameTok, e, src, null);
                p.Scope = scope;
                p.AncestorSyntax.AddRange(tp.Constraints.OfType<NamedTypeNode>());
                e.TypeParams.Add(p);
                tpTable.Add(p);
            }

            headerScope = new TableScope(tpTable, scope);
        }

        e.Scope = headerScope;
        DeclareParams(rd.Params, e, headerScope, src);
        return e;
    }

    private void DeclareRoutine(RoutineDecl rd, Entity parent, MemberTable table, Scope scope, SourceInfo src, bool implementation)
    {
        if (rd.ResolutionTarget is not null)
        {
            return; // method resolution clause: references only
        }

        if (rd.IsQualified)
        {
            _pendingImpls.Add((rd, scope, src, parent));
            return;
        }

        if (implementation && parent.IsUnitLike)
        {
            // An implementation of a routine declared in the interface section or forward-declared earlier.
            var name = src.Tokens[rd.NameTok].Text;
            var fromInterface = ReferenceEquals(table, parent.Members)
                ? []
                : parent.Members.Get(name).Where(c => c.IsRoutine && !_implemented.Contains(c));
            var candidates = fromInterface
                .Concat(table.Get(name).Where(c => c.IsRoutine && c.Directives.Contains("forward") && !_implemented.Contains(c)))
                .Distinct().ToList();
            if (rd.Body is not null && MatchDeclaration(rd, candidates, src) is { } decl)
            {
                _implemented.Add(decl);
                Routines[rd] = new RoutineInfo(decl, null, HeaderScopeFor(decl, scope), true);
                return;
            }
        }

        var e = CreateRoutine(rd, parent, scope, src);
        table.Add(e);
        if (rd.Body is not null)
        {
            _implemented.Add(e);
        }

        Routines[rd] = new RoutineInfo(e, parent.IsType ? parent : null, e.Scope!, false);
    }

    private readonly HashSet<Entity> _implemented = new(ReferenceEqualityComparer.Instance);

    private static Scope HeaderScopeFor(Entity decl, Scope implScope)
    {
        if (decl.TypeParams.Count == 0)
        {
            return implScope;
        }

        var t = new MemberTable();
        foreach (var tp in decl.TypeParams)
        {
            t.Add(tp);
        }

        return new TableScope(t, implScope);
    }

    /// <summary>Normalised parameter signature: modifier and type text per parameter.</summary>
    private static List<string> SignatureKey(IReadOnlyList<ParamGroup> groups, IReadOnlyList<Token> tokens)
    {
        var key = new List<string>();
        foreach (var g in groups)
        {
            var typeText = g.Type is null ? "" : string.Concat(Enumerable.Range(g.Type.Start, Math.Max(0, g.Type.End - g.Type.Start)).Select(i => tokens[i].Text.ToUpperInvariant()));
            var dot = typeText.LastIndexOf('.');
            if (dot >= 0 && !typeText.Contains('<', StringComparison.Ordinal))
            {
                typeText = typeText[(dot + 1)..];
            }

            foreach (var _ in g.NameToks)
            {
                key.Add($"{g.Modifier}:{typeText}");
            }
        }

        return key;
    }

    private static RoutineDecl? DeclOf(Entity e) => e.Decl as RoutineDecl;

    private static Entity? MatchDeclaration(RoutineDecl impl, List<Entity> candidates, SourceInfo implSrc)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var implKey = SignatureKey(impl.Params, implSrc.Tokens);
        if (impl.Params.Count == 0 && candidates.Count == 1)
        {
            return candidates[0]; // parameters may be omitted in the implementation
        }

        foreach (var c in candidates)
        {
            if (DeclOf(c) is { } d && c.Source is { } cs && SignatureKey(d.Params, cs.Tokens).SequenceEqual(implKey))
            {
                return c;
            }
        }

        foreach (var c in candidates)
        {
            if (c.Params.Count == implKey.Count && candidates.Count(x => x.Params.Count == implKey.Count) == 1)
            {
                return c;
            }
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    /// <summary>Links <c>TFoo.Bar</c> implementations to their class declarations. Requires uses clauses to be resolved.</summary>
    public void LinkImplementations()
    {
        foreach (var (rd, scope, src, unit) in _pendingImpls)
        {
            var parts = rd.NameParts;
            Entity? owner = null;
            var path = model.ResolvePath(src.Tokens, parts.Take(parts.Count - 1).ToList(), scope, typeContext: true);
            if (path.Count > 0 && path[^1].LastPart == parts.Count - 2 && path[^1].Entity.IsType)
            {
                owner = path[^1].Entity;
            }

            if (owner is null || owner.IsExternal)
            {
                // Unknown class: keep a unit-level routine so the body is still indexed.
                var orphan = CreateRoutine(rd, unit, scope, src);
                (unit.ImplementationMembers ?? unit.Members).Add(orphan);
                Routines[rd] = new RoutineInfo(orphan, null, orphan.Scope!, false);
                continue;
            }

            var name = src.Tokens[rd.NameTok].Text;
            var candidates = owner.Members.Get(name).Where(c => c.IsRoutine && !_implemented.Contains(c)).ToList();
            var bodyScope = MethodScope(owner, scope);
            if (MatchDeclaration(rd, candidates, src) is { } decl)
            {
                _implemented.Add(decl);
                Routines[rd] = new RoutineInfo(decl, owner, HeaderScopeFor(decl, bodyScope), true);
                continue;
            }

            var e = CreateRoutine(rd, owner, bodyScope, src);
            owner.Members.Add(e);
            Routines[rd] = new RoutineInfo(e, owner, e.Scope!, false);
        }

        _pendingImpls.Clear();
    }

    /// <summary>The scope chain for a method body: the class members (and enclosing classes), its type parameters, then the implementation section.</summary>
    public Scope MethodScope(Entity owner, Scope implScope)
    {
        var chain = new List<Entity>();
        for (var t = owner; t is { IsType: true }; t = t.Parent)
        {
            chain.Add(t);
        }

        var scope = implScope;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var t = chain[i];
            if (t.TypeParams.Count > 0)
            {
                var tpTable = new MemberTable();
                foreach (var tp in t.TypeParams)
                {
                    tpTable.Add(tp);
                }

                scope = new TableScope(tpTable, scope);
            }

            scope = new TypeBodyScope(t, model, scope);
        }

        return scope;
    }

    /// <summary>Assigns overload disambiguators and arity suffixes for colliding generic type names.</summary>
    public void AssignDisambiguators()
    {
        foreach (var table in _tables)
        {
            AssignFor(table.InOrder);
        }

        foreach (var units in model.UnitsByName.Values)
        {
            foreach (var u in units)
            {
                if (u.ImplementationMembers is { } impl)
                {
                    AssignFor(u.Members.InOrder.Concat(impl.InOrder).ToList());
                }
            }
        }
    }

    private static void AssignFor(IReadOnlyList<Entity> entities)
    {
        foreach (var group in entities.Where(e => e.IsRoutine && !e.IsLocal).GroupBy(e => e.Name.ToUpperInvariant()))
        {
            var i = 0;
            foreach (var e in group.Distinct())
            {
                if (e.Parent is not null && ReferenceEquals(e.Parent, group.First().Parent))
                {
                    e.Disambiguator = i == 0 ? "" : $"+{i}";
                    i++;
                }
            }
        }

        foreach (var group in entities.Where(e => e.IsType && e.Kind != EntityKind.TypeParam).GroupBy(e => e.Name.ToUpperInvariant()))
        {
            var types = group.Distinct().ToList();
            if (types.Count > 1)
            {
                foreach (var t in types.Where(t => t.TypeParams.Count > 0))
                {
                    t.NameSuffix = $"-{t.TypeParams.Count}";
                }
            }
        }
    }
}
