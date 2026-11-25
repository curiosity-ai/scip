using ScipSharp.Indexer.Java.Syntax;

namespace ScipSharp.Indexer.Java.Semantics;

using Kind = SymbolInformation.Types.Kind;

public sealed partial class DocumentIndexer
{
    private void VisitBlock(Block b, Scope scope) => VisitBlockBody(b, scope);

    private void VisitBlockBody(Block b, Scope scope)
    {
        foreach (var s in b.Stmts) VisitStmt(s, scope);
    }

    private void VisitStmt(Stmt stmt, Scope scope)
    {
        switch (stmt)
        {
            case Block b:
                VisitBlockBody(b, scope.Child());
                break;
            case VarDeclStmt v:
                VisitVarDecl(v, scope, Kind.Variable);
                break;
            case ExprStmt e:
                Eval(e.E, scope);
                break;
            case SimpleStmt s:
                if (s.E is not null) Eval(s.E, scope);
                break;
            case IfStmt i:
                Eval(i.Cond, scope);
                VisitStmt(i.Then, scope.Child());
                if (i.Else is not null) VisitStmt(i.Else, scope.Child());
                break;
            case WhileStmt w:
                Eval(w.Cond, scope);
                VisitStmt(w.Body, scope.Child());
                break;
            case ForStmt f:
            {
                var fs = scope.Child();
                foreach (var init in f.Init) VisitStmt(init, fs);
                if (f.Cond is not null) Eval(f.Cond, fs);
                foreach (var u in f.Update) Eval(u, fs);
                VisitStmt(f.Body, fs.Child());
                break;
            }
            case ForEachStmt fe:
            {
                var it = Eval(fe.Iterable, scope);
                var fs = scope.Child();
                var d = fe.Var.Vars[0];
                var declared = ResolveDeclType(fe.Var, scope);
                var type = declared ?? ElementType(it, scope);
                DefineLocal(d, type, fs, Kind.Variable, null, null, fe.Var.Mods.Flags, fe.Var.Start, d.End, mods: fe.Var.Mods);
                EmitAnnotations(fe.Var.Mods, scope);
                VisitStmt(fe.Body, fs.Child());
                break;
            }
            case ReturnStmt r:
                if (r.E is not null)
                {
                    var rt = Eval(r.E, scope);
                    if (_returns.Count > 0) _returns.Peek().Add(rt);
                }
                break;
            case ThrowStmt t:
                Eval(t.E, scope);
                break;
            case TryStmt t:
            {
                var ts = scope.Child();
                foreach (var res in t.Resources) { _resource = true; VisitStmt(res, ts); _resource = false; }
                VisitBlockBody(t.Body, ts.Child());
                foreach (var c in t.Catches)
                {
                    var cs = scope.Child();
                    JType? first = null;
                    foreach (var ty in c.Types) first ??= ws.ResolveType(ty, scope, this);
                    foreach (var ty in c.Types.Skip(1)) ws.ResolveType(ty, scope, this);
                    DefineLocal(c, first ?? JType.Unknown, cs, Kind.UnspecifiedKind, null, null, c.Mods.Flags, c.Start, c.NameStart + c.Name.Length, mods: c.Mods);
                    VisitBlockBody(c.Body, cs);
                }
                if (t.Finally is not null) VisitBlockBody(t.Finally, scope.Child());
                break;
            }
            case SwitchStmt sw:
                VisitSwitch(sw, scope);
                break;
            case SyncStmt sy:
                Eval(sy.Lock, scope);
                VisitBlockBody(sy.Body, scope.Child());
                break;
            case LabeledStmt l:
                VisitStmt(l.Body, scope);
                break;
            case LocalClassStmt lc:
            {
                var ti = ws.DeclareLocal(lc.Decl, scope, null);
                VisitType(ti, lc.Decl, scope);
                break;
            }
        }
    }

    private JType? ResolveDeclType(VarDeclStmt v, Scope scope)
    {
        if (v.Type is null) return null;
        if (v.Type.Segs.Count == 1 && v.Type.Segs[0].Name == "var" && v.Type.Dims == 0 && v.Type.Segs[0].Args.Count == 0 && !v.Type.IsPrimitive)
        {
            var (ti, _) = ws.LookupTypeName("var", scope);
            if (ti is null) return null;
        }
        return ws.ResolveType(v.Type, scope, this);
    }

    private void VisitVarDecl(VarDeclStmt v, Scope scope, Kind kind)
    {
        EmitAnnotations(v.Mods, scope);
        var declared = ResolveDeclType(v, scope);
        foreach (var d in v.Vars)
        {
            var type = declared ?? JType.Unknown;
            for (var i = 0; i < d.ExtraDims; i++) type = new ArrayT(type);
            var enclStart = v.Start;
            var enclEnd = v.Vars.Count == 1 ? v.End : d.End;
            if (declared is null && d.Init is not null) type = Eval(d.Init, scope);
            var lv = DefineLocal(d, type, scope, kind, null, null, v.Mods.Flags, enclStart, enclEnd, mods: v.Mods, term: _resource);
            if (declared is not null && d.Init is not null) Eval(d.Init, scope, type);
            _ = lv;
        }
    }

    private JType ElementType(JType iterable, Scope scope)
    {
        if (iterable is ArrayT a) return a.Elem;
        foreach (var (t, map) in ws.Walk(iterable, scope))
        {
            if (t.Name == "java/lang/Iterable" && map.TryGetValue("T", out var e)) return e;
        }
        return JType.Unknown;
    }

    private void VisitSwitch(SwitchStmt sw, Scope scope)
    {
        var selType = Eval(sw.Selector, scope);
        var selInfo = ws.Load(selType);
        var shared = scope.Child();
        foreach (var c in sw.Cases)
        {
            var cs = shared.Child();
            foreach (var label in c.Labels)
            {
                if (label is NameExpr ne && selInfo is { Kind: TypeKind.Enum })
                {
                    var f = selInfo.Fields.FirstOrDefault(x => x.Name == ne.Name);
                    if (f is not null) { Reference(_names.Field(f), ne.Start, ne.End); continue; }
                }
                Eval(label, shared);
            }
            foreach (var p in c.Patterns) DeclarePattern(p, cs);
            if (c.Guard is not null) Eval(c.Guard, cs);
            foreach (var s in c.Body) VisitStmt(s, cs);
        }
    }

    private void DeclarePattern(Pattern p, Scope scope)
    {
        EmitAnnotations(p.Mods, scope);
        var t = ws.ResolveType(p.Type, scope, this);
        if (p.Sub is not null) foreach (var sp in p.Sub) DeclarePattern(sp, scope);
        if (p.Name is not null)
        {
            if (p.Type.Segs.Count == 1 && p.Type.Segs[0].Name == "var") t = JType.Unknown;
            DefineLocal(p, t, scope, Kind.UnspecifiedKind, null, null, p.Mods.Flags, p.Start, p.NameStart + p.Name.Length, mods: p.Mods, term: true);
        }
    }

    private bool _resource;
}
