using Scip.Indexer.Php.Composer;
using Scip.Indexer.Php.Syntax;

namespace Scip.Indexer.Php;

/// <summary>A (best-effort) static type: a set of class symbols, or an iterable with known value types.</summary>
internal abstract class SType
{
    public abstract List<string> Flatten();
}

internal sealed class NamedType(string name) : SType
{
    public override List<string> Flatten() => [name];
}

internal sealed class CompositeType : SType
{
    private readonly List<string> _types = [];

    public CompositeType(params SType?[] types)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in types)
        {
            if (t == null)
            {
                continue;
            }

            foreach (var s in t.Flatten())
            {
                if (seen.Add(s))
                {
                    _types.Add(s);
                }
            }
        }
    }

    public override List<string> Flatten() => _types;
}

internal abstract class IterableType : SType
{
    public override List<string> Flatten() => [];

    public abstract SType? ValueType(string? key);
}

internal sealed class UniformIterableType(SType type) : IterableType
{
    public override SType? ValueType(string? key) => type;
}

internal sealed class MixedIterableType(Dictionary<string, SType> types) : IterableType
{
    public override SType? ValueType(string? key) => key != null && types.TryGetValue(key, out var t) ? t : null;
}

/// <summary>
/// Port of <c>ScipPhp\Types\Types</c>: resolves the definition symbol of class constants, properties, methods and
/// names by a light-weight static type inference over the project and the dependencies it touches.
/// </summary>
internal sealed class Types
{
    private static readonly HashSet<string> BuiltinDocTypes = new(StringComparer.Ordinal)
    {
        "void", "null", "true", "false", "never", "never-return", "never-returns", "no-return", "bool", "boolean", "int", "integer", "positive-int",
        "negative-int", "float", "double", "string", "callable-string", "numeric-string", "non-empty-string", "non-falsy-string", "literal-string",
        "scalar", "mixed", "array", "non-empty-array", "non-empty-list", "callable", "object", "iterable", "resource",
    };

    private readonly ComposerProject _composer;
    private readonly SymbolNamer _namer;
    private readonly PhpFs _fs;
    private readonly Action<string>? _warn;
    private readonly Dictionary<string, List<string>> _uppers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SType?> _defs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenDepFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<DocComment, DocTags> _docCache = new(ReferenceEqualityComparer.Instance);

    public Types(ComposerProject composer, SymbolNamer namer, PhpFs fs, Action<string>? warn = null)
    {
        _composer = composer;
        _namer = namer;
        _fs = fs;
        _warn = warn;
    }

    public DocTags Tags(Node n)
    {
        if (n.Doc == null)
        {
            return DocTags.Empty;
        }

        if (!_docCache.TryGetValue(n.Doc, out var tags))
        {
            tags = PhpDocParser.Parse(n.Doc);
            _docCache[n.Doc] = tags;
        }

        return tags;
    }

    // ------------------------------------------------------------------ public queries

    public string? NameDef(Name n)
    {
        if (n.Text.ToLowerInvariant() is "null" or "true" or "false")
        {
            return null;
        }

        var type = Type(n);
        var flat = type?.Flatten();
        return flat is { Count: > 0 } ? flat[0] : null;
    }

    public string? ConstDef(Node x, string c)
    {
        if (c == "class")
        {
            return null;
        }

        var type = Type(x);
        return type == null ? null : FindDef(x, type.Flatten(), t => _namer.NameConst(t, c));
    }

    public string? PropDef(Node x, string prop)
    {
        var type = Type(x);
        return type == null ? null : FindDef(x, type.Flatten(), t => _namer.NameProp(t, prop));
    }

    public string? MethDef(Node x, string meth)
    {
        var type = Type(x);
        return type == null ? null : FindDef(x, type.Flatten(), t => _namer.NameMeth(t, meth));
    }

    // ------------------------------------------------------------------ type inference

