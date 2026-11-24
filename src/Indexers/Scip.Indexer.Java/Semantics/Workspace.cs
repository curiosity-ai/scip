using Scip.Indexer.Java.Classpath;
using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Semantics;

public sealed class LocalVar
{
    public required string Name { get; init; }
    public JType Type { get; set; } = JType.Unknown;
    public Mod Flags { get; set; }
    public Modifiers? Mods { get; set; }
    public Node? Decl { get; set; }
    public MethodInfo? OwnerMethod { get; set; }
    public TypeInfo? OwnerType { get; set; }

    /// <summary>Resource and pattern-binding variables are not javac <c>LOCAL_VARIABLE</c>s, so semanticdb-javac gives them global <c>method().name.</c> symbols.</summary>
    public MethodInfo? TermOwner { get; set; }
}

/// <summary>Lexical scope: locals, type variables and local classes, chained to enclosing scopes up to the file.</summary>
public sealed class Scope
{
    public Scope? Parent { get; init; }
    public JavaSourceFile? File { get; init; }
    public TypeInfo? Type { get; init; }
    public MethodInfo? Method { get; init; }
    public bool IsStatic { get; init; }
    public Dictionary<string, LocalVar> Vars { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, JType> TypeVars { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, TypeInfo> LocalTypes { get; } = new(StringComparer.Ordinal);

    public JavaSourceFile CurrentFile
    {
        get
        {
            for (var s = this; s is not null; s = s.Parent)
            {
                if (s.File is not null) return s.File;
            }
            throw new InvalidOperationException("scope without file");
        }
    }

    public TypeInfo? EnclosingType
    {
        get
        {
            for (var s = this; s is not null; s = s.Parent)
            {
                if (s.Type is not null) return s.Type;
            }
            return null;
        }
    }

    public MethodInfo? EnclosingMethod
    {
        get
        {
            for (var s = this; s is not null; s = s.Parent)
            {
                if (s.Method is not null) return s.Method;
                if (s.Type is not null) return null;
            }
            return null;
        }
    }

    public Scope Child() => new() { Parent = this, IsStatic = IsStatic };

    public Scope ChildFor(TypeInfo type, bool isStatic = false) => new() { Parent = this, Type = type, IsStatic = isStatic };

    public Scope ChildFor(MethodInfo method) => new() { Parent = this, Method = method, IsStatic = method.IsStatic };
}

/// <summary>A parsed source file with its declared types and import tables.</summary>
public sealed class JavaSourceFile
{
    public required string RelativePath { get; init; }
    public required CompilationUnit Unit { get; init; }
    public string Package { get; set; } = "";
    public JavaPackage Origin { get; set; } = JavaPackage.Empty;

    /// <summary>The <c>--release</c> this file is compiled with, if configured.</summary>
    public int? Release { get; set; }
    public List<TypeInfo> Types { get; } = [];
    public Dictionary<TypeDecl, TypeInfo> Declared { get; } = [];
    public Scope Scope { get; set; } = null!;
}

public sealed record Ref(string Symbol, int Start, int End);

/// <summary>Receives references discovered while resolving types (so the same code serves signature building and indexing).</summary>
public interface IRefSink
{
    void TypeRef(TypeInfo type, int start, int end);

    void PackageRef(string package, int start, int end);

    void TypeVarRef(string name, Scope declaringScope, int start, int end);

    void AnnotationRef(Annotation annotation, Scope scope);
}

/// <summary>Project-wide semantic services: declared types, class path, hierarchy walking and name lookup.</summary>
public sealed class Workspace : IDisposable
{
    private int _localCounter;

    public ClassPath Classes { get; } = new();
    public List<JavaSourceFile> Files { get; } = [];
    public Dictionary<TypeInfo, Scope> TypeScopes { get; } = [];

    public JavaSourceFile AddFile(string relativePath, string text, JavaPackage? origin = null, int? release = null)
    {
        var cu = Parser.ParseFile(text);
        var file = new JavaSourceFile { RelativePath = relativePath, Origin = origin ?? JavaPackage.Empty, Release = release, Unit = cu, Package = string.Join('/', cu.Package.Select(p => p.Name)) };
        file.Scope = new Scope { File = file };
        foreach (var d in cu.Types) file.Types.Add(Declare(d, null, file));
        Files.Add(file);
        return file;
    }

    // ------------------------------------------------------------------ declaring source types

    private TypeInfo Declare(TypeDecl d, TypeInfo? outer, JavaSourceFile file)
    {
        var name = outer is null ? (file.Package.Length > 0 ? file.Package + "/" + d.Name : d.Name) : outer.Name + "$" + d.Name;
        var t = new TypeInfo { Name = name, Simple = d.Name, Outer = outer, Kind = d.Kind, Decl = d, Flags = ImpliedTypeFlags(d, outer), Origin = file.Origin };
        t.TypeParams = d.TypeParams.Select(p => p.Name).ToList();
        file.Declared[d] = t;
        foreach (var m in d.Members.OfType<TypeDecl>()) t.MemberTypes[m.Name] = t.Name + "$" + m.Name;
        Classes.AddSource(t);
        var parentScope = outer is null ? file.Scope : TypeScopes[outer];
        TypeScopes[t] = ScopeOf(t, parentScope);
        t.SetCompleter(x => CompleteSource(x, d));
        foreach (var m in d.Members.OfType<TypeDecl>()) Declare(m, t, file);
        return t;
    }

    private static Scope ScopeOf(TypeInfo t, Scope parent)
    {
        var s = parent.ChildFor(t);
        foreach (var tp in t.TypeParams) s.TypeVars[tp] = new VarT(tp);
        return s;
    }

    private static Mod ImpliedTypeFlags(TypeDecl d, TypeInfo? outer)
    {
        var f = d.Mods.Flags;
        if (outer is not null && (outer.IsInterface || d.Kind is TypeKind.Enum or TypeKind.Record or TypeKind.Interface or TypeKind.Annotation)) f |= Mod.Static;
        if (outer is not null && outer.IsInterface) f |= Mod.Public;
        if (d.Kind is TypeKind.Record or TypeKind.Enum) f |= Mod.Final;
        return f;
    }

    /// <summary>Creates a local or anonymous class (declared inside a method body) and registers it.</summary>
    public TypeInfo DeclareLocal(TypeDecl d, Scope scope, JType? superType)
    {
        var outer = scope.EnclosingType;
        var id = ++_localCounter;
        var anon = d.Name.Length == 0;
        var t = new TypeInfo
        {
            Name = (outer?.Name ?? "") + "$" + id + d.Name,
            Simple = d.Name,
            Kind = d.Kind,
            Decl = d,
            Outer = outer,
            IsLocal = !anon,
            LocalOwner = anon ? null : scope.EnclosingMethod,
            IsAnonymous = anon,
            Flags = d.Mods.Flags,
        };
        t.TypeParams = d.TypeParams.Select(p => p.Name).ToList();
        foreach (var m in d.Members.OfType<TypeDecl>()) t.MemberTypes[m.Name] = t.Name + "$" + m.Name;
        var ts = ScopeOf(t, scope);
        TypeScopes[t] = ts;
        Classes.AddSource(t);
        if (superType is not null)
        {
            // anonymous: super is the instantiated type (class) or Object + that interface
            var st = superType is ClassT c ? Classes.Load(c.Name) : null;
            if (st is not null && st.IsInterface) { t.Super = JType.Object; t.Interfaces = [superType]; }
            else t.Super = superType;
            t.SetCompleter(x => CompleteSource(x, d, skipSupers: true));
        }
        else t.SetCompleter(x => CompleteSource(x, d));
        scope.LocalTypes[d.Name.Length == 0 ? "$anon" + id : d.Name] = t;
        foreach (var m in d.Members.OfType<TypeDecl>())
        {
            var nested = DeclareNestedOfLocal(m, t);
            _ = nested;
        }
        return t;
    }

    private TypeInfo DeclareNestedOfLocal(TypeDecl d, TypeInfo outer)
    {
        var t = new TypeInfo { Name = outer.Name + "$" + d.Name, Simple = d.Name, Outer = outer, Kind = d.Kind, Decl = d, IsLocal = outer.IsLocal || outer.IsAnonymous };
        t.TypeParams = d.TypeParams.Select(p => p.Name).ToList();
        foreach (var m in d.Members.OfType<TypeDecl>()) t.MemberTypes[m.Name] = t.Name + "$" + m.Name;
        TypeScopes[t] = ScopeOf(t, TypeScopes[outer]);
        Classes.AddSource(t);
        t.SetCompleter(x => CompleteSource(x, d));
        foreach (var m in d.Members.OfType<TypeDecl>()) DeclareNestedOfLocal(m, t);
        return t;
    }

    private void CompleteSource(TypeInfo t, TypeDecl d, bool skipSupers = false)
    {
        var scope = TypeScopes[t];
        if (!skipSupers)
        {
            foreach (var tp in d.TypeParams)
            {
                t.TypeParamBounds.Add(tp.Bounds.Count > 0 ? ResolveType(tp.Bounds[0], scope, null) : JType.Object);
            }
            switch (d.Kind)
            {
                case TypeKind.Class:
                    t.Super = d.Extends.Count > 0 ? ResolveType(d.Extends[0], scope, null) : JType.Object;
                    break;
                case TypeKind.Enum:
                    t.Super = new ClassT("java/lang/Enum", [new ClassT(t.Name, [])]);
                    break;
                case TypeKind.Record:
                    t.Super = new ClassT("java/lang/Record", []);
                    break;
                case TypeKind.Annotation:
                    t.Interfaces.Add(new ClassT("java/lang/annotation/Annotation", []));
                    break;
            }
            foreach (var i in d.Implements) t.Interfaces.Add(ResolveType(i, scope, null));
        }
        else
        {
            foreach (var tp in d.TypeParams) t.TypeParamBounds.Add(JType.Object);
        }

        foreach (var ec in d.EnumConstants)
        {
            t.Fields.Add(new FieldInfo
            {
                Name = ec.Name, Owner = t, Decl = ec, IsEnumConstant = true, Type = new ClassT(t.Name, []),
                Flags = Mod.Public | Mod.Static | Mod.Final,
            });
        }
        foreach (var rc in d.RecordComponents)
        {
            var rt = ResolveType(rc.Type!, scope, null);
            t.Fields.Add(new FieldInfo { Name = rc.Name, Owner = t, Decl = rc, Type = rc.Varargs ? new ArrayT(rt) : rt, Flags = Mod.Private | Mod.Final });
        }
        var hasCtor = false;
        foreach (var m in d.Members)
        {
            switch (m)
            {
                case FieldDecl f:
                {
                    var ft = ResolveType(f.Type, scope, null);
                    for (var i = 0; i < f.ExtraDims; i++) ft = new ArrayT(ft);
                    var flags = f.Mods.Flags;
                    if (t.IsInterface) flags |= Mod.Public | Mod.Static | Mod.Final;
                    t.Fields.Add(new FieldInfo { Name = f.Name, Owner = t, Decl = f, Type = ft, Flags = flags });
                    break;
                }
                case MethodDecl md:
                {
                    var ms = scope.ChildFor(new MethodInfo { Name = "$" });
                    foreach (var tp in md.TypeParams) ms.TypeVars[tp.Name] = new VarT(tp.Name);
                    var mi = new MethodInfo
                    {
                        Name = md.IsCtor ? "<init>" : md.Name, Owner = t, Decl = md,
                        TypeParams = md.TypeParams.Select(p => p.Name).ToList(),
                        Flags = md.Mods.Flags,
                    };
                    if (md.CompactCtor)
                    {
                        mi.Params = t.Decl!.RecordComponents.Select(p => ResolveType(p.Type!, scope, null)).ToList();
                        mi.ParamNames = t.Decl.RecordComponents.Select(p => p.Name).ToList();
                    }
                    else
                    {
                        foreach (var p in md.Params)
                        {
                            var pt = ResolveType(p.Type!, ms, null);
                            mi.Params.Add(p.Varargs ? new ArrayT(pt) : pt);
                            mi.ParamNames.Add(p.Name);
                            mi.Varargs |= p.Varargs;
                        }
                    }
                    mi.TypeParamBounds = md.TypeParams.Select(tp => tp.Bounds.Count > 0 ? ResolveType(tp.Bounds[0], ms, null) : JType.Object).ToList();
                    mi.Return = md.Ret is null ? JType.Void : ResolveType(md.Ret, ms, null);
                    mi.Throws = md.Throws.Select(x => ResolveType(x, ms, null)).ToList();
                    if (t.IsInterface)
                    {
                        if ((mi.Flags & Mod.Private) == 0) mi.Flags |= Mod.Public;
                        if (md.Body is null && (mi.Flags & (Mod.Static | Mod.Default | Mod.Private)) == 0) mi.Flags |= Mod.Abstract;
                    }
                    hasCtor |= md.IsCtor;
                    t.Methods.Add(mi);
                    break;
                }
            }
        }
        if (d.Kind == TypeKind.Record)
        {
            if (!t.Methods.Any(m => m.IsCtor && (m.Decl?.CompactCtor == true || m.Params.Count == d.RecordComponents.Count)))
            {
                t.Methods.Add(new MethodInfo
                {
                    Name = "<init>", Owner = t, Flags = t.Flags & (Mod.Public | Mod.Protected | Mod.Private),
                    Params = t.Fields.Where(f => !f.IsStatic).Select(f => f.Type).ToList(),
                    ParamNames = d.RecordComponents.Select(p => p.Name).ToList(),
                });
            }
            foreach (var rc in d.RecordComponents.Where(rc => !t.Methods.Any(m => m.Name == rc.Name && m.Params.Count == 0)))
            {
                t.Methods.Add(new MethodInfo { Name = rc.Name, Owner = t, Flags = Mod.Public, Return = t.Fields.First(f => f.Name == rc.Name).Type });
            }
        }
        else if (!hasCtor && d.Kind is TypeKind.Class or TypeKind.Enum && !t.IsAnonymous)
        {
            t.Methods.Insert(0, new MethodInfo
            {
                Name = "<init>", Owner = t,
                Flags = d.Kind == TypeKind.Enum ? Mod.Private : t.Flags & (Mod.Public | Mod.Protected | Mod.Private),
            });
        }
        if (d.Kind == TypeKind.Enum)
        {
            foreach (var ctor in t.Methods.Where(m => m.IsCtor)) ctor.Flags = (ctor.Flags & ~(Mod.Public | Mod.Protected)) | Mod.Private;
            t.Methods.Add(new MethodInfo { Name = "values", Owner = t, Flags = Mod.Public | Mod.Static, Return = new ArrayT(new ClassT(t.Name, [])) });
            t.Methods.Add(new MethodInfo
            {
                Name = "valueOf", Owner = t, Flags = Mod.Public | Mod.Static, Params = [JType.String], ParamNames = ["name"], Return = new ClassT(t.Name, []),
            });
        }
    }

    // ------------------------------------------------------------------ type lookup

    public TypeInfo? Load(string internalName)
    {
        var t = Classes.Load(internalName);
        t?.Complete();
        return t;
    }

    public TypeInfo? Load(JType t) => t is ClassT c ? Load(c.Name) : null;

    /// <summary>Finds a type by qualified parts, trying every package/class split.</summary>
    public TypeInfo? LoadQualified(IReadOnlyList<string> parts)
    {
        for (var k = 0; k < parts.Count; k++)
        {
            var pkg = string.Join('/', parts.Take(k));
            var name = (k == 0 ? "" : pkg + "/") + parts[k];
            var t = Load(name);
            if (t is null) continue;
            for (var i = k + 1; t is not null && i < parts.Count; i++) t = FindMemberType(t, parts[i]);
            if (t is not null) return t;
        }
        return null;
    }

    public TypeInfo? FindMemberType(TypeInfo t, string name)
    {
        var seen = new HashSet<TypeInfo>();
        return FindMemberType(t, name, seen);
    }

    private TypeInfo? FindMemberType(TypeInfo t, string name, HashSet<TypeInfo> seen)
    {
        if (!seen.Add(t)) return null;
        if (t.MemberTypes.TryGetValue(name, out var n) && Load(n) is { } found) return found;
        t.Complete();
        foreach (var sup in SuperTypes(t))
        {
            if (Load(sup) is { } st && FindMemberType(st, name, seen) is { } r) return r;
        }
        return null;
    }

    public IEnumerable<JType> SuperTypes(TypeInfo t)
    {
        t.Complete();
        if (t.Super is not null) yield return t.Super;
        foreach (var i in t.Interfaces) yield return i;
    }

    public (TypeInfo? Type, JType? Var) LookupTypeName(string name, Scope scope)
    {
        var file = scope.CurrentFile;
        for (var s = scope; s is not null; s = s.Parent)
        {
            if (s.TypeVars.TryGetValue(name, out var tv)) return (null, tv);
            if (s.LocalTypes.TryGetValue(name, out var lt)) return (lt, null);
            if (s.Type is { } ct)
            {
                if (ct.Simple == name && !ct.IsAnonymous) return (ct, null);
                if (FindMemberType(ct, name) is { } mt) return (mt, null);
            }
        }
        var cu = file.Unit;
        foreach (var d in file.Types) if (d.Simple == name) return (d, null);
        foreach (var imp in cu.Imports.Where(i => !i.IsStatic && !i.Wildcard))
        {
            if (imp.Parts[^1].Name != name) continue;
            if (LoadQualified(imp.Parts.Select(p => p.Name).ToList()) is { } t) return (t, null);
        }
        var samePkg = Load(file.Package.Length > 0 ? file.Package + "/" + name : name);
        if (samePkg is not null) return (samePkg, null);
        foreach (var imp in cu.Imports.Where(i => i.Wildcard))
        {
            var parts = imp.Parts.Select(p => p.Name).ToList();
            if (imp.IsStatic)
            {
                if (LoadQualified(parts) is { } owner && FindMemberType(owner, name) is { } m) return (m, null);
                continue;
            }
            var t = Load(string.Join('/', parts) + "/" + name);
            if (t is null && LoadQualified(parts) is { } outerType) t = FindMemberType(outerType, name);
            if (t is not null) return (t, null);
        }
        if (Load("java/lang/" + name) is { } jl) return (jl, null);
        return (null, null);
    }

    /// <summary>Resolves a source type reference; reports every identifier it crosses to <paramref name="sink"/>.</summary>
    public JType ResolveType(TypeRef r, Scope scope, IRefSink? sink)
    {
        foreach (var a in r.Annotations) sink?.AnnotationRef(a, scope);
        if (r.Wild != WildKind.None)
        {
            return new WildT(r.Wild, r.Bound is null ? null : ResolveType(r.Bound, scope, sink));
        }
        foreach (var extra in r.Intersection) ResolveType(extra, scope, sink);
        JType result;
        if (r.IsPrimitive) result = r.Segs[0].Name == "void" ? JType.Void : new PrimT(r.Segs[0].Name);
        else
        {
            result = JType.Unknown;
            TypeInfo? cur = null;
            var pkgParts = new List<string>();
            var resolved = false;
            for (var i = 0; i < r.Segs.Count; i++)
            {
                var seg = r.Segs[i];
                if (i == 0)
                {
                    var (ti, v) = LookupTypeName(seg.Name, scope);
                    if (v is not null && r.Segs.Count == 1)
                    {
                        result = v;
                        for (var ts = scope; ts is not null; ts = ts.Parent)
                        {
                            if (!ts.TypeVars.ContainsKey(seg.Name)) continue;
                            sink?.TypeVarRef(seg.Name, ts, seg.Start, seg.End);
                            break;
                        }
                        resolved = true;
                        break;
                    }
                    if (ti is not null) { cur = ti; sink?.TypeRef(ti, seg.Start, seg.End); }
                    else pkgParts.Add(seg.Name);
                }
                else if (cur is not null)
                {
                    var m = FindMemberType(cur, seg.Name);
                    cur = m;
                    if (m is not null) sink?.TypeRef(m, seg.Start, seg.End);
                }
                else
                {
                    var pkg = string.Join('/', pkgParts);
                    var t = Load(pkg + "/" + seg.Name);
                    if (t is not null)
                    {
                        cur = t;
                        if (sink is not null)
                        {
                            // every segment of a qualified package name references its own package prefix
                            for (var k = 0; k < pkgParts.Count && k < r.Segs.Count; k++)
                            {
                                var prefix = string.Join('/', pkgParts.Take(k + 1));
                                if (Classes.PackageExists(prefix)) sink.PackageRef(prefix, r.Segs[k].Start, r.Segs[k].End);
                            }
                        }
                        sink?.TypeRef(t, seg.Start, seg.End);
                    }
                    else
                    {
                        if (i - 1 >= 0 && Classes.PackageExists(pkg)) sink?.PackageRef(pkg, r.Segs[i - 1].Start, r.Segs[i - 1].End);
                        pkgParts.Add(seg.Name);
                    }
                }
                if (cur is null && i == r.Segs.Count - 1) break;
            }
            if (!resolved)
            {
                if (cur is not null)
                {
                    var last = r.Segs[^1];
                    var args = last.Args.Select(a => ResolveType(a, scope, sink)).ToList();
                    result = new ClassT(cur.Name, args);
                }
                else foreach (var a in r.Segs[^1].Args) ResolveType(a, scope, sink);
                foreach (var seg in r.Segs.Take(r.Segs.Count - 1)) foreach (var a in seg.Args) ResolveType(a, scope, sink);
            }
        }
        for (var i = 0; i < r.Dims; i++) result = new ArrayT(result);
        return result;
    }

    // ------------------------------------------------------------------ hierarchy

    public static JType Subst(JType t, IReadOnlyDictionary<string, JType>? map)
    {
        if (map is null || map.Count == 0) return t;
        switch (t)
        {
            case VarT v: return map.TryGetValue(v.Name, out var r) ? r : t;
            case ArrayT a: return new ArrayT(Subst(a.Elem, map));
            case ClassT c when c.Args.Count > 0: return new ClassT(c.Name, c.Args.Select(x => Subst(x, map)).ToList());
            case WildT w when w.Bound is not null: return new WildT(w.Kind, Subst(w.Bound, map));
            default: return t;
        }
    }

    public static JType Upper(JType t) => t switch
    {
        WildT { Bound: { } b } => b,
        WildT => JType.Object,
        _ => t,
    };

    /// <summary>All bounds of a type variable (<c>T extends A &amp; B</c>), innermost declaration first.</summary>
    public List<JType> BoundsOf(VarT v, Scope? scope)
    {
        for (var s = scope; s is not null; s = s.Parent)
        {
            if (s.Method?.Decl is { } md && md.TypeParams.FirstOrDefault(p => p.Name == v.Name) is { } tp)
            {
                return tp.Bounds.Count > 0 ? tp.Bounds.Select(b => ResolveType(b, s, null)).ToList() : [JType.Object];
            }
            if (s.Type is { Decl: { } td } && td.TypeParams.FirstOrDefault(p => p.Name == v.Name) is { } ctp)
            {
                return ctp.Bounds.Count > 0 ? ctp.Bounds.Select(b => ResolveType(b, TypeScopes[s.Type], null)).ToList() : [JType.Object];
            }
        }
        return [BoundOf(v, scope)];
    }

    public JType BoundOf(VarT v, Scope? scope)
    {
        for (var s = scope; s is not null; s = s.Parent)
        {
            if (s.Type is { } t)
            {
                var i = t.TypeParams.IndexOf(v.Name);
                if (i >= 0) { t.Complete(); if (i < t.TypeParamBounds.Count) return t.TypeParamBounds[i]; }
            }
            if (s.Method?.Decl is { } md)
            {
                var tp = md.TypeParams.FirstOrDefault(p => p.Name == v.Name);
                if (tp is not null) return tp.Bounds.Count > 0 ? ResolveType(tp.Bounds[0], s, null) : JType.Object;
            }
        }
        return JType.Object;
    }

    /// <summary>The type and all its supertypes (breadth first) with type arguments substituted.</summary>
    public IEnumerable<(TypeInfo Type, Dictionary<string, JType> Subst)> Walk(JType recv, Scope? scope)
    {
        recv = Upper(recv);
        var queue = new Queue<(JType, Dictionary<string, JType>)>();
        if (recv is VarT vt)
        {
            foreach (var b in BoundsOf(vt, scope)) queue.Enqueue((Upper(b), []));
        }
        else if (recv is ArrayT) queue.Enqueue((JType.Object, []));
        else if (recv is ClassT) queue.Enqueue((recv, []));
        else yield break;
        var seen = new HashSet<string>();
        var sawObject = false;
        while (queue.Count > 0)
        {
            var (jt, _) = queue.Dequeue();
            if (jt is not ClassT c || !seen.Add(c.Name)) continue;
            var ti = Load(c.Name);
            if (ti is null) continue;
            var map = new Dictionary<string, JType>();
            for (var i = 0; i < ti.TypeParams.Count && i < c.Args.Count; i++) map[ti.TypeParams[i]] = Upper(c.Args[i]);
            yield return (ti, map);
            if (c.Name == "java/lang/Object") sawObject = true;
            foreach (var sup in SuperTypes(ti)) queue.Enqueue((Subst(sup, map), map));
        }
        if (!sawObject && Load("java/lang/Object") is { } obj) yield return (obj, []);
    }

    public (FieldInfo Field, Dictionary<string, JType> Subst)? FindField(JType recv, string name, Scope? scope)
    {
        foreach (var (t, map) in Walk(recv, scope))
        {
            var f = t.Fields.FirstOrDefault(x => x.Name == name);
            if (f is not null) return (f, map);
        }
        return null;
    }

    public List<(MethodInfo Method, Dictionary<string, JType> Subst)> FindMethods(JType recv, string name, Scope? scope)
    {
        var result = new List<(MethodInfo, Dictionary<string, JType>)>();
        var sigs = new HashSet<string>();
        foreach (var (t, map) in Walk(recv, scope))
        {
            foreach (var m in t.Methods.Where(x => x.Name == name))
            {
                var key = string.Join(",", m.Params.Select(p => Erasure(Subst(p, map))));
                if (sigs.Add(key)) result.Add((m, map));
            }
        }
        return result;
    }

    public string Erasure(JType t) => t switch
    {
        ClassT c => c.Name,
        ArrayT a => Erasure(a.Elem) + "[]",
        PrimT p => p.Name,
        VarT v => "?" + v.Name,
        WildT w => w.Bound is null ? "java/lang/Object" : Erasure(w.Bound),
        _ => "?",
    };

    public void Dispose() => Classes.Dispose();
}
