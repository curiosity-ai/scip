using ScipSharp.Indexer.Java.Syntax;

namespace ScipSharp.Indexer.Java.Semantics;

using Kind = SymbolInformation.Types.Kind;

public sealed partial class DocumentIndexer
{
    private enum RKind { None, Value, Type, Package }

    private readonly record struct Res(RKind Kind, JType Type, TypeInfo? T = null, string? Pkg = null)
    {
        public static readonly Res None = new(RKind.None, JType.Unknown);
    }

    private static readonly string[] Numeric = ["byte", "short", "char", "int", "long", "float", "double"];

    private ClassT ThisType(TypeInfo t) => new(t.Name, t.TypeParams.Select(p => (JType)new VarT(p)).ToList());

    /// <summary>Evaluates <paramref name="e"/>, emitting every reference inside it, and returns its static type.</summary>
    private JType Eval(Expr e, Scope s, JType? expected = null)
    {
        switch (e)
        {
            case Literal l:
                return l.Kind switch
                {
                    Tk.String => JType.String,
                    Tk.Char => new PrimT("char"),
                    Tk.Number => NumberType(l.Text),
                    _ => l.Text == "null" ? JType.Null : new PrimT("boolean"),
                };
            case Paren p: return Eval(p.E, s, expected);
            case NameExpr or FieldAccess:
            {
                var r = EvalAmbiguous(e, s);
                return r.Kind == RKind.Value ? r.Type : JType.Unknown;
            }
            case Call c: return EvalCall(c, s);
            case NewExpr n: return EvalNew(n, s);
            case NewArray na:
            {
                var t = ws.ResolveType(na.Elem, s, this);
                foreach (var d in na.Dims) Eval(d, s);
                var dims = na.Dims.Count + na.ExtraDims;
                var result = t;
                for (var i = 0; i < dims; i++) result = new ArrayT(result);
                if (na.Init is not null) Eval(na.Init, s, result);
                return result;
            }
            case ArrayInit ai:
            {
                var elem = expected is ArrayT at ? at.Elem : null;
                foreach (var item in ai.Items) Eval(item, s, elem);
                return expected ?? JType.Unknown;
            }
            case Binary b:
            {
                var l = Eval(b.L, s);
                var r = Eval(b.R, s);
                return b.Op switch
                {
                    "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||" => new PrimT("boolean"),
                    "+" when IsString(l) || IsString(r) => JType.String,
                    _ => Promote(l, r),
                };
            }
            case Unary u:
            {
                var t = Eval(u.E, s);
                return u.Op == "!" ? new PrimT("boolean") : t;
            }
            case Assign a:
            {
                var l = Eval(a.L, s);
                Eval(a.R, s, l);
                return l;
            }
            case Conditional c:
            {
                Eval(c.Cond, s);
                var a = Eval(c.A, s, expected);
                var b = Eval(c.B, s, expected);
                return a.IsKnown && a is not NullT ? a : b;
            }
            case Cast c:
            {
                var t = ws.ResolveType(c.Type, s, this);
                Eval(c.E, s, t);
                return t;
            }
            case IndexExpr ix:
            {
                var t = Eval(ix.Target, s);
                Eval(ix.Index, s);
                return t is ArrayT a ? a.Elem : JType.Unknown;
            }
            case InstanceOf io:
            {
                Eval(io.E, s);
                if (io.Type is not null) ws.ResolveType(io.Type, s, this);
                if (io.Pattern is not null) DeclarePattern(io.Pattern, s);
                return new PrimT("boolean");
            }
            case Lambda lam: return EvalLambda(lam, s, expected);
            case MethodRef mr: return EvalMethodRef(mr, s, expected);
            case ClassLit cl:
            {
                var t = ws.ResolveType(cl.Type, s, this);
                if (cl.Type.Dims > 0) Reference(_names.SpecialClassLiteral("Array"), cl.End - 5, cl.End);
                else if (t is PrimT prim) Reference(_names.SpecialClassLiteral(prim.Name), cl.End - 5, cl.End);
                else if (ws.Load(t) is { } clsInfo) Reference(_names.ClassLiteral(clsInfo), cl.End - 5, cl.End);
                return new ClassT("java/lang/Class", [t is PrimT ? Box(t) : t]);
            }
            case SwitchExpr se:
                VisitSwitch(se.Switch, s);
                return JType.Unknown;
            case AnnotationExpr ae:
                EmitAnnotation(ae.A, s);
                return JType.Unknown;
        }
        return JType.Unknown;
    }

