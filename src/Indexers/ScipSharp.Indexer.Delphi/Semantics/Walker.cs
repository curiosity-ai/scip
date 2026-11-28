using System.Runtime.CompilerServices;
using ScipSharp.Indexer.Delphi.Emit;
using ScipSharp.Indexer.Delphi.Syntax;

namespace ScipSharp.Indexer.Delphi.Semantics;

/// <summary>
/// Walks one compilation unit: emits definition occurrences for its declarations and resolves every name in types,
/// constant expressions and statement blocks (designator chains <c>A.B(x)[i].C</c>, <c>with</c>, <c>inherited</c>,
/// inline variables, anonymous methods, <c>on E: Exception do</c>).
/// </summary>
internal sealed class Walker
{
    private readonly Model _model;
    private readonly Binder _binder;
    private readonly OccurrenceSink _sink;
    private readonly SourceInfo _src;
    private readonly IReadOnlyList<Token> _t;
    private readonly Parser _parser;
    private readonly IReadOnlyDictionary<UsesItem, Entity> _uses;
    private Entity? _anonymousLocalsOwner;

    public Walker(Model model, Binder binder, OccurrenceSink sink, SourceInfo src, IReadOnlyDictionary<UsesItem, Entity> uses)
    {
        _model = model;
        _binder = binder;
        _sink = sink;
        _src = src;
        _t = src.Tokens;
        _parser = new Parser(_t);
        _uses = uses;
    }

    private Token T(int i) => _t[Math.Clamp(i, 0, _t.Count - 1)];

    public void Run()
    {
        var fs = _src.Syntax;
        var unit = _src.Unit!;
        var (intf, impl) = _binder.UnitScopes[unit];
        if (fs.NameToks.Count > 0)
        {
            var whole = OccurrenceSink.Span(_t, 0, _t.Count);
            _sink.Add(T(fs.NameToks[0]), T(fs.NameToks[^1]), unit, SymbolRole.Definition, whole);
        }

        foreach (var item in fs.InterfaceUses.Concat(fs.ImplementationUses))
        {
            if (_uses.TryGetValue(item, out var u))
            {
                _sink.Add(T(item.NameToks[0]), T(item.NameToks[^1]), u, SymbolRole.Import);
            }
        }

        VisitDecls(fs.InterfaceDecls, intf);
        var implScope = unit.Kind == EntityKind.Unit ? (Scope)impl : intf;
        VisitDecls(fs.ImplementationDecls, implScope);
        foreach (var block in fs.Blocks)
        {
            Walk(block.Start, block.End, implScope);
        }
    }

    private Entity? Declared(int tok) => _binder.ByNameToken.GetValueOrDefault((T(tok).File, tok));

    private Range? DeclRange(Decl d) => OccurrenceSink.Span(_t, d.Start, d.End);

    private void Def(int tok, SymbolRole role = SymbolRole.Definition, Range? enclosing = null)
    {
        if (Declared(tok) is { } e)
        {
            _sink.Add(T(tok), e, role, enclosing);
        }
    }

    // -------------------------------------------------------------------------------------------------------------
    // Declarations

    private void VisitDecls(List<Decl> decls, Scope scope)
    {
        foreach (var d in decls)
        {
            try
            {
                VisitDecl(d, scope);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException or KeyNotFoundException or InsufficientExecutionStackException)
            {
                // Never let one declaration abort the whole file.
            }
        }
    }

