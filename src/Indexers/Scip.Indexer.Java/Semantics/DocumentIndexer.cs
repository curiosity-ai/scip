using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Semantics;

using Kind = SymbolInformation.Types.Kind;

/// <summary>
/// Produces the SCIP <see cref="Document"/> for one Java source file: definitions, references, enclosing ranges,
/// symbol information, signatures, documentation and override/implementation relationships.
/// This replaces the upstream javac plugin (<c>ScipVisitor</c>) with a native parser and symbol resolver.
/// </summary>
public sealed partial class DocumentIndexer(Workspace ws, JavaSourceFile file) : IRefSink
{
    private readonly SymbolNamer _names = new();
    private readonly string _src = file.Unit.Source;
    private readonly LineMap _lines = new(file.Unit.Source);
    private readonly Dictionary<(int, int, string, int), Occurrence> _occurrences = [];
    private readonly Dictionary<string, SymbolInformation> _infos = new(StringComparer.Ordinal);
    private readonly List<string> _infoOrder = [];

    public Document Index()
    {
        EmitImports();
        foreach (var d in file.Unit.Types) VisitType(file.Declared[d], d, file.Scope);
        var doc = new Document
        {
            Language = "java",
            RelativePath = file.RelativePath,
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
        };
        doc.Occurrences.AddRange(_occurrences.OrderBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item2).Select(kv => kv.Value));
        foreach (var s in _infoOrder) doc.Symbols.Add(_infos[s]);
        return doc;
    }

    // ------------------------------------------------------------------ occurrence plumbing

    private void SetRange(int start, int end, out int line, out int startCol, out int endLine, out int endCol)
    {
        (line, startCol) = _lines.Position(start);
        (endLine, endCol) = _lines.Position(end);
    }

    private void Emit(string symbol, int start, int end, int roles, (int Start, int End)? enclosing)
    {
        if (symbol.Length == 0 || end <= start) return;
        SetRange(start, end, out var l1, out var c1, out var l2, out var c2);
        var key = (l1, c1, symbol, roles);
        if (_occurrences.ContainsKey(key)) return;
        var occ = new Occurrence { Symbol = symbol, SymbolRoles = roles };
        if (l1 == l2) occ.SingleLineRange = new SingleLineRange { Line = l1, StartCharacter = c1, EndCharacter = c2 };
        else occ.MultiLineRange = new MultiLineRange { StartLine = l1, StartCharacter = c1, EndLine = l2, EndCharacter = c2 };
        if (enclosing is { } e && e.End > e.Start)
        {
            SetRange(e.Start, e.End, out var el1, out var ec1, out var el2, out var ec2);
            if (el1 == el2) occ.SingleLineEnclosingRange = new SingleLineRange { Line = el1, StartCharacter = ec1, EndCharacter = ec2 };
            else occ.MultiLineEnclosingRange = new MultiLineRange { StartLine = el1, StartCharacter = ec1, EndLine = el2, EndCharacter = ec2 };
        }
        _occurrences[key] = occ;
    }

    private void Reference(string symbol, int start, int end) => Emit(symbol, start, end, 0, null);

    private void Definition(string symbol, int start, int end, (int, int)? enclosing) => Emit(symbol, start, end, (int)SymbolRole.Definition, enclosing);

    private SymbolInformation AddInfo(string symbol, string displayName, Kind kind, string? signature, string? doc, string? enclosingSymbol = null)
    {
        if (_infos.TryGetValue(symbol, out var existing)) return existing;
        var info = new SymbolInformation { Symbol = symbol, DisplayName = displayName };
        if (signature is { Length: > 0 }) info.SignatureDocumentation = new Signature { Language = "java", Text = signature };
        if (!string.IsNullOrEmpty(doc)) info.Documentation.Add(doc);
        _ = enclosingSymbol; // upstream snapshots carry no enclosing_symbol
        if (kind != Kind.UnspecifiedKind) info.Kind = kind;
        _infos[symbol] = info;
        _infoOrder.Add(symbol);
        return info;
    }

    void IRefSink.TypeRef(TypeInfo type, int start, int end) => Reference(_names.Type(type), start, end);

    void IRefSink.PackageRef(string package, int start, int end) => Reference(_names.Package(package), start, end);

    void IRefSink.TypeVarRef(string name, Scope declaringScope, int start, int end)
    {
        var sym = declaringScope.Method is { Decl: not null } m ? _names.TypeParameter(m, name)
            : declaringScope.Type is { } t ? _names.TypeParameter(t, name) : "";
        Reference(sym, start, end);
    }

    void IRefSink.AnnotationRef(Annotation annotation, Scope scope) => EmitAnnotation(annotation, scope);

    private (int, int) TypeParamRange(TypeParam tp) => tp.Annotations.Count > 0 ? (tp.Start, tp.End) : (tp.NameStart, tp.NameStart + tp.Name.Length);

    // ------------------------------------------------------------------ imports

    private void EmitImports()
    {
        foreach (var imp in file.Unit.Imports)
        {
            var parts = imp.Parts.Select(p => p.Name).ToList();
            var typeParts = imp.IsStatic && !imp.Wildcard ? parts.Take(parts.Count - 1).ToList() : parts;
            var type = ws.LoadQualified(typeParts);
            var pkgCount = 0;
            if (type is not null)
            {
                var top = type;
                while (top.Outer is not null) top = top.Outer;
                pkgCount = top.Package.Length == 0 ? 0 : top.Package.Split('/').Length;
            }
            else if (imp.Wildcard)
            {
                pkgCount = parts.Count;
            }
            for (var i = 0; i < pkgCount && i < imp.Parts.Count; i++)
            {
                var pkg = string.Join('/', parts.Take(i + 1));
                if (ws.Classes.PackageExists(pkg)) Reference(_names.Package(pkg), imp.Parts[i].Start, imp.Parts[i].End);
            }
            if (type is null) continue;
            // references to the class chain
            var chain = new List<TypeInfo>();
            for (var t = type; t is not null; t = t.Outer) chain.Insert(0, t);
            for (var i = 0; i < chain.Count && pkgCount + i < imp.Parts.Count; i++)
            {
                var p = imp.Parts[pkgCount + i];
                Reference(_names.Type(chain[i]), p.Start, p.End);
            }
            if (imp.IsStatic && !imp.Wildcard)
            {
                var last = imp.Parts[^1];
                var name = last.Name;
                var walkType = new ClassT(type.Name, []);
                foreach (var (m, _) in ws.FindMethods(walkType, name, null)) Reference(_names.Method(m), last.Start, last.End);
                if (ws.FindField(walkType, name, null) is { } f) Reference(_names.Field(f.Field), last.Start, last.End);
                else if (ws.FindMemberType(type, name) is { } mt) Reference(_names.Type(mt), last.Start, last.End);
            }
        }
    }

    // ------------------------------------------------------------------ declarations

    private void VisitType(TypeInfo ti, TypeDecl d, Scope parent)
    {
        ti.Complete();
        var scope = ws.TypeScopes[ti];
        var anon = ti.IsAnonymous;
        var typeSymbol = _names.Type(ti);
        if (!anon)
        {
            EmitAnnotations(d.Mods, scope);
            Definition(typeSymbol, d.NameStart, d.NameStart + d.Name.Length, (d.Start, d.End));
            var info = AddInfo(typeSymbol, d.Name, d.Kind switch
            {
                TypeKind.Enum => Kind.Enum,
                TypeKind.Class => Kind.Class,
                TypeKind.Interface or TypeKind.Annotation => Kind.Interface,
                _ => Kind.UnspecifiedKind,
            }, FormatClass(ti, d), d.Doc, EnclosingSymbolOf(ti));
            foreach (var parent2 in ParentTypes(ti)) info.Relationships.Add(new Relationship { Symbol = _names.Type(parent2), IsImplementation = true });
        }
        for (var i = 0; i < d.TypeParams.Count; i++)
        {
            var tp = d.TypeParams[i];
            var sym = _names.TypeParameter(ti, tp.Name);
            foreach (var a in tp.Annotations) EmitAnnotation(a, scope);
            Definition(sym, TypeParamRange(tp).Item1, TypeParamRange(tp).Item2, (d.Start, d.End));
            AddInfo(sym, tp.Name, Kind.TypeParameter, FormatTypeParam(tp, scope), null, typeSymbol);
            foreach (var b in tp.Bounds) ws.ResolveType(b, scope, this);
        }
        if (!anon)
        {
            foreach (var e in d.Extends) ws.ResolveType(e, scope, this);
            foreach (var e in d.Implements) ws.ResolveType(e, scope, this);
        }
        // implicit default constructor is defined at the class name
        if (!anon && ti.Methods.FirstOrDefault(m => m.Decl is null && m.IsCtor) is { } ctor && d.Kind != TypeKind.Record)
        {
            var cs = _names.Method(ctor);
            Definition(cs, d.NameStart, d.NameStart + d.Name.Length, null);
            AddInfo(cs, "<init>", Kind.Constructor, FormatMethod(ctor, null, scope), null, typeSymbol);
        }
        var ordinal = 0;
        foreach (var rc in d.RecordComponents)
        {
            var f = ti.Fields.First(x => x.Decl == rc);
            ws.ResolveType(rc.Type!, scope, this);
            var fs = _names.Field(f);
            Definition(fs, rc.NameStart, rc.NameStart + rc.Name.Length, (rc.Start, rc.End));
            AddInfo(fs, rc.Name, Kind.UnspecifiedKind, FormatField(f, rc.Mods, scope), null, typeSymbol);
        }
        foreach (var ec in d.EnumConstants)
        {
            VisitEnumConstant(ti, ec, ordinal++, scope);
        }
        VisitMembers(ti, d.Members, scope);
    }

    private string? EnclosingSymbolOf(TypeInfo ti) => ti.Outer is null ? null : (ti.IsLocal || ti.IsAnonymous ? _names.Type(ti.Outer) : null);

    private void VisitMembers(TypeInfo ti, List<Member> members, Scope scope)
    {
        foreach (var m in members)
        {
            switch (m)
            {
                case TypeDecl nd:
                {
                    var nt = ws.Classes.Load(ti.Name + "$" + nd.Name) ?? throw new InvalidOperationException(nd.Name);
                    VisitType(nt, nd, scope);
                    break;
                }
                case FieldDecl f: VisitField(ti, f, scope); break;
                case MethodDecl md: VisitMethod(ti, md, scope); break;
                case InitBlock ib: VisitBlock(ib.Body, scope.Child()); break;
            }
        }
    }

    private void VisitEnumConstant(TypeInfo ti, EnumConstant ec, int ordinal, Scope scope)
    {
        var f = ti.Fields.First(x => x.Decl == ec);
        var fs = _names.Field(f);
        EmitAnnotations(ec.Mods, scope);
        Definition(fs, ec.NameStart, ec.NameStart + ec.Name.Length, (ec.Start, ec.End));
        var argTypes = ec.Args.Select(a => Eval(a, scope)).ToList();
        var args = string.Join(", ", ec.Args.Select(a => _src[a.Start..a.End]));
        var display = ec.Args.Count > 0 ? $"{ec.Name}({args})" : ec.Name;
        var sig = $"{ti.Simple}.{ec.Name}{(ec.Args.Count > 0 ? $"({args})" : "")} /* ordinal {ordinal} */";
        AddInfo(fs, display, Kind.UnspecifiedKind, sig, ec.Doc, null);
        var ctors = ti.Methods.Where(m => m.IsCtor).Select(m => (m, new Dictionary<string, JType>())).ToList();
        var best = ChooseMethod(ctors, argTypes);
        if (best is not null) Reference(_names.Method(best.Value.Method), ec.NameStart, ec.NameStart + ec.Name.Length);
        if (ec.Body is not null)
        {
            var decl = new TypeDecl { Kind = TypeKind.Class, IsLocalOrAnon = true, Start = ec.Start, End = ec.End };
            decl.Members.AddRange(ec.Body);
            var anon = ws.DeclareLocal(decl, scope, new ClassT(ti.Name, []));
            anon.Complete();
            VisitMembers(anon, decl.Members, ws.TypeScopes[anon]);
        }
    }

    private void VisitField(TypeInfo ti, FieldDecl d, Scope scope)
    {
        var f = ti.Fields.First(x => x.Decl == d);
        EmitAnnotations(d.Mods, scope);
        ws.ResolveType(d.Type, scope, this);
        var fs = _names.Field(f);
        var staticScope = scope.Child();
        Definition(fs, d.NameStart, d.NameStart + d.Name.Length, (d.Start, d.End));
        AddInfo(fs, d.Name, f.IsStatic ? Kind.StaticField : Kind.Field, FormatField(f, d.Mods, scope), d.Doc, _names.Type(ti));
        if (d.Init is not null) Eval(d.Init, staticScope, f.Type);
    }

    private void VisitMethod(TypeInfo ti, MethodDecl d, Scope typeScope)
    {
        var m = ti.Methods.First(x => x.Decl == d);
        var scope = typeScope.ChildFor(m);
        foreach (var tp in d.TypeParams) scope.TypeVars[tp.Name] = new VarT(tp.Name);
        var ms = _names.Method(m);
        EmitAnnotations(d.Mods, scope);
        var defKind = m.IsCtor ? Kind.Constructor : m.IsStatic ? Kind.StaticMethod : (m.Flags & Mod.Abstract) != 0 && (m.Flags & Mod.Default) == 0 ? Kind.AbstractMethod : Kind.Method;
        var skipDef = m.IsCtor && ti.IsAnonymous;
        if (!skipDef)
        {
            Definition(ms, d.NameStart, d.NameStart + d.Name.Length, (d.Start, d.End));
            var info = AddInfo(ms, m.Name, defKind, FormatMethod(m, d, scope), d.Doc, ti.IsLocal || ti.IsAnonymous ? _names.Type(ti) : null);
            if (!m.IsCtor && !m.IsStatic)
            {
                foreach (var ov in OverriddenMethods(m, scope))
                {
                    info.Relationships.Add(new Relationship { Symbol = _names.Method(ov), IsImplementation = true, IsReference = true });
                }
            }
        }
        foreach (var tp in d.TypeParams)
        {
            var sym = _names.TypeParameter(m, tp.Name);
            foreach (var a in tp.Annotations) EmitAnnotation(a, scope);
            Definition(sym, TypeParamRange(tp).Item1, TypeParamRange(tp).Item2, (d.Start, d.End));
            AddInfo(sym, tp.Name, Kind.TypeParameter, FormatTypeParam(tp, scope), null, ms);
            foreach (var b in tp.Bounds) ws.ResolveType(b, scope, this);
        }
        if (d.Ret is not null) ws.ResolveType(d.Ret, scope, this);
        if (d.CompactCtor)
        {
            foreach (var rc in ti.Decl!.RecordComponents)
            {
                var lv = new LocalVar { Name = rc.Name, Type = ws.ResolveType(rc.Type!, scope, null), OwnerMethod = m };
                scope.Vars[rc.Name] = lv;
            }
        }
        for (var i = 0; i < d.Params.Count; i++)
        {
            var p = d.Params[i];
            EmitAnnotations(p.Mods, scope);
            ws.ResolveType(p.Type!, scope, this);
            DefineLocal(p, m.Params[i], scope, Kind.UnspecifiedKind, m, null, p.Mods.Flags, p.Start, p.End, mods: p.Mods);
        }
        foreach (var t in d.Throws) ws.ResolveType(t, scope, this);
        if (d.DefaultValue is not null) Eval(d.DefaultValue, scope);
        if (d.Body is not null)
        {
            _returns.Push([]);
            VisitBlockBody(d.Body, scope);
            _returns.Pop();
        }
    }

    private readonly Stack<List<JType>> _returns = new();

    private LocalVar DefineLocal(Node decl, JType type, Scope scope, Kind kind, MethodInfo? method, TypeInfo? owner, Mod flags, int encStart, int encEnd, string? nameOverride = null, int nameStartOverride = -1, Modifiers? mods = null)
    {
        string name;
        int nameStart;
        switch (decl)
        {
            case Param p: name = p.Name; nameStart = p.NameStart; break;
            case Declarator d: name = d.Name; nameStart = d.NameStart; break;
            case CatchClause c: name = c.Name; nameStart = c.NameStart; break;
            case Pattern pt: name = pt.Name!; nameStart = pt.NameStart; break;
            default: throw new InvalidOperationException();
        }
        if (nameOverride is not null) { name = nameOverride; nameStart = nameStartOverride; }
        var v = new LocalVar { Name = name, Type = type, Flags = flags, Decl = decl, Mods = mods, OwnerMethod = method, OwnerType = owner ?? scope.EnclosingType };
        scope.Vars[name] = v;
        var sym = _names.Variable(v);
        Definition(sym, nameStart, nameStart + name.Length, (encStart, encEnd));
        var enclosing = method is not null ? _names.Method(method) : owner is not null ? _names.Type(owner) : scope.EnclosingMethod is { } em ? _names.Method(em) : scope.EnclosingType is { } et ? _names.Type(et) : null;
        AddInfo(sym, name, kind, type.IsKnown ? FormatVar(v) : null, null, enclosing);
        return v;
    }

    private void EmitAnnotations(Modifiers mods, Scope scope)
    {
        foreach (var a in mods.Annotations) EmitAnnotation(a, scope);
    }

    private void EmitAnnotation(Annotation a, Scope scope)
    {
        var t = ws.ResolveType(a.Name, scope, this);
        var ti = ws.Load(t);
        foreach (var (name, nameStart, value) in a.Args)
        {
            if (name is not null && ti is not null)
            {
                var m = ti.Methods.FirstOrDefault(x => x.Name == name);
                if (m is not null) Reference(_names.Method(m), nameStart, nameStart + name.Length);
            }
            else if (name is null && ti is not null)
            {
                var m = ti.Methods.FirstOrDefault(x => x.Name == "value");
                _ = m;
            }
            Eval(value, scope);
        }
    }

    // ------------------------------------------------------------------ hierarchy relationships

    private List<TypeInfo> ParentTypes(TypeInfo ti)
    {
        var result = new List<TypeInfo>();
        var seen = new HashSet<TypeInfo>();
        void Collect(TypeInfo t)
        {
            foreach (var sup in ws.SuperTypes(t))
            {
                var st = ws.Load(sup);
                if (st is null || !seen.Add(st)) continue;
                if (st.Name != "java/lang/Object") result.Add(st);
                Collect(st);
            }
        }
        Collect(ti);
        return result;
    }

    private List<MethodInfo> OverriddenMethods(MethodInfo m, Scope scope)
    {
        var result = new List<MethodInfo>();
        var seen = new HashSet<TypeInfo>();
        var self = ws.Erasure(JType.Unknown);
        _ = self;
        void Walk(TypeInfo owner, Dictionary<string, JType> mapOfOwner)
        {
            foreach (var supT in ws.SuperTypes(owner))
            {
                var sup = Workspace.Subst(supT, mapOfOwner);
                var st = ws.Load(sup);
                if (st is null || !seen.Add(st)) continue;
                var map = new Dictionary<string, JType>();
                if (sup is ClassT c) for (var i = 0; i < st.TypeParams.Count && i < c.Args.Count; i++) map[st.TypeParams[i]] = Workspace.Upper(c.Args[i]);
                var found = false;
                foreach (var cand in st.Methods)
                {
                    if (cand.Name != m.Name || cand.IsStatic || cand.Params.Count != m.Params.Count || (cand.Flags & Mod.Private) != 0) continue;
                    var ok = true;
                    for (var i = 0; i < cand.Params.Count && ok; i++)
                    {
                        ok = ws.Erasure(Workspace.Subst(cand.Params[i], map)) == ws.Erasure(m.Params[i]) ||
                             (Workspace.Subst(cand.Params[i], map) is VarT && m.Params[i] is not PrimT);
                    }
                    if (!ok) continue;
                    result.Add(cand);
                    found = true;
                    Walk(st, map);
                }
                if (!found) Walk(st, map);
            }
        }
        Walk(m.Owner, new Dictionary<string, JType>());
        return result;
    }
}
