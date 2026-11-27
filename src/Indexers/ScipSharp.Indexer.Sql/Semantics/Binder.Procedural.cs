using ScipSharp.Indexer.Sql.Syntax;
using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Sql.Semantics;

internal sealed partial class Binder
{
    private void BindDeclare(DeclareStmt decl, bool packageLevel)
    {
        foreach (var v in decl.Vars)
        {
            var saved = _stmt;
            _stmt = v;
            BindVarDecl(v, packageLevel && _package is not null);
            _stmt = saved;
        }

        foreach (var m in decl.Members)
        {
            var saved = _stmt;
            _stmt = m;
            if (m is CreateRoutineStmt r)
            {
                BindCreateRoutine(r, local: !packageLevel);
            }
            else
            {
                BindStmt(m);
            }

            _stmt = saved;
        }
    }

    private void BindVarDecl(VarDecl v, bool packageMember)
    {
        string sym;
        DbObject? member = null;
        var header = "DECLARE " + v.Name.Text + (v.TypeText.Length > 0 ? " " + v.TypeText : "");
        if (packageMember)
        {
            var pkg = _package!;
            SymbolDescriptor[] descriptors = [.. pkg.Descriptors, v.TypeText == "type" ? SymbolNames.Type(v.Name.Key) : SymbolNames.Term(v.Name.Key)];
            sym = Sym(descriptors);
            member = new DbObject(ObjectKind.Unknown, pkg.DbKey, pkg.SchemaKey, v.Name.Key, v.Name.Text, descriptors, sym) { Package = pkg.Symbol };
            _cat.Add(member);
            _ever.Add(member);
            Def(v.Name.Token, sym, v.TypeText == "type" ? Kind.Type : v.IsException ? Kind.Error : Kind.Variable, v.Name.Text, StmtDocs(header), Range(v));
        }
        else
        {
            sym = NewLocal();
            Def(v.Name.Token, sym, Kind.Variable, v.Name.Text, StmtDocs(header), Range(v));
        }

        var info = new VarInfo(sym);
        if (v.TableColumns.Count > 0)
        {
            info.Fields = [];
            foreach (var col in v.TableColumns)
            {
                var csym = member is not null ? Sym(member, SymbolNames.Term(col.Name.Key)) : NewLocal();
                Def(col.Name.Token, csym, Kind.Field, col.Name.Text, [Code(StmtText(col))]);
                info.Fields.Add(new OutCol(col.Name.Key, col.Name.Text, csym));
                if (col.TypeName is { } ctn)
                {
                    ResolveTypeRef(ctn);
                }

                if (member is not null)
                {
                    member.AddColumn(new ColumnInfo(col.Name.Key, col.Name.Text, csym));
                }
            }
        }

        if (v.Cursor is { } q)
        {
            var cursorCtx = new RoutineContext(_ctx);
            foreach (var p in v.CursorParams)
            {
                if (p.Name is { } pn)
                {
                    var psym = NewLocal();
                    Def(pn.Token, psym, Kind.Parameter, pn.Text, [Code(pn.Text + " " + p.TypeText)]);
                    cursorCtx.Vars[pn.Key] = new VarInfo(psym);
                }
            }

            var saved = _ctx;
            _ctx = cursorCtx;
            var shape = BindQuery(q, new Scope(null), null);
            _ctx = saved;
            info.Fields = shape.Columns;
        }

        if (v.Anchor is { } anchor)
        {
            var rowType = v.TypeText.EndsWith("%ROWTYPE", StringComparison.OrdinalIgnoreCase);
            if (anchor.Parts.Count == 1 && FindVar(anchor.Last.Key) is { } other)
            {
                Ref(anchor.Last.Token, other.Symbol, Kind.Variable);
                info.Fields = other.Fields;
                info.Row = other.Row;
            }
            else if (rowType)
            {
                var obj = ResolveRelationRef(anchor);
                info.Row = obj;
            }
            else
            {
                ResolveName(anchor, new Scope(null));
            }
        }

        if (v.TypeName is { } tn)
        {
            if (tn.Parts.Count == 1 && FindVar(tn.Last.Key) is { } typeVar)
            {
                Ref(tn.Last.Token, typeVar.Symbol, Kind.Type);
                info.Fields ??= typeVar.Fields;
            }
            else if (FindRelation(tn) is { Kind: ObjectKind.Table or ObjectKind.Type or ObjectKind.View } rel)
            {
                ResolveRelationRef(tn, Kind.Type);
                info.Row = rel;
            }
            else if (_package is not null && Lookup(ObjectKind.Table, null, null, tn.Last.Key, null, _package.Symbol) is { } pkgType)
            {
                Ref(tn.Last.Token, pkgType.Symbol, Kind.Type, pkgType.Name);
                info.Fields ??= [.. pkgType.Columns.Select(c => new OutCol(c.Key, c.Name, c.Symbol))];
            }
            else
            {
                ResolveTypeRef(tn);
            }
        }

        if (v.Default is { } def)
        {
            BindExpr(def, new Scope(null));
        }

        if (!packageMember)
        {
            DeclareVar(_ctx, v.Name, info);
        }
    }

    private void BindForLoop(ForLoopStmt loop)
    {
        var sym = NewLocal();
        Def(loop.Variable.Token, sym, Kind.Variable, loop.Variable.Text, [Code("FOR " + loop.Variable.Text + " IN ...")]);
        var info = new VarInfo(sym);
        if (loop.Query is { } q)
        {
            info.Fields = BindQuery(q, new Scope(null), null).Columns;
        }
        else if (loop.Range is { } range)
        {
            if (range.Items is [NameRef { Name.Parts.Count: 1 } n] && FindVar(n.Name.Last.Key) is { } cursor)
            {
                info.Fields = cursor.Fields;
                info.Row = cursor.Row;
            }
            else if (range.Items is [FuncCall fc, ..] && fc.Name.Parts.Count == 1 && FindVar(fc.Name.Last.Key) is { } pcursor)
            {
                Ref(fc.Name.Last.Token, pcursor.Symbol, Kind.Variable);
                info.Fields = pcursor.Fields;
                BindExprs(fc.Args, new Scope(null));
                DeclareVar(_ctx, loop.Variable, info);
                return;
            }

            BindExpr(range, new Scope(null));
        }

        DeclareVar(_ctx, loop.Variable, info);
    }
}
