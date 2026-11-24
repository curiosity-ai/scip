namespace Scip.Indexer.Clang;

/// <summary>Receives definitions and references found by <see cref="CppParser"/>.</summary>
public interface IOccurrenceSink
{
    void Emit(Tok at, Entity entity, bool definition);
}

/// <summary>
/// A tolerant, declaration-level C++ parser with approximate name resolution (the same trade-off upstream makes in its
/// <c>ApproximateNameResolver</c> for dependent code). It builds a scope tree (namespaces, classes, enums, functions),
/// assigns SCIP symbols following upstream's naming scheme and resolves identifiers in types and function bodies.
/// Pass one declares entities; pass two (deferred jobs, in file order) scans types, initialisers and bodies, so uses
/// before declaration inside a translation unit resolve.
/// </summary>
public sealed partial class CppParser
{
    private static readonly HashSet<string> Builtins =
    [
        "void", "bool", "char", "short", "int", "long", "float", "double", "signed", "unsigned", "wchar_t", "char8_t", "char16_t", "char32_t", "auto", "__int128", "_Bool",
    ];

    private static readonly HashSet<string> Qualifiers =
    [
        "const", "volatile", "static", "extern", "inline", "constexpr", "consteval", "constinit", "virtual", "explicit", "friend", "mutable", "register",
        "thread_local", "typename", "typedef", "struct", "class", "union", "enum", "restrict", "__restrict", "__inline", "_Noreturn", "noexcept", "override", "final",
    ];

    private static readonly HashSet<string> Keywords =
    [
        "if", "else", "for", "while", "do", "switch", "case", "default", "return", "break", "continue", "goto", "try", "catch", "throw", "new", "delete", "sizeof",
        "alignof", "alignas", "decltype", "typeid", "static_cast", "dynamic_cast", "reinterpret_cast", "const_cast", "nullptr", "true", "false", "this", "operator",
        "template", "namespace", "using", "public", "private", "protected", "asm", "static_assert", "co_await", "co_return", "co_yield", "requires", "concept",
        ..Builtins, ..Qualifiers,
    ];

    private readonly List<Tok> _t;
    private readonly int[] _match;
    private readonly IOccurrenceSink _sink;
    private readonly FileTable _files;
    private readonly List<Action> _jobs = [];
    private readonly Dictionary<int, int> _localCounters = [];
    private readonly Dictionary<int, int> _anonCounters = [];
    private readonly Dictionary<string, List<Entity>> _members = [];
    private bool _pass2;
    private readonly TypeCanon _canon;

    public Scope Global { get; } = new(null, SKind.Global) { Path = [] };

    public CppParser(List<Tok> tokens, FileTable files, IOccurrenceSink sink)
    {
        _t = tokens;
        _files = files;
        _sink = sink;
        _canon = new TypeCanon(static (sc, name, leading) => leading ? Root(sc).LookupHere(name) : sc.Lookup(name));
        _match = new int[tokens.Count];
        Array.Fill(_match, -1);
        var stack = new List<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != TokKind.Punct)
            {
                continue;
            }