    private SType? Type(Node x)
    {
        switch (x)
        {
            case ArrayDimFetch a:
            {
                var iter = Type(a.Var);
                if (iter is IterableType it)
                {
                    string? key = a.Dim switch
                    {
                        StringLit { Kind: StringKind.Single or StringKind.Double } s => s.Value,
                        NumberLit n when long.TryParse(n.Text, out var l) => l.ToString(),
                        _ => null,
                    };
                    return it.ValueType(key);
                }

                return null;
            }

            case Assign asg:
                return Type(asg.Value);
            case BinaryOp { Op: "??" } b:
                return new CompositeType(Type(b.Left), Type(b.Right));
            case CloneExpr c:
                return Type(c.Value);
            case FuncCall { Callee: Name { Text: not "" } fnName }:
            {
                var name = _namer.Name(fnName);
                return name != null && _defs.TryGetValue(name, out var t) ? t : null;
            }

            case MatchExpr m:
                return new CompositeType(m.Arms.Select(arm => Type(arm.Body)).ToArray());
            case MethodCall { NameNode: Ident { Name: not "" } mn } mc:
                return FindDefType(Type(mc.Var), mc, t => _namer.NameMeth(t, mn.Name));
            case StaticCall { NameNode: Ident { Name: not "" } sn } sc:
                return FindDefType(Type(sc.Class), sc, t => _namer.NameMeth(t, sn.Name));
            case NewExpr ne:
            {
                if (ne.Class is ClassStmt cls)
                {
                    var name = _namer.Name(cls);
                    return name == null ? null : new NamedType(name);
                }

                return Type(ne.Class);
            }

            case Name n:
            {
                var name = _namer.Name(n);
                return name == null ? null : new NamedType(name);
            }

            case PropertyFetch { NameNode: Ident { Name: not "" } pn } pf:
                return FindDefType(Type(pf.Var), pf, t => _namer.NameProp(t, pn.Name));
            case StaticPropertyFetch { NameNode: Ident { Name: not "" } spn } spf:
                return FindDefType(Type(spf.Class), spf, t => _namer.NameProp(t, spn.Name));
            case Ternary tern:
            {
                var elseType = Type(tern.Else);
                if (tern.If != null)
                {
                    return new CompositeType(Type(tern.If), elseType);
                }

                return new CompositeType(Type(tern.Cond), elseType);
            }

            case Variable { Name: "this" } v:
            {
                var name = _namer.NameNearestClassLike(v);
                return name == null ? null : new NamedType(name);
            }
        }

        return null;
    }

    private SType? FindDefType(SType? t, Node x, Func<string, string> name)
    {
        if (t == null)
        {
            return null;
        }

        var def = FindDef(x, t.Flatten(), name);
        return def != null && _defs.TryGetValue(def, out var type) ? type : null;
    }