    private void VisitDecl(Decl d, Scope scope)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack(); // deeply nested source: caught per declaration / per file
        VisitAttributes(d.Attributes, scope);
        switch (d)
        {
            case TypeDecl td:
            {
                var e = Declared(td.NameTok);
                var forward = td.Type is StructTypeNode { IsForward: true };
                Def(td.NameTok, forward ? SymbolRole.ForwardDefinition : SymbolRole.Definition, DeclRange(td));
                var typeScope = e?.Scope ?? scope;
                foreach (var tp in td.TypeParams)
                {
                    Def(tp.NameTok);
                    foreach (var c in tp.Constraints)
                    {
                        VisitType(c, typeScope);
                    }
                }

                VisitType(td.Type, typeScope, e);
                break;
            }

            case ConstDecl cd:
                Def(cd.NameTok, SymbolRole.Definition, DeclRange(cd));
                VisitType(cd.Type, scope, Declared(cd.NameTok));
                if (cd.Value is { } v)
                {
                    Walk(v.Start, v.End, scope);
                }

                break;
            case VarDecl vd:
                foreach (var nt in vd.NameToks)
                {
                    Def(nt, SymbolRole.Definition, DeclRange(vd));
                }

                VisitType(vd.Type, scope, vd.NameToks.Count > 0 ? Declared(vd.NameToks[0]) : null);
                if (vd.Init is { } init)
                {
                    Walk(init.Start, init.End, scope);
                }

                if (vd.Absolute is { } abs)
                {
                    Walk(abs.Start, abs.End, scope);
                }

                break;
            case RoutineDecl rd:
                VisitRoutine(rd, scope);
                break;
            case PropertyDecl pd:
                VisitProperty(pd, scope);
                break;
            case LabelDecl ld:
                foreach (var nt in ld.NameToks)
                {
                    Def(nt);
                }

                break;
        }
    }

    private void VisitAttributes(List<AttributeNode> attrs, Scope scope)
    {
        foreach (var a in attrs)
        {
            var parts = a.Name.Parts;
            if (parts.Count == 1)
            {
                var name = T(parts[0].Tok).Text;
                var found = scope.Lookup(name + "Attribute", static e => e.IsType);
                if (found.Count == 0)
                {
                    found = scope.Lookup(name, static e => e.IsType);
                }

                if (found.Count > 0)
                {
                    _sink.Add(T(parts[0].Tok), found[0]);
                }
            }
            else
            {
                VisitType(a.Name, scope);
            }

            if (a.Args is { } args)
            {
                Walk(args.Start, args.End, scope);
            }
        }
    }

    private void VisitProperty(PropertyDecl pd, Scope scope)
    {
        var e = Declared(pd.NameTok);
        Def(pd.NameTok, SymbolRole.Definition, DeclRange(pd));
        foreach (var g in pd.IndexParams)
        {
            foreach (var nt in g.NameToks)
            {
                Def(nt);
            }

            VisitType(g.Type, scope);
        }

        VisitType(pd.Type, scope);
        foreach (var spec in pd.Specs)
        {
            if (spec.Value.IsEmpty)
            {
                continue;
            }

            Walk(spec.Value.Start, spec.Value.End, scope);
        }

        _ = e;
    }

    private void VisitRoutine(RoutineDecl rd, Scope scope)
    {
        if (rd.ResolutionTarget is { } target)
        {
            VisitResolutionClause(rd, target, scope);
            return;
        }

        if (!_binder.Routines.TryGetValue(rd, out var info))
        {
            return;
        }

        var e = info.Entity;
        var enclosing = DeclRange(rd);
        var headerScope = info.HeaderScope;

        // Qualifier: TFoo.Bar / TOuter.TInner<T>.Bar
        if (rd.NameParts.Count > 1)
        {
            var quals = rd.NameParts.Take(rd.NameParts.Count - 1).ToList();
            var path = _model.ResolvePath(_t, quals, scope, typeContext: true);
            EmitPath(quals, path);
            if (info.OwnerType is { } owner)
            {
                var lastQual = quals[^1];
                if (lastQual.Args is { } targs)
                {
                    for (var k = 0; k < targs.Count && k < owner.TypeParams.Count; k++)
                    {
                        if (targs[k] is NamedTypeNode { Parts.Count: 1 } n)
                        {
                            _sink.Add(T(n.Parts[0].Tok), owner.TypeParams[k]);
                        }
                    }
                }
            }
        }

        var role = info.IsImplementationOfDeclaration ? (SymbolRole)0 : SymbolRole.Definition;
        _sink.Add(T(rd.NameTok), e, role, enclosing);

        // Type parameters
        for (var k = 0; k < rd.TypeParams.Count; k++)
        {
            if (info.IsImplementationOfDeclaration)
            {
                if (k < e.TypeParams.Count)
                {
                    _sink.Add(T(rd.TypeParams[k].NameTok), e.TypeParams[k]);
                }
            }
            else
            {
                Def(rd.TypeParams[k].NameTok);
            }

            foreach (var c in rd.TypeParams[k].Constraints)
            {
                VisitType(c, headerScope);
            }
        }

        // Parameters
        var index = 0;
        foreach (var g in rd.Params)
        {
            VisitAttributes(g.Attributes, headerScope);
            foreach (var nt in g.NameToks)
            {
                if (info.IsImplementationOfDeclaration)
                {
                    if (index < e.Params.Count)
                    {
                        _sink.Add(T(nt), e.Params[index]);
                    }
                }
                else
                {
                    Def(nt);
                }

                index++;
            }

            VisitType(g.Type, headerScope);
            if (g.Default is { } dv)
            {
                Walk(dv.Start, dv.End, headerScope);
            }
        }

        VisitType(rd.Return, headerScope);
        foreach (var arg in rd.DirectiveArgs)
        {
            Walk(arg.Start, arg.End, headerScope);
        }

        if (rd.Body is { } body)
        {
            VisitBody(rd, body, e, info.OwnerType ?? OwnerTypeOf(scope), headerScope);
        }
    }

    private static Entity? OwnerTypeOf(Scope scope) => scope.RoutineScope?.OwnerType;

    private void VisitBody(RoutineDecl rd, RoutineBody body, Entity routine, Entity? ownerType, Scope parentScope)
    {
        var table = new MemberTable();
        foreach (var p in routine.Params)
        {
            table.Add(p);
        }

        var rs = new RoutineScope(routine, ownerType, table, parentScope);
        var localsOwner = new Entity(EntityKind.Routine, "", routine)
        {
            IsLocal = true, Unit = routine.Unit, Package = routine.Package, Source = _src,
        };
        rs.LocalsOwner = localsOwner;
        _binder.Declare(body.Locals, localsOwner, table, rs, _src, implementation: true);
        VisitDecls(body.Locals, rs);
        if (!body.IsAsm && !body.Block.IsEmpty)
        {
            Walk(body.Block.Start, body.Block.End, rs);
        }

        _ = rd;
    }

    private void VisitResolutionClause(RoutineDecl rd, int target, Scope scope)
    {
        // procedure IFoo.Bar = Baz;
        var quals = rd.NameParts.Take(rd.NameParts.Count - 1).ToList();
        var path = _model.ResolvePath(_t, quals, scope, typeContext: true);
        EmitPath(quals, path);
        Entity? intfMethod = null;
        if (path.Count > 0 && path[^1].LastPart == quals.Count - 1)
        {
            var intf = path[^1].Entity;
            var r = _model.FindMember(NamedTy.Of(intf), T(rd.NameTok).Text);
            if (r.Found.Count > 0)
            {
                intfMethod = r.Found[0];
                _sink.Add(T(rd.NameTok), intfMethod);
            }
        }

        var impl = scope.Lookup(T(target).Text, static e => e.IsRoutine);
        if (impl.Count > 0)
        {
            _sink.Add(T(target), impl[0]);
            if (intfMethod is not null)
            {
                _sink.AddRelationship(impl[0], intfMethod, implementation: true, reference: false);
            }
        }
    }

    private void EmitPath(IReadOnlyList<NamePart> parts, List<(int FirstPart, int LastPart, Entity Entity)> path)
    {
        foreach (var (first, last, entity) in path)
        {
            _sink.Add(T(parts[first].Tok), T(parts[last].Tok), entity);
        }
    }

    // -------------------------------------------------------------------------------------------------------------
    // Types

    private void VisitType(TypeNode? node, Scope scope, Entity? owner = null)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack(); // deeply nested source: caught per declaration / per file
        switch (node)
        {
            case null:
                return;
            case NamedTypeNode n:
            {
                var path = _model.ResolvePath(_t, n.Parts, scope, typeContext: true);
                if (path.Count == 0 && n.Parts.Count > 0 && UnknownIdentifier(T(n.Parts[0].Tok).Text, scope, isCall: false, isType: true) is { } unknown)
                {
                    path.Add((0, 0, unknown));
                    var cur = unknown;
                    for (var k = 1; k < n.Parts.Count; k++)
                    {
                        cur = Model.Synthesize(cur, T(n.Parts[k].Tok).Text, isCall: false, isType: true);
                        path.Add((k, k, cur));
                    }
                }

                EmitPath(n.Parts, path);
                foreach (var part in n.Parts)
                {
                    if (part.Args is { } args)
                    {
                        foreach (var a in args)
                        {
                            VisitType(a, scope);
                        }
                    }
                }

                if (n.Length is { } len)
                {
                    Walk(len.Start, len.End, scope);
                }

                return;
            }

            case StructTypeNode s:
            {
                var e = owner is { IsStructured: true } ? owner : _model.AnonymousTypes.GetValueOrDefault(s);
                foreach (var a in s.Ancestors)
                {
                    VisitType(a, scope);
                }

                VisitType(s.HelperFor, scope);
                if (e is not null && _binder.BodyScopes.TryGetValue(e, out var body))
                {
                    VisitDecls(s.Members, body);
                }

                return;
            }

            case EnumTypeNode en:
                foreach (var item in en.Items)
                {
                    Def(item.NameTok);
                    if (item.Value is { } v)
                    {
                        Walk(v.Start, v.End, scope);
                    }
                }

                return;
            case SetTypeNode st:
                VisitType(st.Element, scope, owner);
                return;
            case ArrayTypeNode a:
                foreach (var i in a.Indexes)
                {
                    VisitType(i, scope);
                }

                VisitType(a.Element, scope, owner);
                return;
            case PointerTypeNode p:
                VisitType(p.Target, scope);
                return;
            case ClassOfTypeNode c:
                VisitType(c.Target, scope);
                return;
            case FileTypeNode f:
                VisitType(f.Element, scope);
                return;
            case ProcTypeNode pt:
                foreach (var g in pt.Params)
                {
                    foreach (var nt in g.NameToks)
                    {
                        Def(nt);
                    }

                    VisitType(g.Type, scope);
                    if (g.Default is { } dv)
                    {
                        Walk(dv.Start, dv.End, scope);
                    }
                }

                VisitType(pt.Return, scope);
                return;
            case ExprTypeNode x:
                Walk(x.Expr.Start, x.Expr.End, scope);
                return;
        }
    }

    // -------------------------------------------------------------------------------------------------------------
    // Statements and expressions

    private static Entity? UnknownIdentifier(string name, Scope scope, bool isCall, bool isType) =>
        Model.SingleUnknownUnit(scope) is { } only ? Model.Synthesize(only, name, isCall, isType) : null;

    private Entity LocalsOwner(Scope scope)
    {
        if (scope.RoutineScope?.LocalsOwner is { } o)
        {
            return o;
        }

        return _anonymousLocalsOwner ??= new Entity(EntityKind.Routine, "", _src.Unit)
        {
            IsLocal = true, Unit = _src.Unit, Package = _src.Unit?.Package, Source = _src,
        };
    }

    /// <summary>Walks the tokens <c>[start, end)</c>, resolving every name found.</summary>
    public void Walk(int start, int end, Scope scope)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack(); // deeply nested source: caught per declaration / per file
        var stack = new List<(int End, Scope Saved)>();
        var i = start;
        var guard = 0;
        while (i < end && guard++ < 1_000_000)
        {
            while (stack.Count > 0 && i >= stack[^1].End)
            {
                scope = stack[^1].Saved;
                stack.RemoveAt(stack.Count - 1);
            }

            var t = T(i);
            if (t.IsEof)
            {
                return;
            }

            var before = i;
            if (t.Kind == TokenKind.Keyword)
            {
                switch (t.Text.ToLowerInvariant())
                {
                    case "with":
                        i = WalkWith(i, end, ref scope, stack);
                        break;
                    case "var" or "const":
                        i = WalkInlineDecl(i, end, ref scope);
                        break;
                    case "for" when T(i + 1).IsKeyword("var"):
                        i = WalkForVar(i, end, ref scope);
                        break;
                    case "procedure" or "function":
                        i = WalkAnonymous(i, end, scope);
                        break;
                    case "inherited":
                        i = WalkInherited(i, end, scope);
                        break;
                    case "asm":
                        i = _parser.MatchEnd(i);
                        break;
                    case "string":
                        i = Designator(i, end, scope, out _);
                        break;
                    default:
                        i++;
                        break;
                }
            }
            else if (t.IsIdentifier)
            {
                if (t.IsWord("on") && T(i + 1).IsIdentifier && T(i + 2).IsSymbol(":"))
                {
                    i = WalkOn(i, end, ref scope, stack);
                }
                else
                {
                    i = Designator(i, end, scope, out _);
                }
            }
            else if (t.IsSymbol("("))
            {
                i = WalkParenthesized(i, end, scope, out _);
            }
            else
            {
                i++;
            }

            if (i <= before)
            {
                i = before + 1;
            }
        }
    }

    private int CountArgs(int open)
    {
        if (T(open + 1).IsSymbol(")"))
        {
            return 0;
        }

        var count = 1;
        for (var j = open + 1; j < _t.Count; j++)
        {
            var t = T(j);
            if (t.IsEof)
            {
                break;
            }

            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                // Jump over nested groups (instead of tracking depth): f(f(f(...))) stays linear, not quadratic.
                j = _parser.SkipParens(j) - 1;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
            {
                break;
            }
            else if (t.IsSymbol(","))
            {
                count++;
            }
            else if (t.IsKeyword("begin") || t.IsKeyword("case") || t.IsKeyword("try"))
            {
                j = _parser.MatchEnd(j) - 1;
            }
        }

        return count;
    }

    private Ty? SelfTy(Scope scope)
    {
        for (var s = scope.RoutineScope; s is not null; s = s.Parent?.RoutineScope)
        {
            if (s.OwnerType is { } owner)
            {
                var self = Model.SelfType(owner);
                return s.Routine is { IsClassMember: true } ? new ClassOfTy(self) : self;
            }
        }

        return null;
    }

    private Ty? ResultTy(Scope scope)
    {
        for (var s = scope.RoutineScope; s is not null; s = s.Parent?.RoutineScope)
        {
            if (s.Routine is { } r && (r.ReturnSyntax is not null || r.RoutineKind == RoutineKind.Function))
            {
                return _model.TypeOf(r);
            }
        }

        return null;
    }

    /// <summary>Resolves a designator chain starting at the identifier <paramref name="i"/>; returns the index after it.</summary>
    private int Designator(int i, int end, Scope scope, out Ty? ty)
    {
        ty = null;
        var tok = T(i);
        var name = tok.Text;
        Entity? ent = null;
        var typeRef = false;
        var j = i + 1;

        // Unit-qualified names: System.SysUtils.Format, Vcl.Forms.TForm.
        var chain = new List<int> { i };
        for (var k = i; T(k + 1).IsSymbol(".") && T(k + 2).IsIdentifier && k + 2 < end; k += 2)
        {
            chain.Add(k + 2);
        }

        var consumed = 0;
        var unit = chain.Count >= 3 ? _model.MatchUnitPrefix(_t, chain, scope, out consumed) : null;
        if (unit is not null)
        {
            _sink.Add(tok, T(chain[consumed - 1]), unit);
            ent = unit;
            j = chain[consumed - 1] + 1;
        }
        else
        {
            var found = scope.Lookup(name);
            if (found.Count == 0 && (tok.IsWord("Self")))
            {
                ty = SelfTy(scope);
                return Postfix(j, end, scope, null, ty, false, out ty);
            }

            if (found.Count == 0 && tok.IsWord("Result") && ResultTy(scope) is { } rt)
            {
                return Postfix(j, end, scope, null, rt, false, out ty);
            }

            if (found.Count == 0 && _model.MatchSingleUnit(name, scope) is { } single && T(j).IsSymbol("."))
            {
                _sink.Add(tok, single);
                ent = single;
            }
            else
            {
                var isCall = T(j).IsSymbol("(");
                Entity? e;
                if (found.Count > 0)
                {
                    e = Model.ChooseOverload(found, isCall ? CountArgs(j) : (found.Any(f => f.IsRoutine) ? 0 : null));
                }
                else if (tok.IsKeyword("string"))
                {
                    e = _model.SystemType("string");
                }
                else
                {
                    // Inside a method of a class with an external (stub/unknown) ancestor, an unresolved name is most
                    // likely an inherited member the stubs do not describe; otherwise try the single unknown unit.
                    e = SelfTy(scope) is { } self && _model.ExternalBase(self) is { } ext
                        ? Model.Synthesize(ext, name, isCall, isType: false)
                        : UnknownIdentifier(name, scope, isCall, isType: false);
                }

                if (e is null)
                {
                    return Postfix(j, end, scope, null, null, false, out ty);
                }

                var occ = _sink.Add(tok, e);
                MarkWrite(occ, j, end);
                ent = e;
                if (e.IsType)
                {
                    typeRef = true;
                    ty = NamedTy.Of(e);
                }
                else
                {
                    ty = _model.TypeOf(e);
                }
            }
        }

        return Postfix(j, end, scope, ent, ty, typeRef, out ty);
    }

    private void MarkWrite(Occurrence? occ, int next, int end)
    {
        if (occ is not null && next < end && T(next).IsSymbol(":="))
        {
            occ.SymbolRoles |= (int)SymbolRole.WriteAccess;
        }
    }

    /// <summary>Resolves the postfix part of a designator (<c>.Member</c>, <c>(args)</c>, <c>[index]</c>, <c>^</c>, <c>&lt;T&gt;</c>).</summary>
    private int Postfix(int j, int end, Scope scope, Entity? ent, Ty? ty, bool typeRef, out Ty? result)
    {
        var guard = 0;
        while (j < end && guard++ < 10_000)
        {
            var t = T(j);
            if (t.IsSymbol(".") && (T(j + 1).IsIdentifier || T(j + 1).Kind == TokenKind.Keyword) && j + 1 < end)
            {
                var nameTok = T(j + 1);
                var name = nameTok.Text;
                var next = j + 2;
                var isCall = T(next).IsSymbol("(");
                IReadOnlyList<Entity> found;
                IReadOnlyDictionary<Entity, Ty> env = Model.EmptyEnv;
                if (ent is { IsUnitLike: true } u)
                {
                    found = u.Members.Get(name);
                    if (found.Count == 0 && u.ImplementationMembers is { } im && ReferenceEquals(u, _src.Unit))
                    {
                        found = im.Get(name);
                    }

                    if (found.Count == 0 && u.IsUnknownUnit)
                    {
                        found = [Model.Synthesize(u, name, isCall, isType: T(next).IsSymbol(".") && name.StartsWith('T'))];
                    }
                }
                else
                {
                    var r = _model.FindMember(ty, name);
                    found = r.Found;
                    env = r.Env;
                    if (found.Count == 0 && _model.ExternalBase(ty) is { } ext)
                    {
                        found = [Model.Synthesize(ext, name, isCall, isType: false)];
                    }
                }

                if (found.Count == 0)
                {
                    ent = null;
                    ty = null;
                    typeRef = false;
                    j = next;
                    continue;
                }

                var m = Model.ChooseOverload(found, isCall ? CountArgs(next) : (found.Any(f => f.IsRoutine) ? 0 : null));
                var occ = _sink.Add(nameTok, m);
                MarkWrite(occ, next, end);
                if (m.IsType)
                {
                    typeRef = true;
                    ty = NamedTy.Of(m);
                }
                else if (m is { IsRoutine: true, RoutineKind: RoutineKind.Constructor } && _model.MemberContainer(ty) is { } inst && inst.Decl.IsStructured)
                {
                    typeRef = false;
                    ty = inst;
                }
                else
                {
                    typeRef = false;
                    ty = Model.Subst(_model.TypeOf(m), env);
                }

                ent = m;
                j = next;
                continue;
            }

            if (t.IsSymbol("("))
            {
                var close = _parser.SkipParens(j);
                Walk(j + 1, Math.Min(close - 1, end), scope);
                if (typeRef && ent is { IsType: true })
                {
                    ty = NamedTy.Of(ent); // typecast TFoo(x)
                }
                else if (_model.Normalize(ty) is ProcTy p && ent is not { IsRoutine: true })
                {
                    ty = p.Return;
                }

                typeRef = false;
                ent = null;
                j = close;
                continue;
            }

            if (t.IsSymbol("["))
            {
                var close = _parser.SkipParens(j);
                Walk(j + 1, Math.Min(close - 1, end), scope);
                ty = _model.IndexType(ty, out _);
                typeRef = false;
                ent = null;
                j = close;
                continue;
            }

            if (t.IsSymbol("^"))
            {
                ty = _model.Normalize(ty) is PointerTy p ? p.Target : null;
                ent = null;
                j++;
                continue;
            }

            if (t.IsSymbol("<") && ent is { } g && (g.IsType || g.IsRoutine) && g.TypeParams.Count > 0 && _parser.LooksLikeTypeArgs(j))
            {
                _parser.Pos = j;
                var args = _parser.ParseTypeArgs();
                foreach (var a in args)
                {
                    VisitType(a, scope);
                }

                if (g.IsType)
                {
                    ty = new NamedTy(g, args.Select(a => _model.ResolveTypeNode(a, scope, _t) ?? Model.Unknown).ToList());
                }

                j = Math.Max(_parser.Pos, j + 1);
                continue;
            }

            break;
        }

        result = ty;
        return j;
    }

    private int WalkParenthesized(int i, int end, Scope scope, out Ty? ty)
    {
        ty = null;
        var close = Math.Min(_parser.SkipParens(i), end);
        var innerEnd = close - 1;
        var asIdx = -1;
        for (var k = i + 1; k < innerEnd; k++)
        {
            var t = T(k);
            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                // Jump over nested groups (instead of tracking depth): deep nesting stays linear, not quadratic.
                k = _parser.SkipParens(k) - 1;
            }
            else if (t.IsKeyword("as"))
            {
                asIdx = k;
            }
        }

        if (asIdx > 0)
        {
            Walk(i + 1, asIdx, scope);
            if (T(asIdx + 1).IsIdentifier)
            {
                var k = Designator(asIdx + 1, innerEnd, scope, out var castTy);
                ty = castTy;
                Walk(k, innerEnd, scope);
            }
            else
            {
                Walk(asIdx + 1, innerEnd, scope);
            }
        }
        else if (T(i + 1).IsIdentifier)
        {
            var k = Designator(i + 1, innerEnd, scope, out var innerTy);
            if (k >= innerEnd)
            {
                ty = innerTy;
            }

            Walk(k, innerEnd, scope);
        }
        else if (T(i + 1).IsKeyword("inherited"))
        {
            Walk(WalkInherited(i + 1, innerEnd, scope), innerEnd, scope);
        }
        else
        {
            Walk(i + 1, innerEnd, scope);
        }

        if (close < end && (T(close).IsSymbol(".") || T(close).IsSymbol("[") || T(close).IsSymbol("^")))
        {
            return Postfix(close, end, scope, null, ty, false, out ty);
        }

        return close;
    }

    private int WalkInherited(int i, int end, Scope scope)
    {
        RoutineScope? rs = null;
        for (var s = scope.RoutineScope; s is not null; s = s.Parent?.RoutineScope)
        {
            if (s.OwnerType is not null)
            {
                rs = s;
                break;
            }
        }

        if (rs?.OwnerType is not { } owner)
        {
            return i + 1;
        }

        var next = T(i + 1);
        if (next.IsIdentifier && i + 1 < end)
        {
            var isCall = T(i + 2).IsSymbol("(");
            var m = _model.InheritedMember(owner, next.Text, isCall ? CountArgs(i + 2) : 0);
            if (m is null)
            {
                return Postfix(i + 2, end, scope, null, null, false, out _);
            }

            _sink.Add(next, m);
            var ty = m.IsType ? NamedTy.Of(m) : _model.TypeOf(m);
            if (m.RoutineKind == RoutineKind.Constructor)
            {
                ty = Model.SelfType(owner);
            }

            return Postfix(i + 2, end, scope, m, ty, m.IsType, out _);
        }

        if (rs.Routine is { } routine)
        {
            var m = _model.InheritedMember(owner, routine.Name, routine.Params.Count);
            if (m is not null)
            {
                _sink.Add(T(i), m);
            }
        }

        return i + 1;
    }

    private int StatementEnd(int k, int limit)
    {
        if (k >= limit)
        {
            return limit;
        }

        var t = T(k);
        if (t.IsKeyword("begin") || t.IsKeyword("try") || t.IsKeyword("case") || t.IsKeyword("asm"))
        {
            return Math.Min(_parser.MatchEnd(k), limit);
        }

        var depth = 0;
        var ifs = 0;
        for (var j = k; j < limit; j++)
        {
            t = T(j);
            if (t.IsEof)
            {
                return j;
            }

            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                depth++;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
            {
                depth--;
                if (depth < 0)
                {
                    return j;
                }
            }
            else if (depth == 0)
            {
                if (t.IsKeyword("begin") || t.IsKeyword("try") || t.IsKeyword("case") || t.IsKeyword("asm"))
                {
                    j = _parser.MatchEnd(j) - 1;
                }
                else if (t.IsKeyword("if"))
                {
                    ifs++;
                }
                else if (t.IsKeyword("else"))
                {
                    if (ifs > 0)
                    {
                        ifs--;
                    }
                    else
                    {
                        return j;
                    }
                }
                else if (t.IsSymbol(";") || t.IsKeyword("end") || t.IsKeyword("until") || t.IsKeyword("except") || t.IsKeyword("finally"))
                {
                    return j;
                }
            }
        }

        return limit;
    }

    private const int MaxWithScopes = 32;

    private int WalkWith(int i, int end, ref Scope scope, List<(int End, Scope Saved)> stack)
    {
        var j = i + 1;
        var types = new List<Ty>();
        while (j < end && !T(j).IsKeyword("do"))
        {
            var t = T(j);
            Ty? ty = null;
            if (t.IsIdentifier || t.IsKeyword("string"))
            {
                j = Designator(j, end, scope, out ty);
            }
            else if (t.IsSymbol("("))
            {
                j = WalkParenthesized(j, end, scope, out ty);
            }
            else if (t.IsKeyword("inherited"))
            {
                j = WalkInherited(j, end, scope);
            }
            else
            {
                j++;
            }

            if (ty is not null)
            {
                types.Add(ty);
            }
        }

        if (j >= end)
        {
            return j;
        }

        // Every name looked up inside searches all enclosing with-scopes: in malformed input (no `;`/`end`/`do`)
        // thousands pile up and lookups turn quadratic. Real code never nests this deep, so further ones are ignored.
        var withDepth = 0;
        for (var s = scope; s is not null && withDepth < MaxWithScopes; s = s.Parent)
        {
            if (s is WithScope)
            {
                withDepth++;
            }
        }

        if (withDepth >= MaxWithScopes)
        {
            return j + 1;
        }

        var stmtEnd = StatementEnd(j + 1, end);
        stack.Add((stmtEnd, scope));
        foreach (var ty in types)
        {
            if (withDepth++ < MaxWithScopes)
            {
                scope = new WithScope(ty, _model, scope);
            }
        }

        return j + 1;
    }

    private int ExpressionEnd(int k, int limit, params string[] stopWords)
    {
        var depth = 0;
        for (var j = k; j < limit; j++)
        {
            var t = T(j);
            if (t.IsEof)
            {
                return j;
            }

            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                depth++;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
            {
                depth--;
                if (depth < 0)
                {
                    return j;
                }
            }
            else if (t.IsKeyword("begin") || t.IsKeyword("case") || t.IsKeyword("try"))
            {
                j = _parser.MatchEnd(j) - 1;
            }
            else if (depth == 0 && (t.IsSymbol(";") || t.IsKeyword("end") || stopWords.Any(w => t.IsKeyword(w))))
            {
                return j;
            }
        }

        return limit;
    }

    /// <summary>Walks an initialiser expression; returns the type of the expression when it is a single designator.</summary>
    private Ty? WalkInitializer(int start, int stop, Scope scope)
    {
        Ty? inferred = null;
        if (T(start).IsIdentifier || T(start).IsKeyword("string"))
        {
            var k = Designator(start, stop, scope, out var t);
            if (k >= stop)
            {
                inferred = t;
            }

            Walk(k, stop, scope);
        }
        else if (T(start).IsSymbol("("))
        {
            var k = WalkParenthesized(start, stop, scope, out var t);
            if (k >= stop)
            {
                inferred = t;
            }

            Walk(k, stop, scope);
        }
        else
        {
            Walk(start, stop, scope);
        }

        return inferred;
    }

    private Entity NewLocal(EntityKind kind, int tok, Scope scope, TypeNode? type)
    {
        var owner = LocalsOwner(scope);
        var e = new Entity(kind, T(tok).Text, owner)
        {
            IsLocal = true, Unit = owner.Unit, Package = owner.Package, Source = _src, DefToken = T(tok), TypeSyntax = type, Scope = scope,
        };
        _binder.ByNameToken[(T(tok).File, tok)] = e;
        return e;
    }

    private int WalkInlineDecl(int i, int end, ref Scope scope)
    {
        var isConst = T(i).IsKeyword("const");
        var j = i + 1;
        var names = new List<int>();
        while (T(j).IsIdentifier && j < end)
        {
            names.Add(j);
            j++;
            if (!T(j).IsSymbol(","))
            {
                break;
            }

            j++;
        }

        if (names.Count == 0)
        {
            return i + 1;
        }

        TypeNode? type = null;
        if (T(j).IsSymbol(":"))
        {
            _parser.Pos = j + 1;
            type = _parser.ParseType();
            j = Math.Max(_parser.Pos, j + 1);
            VisitType(type, scope);
        }

        Ty? inferred = null;
        if (T(j).IsSymbol(":=") || T(j).IsSymbol("="))
        {
            var stop = ExpressionEnd(j + 1, end);
            inferred = WalkInitializer(j + 1, stop, scope);
            j = stop;
        }

        var table = new MemberTable();
        foreach (var n in names)
        {
            var e = NewLocal(isConst ? EntityKind.Const : EntityKind.Var, n, scope, type);
            if (type is null && inferred is not null)
            {
                e.TypeCache = inferred;
                e.TypeResolved = true;
            }

            table.Add(e);
            _sink.Add(T(n), e, SymbolRole.Definition);
        }

        scope = new TableScope(table, scope);
        return j;
    }

    private int WalkForVar(int i, int end, ref Scope scope)
    {
        var nameTok = i + 2;
        if (!T(nameTok).IsIdentifier)
        {
            return i + 2;
        }

        var j = nameTok + 1;
        TypeNode? type = null;
        if (T(j).IsSymbol(":"))
        {
            _parser.Pos = j + 1;
            type = _parser.ParseType();
            j = Math.Max(_parser.Pos, j + 1);
            VisitType(type, scope);
        }

        Ty? elementTy = null;
        if (T(j).IsSymbol(":="))
        {
            var stop = ExpressionEnd(j + 1, end, "to", "downto", "do");
            elementTy = WalkInitializer(j + 1, stop, scope);
            j = stop;
        }
        else if (T(j).IsKeyword("in"))
        {
            var stop = ExpressionEnd(j + 1, end, "do");
            var collection = WalkInitializer(j + 1, stop, scope);
            elementTy = _model.EnumeratedType(collection);
            j = stop;
        }

        var e = NewLocal(EntityKind.Var, nameTok, scope, type);
        if (type is null)
        {
            e.TypeCache = elementTy;
            e.TypeResolved = true;
        }

        _sink.Add(T(nameTok), e, SymbolRole.Definition);
        var table = new MemberTable();
        table.Add(e);
        scope = new TableScope(table, scope);
        return j;
    }

    private int WalkOn(int i, int end, ref Scope scope, List<(int End, Scope Saved)> stack)
    {
        // on E: EFoo do <statement>
        var nameTok = i + 1;
        _parser.Pos = i + 3;
        var type = _parser.ParseNamedType();
        var j = Math.Max(_parser.Pos, i + 3);
        VisitType(type, scope);
        var e = NewLocal(EntityKind.Var, nameTok, scope, type);
        _sink.Add(T(nameTok), e, SymbolRole.Definition);
        if (!T(j).IsKeyword("do"))
        {
            return j;
        }

        var stmtEnd = StatementEnd(j + 1, end);
        stack.Add((stmtEnd, scope));
        var table = new MemberTable();
        table.Add(e);
        scope = new TableScope(table, scope);
        return j + 1;
    }

    private int WalkAnonymous(int i, int end, Scope scope)
    {
        var next = T(i + 1);
        if (!(next.IsSymbol("(") || next.IsKeyword("begin") || next.IsKeyword("var") || next.IsKeyword("const") || next.IsSymbol(":") || next.IsKeyword("type")))
        {
            return i + 1;
        }

        _parser.Pos = i;
        var rd = _parser.ParseRoutine(i, [], allowBody: true, inClass: false);
        var stop = Math.Min(Math.Max(_parser.Pos, i + 1), end);
        var owner = LocalsOwner(scope);
        var e = _binder.CreateRoutine(rd, owner, scope, _src);
        e.IsLocal = true;
        foreach (var g in rd.Params)
        {
            foreach (var nt in g.NameToks)
            {
                Def(nt);
            }

            VisitType(g.Type, scope);
        }

        VisitType(rd.Return, scope);
        if (rd.Body is { } body)
        {
            // An anonymous method lies inside the region being walked. In broken code its parse (an unterminated
            // `begin`) runs past it; walking that tail here and again in every enclosing walk multiplied the work.
            body.Locals.RemoveAll(d => d.Start >= end);
            if (body.Block.End > end)
            {
                body.Block = new TokRange(body.Block.Start, Math.Max(body.Block.Start, end));
            }

            VisitBody(rd, body, e, OwnerTypeOf(scope), scope);
        }

        return stop;
    }
}