            switch (tokens[i].Text)
            {
                case "(" or "[" or "{":
                    stack.Add(i);
                    break;
                case ")" or "]" or "}":
                    // Pop to the matching opener kind; unbalanced preprocessor tricks degrade gracefully.
                    var open = tokens[i].Text switch { ")" => "(", "]" => "[", _ => "{" };
                    for (var k = stack.Count - 1; k >= 0; k--)
                    {
                        if (tokens[stack[k]].Text == open)
                        {
                            _match[stack[k]] = i;
                            _match[i] = stack[k];
                            stack.RemoveRange(k, stack.Count - k);
                            break;
                        }
                    }

                    break;
            }
        }
    }

    public void Run()
    {
        var p = 0;
        while (p < _t.Count)
        {
            var before = p;
            p = ParseDecl(Global, p, _t.Count);
            if (p <= before)
            {
                p = before + 1;
            }
        }

        _pass2 = true;
        foreach (var job in _jobs)
        {
            job();
        }

        _jobs.Clear();
    }

    // ---- token helpers -------------------------------------------------------------------------------------

    private bool P(int i, string s) => i >= 0 && i < _t.Count && _t[i].Kind is TokKind.Punct or TokKind.Ident && _t[i].Text == s;

    private bool Id(int i) => i >= 0 && i < _t.Count && _t[i].Kind == TokKind.Ident;

    private bool IsName(int i) => Id(i) && !Keywords.Contains(_t[i].Text);

    private int M(int i) => i >= 0 && i < _match.Length ? _match[i] : -1;

    private void Defer(Action a)
    {
        if (_pass2)
        {
            a();
        }
        else
        {
            _jobs.Add(a);
        }
    }

    /// <summary>Index of the matching '&gt;' for the '&lt;' at <paramref name="i"/>, or -1 if this does not look like template arguments.</summary>
    private int SkipAngles(int i, int limit)
    {
        var depth = 0;
        for (var k = i; k < limit && k < _t.Count; k++)
        {
            var t = _t[k];
            if (t.Kind != TokKind.Punct)
            {
                continue;
            }

            switch (t.Text)
            {
                case "<": depth++; break;
                case ">":
                    depth--;
                    if (depth == 0)
                    {
                        return k;
                    }

                    break;
                case "(" or "[" or "{":
                    if (M(k) < 0)
                    {
                        return -1;
                    }

                    k = M(k);
                    break;
                case ";" or "}" or ")" or "]" or "&&" or "||" or "=":
                    return -1;
            }
        }

        return -1;
    }

    /// <summary>End of a simple statement/declarator list: index of the ';' at depth 0 (or <paramref name="end"/>).</summary>
    private int StmtEnd(int p, int end)
    {
        for (var k = p; k < end; k++)
        {
            if (_t[k].Kind != TokKind.Punct)
            {
                continue;
            }

            if (_t[k].Text is "(" or "[" or "{" && M(k) > k)
            {
                k = M(k);
            }
            else if (_t[k].Text == ";")
            {
                return k;
            }
            else if (_t[k].Text == "}")
            {
                return k;
            }
        }

        return end;
    }

    private int SkipStatement(int p, int end)
    {
        var e = StmtEnd(p, end);
        return e < end && P(e, ";") ? e + 1 : Math.Max(e, p + 1);
    }

    // ---- entities ------------------------------------------------------------------------------------------

    private static SymbolDescriptor D(string name, SymbolDescriptor.Types.Suffix suffix, string disambiguator = "") =>
        new() { Name = name, Suffix = suffix, Disambiguator = disambiguator };

    private static string MakeSymbol(IEnumerable<SymbolDescriptor> descriptors)
    {
        var symbol = new Symbol { Scheme = "cxx", Package = new Package { Manager = ".", Name = "", Version = "$" } };
        symbol.Descriptors.AddRange(descriptors);
        return SymbolFormatter.Verbose.Format(symbol);
    }

    private Entity NewEntity(Scope into, string name, EKind kind, SymbolDescriptor desc, Tok at, SymbolInformation.Types.Kind sk, string? key = null)
    {
        string symbol;
        if (into.Path is null)
        {
            _localCounters.TryGetValue(at.File, out var n);
            _localCounters[at.File] = n + 1;
            symbol = "local " + n;
        }
        else
        {
            symbol = MakeSymbol([.. into.Path, desc]);
        }

        var e = new Entity(name, kind, symbol, into, _files.Relative(at.File) is not null) { SymbolKind = sk };
        into.Add(e, key);
        if (into.Kind == SKind.Class && kind is EKind.Func or EKind.Field or EKind.Var)
        {
            if (!_members.TryGetValue(name, out var l))
            {
                _members[name] = l = [];
            }

            l.Add(e);
        }

        return e;
    }

    private static Scope Root(Scope s)
    {
        while (s.Parent is not null)
        {
            s = s.Parent;
        }

        return s;
    }

    private static Scope OwnScope(Entity e, Scope parent, SKind kind, SymbolDescriptor desc, Scope? lexicalParent = null)
    {
        var s = new Scope(lexicalParent ?? parent, kind) { Owner = e, Path = parent.Path is null ? null : [.. parent.Path, desc] };
        e.Own = s;
        return s;
    }

    private void Emit(Tok at, Entity e, bool def)
    {
        if (!e.Silent)
        {
            _sink.Emit(at, e, def);
        }
    }

    // ---- declarations --------------------------------------------------------------------------------------

    private int ParseDecl(Scope scope, int p, int end)
    {
        if (p >= end)
        {
            return end;
        }

        var t = _t[p];
        if (t.Kind == TokKind.Punct)
        {
            return t.Text == ";" ? p + 1 : t.Text is "}" ? p + 1 : SkipStatement(p, end);
        }

        if (t.Kind != TokKind.Ident)
        {
            return SkipStatement(p, end);
        }

        switch (t.Text)
        {
            case "namespace":
                return ParseNamespace(scope, p, end);
            case "inline" when P(p + 1, "namespace"):
                return ParseNamespace(scope, p + 1, end, inline: true);
            case "extern" when _t.Count > p + 1 && _t[p + 1].Kind == TokKind.StringLiteral:
                if (P(p + 2, "{") && M(p + 2) > 0)
                {
                    var close = M(p + 2);
                    ParseSeq(scope, p + 3, close);
                    return close + 1;
                }

                return ParseDecl(scope, p + 2, end);
            case "template":
                return ParseTemplate(scope, p, end);
            case "using":
                return ParseUsing(scope, p, end);
            case "static_assert" or "asm" or "__asm__":
                return SkipStatement(p, end);
            case "public" or "private" or "protected" when P(p + 1, ":"):
                return p + 2;
            case "friend":
                return ParseGeneral(scope, p, end, friend: true);
            default:
                return ParseGeneral(scope, p, end, friend: false);
        }
    }

    private void ParseSeq(Scope scope, int p, int end)
    {
        while (p < end)
        {
            var before = p;
            p = ParseDecl(scope, p, end);
            if (p <= before)
            {
                p = before + 1;
            }
        }
    }

    private int ParseNamespace(Scope scope, int p, int end, bool inline = false)
    {
        var q = p + 1;
        // attributes
        while (P(q, "[") && P(q + 1, "[") && M(q) > 0)
        {
            q = M(q) + 1;
        }

        var parent = scope.Target;
        var cur = parent;
        var any = false;
        while (true)
        {
            if (P(q, "inline"))
            {
                q++;
            }

            if (!IsName(q))
            {
                break;
            }

            var nameTok = _t[q];
            if (P(q + 1, "="))
            {
                var aliasEnd = StmtEnd(q, end);
                var aliasScope = scope;
                var aliasName = nameTok.Text;
                var tgtStart = q + 2;
                void MakeAlias()
                {
                    if (aliasScope.Target.Names.ContainsKey(aliasName))
                    {
                        return;
                    }

                    if (ResolveQualifierChain(aliasScope, tgtStart, aliasEnd, emit: false) is { Own: { } own })
                    {
                        var host = aliasScope.Target;
                        host.Add(new Entity(aliasName, EKind.Namespace, "", host, false) { Own = own, Silent = true, Defined = true });
                    }
                }

                MakeAlias();
                Defer(MakeAlias);
                return aliasEnd < end ? aliasEnd + 1 : aliasEnd;
            }

            any = true;
            cur = GetNamespace(cur, nameTok.Text, nameTok, emitDef: true);
            q++;
            if (P(q, "::"))
            {
                q++;
                continue;
            }

            break;
        }

        if (!any)
        {
            var file = Path.GetFileName(_files.Path(_t[p].File));
            var name = "$anonymous_namespace_" + file;
            cur = GetNamespace(parent, name, _t[p], emitDef: true);
            if (!parent.Usings.Contains(cur))
            {
                parent.Usings.Add(cur);
            }
        }
        else if (inline && !parent.Usings.Contains(cur))
        {
            parent.Usings.Add(cur);
        }

        if (!P(q, "{") || M(q) < 0)
        {
            return SkipStatement(q, end);
        }

        var close = M(q);
        ParseSeq(cur, q + 1, close);
        return close + 1;
    }

    private Scope GetNamespace(Scope parent, string name, Tok at, bool emitDef)
    {
        Entity? e = parent.Names.TryGetValue(name, out var l) ? l.FirstOrDefault(static x => x.Kind == EKind.Namespace) : null;
        if (e is null)
        {
            var desc = D(name, SymbolDescriptor.Types.Suffix.Namespace);
            e = NewEntity(parent, name, EKind.Namespace, desc, at, SymbolInformation.Types.Kind.Namespace);
            OwnScope(e, parent, SKind.Namespace, desc);
            e.Defined = true;
        }

        if (emitDef)
        {
            Emit(at, e, true);
        }

        return e.Own!;
    }

    private int ParseTemplate(Scope scope, int p, int end)
    {
        if (!P(p + 1, "<"))
        {
            return SkipStatement(p, end); // explicit instantiation
        }

        var close = SkipAngles(p + 1, end);
        if (close < 0)
        {
            return SkipStatement(p, end);
        }

        var ts = new Scope(scope, SKind.Template) { Path = null };
        // split parameters
        var segStart = p + 2;
        for (var k = p + 2; k <= close; k++)
        {
            if (k < close && _t[k].Kind == TokKind.Punct && _t[k].Text is "(" or "[" or "{" && M(k) > 0)
            {
                k = M(k);
                continue;
            }

            if (k < close && P(k, "<"))
            {
                var c = SkipAngles(k, close + 1);
                if (c > 0)
                {
                    k = c;
                    continue;
                }
            }

            if (k == close || P(k, ","))
            {
                DeclareTemplateParam(ts, scope, segStart, k);
                segStart = k + 1;
            }
        }

        var q = close + 1;
        if (P(q, "requires"))
        {
            q = SkipRequires(q, end);
        }

        return ParseDecl(ts, q, end);
    }

    private int SkipRequires(int q, int end)
    {
        q++;
        if (P(q, "(") && M(q) > 0)
        {
            return M(q) + 1;
        }

        while (q < end && (IsName(q) || P(q, "::") || P(q, "<") || P(q, ">")))
        {
            q++;
        }

        return q;
    }

    private void DeclareTemplateParam(Scope ts, Scope outer, int a, int b)
    {
        if (b > a)
        {
            var nm = "";
            for (var x = a; x < b && !P(x, "="); x++)
            {
                if (IsName(x))
                {
                    nm = _t[x].Text;
                }
            }

            ts.TemplateParamNames.Add(nm);
            if (P(a, "template"))
            {
                ts.TemplateTemplateNames.Add(nm);
            }
        }

        var eq = a;
        while (eq < b && !P(eq, "="))
        {
            eq++;
        }

        if (eq > a)
        {
            var last = eq - 1;
            if (IsName(last) && last > a)
            {
                var e = NewEntity(ts, _t[last].Text, EKind.TemplateParam, D(_t[last].Text, SymbolDescriptor.Types.Suffix.TypeParameter), _t[last], SymbolInformation.Types.Kind.TypeParameter);
                Emit(_t[last], e, true);
                var tp = new Scope(null, SKind.Block);
                _ = tp;
            }
        }

        if (eq < b)
        {
            Defer(() => Scan(ts, eq + 1, b));
        }
    }

    private int ParseUsing(Scope scope, int p, int end)
    {
        var e = StmtEnd(p, end);
        if (P(p + 1, "namespace"))
        {
            var q = p + 2;
            Defer(() => Scan(scope, p + 2, e));
            // resolve for using-directive immediately in pass 2 (needs all declarations)
            Defer(() =>
            {
                var ent = ResolveQualifierChain(scope, q, e, emit: false);
                if (ent?.Own is { } own && !scope.Target.Usings.Contains(own))
                {
                    scope.Target.Usings.Add(own);
                }
            });
            return e < end ? e + 1 : e;
        }

        if (IsName(p + 1) && P(p + 2, "="))
        {
            var target = scope.Target;
            var nameTok = _t[p + 1];
            var ent = NewEntity(target, nameTok.Text, EKind.Typedef, D(nameTok.Text, SymbolDescriptor.Types.Suffix.Type), nameTok, SymbolInformation.Types.Kind.TypeAlias);
            ent.Type = MakeTypeRef(scope, p + 3, e);
            ent.AliasToks = _t.GetRange(p + 3, Math.Max(0, e - (p + 3))).ToArray();
            ent.AliasScope = scope;
            ent.Defined = true;
            Emit(nameTok, ent, true);
            Defer(() => Scan(scope, p + 3, e));
            return e < end ? e + 1 : e;
        }

        Defer(() => Scan(scope, p + 1, e));
        return e < end ? e + 1 : e;
    }

    // ---- general declarations ------------------------------------------------------------------------------

    private int ParseGeneral(Scope scope, int p, int end, bool friend)
    {
        var q = p;
        var isTypedef = false;
        var isStatic = false;
        while (q < end)
        {
            if (P(q, "[") && P(q + 1, "[") && M(q) > 0)
            {
                q = M(q) + 1;
            }
            else if (Id(q) && _t[q].Text is "__attribute__" or "alignas" or "__declspec" && P(q + 1, "(") && M(q + 1) > 0)
            {
                q = M(q + 1) + 1;
            }
            else if (Id(q) && _t[q].Text is "typedef" or "static" or "inline" or "constexpr" or "const" or "extern" or "friend" or "virtual" or "explicit" or "mutable" or "volatile" or "thread_local" or "consteval" or "constinit" or "register")
            {
                isTypedef |= _t[q].Text == "typedef";
                isStatic |= _t[q].Text == "static";
                q++;
            }
            else
            {
                break;
            }
        }

        if (Id(q) && _t[q].Text is "class" or "struct" or "union" or "enum")
        {
            return ParseTag(scope, p, q, end, friend, isTypedef);
        }

        return ParseFuncOrVar(scope, p, end, friend, isTypedef, isStatic);
    }

    private TypeRef? MakeTypeRef(Scope scope, int a, int b)
    {
        var r = MakeTypeRefCore(scope, a, b);
        if (r is not null)
        {
            for (var x = a; x < b && x < _t.Count; x++)
            {
                if (_t[x].Kind == TokKind.Punct && _t[x].Text == "*")
                {
                    return r with { Indirect = true };
                }
            }
        }

        return r;
    }

    private TypeRef? MakeTypeRefCore(Scope scope, int a, int b)
    {
        for (var i = a; i < b; i++)
        {
            if (!Id(i))
            {
                if (P(i, "::") && IsName(i + 1))
                {
                    return ChainRef(scope, i + 1, b, leading: true);
                }

                continue;
            }

            var text = _t[i].Text;
            if (Builtins.Contains(text) && text != "auto" || text is "decltype")
            {
                return null;
            }

            if (Keywords.Contains(text))
            {
                continue;
            }

            return ChainRef(scope, i, b, leading: i > a && P(i - 1, "::") && !Id(i - 2));
        }

        return null;
    }

    private TypeRef ChainRef(Scope scope, int i, int b, bool leading)
    {
        var chain = new List<string> { _t[i].Text };
        var j = i + 1;
        while (j < b)
        {
            if (P(j, "<"))
            {
                var c = SkipAngles(j, b);
                if (c < 0)
                {
                    break;
                }

                j = c + 1;
            }

            if (P(j, "::") && IsName(j + 1))
            {
                chain.Add(_t[j + 1].Text);
                j += 2;
            }
            else
            {
                break;
            }
        }

        return new TypeRef(scope, chain, leading);
    }

    // -- tags (class / struct / union / enum)

    private int ParseTag(Scope scope, int start, int kwIdx, int end, bool friend, bool isTypedef)
    {
        var kw = _t[kwIdx].Text;
        var q = kwIdx + 1;
        var scoped = false;
        if (kw == "enum" && Id(q) && _t[q].Text is "class" or "struct")
        {
            scoped = true;
            q++;
        }

        while (P(q, "[") && P(q + 1, "[") && M(q) > 0)
        {
            q = M(q) + 1;
        }

        if (Id(q) && _t[q].Text is "alignas" or "__attribute__" && P(q + 1, "(") && M(q + 1) > 0)
        {
            q = M(q + 1) + 1;
        }

        // name chain
        var seg = new List<int>();
        if (P(q, "::"))
        {
            q++;
        }

        while (IsName(q) && !(Id(q) && _t[q].Text == "final" && (P(q + 1, "{") || P(q + 1, ":"))))
        {
            seg.Add(q);
            q++;
            if (P(q, "<"))
            {
                var c = SkipAngles(q, end);
                if (c > 0)
                {
                    q = c + 1;
                }
            }

            if (P(q, "::") && IsName(q + 1))
            {
                q++;
                continue;
            }

            break;
        }

        if (Id(q) && _t[q].Text == "final")
        {
            q++;
        }

        var isDef = P(q, "{") || (P(q, ":") && !P(q, "::"));
        var isForward = P(q, ";") || (!isDef && seg.Count > 0 && friend);
        var hasName = seg.Count > 0;

        if (!isDef && !isForward)
        {
            // elaborated type in a declaration: "struct S s;" -> reference, then declarators
            if (hasName)
            {
                var scopeCopy = scope;
                var firstSeg = seg[0];
                var lastSeg = seg[^1];
                Defer(() => Scan(scopeCopy, firstSeg, lastSeg + 1));
            }

            return ParseFuncOrVar(scope, p: start, end, friend, isTypedef, isStatic: false, skipTagKw: kwIdx);
        }

        var isEnum = kw == "enum";
        var target = scope.Target;
        if (seg.Count > 1)
        {
            var qual = ResolveSegments(scope, seg.Take(seg.Count - 1).ToList(), emit: false);
            if (qual?.Own is { } qs)
            {
                target = qs;
            }
        }

        Entity? ent = null;
        var nameTok = hasName ? _t[seg[^1]] : _t[kwIdx];
        if (hasName)
        {
            var lookupScope = friend ? scope.NearestNamespace() : target;
            if (lookupScope.Names.TryGetValue(nameTok.Text, out var existing))
            {
                ent = existing.FirstOrDefault(static x => x.Kind is EKind.Class or EKind.Enum);
            }

            target = lookupScope;
        }

        string descName = nameTok.Text;
        if (!hasName)
        {
            _anonCounters.TryGetValue(nameTok.File, out var n);
            _anonCounters[nameTok.File] = n + 1;
            descName = $"$anonymous_type_{AstHash.Hex(_files.Relative(nameTok.File) ?? _files.Path(nameTok.File), 8)}_{n}";
        }

        var sk = isEnum ? SymbolInformation.Types.Kind.Enum
            : kw == "union" ? SymbolInformation.Types.Kind.Union
            : kw == "struct" ? SymbolInformation.Types.Kind.Struct
            : SymbolInformation.Types.Kind.Class;
        var desc = D(descName, SymbolDescriptor.Types.Suffix.Type);
        if (ent is null)
        {
            ent = NewEntity(target, descName, isEnum ? EKind.Enum : EKind.Class, desc, nameTok, sk);
            OwnScope(ent, target, isEnum ? SKind.Enum : SKind.Class, desc, scope.Kind == SKind.Template ? scope : null);
        }

        if (scope.Kind == SKind.Template && scope.TemplateParamNames.Count > 0 && !isEnum)
        {
            ent.TemplateScope ??= scope;
        }

        Emit(nameTok, ent, isDef);
        if (hasName)
        {
            if (seg.Count > 1)
            {
                var qSeg = seg.Take(seg.Count - 1).ToList();
                var scopeCopy = scope;
                Defer(() => ResolveSegments(scopeCopy, qSeg, emit: true));
            }
        }

        if (isForward && !isDef)
        {
            return q + 1;
        }

        ent.Defined = true;
        var typeRef = new TypeRef(scope, [ent.Name], false);
        _ = typeRef;

        // bases / underlying type
        if (P(q, ":"))
        {
            var bodyStart = q + 1;
            while (bodyStart < end && !P(bodyStart, "{") && !P(bodyStart, ";"))
            {
                bodyStart = M(bodyStart) > bodyStart && _t[bodyStart].Text is "(" or "[" ? M(bodyStart) + 1 : bodyStart + 1;
            }

            if (!isEnum)
            {
                var segStart = q + 1;
                for (var k = q + 1; k <= bodyStart; k++)
                {
                    if (k < bodyStart && P(k, "<"))
                    {
                        var c = SkipAngles(k, bodyStart);
                        if (c > 0)
                        {
                            k = c;
                            continue;
                        }
                    }

                    if (k == bodyStart || P(k, ","))
                    {
                        var s0 = segStart;
                        var e0 = k;
                        if (MakeTypeRef(scope, s0, e0) is { } br)
                        {
                            ent.Bases.Add(br);
                        }

                        Defer(() => Scan(scope, s0, e0));
                        segStart = k + 1;
                    }
                }
            }
            else
            {
                var s0 = q + 1;
                var e0 = bodyStart;
                Defer(() => Scan(scope, s0, e0));
            }

            q = bodyStart;
        }

        if (!P(q, "{") || M(q) < 0)
        {
            return SkipStatement(q, end);
        }

        var close = M(q);
        if (isEnum)
        {
            ParseEnumBody(scope, ent, scoped, q + 1, close);
        }
        else
        {
            ParseSeq(ent.Own!, q + 1, close);
        }

        var after = close + 1;
        if (P(after, ";"))
        {
            return after + 1;
        }

        // declarators following the definition: "struct { int a; } v;"
        var tr = new TypeRef(scope, [descName], false);
        return ParseDeclarators(scope, after, end, tr, isTypedef, false, typeStart: -1, typeEnd: -1, firstNameIdx: -1, forceVar: true);
    }

    private void ParseEnumBody(Scope scope, Entity enumEnt, bool scoped, int a, int b)
    {
        var target = scoped ? enumEnt.Own! : scope.Target;
        var k = a;
        while (k < b)
        {
            while (P(k, "[") && P(k + 1, "[") && M(k) > 0)
            {
                k = M(k) + 1;
            }

            if (!IsName(k))
            {
                k++;
                continue;
            }

            var nameTok = _t[k];
            var desc = D(nameTok.Text, SymbolDescriptor.Types.Suffix.Term);
            var c = NewEntity(target, nameTok.Text, EKind.EnumConst, desc, nameTok, SymbolInformation.Types.Kind.EnumMember);
            c.Type = new TypeRef(scope, [enumEnt.Name], false);
            c.Defined = true;
            if (!scoped)
            {
                enumEnt.Own!.Add(c);
            }

            Emit(nameTok, c, true);
            k++;
            if (P(k, "="))
            {
                var e = k + 1;
                while (e < b && !P(e, ","))
                {
                    e = M(e) > e && _t[e].Text is "(" or "[" or "{" ? M(e) + 1 : e + 1;
                }

                var s0 = k + 1;
                var e0 = e;
                var lookup = scoped ? enumEnt.Own! : enumEnt.Own!;
                Defer(() => Scan(lookup, s0, e0));
                k = e;
            }

            if (P(k, ","))
            {
                k++;
            }
        }
    }

    // -- functions and variables

    private sealed record Param(int A, int TypeEnd, int NameIdx, int DefA, int DefB);

    private List<Param> ParseParams(int open, int close)
    {
        var res = new List<Param>();
        var segStart = open + 1;
        for (var k = open + 1; k <= close; k++)
        {
            if (k < close && _t[k].Kind == TokKind.Punct && _t[k].Text is "(" or "[" or "{" && M(k) > k)
            {
                k = M(k);
                continue;
            }

            if (k < close && P(k, "<") && Id(k - 1))
            {
                var c = SkipAngles(k, close);
                if (c > 0)
                {
                    k = c;
                    continue;
                }
            }

            if (k != close && !P(k, ","))
            {
                continue;
            }

            var a = segStart;
            var b = k;
            segStart = k + 1;
            if (b <= a || (b - a == 1 && (P(a, "void") || P(a, "..."))))
            {
                continue;
            }

            var eq = a;
            while (eq < b && !P(eq, "="))
            {
                if (_t[eq].Kind == TokKind.Punct && _t[eq].Text is "(" or "[" or "{" && M(eq) > eq)
                {
                    eq = M(eq);
                }

                eq++;
            }

            var te = Math.Min(eq, b);
            var nameIdx = -1;
            // function pointer: T (*name)(...)
            for (var x = a; x < te; x++)
            {
                if (P(x, "(") && (P(x + 1, "*") || P(x + 1, "&")) && IsName(x + 2) && M(x) > x)
                {
                    nameIdx = x + 2;
                    break;
                }
            }

            if (nameIdx < 0)
            {
                var e2 = te;
                while (e2 > a && P(e2 - 1, "]") && M(e2 - 1) >= a)
                {
                    e2 = M(e2 - 1);
                }

                if (e2 - a >= 2 && IsName(e2 - 1) && !P(e2 - 2, "::") && !Qualifiers.Contains(_t[e2 - 1].Text)
                    && !(e2 - a == 2 && Qualifiers.Contains(_t[a].Text) && Builtins.Contains(_t[e2 - 1].Text)))
                {
                    nameIdx = e2 - 1;
                }

                te = nameIdx >= 0 ? nameIdx : te;
            }
            else
            {
                te = nameIdx;
            }

            res.Add(new Param(a, te, nameIdx, eq < b ? eq + 1 : -1, eq < b ? b : -1));
        }

        return res;
    }

    private string TypeText(int a, int b) => string.Join(' ', Enumerable.Range(a, Math.Max(0, b - a)).Select(i => _t[i].Text));

    private int ParseFuncOrVar(Scope scope, int p, int end, bool friend, bool isTypedef, bool isStatic, int skipTagKw = -1)
    {
        // Find the first structural terminator at depth 0.
        var k = p;
        var parenIdx = -1;
        var terminator = -1;
        while (k < end)
        {
            var t = _t[k];
            if (t.Kind == TokKind.Ident && t.Text == "operator")
            {
                k = SkipOperatorName(k, end);
                continue;
            }

            if (t.Kind == TokKind.Punct)
            {
                if (t.Text is "[" && P(k + 1, "[") && M(k) > 0)
                {
                    k = M(k) + 1;
                    continue;
                }

                if (t.Text == "<" && k > p && Id(k - 1))
                {
                    var c = SkipAngles(k, end);
                    if (c > 0)
                    {
                        k = c + 1;
                        continue;
                    }
                }

                if (t.Text == "(")
                {
                    if (k > p && P(k - 1, "operator") && P(k + 1, ")") && P(k + 2, "("))
                    {
                        k += 2;
                        continue;
                    }

                    parenIdx = k;
                    break;
                }

                if (k > p && P(k - 1, "operator"))
                {
                    k++;
                    continue;
                }

                if (t.Text is "=" or ";" or "," or "{" or "[" or ":" or "}")
                {
                    terminator = k;
                    break;
                }
            }

            k++;
        }

        if (parenIdx >= 0 && M(parenIdx) > parenIdx)
        {
            // function pointer / grouped declarator?
            var headIsEmpty = parenIdx == p || !(FindOperatorKeyword(p, parenIdx) >= 0 || IsName(parenIdx - 1) || Id(parenIdx - 1) && _t[parenIdx - 1].Text == "operator" || P(parenIdx - 1, ">") || IsOperatorName(parenIdx - 1) || P(parenIdx - 1, "~") || P(parenIdx - 2, "~"));
            if (!headIsEmpty && !(P(parenIdx + 1, "*") || P(parenIdx + 1, "&")) && !(isTypedef && false))
            {
                return ParseFunction(scope, p, end, parenIdx, friend, isStatic, isTypedef);
            }

            if (!headIsEmpty || parenIdx > p)
            {
                // variable of function-pointer type (or typedef thereof)
                if (P(parenIdx + 1, "*") || P(parenIdx + 1, "&"))
                {
                    var nameIdx = parenIdx + 2;
                    if (IsName(nameIdx))
                    {
                        return ParseDeclarators(scope, nameIdx, end, MakeTypeRef(scope, p, parenIdx), isTypedef, isStatic, p, parenIdx, nameIdx, false);
                    }
                }
            }

            // Macro-like invocation at declaration level, e.g. DECLARE_THING(x);
            var after = M(parenIdx) + 1;
            if (P(after, "{") && M(after) > 0)
            {
                return M(after) + 1;
            }

            return P(after, ";") ? after + 1 : SkipStatement(after, end);
        }

        if (terminator < 0)
        {
            return SkipStatement(p, end);
        }

        // variable declaration: the name is the identifier before the terminator (possibly qualified).
        var nameI = terminator - 1;
        if (terminator < 0 || !IsName(nameI) || nameI < p)
        {
            // e.g. "int;" or garbage
            return SkipStatement(p, end);
        }

        if (nameI == p && skipTagKw < 0 && terminator != nameI + 1)
        {
            return SkipStatement(p, end);
        }

        if (nameI == p && !isTypedef && (P(terminator, ";") || P(terminator, ",")))
        {
            return SkipStatement(p, end); // single identifier statement
        }

        return ParseDeclarators(scope, nameI, end, MakeTypeRef(scope, p, QualifiedStart(nameI, p)), isTypedef, isStatic, p, QualifiedStart(nameI, p), nameI, false);
    }

    private bool IsOperatorName(int i) => i >= 1 && _t[i].Kind == TokKind.Punct && (P(i - 1, "operator") || (i >= 2 && P(i - 2, "operator")));

    /// <summary>Walks back from a name over "A::B::" qualifiers; returns the index where the qualified name starts.</summary>
    private int QualifiedStart(int nameIdx, int limit)
    {
        var j = nameIdx;
        while (j - 2 >= limit && P(j - 1, "::") && (Id(j - 2) || P(j - 2, ">")))
        {
            if (P(j - 2, ">"))
            {
                var m = j - 2;
                var depth = 0;
                var found = -1;
                for (var x = m; x >= limit; x--)
                {
                    if (P(x, ">"))
                    {
                        depth++;
                    }
                    else if (P(x, "<") && --depth == 0)
                    {
                        found = x;
                        break;
                    }
                }

                if (found <= limit)
                {
                    break;
                }

                j = found - 1;
                continue;
            }

            j -= 2;
        }

        return j;
    }

    private int ParseDeclarators(Scope scope, int p, int end, TypeRef? type, bool isTypedef, bool isStatic, int typeStart, int typeEnd, int firstNameIdx, bool forceVar)
    {
        var first = true;
        while (p < end)
        {
            // skip pointer/ref/cv between declarators
            while (P(p, "*") || P(p, "&") || P(p, "&&") || (Id(p) && _t[p].Text is "const" or "volatile" or "__restrict"))
            {
                p++;
            }

            if (!IsName(p) && !(first && firstNameIdx >= 0))
            {
                return SkipStatement(p, end);
            }

            var nameIdx = first && firstNameIdx >= 0 ? firstNameIdx : p;
            var qs = QualifiedStart(nameIdx, first ? Math.Max(typeStart, 0) : nameIdx);
            var nameTok = _t[nameIdx];
            var target = scope.Target;
            Scope lexical = scope;
            Entity? qualifier = null;
            if (qs < nameIdx)
            {
                var segs = new List<int>();
                for (var x = qs; x < nameIdx; x += 2)
                {
                    if (Id(x))
                    {
                        segs.Add(x);
                    }
                    else
                    {
                        x--;
                    }
                }

                var segsCopy = segs;
                var sc = scope;
                qualifier = ResolveSegments(scope, segs, emit: false);
                Defer(() => ResolveSegments(sc, segsCopy, emit: true));
                if (qualifier?.Own is { } own)
                {
                    target = own;
                    lexical = own;
                }
            }

            var inFunction = target.Path is null;
            var kind = isTypedef ? EKind.Typedef : target.Kind == SKind.Class && !isStatic ? EKind.Field : inFunction ? EKind.Local : EKind.Var;
            var sk = kind switch
            {
                EKind.Typedef => SymbolInformation.Types.Kind.TypeAlias,
                EKind.Field => SymbolInformation.Types.Kind.Field,
                _ => isStatic && target.Kind == SKind.Class ? SymbolInformation.Types.Kind.StaticVariable : SymbolInformation.Types.Kind.Variable,
            };
            var suffix = kind == EKind.Typedef ? SymbolDescriptor.Types.Suffix.Type : SymbolDescriptor.Types.Suffix.Term;
            var desc = D(nameTok.Text, suffix);

            // Out-of-line definition of a static data member: reuse the in-class declaration.
            Entity? existing = null;
            if (qs < nameIdx && target.Names.TryGetValue(nameTok.Text, out var ex))
            {
                existing = ex.FirstOrDefault(static x => x.Kind is EKind.Var or EKind.Field);
            }

            var ent = existing ?? NewEntity(target, nameTok.Text, kind, desc, nameTok, sk);
            ent.Type ??= type;
            if (kind == EKind.Typedef && first && typeStart >= 0 && ent.AliasToks is null)
            {
                var ae = nameIdx + 1;
                while (ae < end && !P(ae, ",") && !P(ae, ";") && !P(ae, "=") && !P(ae, "{"))
                {
                    ae = _t[ae].Kind == TokKind.Punct && _t[ae].Text is "(" or "[" && M(ae) > ae ? M(ae) + 1 : ae + 1;
                }

                var al = new List<Tok>();
                for (var x = typeStart; x < ae; x++)
                {
                    if (x != nameIdx)
                    {
                        al.Add(_t[x]);
                    }
                }

                ent.AliasToks = [.. al];
                ent.AliasScope = scope;
            }

            ent.IsStatic |= isStatic;
            var isExtern = false;
            for (var x = Math.Max(typeStart, 0); x < nameIdx && typeStart >= 0; x++)
            {
                isExtern |= Id(x) && _t[x].Text == "extern";
            }

            // find end of this declarator to know whether an initializer follows
            var q = nameIdx + 1;
            var hasInit = false;
            while (q < end)
            {
                if (P(q, "[") && M(q) > q)
                {
                    var s0 = q + 1;
                    var e0 = M(q);
                    var sc = lexical;
                    Defer(() => Scan(sc, s0, e0));
                    q = M(q) + 1;
                    continue;
                }

                break;
            }

            var def = !isExtern || hasInit;
            if (P(q, "=") || P(q, "{") || P(q, "("))
            {
                hasInit = true;
            }

            def = !isExtern || hasInit;
            Emit(nameTok, ent, def || kind == EKind.Typedef);
            ent.Defined |= def;

            if (typeStart >= 0 && first && typeEnd > typeStart)
            {
                var s0 = typeStart;
                var e0 = typeEnd;
                var sc = scope;
                Defer(() => Scan(sc, s0, e0));
            }

            // initializer
            var stop = q;
            if (P(q, "=") || P(q, "{") || P(q, "(") || P(q, ":"))
            {
                var initStart = P(q, "=") || P(q, ":") ? q + 1 : q;
                var e = initStart;
                while (e < end && !P(e, ",") && !P(e, ";"))
                {
                    if (_t[e].Kind == TokKind.Punct && _t[e].Text is "(" or "[" or "{" && M(e) > e)
                    {
                        e = M(e);
                    }
                    else if (P(e, "<") && Id(e - 1))
                    {
                        var c = SkipAngles(e, end);
                        if (c > 0)
                        {
                            e = c;
                        }
                    }
                    else if (P(e, "}"))
                    {
                        break;
                    }

                    e++;
                }

                var s0 = initStart;
                var e0 = e;
                var sc = lexical;
                if (kind != EKind.Typedef)
                {
                    Defer(() => Scan(sc, s0, e0));
                }

                stop = e;
            }

            first = false;
            if (P(stop, ","))
            {
                p = stop + 1;
                continue;
            }

            return P(stop, ";") ? stop + 1 : stop;
        }

        return p;
    }

    private int ParseFunction(Scope scope, int p, int end, int open, bool friend, bool isStatic, bool isTypedef)
    {
        var close = M(open);
        // --- declared name
        var nameIdx = open - 1;
        string name;
        var nameStartTok = _t[nameIdx];
        var nameLen = _t[nameIdx].Len;
        var nameFirstIdx = nameIdx;
        var opIdx = FindOperatorKeyword(p, open);
        if (opIdx >= 0)
        {
            name = OperatorName(scope, opIdx, open - 1);
            nameFirstIdx = opIdx;
            nameStartTok = _t[opIdx];
            nameLen = nameStartTok.Len;
        }
        else if (P(nameIdx, ")") && P(nameIdx - 1, "(") && P(nameIdx - 2, "operator"))
        {
            name = "operator()";
            nameFirstIdx = nameIdx - 2;
            nameStartTok = _t[nameFirstIdx];
            nameLen = nameStartTok.Len;
        }
        else if (IsOperatorName(nameIdx) || (Id(nameIdx) && _t[nameIdx].Text == "operator") || (P(nameIdx, ">") && false))
        {
            var o = nameIdx;
            while (o > p && !P(o, "operator"))
            {
                o--;
            }

            nameFirstIdx = o;
            name = "operator" + string.Concat(Enumerable.Range(o + 1, nameIdx - o).Select(i => (Id(i) && Id(i - 1) ? " " : "") + _t[i].Text));
            nameStartTok = _t[o];
            nameLen = _t[o].Len;
        }
        else if (Id(nameIdx) && Id(nameIdx - 1) && _t[nameIdx - 1].Text == "operator")
        {
            nameFirstIdx = nameIdx - 1;
            name = "operator " + _t[nameIdx].Text;
            nameStartTok = _t[nameIdx - 1];
            nameLen = nameStartTok.Len;
        }
        else if (IsName(nameIdx))
        {
            name = _t[nameIdx].Text;
            if (P(nameIdx - 1, "~"))
            {
                name = "~" + name;
                nameFirstIdx = nameIdx - 1;
                nameStartTok = _t[nameIdx - 1];
                nameLen = _t[nameIdx].Col + _t[nameIdx].Len - nameStartTok.Col;
            }
        }
        else
        {
            return SkipStatement(p, end);
        }

        var qs = QualifiedStart(nameFirstIdx, p);
        var qualSegs = new List<int>();
        for (var x = qs; x < nameFirstIdx; x++)
        {
            if (IsName(x) && P(x + 1, "::") || (IsName(x) && P(x + 1, "<")))
            {
                qualSegs.Add(x);
            }
        }

        qualSegs = qualSegs.Where(x => P(x + 1, "::") || P(x + 1, "<")).ToList();
        var headEnd = qs;

        // --- parameters and tail
        var parms = ParseParams(open, close);
        var tailEnd = close + 1;
        var isPure = false;
        var bodyOpen = -1;
        var ctorInitStart = -1;
        var q = close + 1;
        var trailingStart = -1;
        while (q < end)
        {
            var t = _t[q];
            if (t.Kind == TokKind.Punct && t.Text is "{")
            {
                bodyOpen = q;
                break;
            }

            if (t.Kind == TokKind.Punct && t.Text is ";" or "}")
            {
                break;
            }

            if (t.Kind == TokKind.Punct && t.Text == "=")
            {
                isPure = q + 1 < _t.Count && _t[q + 1].Text is "0" or "default";
                q += 2;
                continue;
            }

            if (t.Kind == TokKind.Punct && t.Text == ":")
            {
                ctorInitStart = q + 1;
                // skip initializer list up to the body brace
                var r = q + 1;
                while (r < end)
                {
                    if (_t[r].Kind == TokKind.Punct && _t[r].Text is "(" or "[" && M(r) > r)
                    {
                        r = M(r) + 1;
                        continue;
                    }

                    if (P(r, "{"))
                    {
                        // a brace directly after an identifier is a braced initializer; after ')' / '}' / '>' it is the body
                        if ((Id(r - 1) || P(r - 1, ">")) && M(r) > r)
                        {
                            r = M(r) + 1;
                            continue;
                        }

                        break;
                    }

                    if (P(r, ";"))
                    {
                        break;
                    }

                    r++;
                }

                q = r;
                continue;
            }

            if (t.Kind == TokKind.Punct && t.Text == "->")
            {
                trailingStart = q + 1;
            }

            if (t.Kind == TokKind.Punct && t.Text is "(" or "[" && M(q) > q)
            {
                q = M(q) + 1;
                continue;
            }

            q++;
        }

        var trailingEnd = bodyOpen > 0 ? bodyOpen : q;
        var hasBody = bodyOpen > 0 && M(bodyOpen) > bodyOpen;
        var isDef = hasBody || isPure;
        int after;
        if (hasBody)
        {
            after = M(bodyOpen) + 1;
        }
        else
        {
            after = q < end && P(q, ";") ? q + 1 : q;
        }

        // --- target scope and entity
        var target = scope.Target;
        Scope lexicalParent = scope;
        Entity? qualEnt = null;
        if (qualSegs.Count > 0)
        {
            qualEnt = ResolveSegments(scope, qualSegs, emit: false);
            if (qualEnt?.Own is { } own)
            {
                target = own;
                lexicalParent = own;
            }
            else if (qualEnt is null)
            {
                // Unknown qualifier (e.g. std::): scan body for references only.
                if (hasBody)
                {
                    var fsx = new Scope(scope, SKind.Function) { Path = null };
                    var bo = bodyOpen;
                    Defer(() => FunctionBody(fsx, parms, bo, ctorInitStart, -1, -1, null));
                }

                return after;
            }
        }

        if (friend)
        {
            target = scope.NearestNamespace();
        }

        var ownerClass = target.Kind == SKind.Class ? target.Owner : null;
        var isCtor = ownerClass is not null && name == ownerClass.Name;
        var isDtor = ownerClass is not null && name == "~" + ownerClass.Name;
        var key = isCtor ? "<ctor>" : name;
        var (sig, fnType, paramTypes) = CanonicalFunction(lexicalParent, p, headEnd, nameFirstIdx, open, close, end, isCtor || isDtor, name);
        Entity? ent = null;
        if (target.Names.TryGetValue(key, out var cands))
        {
            ent = cands.FirstOrDefault(c => c.Kind == EKind.Func && c.Signature == sig);
        }

        if (ent is null)
        {
            var desc = D(name, SymbolDescriptor.Types.Suffix.Method, WyHash.HexOfText(fnType));
            var sk = isCtor ? SymbolInformation.Types.Kind.Constructor
                : target.Kind == SKind.Class ? (isStatic ? SymbolInformation.Types.Kind.StaticMethod : SymbolInformation.Types.Kind.Method)
                : SymbolInformation.Types.Kind.Function;
            ent = NewEntity(target, name, EKind.Func, desc, nameStartTok, sk, key);
            ent.Signature = sig;
            ent.MinArgs = parms.Count(pm => pm.DefA < 0);
            ent.MaxArgs = parms.Count;
            if (Enumerable.Range(open + 1, Math.Max(0, close - open - 1)).Any(i => P(i, "...") && (M(i) < 0)) && parms.Count >= 0)
            {
                ent.MaxArgs = int.MaxValue;
            }

            ent.IsStatic = isStatic;
            ent.ParamTypes = paramTypes;
            ent.Type = headEnd > p ? MakeTypeRef(scope, p, headEnd) : null;
        }

        _ = isDtor;
        var nameTokForEmit = nameStartTok with { Len = nameLen };
        if (isTypedef)
        {
            ent.SymbolKind = SymbolInformation.Types.Kind.TypeAlias;
        }

        Emit(nameTokForEmit, ent, isDef && !friend);
        ent.Defined |= isDef;

        // --- deferred scanning: qualifiers, return type, params, body
        var sc = scope;
        var segsCopy = qualSegs;
        var pStart = p;
        var hEnd = headEnd;
        var fs = new Scope(lexicalParent, SKind.Function) { Path = null };
        var bodyO = hasBody ? bodyOpen : -1;
        var tS = trailingStart;
        var tE = trailingEnd;
        var ciStart = ctorInitStart;
        var ownerCls = target.Kind == SKind.Class ? target : lexicalParent.NearestClass();
        Defer(() =>
        {
            if (segsCopy.Count > 0)
            {
                ResolveSegments(sc, segsCopy, emit: true);
            }

            Scan(sc, pStart, hEnd);
            FunctionBody(fs, parms, bodyO, ciStart, tS, tE, ownerCls);
        });

        return after;
    }

    /// <summary>Declares parameters, scans types/defaults, constructor initialisers and the body of a function.</summary>
    private void FunctionBody(Scope fs, List<Param> parms, int bodyOpen, int ctorInit, int trailStart, int trailEnd, Scope? cls)
    {
        foreach (var pm in parms)
        {
            if (pm.A < pm.TypeEnd)
            {
                Scan(fs.Parent ?? fs, pm.A, pm.TypeEnd);
            }

            if (pm.NameIdx >= 0)
            {
                var nt = _t[pm.NameIdx];
                var e = NewEntity(fs, nt.Text, EKind.Param, D(nt.Text, SymbolDescriptor.Types.Suffix.Parameter), nt, SymbolInformation.Types.Kind.Parameter);
                e.Type = MakeTypeRef(fs, pm.A, pm.TypeEnd);
                e.Defined = true;
                Emit(nt, e, true);
            }

            if (pm.DefA >= 0)
            {
                Scan(fs, pm.DefA, pm.DefB);
            }
        }

        if (trailStart >= 0 && trailEnd > trailStart)
        {
            Scan(fs, trailStart, trailEnd);
        }

        if (bodyOpen < 0)
        {
            return;
        }

        if (ctorInit >= 0)
        {
            ScanCtorInit(fs, cls, ctorInit, bodyOpen);
        }

        Block(fs, bodyOpen + 1, M(bodyOpen));
    }

    private void ScanCtorInit(Scope fs, Scope? cls, int a, int b)
    {
        var i = a;
        while (i < b)
        {
            if (IsName(i))
            {
                var j = i;
                while (P(j + 1, "::") && IsName(j + 2))
                {
                    j += 2;
                }

                if (P(j + 1, "<"))
                {
                    var c = SkipAngles(j + 1, b);
                    if (c > 0)
                    {
                        j = c - 1;
                    }
                }

                var open = j + 1;
                if (P(open, "<"))
                {
                    open = SkipAngles(open, b) + 1;
                }

                if ((P(open, "(") || P(open, "{")) && M(open) > open)
                {
                    List<Entity>? cands = null;
                    if (i == j && cls is not null)
                    {
                        cands = cls.LookupHere(_t[i].Text);
                    }

                    if (cands is { Count: > 0 })
                    {
                        Emit(_t[i], cands[0], false);
                    }
                    else
                    {
                        Scan(fs, i, j + 1);
                    }

                    Scan(fs, open + 1, M(open));
                    i = M(open) + 1;
                    continue;
                }
            }

            i++;
        }
    }

    // ---- statements ---------------------------------------------------------------------------------------

    private void Block(Scope s, int p, int end)
    {
        while (p < end)
        {
            var before = p;
            p = Statement(s, p, end);
            if (p <= before)
            {
                p = before + 1;
            }
        }
    }

    private int Statement(Scope s, int p, int end)
    {
        var t = _t[p];
        if (t.Kind == TokKind.Punct)
        {
            if (t.Text == ";")
            {
                return p + 1;
            }

            if (t.Text == "{" && M(p) > p)
            {
                var bs = new Scope(s, SKind.Block) { Path = null };
                Block(bs, p + 1, M(p));
                return M(p) + 1;
            }
        }

        if (t.Kind == TokKind.Ident)
        {
            switch (t.Text)
            {
                case "if" or "while" or "switch":
                {
                    var open = p + 1;
                    while (open < end && !P(open, "(") && Id(open))
                    {
                        open++;
                    }

                    if (!P(open, "(") || M(open) < 0)
                    {
                        return p + 1;
                    }

                    var cs = new Scope(s, SKind.Block) { Path = null };
                    var close = M(open);
                    var semi = FindTop(open + 1, close, ";");
                    if (semi >= 0)
                    {
                        SimpleOrDecl(cs, open + 1, semi);
                        SimpleOrDecl(cs, semi + 1, close);
                    }
                    else
                    {
                        SimpleOrDecl(cs, open + 1, close);
                    }

                    var next = Statement(cs, close + 1, end);
                    if (t.Text == "if" && P(next, "else"))
                    {
                        next = Statement(cs, next + 1, end);
                    }

                    return next;
                }

                case "for":
                {
                    var open = p + 1;
                    if (!P(open, "(") || M(open) < 0)
                    {
                        return p + 1;
                    }

                    var close = M(open);
                    var fs2 = new Scope(s, SKind.Block) { Path = null };
                    var colon = FindTop(open + 1, close, ":");
                    var semi = FindTop(open + 1, close, ";");
                    if (semi >= 0)
                    {
                        SimpleOrDecl(fs2, open + 1, semi);
                        var semi2 = FindTop(semi + 1, close, ";");
                        if (semi2 >= 0)
                        {
                            Scan(fs2, semi + 1, semi2);
                            Scan(fs2, semi2 + 1, close);
                        }
                    }
                    else if (colon >= 0)
                    {
                        Scan(fs2, colon + 1, close);
                        SimpleOrDecl(fs2, open + 1, colon);
                    }

                    return Statement(fs2, close + 1, end);
                }

                case "do":
                {
                    var next = Statement(s, p + 1, end);
                    if (P(next, "while") && P(next + 1, "(") && M(next + 1) > 0)
                    {
                        Scan(s, next + 2, M(next + 1));
                        next = M(next + 1) + 1;
                        if (P(next, ";"))
                        {
                            next++;
                        }
                    }

                    return next;
                }

                case "else":
                    return Statement(s, p + 1, end);
                case "try":
                {
                    var next = Statement(s, p + 1, end);
                    while (P(next, "catch") && P(next + 1, "(") && M(next + 1) > 0)
                    {
                        var cs = new Scope(s, SKind.Block) { Path = null };
                        SimpleOrDecl(cs, next + 2, M(next + 1));
                        next = Statement(cs, M(next + 1) + 1, end);
                    }

                    return next;
                }

                case "return" or "throw" or "co_return" or "co_yield" or "goto" or "break" or "continue" or "delete" or "case" or "default":
                {
                    var e = StmtEnd(p, end);
                    if (t.Text is "case" or "default")
                    {
                        var colon = FindTop(p, e, ":");
                        if (colon >= 0)
                        {
                            Scan(s, p + 1, colon);
                            return colon + 1;
                        }
                    }

                    Scan(s, p + 1, e);
                    return e < end ? e + 1 : e;
                }

                case "using" or "typedef" or "struct" or "class" or "union" or "enum" or "static_assert" or "namespace":
                    return ParseDecl(s, p, end);
            }

            if (P(p + 1, ":") && IsName(p) && !P(p + 1, "::"))
            {
                return p + 2; // label
            }
        }

        var stmtEnd = StmtEnd(p, end);
        SimpleOrDecl(s, p, stmtEnd);
        return stmtEnd < end ? stmtEnd + 1 : stmtEnd;
    }

    private int FindTop(int a, int b, string text)
    {
        for (var k = a; k < b; k++)
        {
            if (_t[k].Kind == TokKind.Punct && _t[k].Text is "(" or "[" or "{" && M(k) > k)
            {
                k = M(k);
            }
            else if (P(k, text) && _t[k].Kind == TokKind.Punct)
            {
                return k;
            }
        }

        return -1;
    }

    private void SimpleOrDecl(Scope s, int a, int e)
    {
        if (a >= e)
        {
            return;
        }

        if (LooksLikeDecl(s, a, e))
        {
            ParseLocalDecl(s, a, e);
        }
        else
        {
            Scan(s, a, e);
        }
    }

    private bool LooksLikeDecl(Scope s, int a, int e)
    {
        var i = a;
        while (i < e && Id(i) && _t[i].Text is "const" or "static" or "constexpr" or "volatile" or "register" or "thread_local" or "inline" or "extern" or "mutable" or "typename" or "struct" or "class" or "enum" or "union")
        {
            i++;
        }

        if (i >= e || !Id(i))
        {
            return P(i, "::") && IsName(i + 1) && false;
        }

        var first = _t[i];
        if (Keywords.Contains(first.Text) && !Builtins.Contains(first.Text))
        {
            return false;
        }

        var j = i;
        var builtin = Builtins.Contains(first.Text);
        if (builtin)
        {
            while (j < e && Id(j) && Builtins.Contains(_t[j].Text))
            {
                j++;
            }
        }
        else
        {
            var lookup = s.Lookup(first.Text);
            var ok = lookup is not null && lookup.Any(static x => x.IsType);
            j = i + 1;
            while (true)
            {
                if (P(j, "<"))
                {
                    var c = SkipAngles(j, e);
                    if (c < 0)
                    {
                        break;
                    }

                    j = c + 1;
                    ok = ok || lookup is null;
                }

                if (P(j, "::") && IsName(j + 1))
                {
                    j += 2;
                    ok = lookup is null || ok;
                    lookup = null;
                    continue;
                }

                break;
            }

            if (!ok && !(lookup is null && Id(j) && !Keywords.Contains(_t[j].Text)))
            {
                return false;
            }

            if (lookup is not null && !lookup.Any(static x => x.IsType))
            {
                return false;
            }
        }

        while (j < e && (P(j, "*") || P(j, "&") || P(j, "&&") || (Id(j) && _t[j].Text is "const" or "volatile" or "__restrict")))
        {
            j++;
        }

        if (j >= e)
        {
            return false;
        }

        if (P(j, "[") && first.Text == "auto")
        {
            return true;
        }

        if (!IsName(j))
        {
            return false;
        }

        var nx = j + 1;
        return nx >= e || (_t[nx].Kind == TokKind.Punct && _t[nx].Text is "=" or ";" or "," or "(" or "{" or "[" or ":");
    }

    private void ParseLocalDecl(Scope s, int a, int e)
    {
        // structured bindings: auto [x, y] = ...;
        var k = a;
        while (k < e && Id(k) && _t[k].Text is "const" or "static" or "auto" or "volatile")
        {
            k++;
        }

        if (P(k, "[") && M(k) > k && k > a)
        {
            for (var x = k + 1; x < M(k); x++)
            {
                if (IsName(x))
                {
                    var nt = _t[x];
                    var ent = NewEntity(s, nt.Text, EKind.Local, D(nt.Text, SymbolDescriptor.Types.Suffix.Local), nt, SymbolInformation.Types.Kind.Variable);
                    ent.Defined = true;
                    Emit(nt, ent, true);
                }
            }

            Scan(s, M(k) + 1, e);
            return;
        }

        var save = _pass2;
        ParseFuncOrVarLocal(s, a, e);
        _pass2 = save;
    }

    private void ParseFuncOrVarLocal(Scope s, int a, int e)
    {
        // Local declarations never declare functions here; reuse the variable path.
        var stmtEnd = e;
        var isStatic = false;
        for (var i = a; i < e && Id(i); i++)
        {
            isStatic |= _t[i].Text == "static";
        }

        var term = a;
        while (term < stmtEnd)
        {
            if (_t[term].Kind == TokKind.Punct && _t[term].Text is "(" or "[" or "{" or "=" or ";" or "," or ":" )
            {
                if (_t[term].Text == "[" && P(term + 1, "["))
                {
                    term = M(term) + 1;
                    continue;
                }

                break;
            }

            if (P(term, "<") && Id(term - 1))
            {
                var c = SkipAngles(term, stmtEnd);
                if (c > 0)
                {
                    term = c + 1;
                    continue;
                }
            }

            term++;
        }

        var nameI = term - 1;
        if (nameI < a || !IsName(nameI))
        {
            Scan(s, a, e);
            return;
        }

        ParseDeclarators(s, nameI, e + 1, MakeTypeRef(s, a, nameI), false, isStatic, a, nameI, nameI, false);
    }

    // ---- reference scanning --------------------------------------------------------------------------------

    private Entity? ResolveSegments(Scope scope, List<int> segs, bool emit)
    {
        Entity? cur = null;
        for (var i = 0; i < segs.Count; i++)
        {
            var tok = _t[segs[i]];
            List<Entity>? cands;
            if (i == 0)
            {
                cands = P(segs[0] - 1, "::") && !Id(segs[0] - 2) ? Global.LookupHere(tok.Text) : scope.Lookup(tok.Text);
            }
            else
            {
                var holder = cur is { Kind: EKind.Typedef } ? SymbolTable.ResolveType(cur.Type) : cur;
                cands = holder?.Own?.LookupHere(tok.Text);
            }

            var pick = cands?.FirstOrDefault(static x => x.Kind is EKind.Namespace or EKind.Class or EKind.Enum or EKind.Typedef) ?? cands?.FirstOrDefault();
            if (pick is null)
            {
                return null;
            }

            if (emit)
            {
                Emit(tok, pick, false);
            }

            cur = pick;
        }

        if (cur is { Kind: EKind.Typedef })
        {
            cur = SymbolTable.ResolveType(cur.Type) ?? cur;
        }

        return cur;
    }

    private Entity? ResolveQualifierChain(Scope scope, int a, int b, bool emit)
    {
        var segs = new List<int>();
        for (var i = a; i < b; i++)
        {
            if (IsName(i))
            {
                segs.Add(i);
            }
        }

        return ResolveSegments(scope, segs, emit);
    }

    private static Scope? ClassScopeOf(Entity? e)
    {
        if (e is null)
        {
            return null;
        }

        if (e.Kind == EKind.Class)
        {
            return e.Own;
        }

        var t = SymbolTable.ResolveType(e.Type);
        return t?.Kind == EKind.Class ? t.Own : null;
    }

    private int CountArgs(int open)
    {
        var close = M(open);
        if (close < 0 || close == open + 1)
        {
            return close < 0 ? -1 : 0;
        }

        var n = 1;
        for (var k = open + 1; k < close; k++)
        {
            if (_t[k].Kind == TokKind.Punct && _t[k].Text is "(" or "[" or "{" && M(k) > k)
            {
                k = M(k);
            }
            else if (P(k, ",") && _t[k].Kind == TokKind.Punct)
            {
                n++;
            }
        }

        return n;
    }

    private static Entity? Pick(List<Entity>? cands, int argc)
    {
        if (cands is null || cands.Count == 0)
        {
            return null;
        }

        if (cands.Count == 1)
        {
            return cands[0];
        }

        if (argc >= 0)
        {
            var m = cands.FirstOrDefault(c => c.Kind == EKind.Func && argc >= c.MinArgs && argc <= c.MaxArgs);
            if (m is not null)
            {
                return m;
            }
        }

        return cands.FirstOrDefault(static c => c.Kind != EKind.Func) ?? cands[0];
    }

    /// <summary>Scans <c>[a,b)</c> as a type or expression and emits references for every identifier that resolves.</summary>
    private void Scan(Scope scope, int a, int b)
    {
        Entity? last = null;
        Entity? opFn = null;
        var opEnd = -2;
        var lastIdx = -2;
        var i = a;
        while (i < b)
        {
            var t = _t[i];
            if (t.Kind == TokKind.Punct)
            {
                if (t.Text == "[" && IsLambdaStart(i, a, b))
                {
                    i = Lambda(scope, i, b);
                    last = null;
                    continue;
                }

                if (t.Text == "::" && IsName(i + 1) && !(i > a && (Id(i - 1) || P(i - 1, ">"))))
                {
                    i = ScanChain(scope, i + 1, b, true, ref last, ref lastIdx);
                    continue;
                }

                OperatorUse(scope, a, b, i, ref last, ref lastIdx, ref opFn, ref opEnd);
                i++;
                continue;
            }

            if (t.Kind != TokKind.Ident)
            {
                LiteralOperatorUse(scope, i);
                i++;
                continue;
            }

            if (t.Text == "operator")
            {
                i = OperatorKeywordUse(scope, i, b, i > a && P(i - 1, "::") && !(i - 2 >= a && Id(i - 2)));
                continue;
            }

            if (t.Text.StartsWith('_') && i > a && !t.Space && _t[i - 1].Kind is TokKind.StringLiteral or TokKind.CharLiteral && LiteralOperatorUse(scope, i))
            {
                i++;
                continue;
            }

            if (t.Text == "this")
            {
                var cls = scope.NearestClass()?.Owner;
                last = cls;
                lastIdx = i;
                i++;
                continue;
            }

            if (Keywords.Contains(t.Text))
            {
                i++;
                continue;
            }

            if (i > a && (P(i - 1, ".") || P(i - 1, "->")) && _t[i - 1].Kind == TokKind.Punct)
            {
                var recv = lastIdx == i - 2 ? last : null;
                var cls = ClassScopeOf(recv);
                var argc = P(i + 1, "(") ? CountArgs(i + 1) : -1;
                List<Entity>? cands = cls?.LookupHere(t.Text);
                if (cands is null && cls is null && _members.TryGetValue(t.Text, out var all))
                {
                    var distinct = all.Where(static x => x.Kind != EKind.Func || true).ToList();
                    if (distinct.Count == 1 || distinct.All(x => x.DeclScope == distinct[0].DeclScope))
                    {
                        cands = distinct;
                    }
                }

                var ent = Pick(cands, argc);
                if (ent is not null)
                {
                    Emit(t, ent, false);
                }

                last = ent;
                lastIdx = i;
                i++;
                continue;
            }

            i = ScanChain(scope, i, b, false, ref last, ref lastIdx);
        }
    }

    private int ScanChain(Scope scope, int i, int b, bool leading, ref Entity? last, ref int lastIdx)
    {
        Entity? ent = null;
        var j = i;
        while (true)
        {
            var tok = _t[j];
            List<Entity>? cands;
            if (ent is null)
            {
                cands = leading ? Global.LookupHere(tok.Text) : scope.Lookup(tok.Text);
            }
            else
            {
                var holder = ent.Kind == EKind.Typedef ? SymbolTable.ResolveType(ent.Type) : ent;
                cands = holder?.Own?.LookupHere(tok.Text);
            }

            var next = j + 1;
            var argc = P(next, "(") ? CountArgs(next) : -1;
            var picked = Pick(cands, argc);
            if (picked is null)
            {
                // skip the rest of an unresolved qualified name
                while (P(j + 1, "::") && IsName(j + 2))
                {
                    j += 2;
                }

                last = null;
                lastIdx = j;
                return j + 1;
            }

            Emit(tok, picked, false);
            ent = picked;
            var after = next;
            if (P(after, "<"))
            {
                var c = SkipAngles(after, b);
                if (c > 0 && P(c + 1, "::") && IsName(c + 2))
                {
                    Scan(scope, after + 1, c);
                    j = c + 2;
                    continue;
                }
            }

            if (P(after, "::") && IsName(after + 1) && after + 1 < b)
            {
                j = after + 1;
                continue;
            }

            break;
        }

        last = ent;
        lastIdx = j;
        return j + 1;
    }

    private bool IsLambdaStart(int i, int a, int b)
    {
        var close = M(i);
        if (close < 0 || close >= b)
        {
            return false;
        }

        if (i > a && (Id(i - 1) && !Keywords.Contains(_t[i - 1].Text) || P(i - 1, ")") || P(i - 1, "]") || P(i - 1, ">")) && !(Id(i - 1) && _t[i - 1].Text == "return"))
        {
            return false; // subscript
        }

        var n = close + 1;
        return P(n, "(") || P(n, "{") || (Id(n) && _t[n].Text is "mutable" or "noexcept" or "constexpr") || P(n, "->") || P(n, "<");
    }

    private int Lambda(Scope scope, int i, int b)
    {
        var close = M(i);
        Scan(scope, i + 1, close); // captures
        var ls = new Scope(scope, SKind.Function) { Path = null };
        var n = close + 1;
        if (P(n, "<") && SkipAngles(n, b) is var c && c > 0)
        {
            n = c + 1;
        }

        if (P(n, "(") && M(n) > n)
        {
            var parms = ParseParams(n, M(n));
            foreach (var pm in parms)
            {
                if (pm.A < pm.TypeEnd)
                {
                    Scan(scope, pm.A, pm.TypeEnd);
                }

                if (pm.NameIdx >= 0)
                {
                    var nt = _t[pm.NameIdx];
                    var e = NewEntity(ls, nt.Text, EKind.Param, D(nt.Text, SymbolDescriptor.Types.Suffix.Parameter), nt, SymbolInformation.Types.Kind.Parameter);
                    e.Type = MakeTypeRef(ls, pm.A, pm.TypeEnd);
                    Emit(nt, e, true);
                }
            }

            n = M(n) + 1;
        }

        while (n < b && !P(n, "{"))
        {
            if (P(n, "->"))
            {
                var e2 = n + 1;
                while (e2 < b && !P(e2, "{"))
                {
                    e2++;
                }

                Scan(scope, n + 1, e2);
                n = e2;
                break;
            }

            n++;
        }

        if (P(n, "{") && M(n) > n)
        {
            Block(ls, n + 1, M(n));
            return M(n) + 1;
        }

        return Math.Min(n + 1, b);
    }
}

internal static class AstHash
{
    /// <summary>Lower-case hex of the first <paramref name="bytes"/> bytes of SHA-256 (upstream uses a 64-bit hash of the canonical type).</summary>
    public static string Hex(string text, int bytes) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)).AsSpan(0, bytes));
}