    private static bool IsString(JType t) => t is ClassT { Name: "java/lang/String" };

    private static JType NumberType(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.StartsWith("0x", StringComparison.Ordinal))
        {
            return t.EndsWith('l') ? new PrimT("long") : new PrimT("int");
        }
        if (t.EndsWith('l')) return new PrimT("long");
        if (t.EndsWith('f')) return new PrimT("float");
        if (t.EndsWith('d') || t.Contains('.') || t.Contains('e')) return new PrimT("double");
        return new PrimT("int");
    }

    private JType Promote(JType a, JType b)
    {
        a = Unbox(a);
        b = Unbox(b);
        if (a is PrimT pa && b is PrimT pb)
        {
            if (pa.Name == "boolean") return pa;
            var ia = Array.IndexOf(Numeric, pa.Name);
            var ib = Array.IndexOf(Numeric, pb.Name);
            return new PrimT(Numeric[Math.Max(Math.Max(ia, ib), 3)]);
        }
        return a is PrimT ? a : b is PrimT ? b : JType.Unknown;
    }

    private static JType Unbox(JType t) => t is ClassT c ? c.Name switch
    {
        "java/lang/Integer" => new PrimT("int"),
        "java/lang/Long" => new PrimT("long"),
        "java/lang/Double" => new PrimT("double"),
        "java/lang/Float" => new PrimT("float"),
        "java/lang/Short" => new PrimT("short"),
        "java/lang/Byte" => new PrimT("byte"),
        "java/lang/Character" => new PrimT("char"),
        "java/lang/Boolean" => new PrimT("boolean"),
        _ => t,
    } : t;

    private static JType Box(JType t) => t is PrimT p ? p.Name switch
    {
        "int" => new ClassT("java/lang/Integer", []),
        "long" => new ClassT("java/lang/Long", []),
        "double" => new ClassT("java/lang/Double", []),
        "float" => new ClassT("java/lang/Float", []),
        "short" => new ClassT("java/lang/Short", []),
        "byte" => new ClassT("java/lang/Byte", []),
        "char" => new ClassT("java/lang/Character", []),
        "boolean" => new ClassT("java/lang/Boolean", []),
        "void" => new ClassT("java/lang/Void", []),
        _ => t,
    } : t;

    // ------------------------------------------------------------------ names and member selection

    private Res EvalAmbiguous(Expr e, Scope s)
    {
        switch (e)
        {
            case Paren p: return new Res(RKind.Value, Eval(p.E, s));
            case NameExpr n: return EvalName(n, s);
            case FieldAccess fa: return EvalFieldAccess(fa, s);
            default: return new Res(RKind.Value, Eval(e, s));
        }
    }

    private Res EvalName(NameExpr n, Scope s)
    {
        var name = n.Name;
        if (name == "this") return s.EnclosingType is { } tt ? new Res(RKind.Value, ThisType(tt)) : Res.None;
        if (name == "super")
        {
            var cur = s.EnclosingType;
            if (cur is null) return Res.None;
            if (_names.ThisSuper(cur, "super") is { Length: > 0 } superSym) Reference(superSym, n.Start, n.End);
            return new Res(RKind.Value, cur.Super ?? JType.Object);
        }
        for (var sc = s; sc is not null; sc = sc.Parent)
        {
            if (sc.Vars.TryGetValue(name, out var v))
            {
                Reference(_names.Variable(v), n.Start, n.End);
                return new Res(RKind.Value, v.Type);
            }
            if (sc.Type is { } t && ws.FindField(ThisType(t), name, sc) is { } f)
            {
                Reference(_names.Field(f.Field), n.Start, n.End);
                return new Res(RKind.Value, Workspace.Subst(f.Field.Type, f.Subst));
            }
        }
        foreach (var imp in file.Unit.Imports.Where(i => i.IsStatic))
        {
            var parts = imp.Parts.Select(p => p.Name).ToList();
            if (!imp.Wildcard)
            {
                if (parts[^1] != name) continue;
                parts.RemoveAt(parts.Count - 1);
            }
            var owner = ws.LoadQualified(parts);
            if (owner is null) continue;
            if (ws.FindField(new ClassT(owner.Name, []), name, s) is { } sf)
            {
                Reference(_names.Field(sf.Field), n.Start, n.End);
                return new Res(RKind.Value, Workspace.Subst(sf.Field.Type, sf.Subst));
            }
        }
        var (ti, tv) = ws.LookupTypeName(name, s);
        if (ti is not null)
        {
            Reference(_names.Type(ti), n.Start, n.End);
            return new Res(RKind.Type, new ClassT(ti.Name, []), ti);
        }
        _ = tv;
        if (ws.Classes.PackageExists(name))
        {
            Reference(_names.Package(name), n.Start, n.End);
            return new Res(RKind.Package, JType.Unknown, null, name);
        }
        return Res.None;
    }

    private Res EvalFieldAccess(FieldAccess fa, Scope s)
    {
        var target = EvalAmbiguous(fa.Target, s);
        var name = fa.Name;
        var end = fa.NameStart + name.Length;
        switch (target.Kind)
        {
            case RKind.Package:
            {
                var full = target.Pkg + "/" + name;
                if (ws.Load(full) is { } t)
                {
                    Reference(_names.Type(t), fa.NameStart, end);
                    return new Res(RKind.Type, new ClassT(t.Name, []), t);
                }
                if (ws.Classes.PackageExists(full))
                {
                    Reference(_names.Package(full), fa.NameStart, end);
                    return new Res(RKind.Package, JType.Unknown, null, full);
                }
                return Res.None;
            }
            case RKind.Type:
            {
                if (name == "this")
                {
                    if (_names.ThisSuper(target.T!, "this") is { Length: > 0 } thisSym) Reference(thisSym, fa.NameStart, end);
                    return new Res(RKind.Value, ThisType(target.T!));
                }
                if (name == "super")
                {
                    if (_names.ThisSuper(target.T!, "super") is { Length: > 0 } superSym) Reference(superSym, fa.NameStart, end);
                    return new Res(RKind.Value, target.T!.Super ?? JType.Object);
                }
                if (name == "class") return new Res(RKind.Value, new ClassT("java/lang/Class", [new ClassT(target.T!.Name, [])]));
                if (ws.FindField(new ClassT(target.T!.Name, []), name, s) is { } f)
                {
                    Reference(_names.Field(f.Field), fa.NameStart, end);
                    return new Res(RKind.Value, Workspace.Subst(f.Field.Type, f.Subst));
                }
                if (ws.FindMemberType(target.T, name) is { } mt)
                {
                    Reference(_names.Type(mt), fa.NameStart, end);
                    return new Res(RKind.Type, new ClassT(mt.Name, []), mt);
                }
                return Res.None;
            }
            case RKind.Value:
            {
                if (target.Type is ArrayT && name == "length")
                {
                    Reference(_names.ArrayMember("length", false), fa.NameStart, end);
                    return new Res(RKind.Value, new PrimT("int"));
                }
                if (ws.FindField(target.Type, name, s) is { } f)
                {
                    Reference(_names.Field(f.Field), fa.NameStart, end);
                    return new Res(RKind.Value, Workspace.Subst(f.Field.Type, f.Subst));
                }
                return Res.None;
            }
        }
        return Res.None;
    }

    // ------------------------------------------------------------------ calls

    private JType EvalCall(Call c, Scope s)
    {
        var explicitTypeArgs = c.TypeArgs.Select(t => ws.ResolveType(t, s, this)).ToList();
        var argTypes = EvalArgs(c.Args, s);
        var nameEnd = c.NameStart + c.Name.Length;
        List<(MethodInfo Method, Dictionary<string, JType> Subst)> candidates = [];
        JType? receiver = null;
        if (c.Target is null)
        {
            if (c.Name is "this" or "super")
            {
                var cur = s.EnclosingType;
                var ctorOwner = c.Name == "this" ? cur : ws.Load(cur?.Super ?? JType.Unknown);
                if (ctorOwner is null) { EvalDeferredArgs(c.Args, s); return JType.Void; }
                var ctors = ctorOwner.Methods.Where(m => m.IsCtor).Select(m => (m, new Dictionary<string, JType>())).ToList();
                var best = ChooseMethod(ctors, argTypes, s);
                if (best is not null)
                {
                    Reference(_names.Method(best.Value.Method), c.NameStart, nameEnd);
                    EvalLambdaArgs(c.Args, best.Value.Method, new Dictionary<string, JType>(), s);
                }
                else EvalDeferredArgs(c.Args, s);
                return JType.Void;
            }
            for (var sc = s; sc is not null && candidates.Count == 0; sc = sc.Parent)
            {
                if (sc.Type is null) continue;
                receiver = ThisType(sc.Type);
                candidates = ws.FindMethods(receiver, c.Name, sc);
            }
            if (candidates.Count == 0)
            {
                foreach (var imp in file.Unit.Imports.Where(i => i.IsStatic))
                {
                    var parts = imp.Parts.Select(p => p.Name).ToList();
                    if (!imp.Wildcard)
                    {
                        if (parts[^1] != c.Name) continue;
                        parts.RemoveAt(parts.Count - 1);
                    }
                    if (ws.LoadQualified(parts) is { } owner) candidates.AddRange(ws.FindMethods(new ClassT(owner.Name, []), c.Name, s).Where(m => m.Method.IsStatic));
                }
            }
        }
        else
        {
            var target = EvalAmbiguous(c.Target, s);
            if (target.Kind == RKind.Value && target.Type is ArrayT && c.Name == "clone" && c.Args.Count == 0)
            {
                Reference(_names.ArrayMember("clone", true), c.NameStart, nameEnd);
                return target.Type;
            }
            if (target.Kind == RKind.Value) { receiver = target.Type; candidates = ws.FindMethods(target.Type, c.Name, s); }
            else if (target.Kind == RKind.Type) { receiver = target.Type; candidates = ws.FindMethods(target.Type, c.Name, s); }
        }
        if (explicitTypeArgs.Count > 0)
        {
            candidates = candidates.Select(cd =>
            {
                if (cd.Method.TypeParams.Count != explicitTypeArgs.Count) return cd;
                var extended = new Dictionary<string, JType>(cd.Subst);
                for (var i = 0; i < explicitTypeArgs.Count; i++) extended[cd.Method.TypeParams[i]] = explicitTypeArgs[i];
                return (cd.Method, extended);
            }).ToList();
        }
        var chosen = ChooseMethod(candidates, argTypes, s);
        if (chosen is null)
        {
            EvalDeferredArgs(c.Args, s);
            return JType.Unknown;
        }
        var (m, map) = chosen.Value;
        Reference(_names.Method(m), c.NameStart, nameEnd);
        var bindings = new Dictionary<string, JType>(map);
        for (var i = 0; i < explicitTypeArgs.Count && i < m.TypeParams.Count; i++) bindings[m.TypeParams[i]] = explicitTypeArgs[i];
        for (var i = 0; i < m.Params.Count && i < argTypes.Count; i++) Bind(Workspace.Subst(m.Params[i], map), argTypes[i], m.TypeParams, bindings);
        EvalLambdaArgs(c.Args, m, bindings, s);
        var ret = Workspace.Subst(m.Return, bindings);
        if (m.Name == "getClass" && m.Params.Count == 0) return new ClassT("java/lang/Class", [JType.Unknown]);
        return ret is VarT v && m.TypeParams.Contains(v.Name) && !bindings.ContainsKey(v.Name) ? JType.Unknown : ret;
    }

    private void Bind(JType param, JType arg, List<string> typeParams, Dictionary<string, JType> map)
    {
        if (!arg.IsKnown || arg is NullT) return;
        switch (param)
        {
            case VarT v when typeParams.Contains(v.Name) && !map.ContainsKey(v.Name):
                map[v.Name] = Box(arg);
                break;
            case ArrayT pa when arg is ArrayT aa:
                Bind(pa.Elem, aa.Elem, typeParams, map);
                break;
            case ClassT pc when arg is ClassT ac && pc.Args.Count > 0:
                if (pc.Name == ac.Name && pc.Args.Count == ac.Args.Count)
                {
                    for (var i = 0; i < pc.Args.Count; i++) Bind(pc.Args[i], ac.Args[i], typeParams, map);
                    break;
                }
                foreach (var (t, sm) in ws.Walk(ac, null))
                {
                    if (t.Name != pc.Name) continue;
                    for (var i = 0; i < pc.Args.Count && i < t.TypeParams.Count; i++)
                    {
                        if (sm.TryGetValue(t.TypeParams[i], out var actual)) Bind(pc.Args[i], actual, typeParams, map);
                    }
                    break;
                }
                break;
            case WildT w when w.Bound is not null:
                Bind(w.Bound, arg, typeParams, map);
                break;
        }
    }

    private bool _boxing;
    private Scope? _callScope;
    private bool _usedVarargs;

    /// <summary>
    /// Overload resolution after JLS 15.12.2: the first of the strict, loose (boxing) and variable-arity phases that has
    /// an applicable method wins, then the most specific applicable method; ties fall back to the lowest conversion cost.
    /// </summary>
    private (MethodInfo Method, Dictionary<string, JType> Subst)? ChooseMethod(
        List<(MethodInfo Method, Dictionary<string, JType> Subst)> candidates, List<JType> args, Scope? scope = null)
    {
        if (candidates.Count == 0) return null;
        _callScope = scope;
        var applicable = new List<(MethodInfo Method, Dictionary<string, JType> Subst, int Cost, int Phase)>();
        foreach (var (m, map) in candidates)
        {
            _boxing = false;
            _usedVarargs = false;
            var cost = ApplicabilityCost(m, map, args);
            if (cost < 0) continue;
            applicable.Add((m, map, cost, _usedVarargs ? 3 : _boxing ? 2 : 1));
        }
        if (applicable.Count > 0)
        {
            var phase = applicable.Min(a => a.Phase);
            var set = applicable.Where(a => a.Phase == phase).ToList();
            if (set.Count == 1) return (set[0].Method, set[0].Subst);
            var specific = set.Where(a => set.All(b => ReferenceEquals(a.Method, b.Method) || MoreSpecific(a.Method, a.Subst, b.Method, b.Subst, args.Count, phase == 3))).ToList();
            var pool = specific.Count > 0 ? specific : set;
            var best = pool.OrderBy(a => a.Cost).First();
            return (best.Method, best.Subst);
        }
        // fall back to arity match, then to the first candidate by name
        foreach (var (m, map) in candidates)
        {
            if (m.Params.Count == args.Count || (m.Varargs && args.Count >= m.Params.Count - 1)) return (m, map);
        }
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private JType ParamAt(MethodInfo m, Dictionary<string, JType> map, int i, bool varargs)
    {
        var n = m.Params.Count;
        var p = varargs && i >= n - 1 ? (m.Params[n - 1] is ArrayT pa ? pa.Elem : JType.Unknown) : m.Params[i];
        return Workspace.Subst(p, map);
    }

    private bool MoreSpecific(MethodInfo a, Dictionary<string, JType> amap, MethodInfo b, Dictionary<string, JType> bmap, int argCount, bool varargs)
    {
        var k = varargs ? Math.Max(argCount, Math.Max(a.Params.Count, b.Params.Count)) : a.Params.Count;
        if (!varargs && a.Params.Count != b.Params.Count) return false;
        for (var i = 0; i < k; i++)
        {
            if (!IsSubtypeForSpecificity(ParamAt(a, amap, i, varargs), a, ParamAt(b, bmap, i, varargs), b)) return false;
        }
        return true;
    }

    private JType ErasedBound(JType t, MethodInfo owner)
    {
        if (t is VarT v)
        {
            var i = owner.TypeParams.IndexOf(v.Name);
            if (i >= 0 && i < owner.TypeParamBounds.Count) return ErasedBound(owner.TypeParamBounds[i], owner);
            var ot = owner.Owner;
            var j = ot.TypeParams.IndexOf(v.Name);
            if (j >= 0 && j < ot.TypeParamBounds.Count && ot.TypeParamBounds[j] is not VarT) return ErasedBound(ot.TypeParamBounds[j], owner);
            return JType.Object;
        }
        return t is WildT w ? (w.Bound is null || w.Kind == WildKind.Super ? JType.Object : ErasedBound(w.Bound, owner)) : t;
    }

    private bool IsSubtypeForSpecificity(JType s, MethodInfo sm, JType t, MethodInfo tm)
    {
        s = ErasedBound(s, sm);
        t = ErasedBound(t, tm);
        if (!s.IsKnown || !t.IsKnown) return true;
        switch (s)
        {
            case PrimT ps:
                if (t is not PrimT pt) return false;
                if (ps.Name == pt.Name) return true;
                var i1 = Array.IndexOf(Numeric, ps.Name);
                var i2 = Array.IndexOf(Numeric, pt.Name);
                if (i1 < 0 || i2 < 0 || pt.Name == "char") return false;
                if (ps.Name == "char") return i2 >= 3;
                return i2 >= i1;
            case ArrayT sa:
                if (t is ArrayT ta) return sa.Elem is PrimT || ta.Elem is PrimT ? Equals(sa.Elem, ta.Elem) : IsSubtypeForSpecificity(sa.Elem, sm, ta.Elem, tm);
                return t is ClassT { Name: "java/lang/Object" or "java/lang/Cloneable" or "java/io/Serializable" };
            case ClassT sc:
                return t is ClassT tc && IsSubclass(sc.Name, tc.Name);
        }
        return false;
    }

    private int ApplicabilityCost(MethodInfo m, Dictionary<string, JType> map, List<JType> args)
    {
        var n = m.Params.Count;
        var varargs = m.Varargs && (args.Count != n || !(args.Count > 0 && args[^1] is ArrayT or NullT));
        if (!varargs && args.Count != n) return -1;
        if (varargs && args.Count < n - 1) return -1;
        _usedVarargs = varargs;
        var total = varargs ? 20 : 0;
        for (var i = 0; i < args.Count; i++)
        {
            var p = ParamAt(m, map, i, varargs);
            var c = AssignCost(args[i], p, m);
            if (c < 0) return -1;
            total += c;
        }
        return total;
    }

    private int AssignCost(JType arg, JType param, MethodInfo m)
    {
        if (!arg.IsKnown || !param.IsKnown) return 2;
        if (arg is VarT av && _callScope is not null && DeclaredTypeVar(av.Name, _callScope)) arg = ErasedBoundInScope(av);
        if (param is VarT pv)
        {
            // a method type variable only accepts arguments within its bound
            var bi = m.TypeParams.IndexOf(pv.Name);
            if (bi >= 0 && bi < m.TypeParamBounds.Count && m.TypeParamBounds[bi] is ClassT { Name: not "java/lang/Object" } bound && arg is not NullT)
            {
                var ac = arg is PrimT ap ? Box(ap) : arg;
                if (arg is PrimT) _boxing = true;
                if (ac is ClassT acc) { if (!IsSubclass(acc.Name, bound.Name)) return -1; }
                else if (ac is ArrayT) return bound.Name is "java/lang/Cloneable" or "java/io/Serializable" ? 2 : -1;
            }
            else if (arg is PrimT) _boxing = true;
            return arg is PrimT ? 3 : 2;
        }
        if (param is WildT)
        {
            if (arg is PrimT) _boxing = true;
            return arg is PrimT ? 3 : 2;
        }
        switch (arg)
        {
            case NullT: return param is PrimT ? -1 : 1;
            case PrimT pa:
            {
                if (param is PrimT pp)
                {
                    if (pa.Name == pp.Name) return 0;
                    var ia = Array.IndexOf(Numeric, pa.Name);
                    var ip = Array.IndexOf(Numeric, pp.Name);
                    if (ia < 0 || ip < 0 || pp.Name == "char") return -1;
                    if (pa.Name == "char") return ip >= 3 ? 1 + (ip - 3) : -1;
                    return ip > ia ? 1 + (ip - ia) : -1;
                }
                if (param is ClassT pc && Box(pa) is ClassT bc && IsSubclass(bc.Name, pc.Name))
                {
                    _boxing = true;
                    return 10;
                }
                return -1;
            }
            case ClassT ca:
            {
                if (param is PrimT pp2)
                {
                    if (Unbox(ca) is PrimT up && (up.Name == pp2.Name || IsWidening(up.Name, pp2.Name))) { _boxing = true; return 10; }
                    return -1;
                }
                if (param is ClassT cp) return IsSubclass(ca.Name, cp.Name) ? (ca.Name == cp.Name ? 0 : 1) : -1;
                return -1;
            }
            case ArrayT aa:
                if (param is ArrayT pa2) return AssignCost(aa.Elem, pa2.Elem, m) >= 0 && (aa.Elem is PrimT == pa2.Elem is PrimT) ? 0 : -1;
                return param is ClassT { Name: "java/lang/Object" or "java/lang/Cloneable" or "java/io/Serializable" } ? 2 : -1;
            case VarT:
                return 2;
        }
        return 2;
    }

    private static bool DeclaredTypeVar(string name, Scope scope)
    {
        for (var sc = scope; sc is not null; sc = sc.Parent) if (sc.TypeVars.ContainsKey(name)) return true;
        return false;
    }

    private JType ErasedBoundInScope(VarT v)
    {
        var bound = ws.BoundsOf(v, _callScope).FirstOrDefault() ?? JType.Object;
        return bound is VarT inner && inner.Name != v.Name ? JType.Object : bound is VarT ? JType.Object : bound;
    }

    private static bool IsWidening(string from, string to)
    {
        var ia = Array.IndexOf(Numeric, from);
        var ip = Array.IndexOf(Numeric, to);
        if (ia < 0 || ip < 0 || to == "char") return false;
        if (from == "char") return ip >= 3;
        return ip > ia;
    }

    private bool IsSubclass(string sub, string sup)
    {
        if (sub == sup || sup == "java/lang/Object") return true;
        foreach (var (t, _) in ws.Walk(new ClassT(sub, []), null))
        {
            if (t.Name == sup) return true;
        }
        // an unloadable type is assumed compatible
        return ws.Load(sub) is null;
    }

    // ------------------------------------------------------------------ new / lambda / method refs

    private sealed class SuppressTypeSink(IRefSink inner, int suppressStart) : IRefSink
    {
        public void TypeRef(TypeInfo type, int start, int end)
        {
            if (start != suppressStart) inner.TypeRef(type, start, end);
        }

        public void PackageRef(string package, int start, int end) => inner.PackageRef(package, start, end);

        public void TypeVarRef(string name, Scope declaringScope, int start, int end) => inner.TypeVarRef(name, declaringScope, start, end);

        public void AnnotationRef(Annotation annotation, Scope scope) => inner.AnnotationRef(annotation, scope);
    }

    private JType EvalNew(NewExpr n, Scope s)
    {
        if (n.Outer is not null) Eval(n.Outer, s);
        var argTypes = EvalArgs(n.Args, s);
        var last = n.Type.Segs[^1];
        if (n.Body is null)
        {
            var t = ws.ResolveType(n.Type, s, new SuppressTypeSink(this, last.Start));
            var ti = ws.Load(t);
            if (ti is not null)
            {
                var ctors = ti.Methods.Where(m => m.IsCtor).Select(m => (m, ConstructorSubst(ti, t))).ToList();
                var best = ChooseMethod(ctors, argTypes, s);
                if (best is not null)
                {
                    Reference(_names.Method(best.Value.Method), last.Start, last.End);
                    var b = new Dictionary<string, JType>(best.Value.Subst);
                    for (var i = 0; i < best.Value.Method.Params.Count && i < argTypes.Count; i++) Bind(Workspace.Subst(best.Value.Method.Params[i], b), argTypes[i], ti.TypeParams, b);
                    EvalLambdaArgs(n.Args, best.Value.Method, b, s);
                }
                else EvalDeferredArgs(n.Args, s);
                // an annotated type (`new @A Foo()`) keeps its own type reference
                if (n.Type.Annotations.Count > 0) Reference(_names.Type(ti), last.Start, last.End);
            }
            else EvalDeferredArgs(n.Args, s);
            return t;
        }
        var type = ws.ResolveType(n.Type, s, this);
        EvalDeferredArgs(n.Args, s);
        var anon = ws.DeclareLocal(n.Body, s, type);
        anon.Complete();
        VisitMembers(anon, n.Body.Members, ws.TypeScopes[anon]);
        return new ClassT(anon.Name, []);
    }

    private static Dictionary<string, JType> ConstructorSubst(TypeInfo ti, JType t)
    {
        var map = new Dictionary<string, JType>();
        if (t is ClassT c) for (var i = 0; i < ti.TypeParams.Count && i < c.Args.Count; i++) map[ti.TypeParams[i]] = c.Args[i];
        return map;
    }

    private List<JType> EvalArgs(List<Expr> args, Scope s) => args.Select(a => a is Lambda or MethodRef ? JType.Unknown : Eval(a, s)).ToList();

    /// <summary>Evaluates the lambda / method reference arguments of a call whose target method could not be resolved.</summary>
    private void EvalDeferredArgs(List<Expr> args, Scope s)
    {
        foreach (var a in args) if (a is Lambda or MethodRef) Eval(a, s);
    }

    /// <summary>Evaluates lambda arguments once the target method is known, so parameter types can be inferred from it.</summary>
    private void EvalLambdaArgs(List<Expr> args, MethodInfo m, Dictionary<string, JType> bindings, Scope s)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is not (Lambda or MethodRef)) continue;
            JType? p = i < m.Params.Count ? m.Params[i] : m.Params.Count > 0 ? m.Params[^1] : null;
            if (m.Varargs && i >= m.Params.Count - 1 && p is ArrayT pa) p = pa.Elem;
            var expected = p is null ? null : Workspace.Subst(p, bindings);
            var result = Eval(args[i], s, expected);
            if (expected is not null && FunctionalSig(expected, s) is { } sig && result.IsKnown) Bind(sig.Return, result, m.TypeParams, bindings);
        }
    }

    private (List<JType> Params, JType Return)? FunctionalSig(JType t, Scope s)
    {
        if (t is not ClassT fi) return null;
        var info = ws.Load(fi.Name);
        if (info is null || !info.IsInterface) return null;
        foreach (var (ti, map0) in ws.Walk(fi, s))
        {
            var map = new Dictionary<string, JType>();
            if (ti == info) for (var i = 0; i < ti.TypeParams.Count && i < fi.Args.Count; i++) map[ti.TypeParams[i]] = Workspace.Upper(fi.Args[i]);
            else map = map0;
            var sam = ti.Methods.FirstOrDefault(x => (x.Flags & Mod.Abstract) != 0 && !x.IsStatic && (x.Flags & Mod.Default) == 0 && x.Name is not ("equals" or "hashCode" or "toString"));
            if (sam is null) continue;
            return (sam.Params.Select(p => Workspace.Subst(p, map)).ToList(), Workspace.Subst(sam.Return, map));
        }
        return null;
    }

    private JType EvalLambda(Lambda lam, Scope s, JType? expected = null)
    {
        var ls = s.Child();
        var sig = expected is null ? null : FunctionalSig(expected, s);
        for (var i = 0; i < lam.Params.Count; i++)
        {
            var p = lam.Params[i];
            JType type = p.Type is null ? JType.Unknown : ws.ResolveType(p.Type, s, this);
            if (p.Type is { Segs.Count: 1 } && p.Type.Segs[0].Name == "var") type = JType.Unknown;
            if (!type.IsKnown && sig is { } sg && i < sg.Params.Count) type = sg.Params[i] is VarT ? JType.Unknown : sg.Params[i];
            DefineLocal(p, type, ls, Kind.UnspecifiedKind, null, null, p.Mods.Flags, p.Start, p.End, mods: p.Mods);
        }
        if (lam.BodyExpr is not null) return Eval(lam.BodyExpr, ls);
        _returns.Push([]);
        if (lam.BodyBlock is not null) VisitBlockBody(lam.BodyBlock, ls);
        var rets = _returns.Pop();
        return rets.FirstOrDefault(r => r.IsKnown && r is not NullT) ?? JType.Unknown;
    }

    private JType EvalMethodRef(MethodRef mr, Scope s, JType? expected = null)
    {
        JType recv;
        var viaType = mr.TargetType is not null;
        if (mr.TargetType is not null) recv = ws.ResolveType(mr.TargetType, s, this);
        else
        {
            var r = EvalAmbiguous(mr.Target!, s);
            recv = r.Kind is RKind.Value or RKind.Type ? r.Type : JType.Unknown;
            viaType = r.Kind == RKind.Type;
        }
        if (!recv.IsKnown) return JType.Unknown;
        var end = mr.NameStart + mr.Name.Length;
        var sig = expected is null ? null : FunctionalSig(expected, s);
        if (mr.Name == "new" && recv is ArrayT)
        {
            Reference(_names.ArrayConstructor(), mr.Start, end);
            return recv;
        }
        if (mr.Name == "new")
        {
            var ti = ws.Load(recv);
            if (ti is null) return JType.Unknown;
            var ctors = ti.Methods.Where(m => m.IsCtor).Select(m => (m, ConstructorSubst(ti, recv))).ToList();
            var ctor = sig is not null ? ChooseMethod(ctors, sig.Value.Params, s)?.Method : null;
            ctor ??= ctors.Count > 0 ? ctors[0].m : null;
            // javac positions a constructor reference on the whole `Type::new`
            if (ctor is not null) Reference(_names.Method(ctor), mr.Start, end);
            return new ClassT(ti.Name, []);
        }
        var methods = ws.FindMethods(recv, mr.Name, s);
        if (methods.Count == 0) return JType.Unknown;
        (MethodInfo Method, Dictionary<string, JType> Subst)? pick = null;
        if (sig is not null)
        {
            var sp = sig.Value.Params;
            if (viaType)
            {
                pick = ChooseMethod(methods.Where(m => m.Method.IsStatic).ToList(), sp, s);
                if (pick is null && sp.Count > 0) pick = ChooseMethod(methods.Where(m => !m.Method.IsStatic).ToList(), sp.Skip(1).ToList(), s);
            }
            else pick = ChooseMethod(methods, sp, s);
        }
        pick ??= methods[0];
        Reference(_names.Method(pick.Value.Method), mr.NameStart, end);
        return JType.Unknown;
    }
}