    private string? FindDef(Node x, List<string> types, Func<string, string> name, int depth = 0)
    {
        foreach (var t in types)
        {
            var c = name(t);
            if (_defs.ContainsKey(c))
            {
                return c;
            }
        }

        if (depth < 64)
        {
            foreach (var t in types)
            {
                if (_uppers.TryGetValue(t, out var uppers))
                {
                    var c = FindDef(x, [.. uppers], name, depth + 1);
                    if (c != null)
                    {
                        return c;
                    }
                }
            }
        }

        foreach (var t in types)
        {
            var ident = _namer.ExtractIdent(t);
            if (ident == null || !_composer.IsDependency(ident))
            {
                continue;
            }

            var f = _composer.FindFile(ident);
            if (f == null)
            {
                continue;
            }

            if (_seenDepFiles.Contains(f))
            {
                return null;
            }

            CollectDepFile(f);
            _seenDepFiles.Add(f);
            var type = Type(x);
            if (type != null)
            {
                return FindDef(x, type.Flatten(), name, depth + 1);
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ definition collection

    public void Collect(IEnumerable<string> filenames)
    {
        foreach (var f in filenames)
        {
            var parsed = _fs.Parse(f, _warn);
            if (parsed != null)
            {
                CollectDefs(parsed);
            }
        }
    }

    private void CollectDepFile(string file)
    {
        if (file.StartsWith("stub:", StringComparison.Ordinal))
        {
            CollectStub(file[5..]);
            return;
        }

        var parsed = _fs.Parse(file, _warn);
        if (parsed != null)
        {
            CollectDefs(parsed);
        }
    }

    /// <summary>Registers what we know about a built-in class (replaces parsing the phpstorm-stubs file).</summary>
    private void CollectStub(string ident)
    {
        var sym = _namer.ClassSymbol(ident);
        if (sym == null)
        {
            return;
        }

        _defs[sym] = new NamedType(sym);
        if (Builtins.Parents.TryGetValue(ident, out var parents))
        {
            foreach (var p in parents)
            {
                var ps = _namer.ClassSymbol(p);
                if (ps == null)
                {
                    continue;
                }

                if (!_uppers.TryGetValue(sym, out var list))
                {
                    list = [];
                    _uppers[sym] = list;
                }

                list.Add(ps);
                _defs.TryAdd(ps, new NamedType(ps));
            }
        }

        foreach (var m in Builtins.Members(ident))
        {
            _defs[_namer.NameMeth(sym, m)] = null;
        }
    }

    private void CollectDefs(ParsedFile file)
    {
        foreach (var n in file.PostOrder())
        {
            switch (n)
            {
                case ClassConst cc:
                    foreach (var c in cc.Consts)
                    {
                        var name = _namer.Name(c);
                        if (name != null)
                        {
                            _defs[name] = null;
                        }
                    }

                    break;
                case ClassLike cl:
                {
                    CollectUppers(cl);
                    var name = _namer.Name(cl);
                    if (name != null)
                    {
                        _defs[name] = new NamedType(name);
                    }

                    break;
                }

                case EnumCase ec:
                {
                    var name = _namer.Name(ec);
                    if (name != null)
                    {
                        _defs[name] = null;
                    }

                    break;
                }

                case IFunctionLike fl:
                {
                    var name = _namer.Name(n);
                    if (name != null)
                    {
                        var type = ParseType(fl.ReturnType) ?? ParseDoc(n, Tags(n).ReturnType);
                        _defs[name] = type;
                    }

                    break;
                }

                case Param { Flags: not Mod.None, Var.Name: { } } p:
                {
                    // Constructor property promotion.
                    var name = _namer.Name(Promote(p));
                    if (name != null)
                    {
                        _defs[name] = ParseType(p.Type);
                    }

                    break;
                }

                case PropertyStmt ps:
                    foreach (var item in ps.Props)
                    {
                        var name = _namer.Name(item);
                        if (name == null)
                        {
                            continue;
                        }

                        var type = ParseType(ps.Type);
                        type ??= ParseDoc(ps, Tags(ps).VarType);
                        _defs[name] = type;
                    }

                    break;
            }
        }
    }

    /// <summary>Synthesizes the property that a promoted constructor parameter declares.</summary>
    public static PropertyItem Promote(Param p)
    {
        var item = new PropertyItem
        {
            Name = new Ident { Name = p.Var.Name!, Start = p.Var.Start, End = p.Var.End },
            Default = p.Default,
            Start = p.Start,
            End = p.End,
            TokPos = p.TokPos,
        };
        var stmt = new PropertyStmt { Flags = p.Flags, Type = p.Type, AttrGroups = p.AttrGroups, Props = [item], Start = p.Start, End = p.End, Doc = p.Doc, Parent = p.Parent };
        item.Parent = stmt;
        return item;
    }

    private void CollectUppers(ClassLike c)
    {
        var name = _namer.Name(c);
        if (name == null)
        {
            return;
        }

        foreach (var use in c.TraitUses)
        {
            foreach (var t in use.Traits)
            {
                AddUpper(name, t);
            }
        }

        switch (c)
        {
            case ClassStmt cs:
                if (cs.Extends != null)
                {
                    AddUpper(name, cs.Extends);
                }

                foreach (var i in cs.Implements)
                {
                    AddUpper(name, i);
                }

                break;
            case InterfaceStmt ist:
                foreach (var i in ist.Extends)
                {
                    AddUpper(name, i);
                }

                break;
        }

        var tags = Tags(c);
        foreach (var p in tags.Properties)
        {
            var propName = p.Name.TrimStart('$');
            if (propName == "")
            {
                continue;
            }

            _defs[_namer.NameProp(name, propName)] = ParseDoc(c, p.Type);
        }

        foreach (var m in tags.Methods)
        {
            if (m.Name == "")
            {
                continue;
            }

            _defs[_namer.NameMeth(name, m.Name)] = ParseDoc(c, m.ReturnType);
        }
    }

    private void AddUpper(string c, Name upper)
    {
        var name = _namer.Name(upper);
        if (name == null)
        {
            return;
        }

        if (!_uppers.TryGetValue(c, out var list))
        {
            list = [];
            _uppers[c] = list;
        }

        list.Add(name);
        _defs[name] = new NamedType(name);
    }

    // ------------------------------------------------------------------ type parsing (Types\Internal\TypeParser)

    private SType? ParseType(Node? n)
    {
        switch (n)
        {
            case Name { Kind: NameKind.FullyQualified } name:
            {
                var s = _namer.Name(name);
                return s == null ? null : new NamedType(s);
            }

            case NullableType nt:
                return ParseType(nt.Type);
            case UnionType ut:
                return new CompositeType(ut.Types.Select(ParseType).ToArray());
            case IntersectionType it:
                return new CompositeType(it.Types.Select(ParseType).ToArray());
        }

        return null;
    }

    public SType? ParseDoc(Node node, DocType? type)
    {
        switch (type)
        {
            case DocShape shape:
            {
                var types = new Dictionary<string, SType>();
                for (var i = 0; i < shape.Items.Count; i++)
                {
                    var t = ParseDoc(node, shape.Items[i].Type);
                    if (t == null)
                    {
                        continue;
                    }

                    types[shape.Items[i].Key ?? i.ToString()] = t;
                }

                return types.Count > 0 ? new MixedIterableType(types) : null;
            }

            case DocArray arr:
            {
                var t = ParseDoc(node, arr.Type);
                return t == null ? null : new UniformIterableType(t);
            }

            case DocConditional cond:
                return new CompositeType(ParseDoc(node, cond.If), ParseDoc(node, cond.Else));
            case DocIdent id:
            {
                if (BuiltinDocTypes.Contains(id.Name))
                {
                    return null;
                }

                var fq = id.Name.StartsWith('\\');
                var name = new Name { Text = fq ? id.Name.TrimStart('\\') : id.Name, Kind = fq ? NameKind.FullyQualified : (id.Name.Contains('\\') ? NameKind.Qualified : NameKind.Unqualified), Parent = node };
                if (name.IsSpecialClassName)
                {
                    var sn = _namer.Name(name);
                    return sn == null ? null : new NamedType(sn);
                }

                var resolved = ResolveName(node, name);
                if (resolved == null)
                {
                    return null;
                }

                var s = _namer.Name(resolved);
                return s == null ? null : new NamedType(s);
            }

            case DocIntersection di:
                return new CompositeType(di.Types.Select(t => ParseDoc(node, t)).ToArray());
            case DocUnion du:
                return new CompositeType(du.Types.Select(t => ParseDoc(node, t)).ToArray());
            case DocNullable dn:
                return ParseDoc(node, dn.Type);
            case DocThis:
            {
                var cl = NearestClassLike(node);
                if (cl == null)
                {
                    return null;
                }

                var name = _namer.Name(cl);
                return name == null ? null : new NamedType(name);
            }
        }

        return null;
    }

    private static Name? ResolveName(Node n, Name name)
    {
        NamespaceStmt? ns = null;
        for (var p = n.Parent; p != null; p = p.Parent)
        {
            if (p is NamespaceStmt s)
            {
                ns = s;
                break;
            }
        }

        // TODO (upstream): make this work in files without a namespace.
        if (ns == null)
        {
            return null;
        }

        if (name.Kind == NameKind.FullyQualified)
        {
            return name;
        }

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stmt in ns.Stmts.OfType<UseStmt>())
        {
            foreach (var use in stmt.Uses)
            {
                if ((stmt.Type | use.Type) != 1)
                {
                    continue;
                }

                var target = stmt.Prefix != null ? stmt.Prefix.Text + "\\" + use.Name.Text : use.Name.Text;
                aliases[use.AliasName] = target;
            }
        }

        var text = name.Text;
        var slash = text.IndexOf('\\');
        var first = slash < 0 ? text : text[..slash];
        string full;
        if (aliases.TryGetValue(first, out var aliased))
        {
            full = slash < 0 ? aliased : aliased + text[slash..];
        }
        else
        {
            var nsName = ns.Name?.Text;
            full = nsName == null ? text : nsName + "\\" + text;
        }

        return new Name { Text = full, Kind = NameKind.FullyQualified, Parent = n };
    }

    private static ClassLike? NearestClassLike(Node n)
    {
        for (var p = n; p != null; p = p.Parent)
        {
            if (p is ClassLike c)
            {
                return c;
            }
        }

        return null;
    }
}
