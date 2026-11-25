using System.Text;

namespace ScipSharp.Indexer.Clang;

/// <summary>
/// Native model of Clang's canonical type string (<c>QualType::getCanonicalType().getAsString()</c> with a default
/// <c>PrintingPolicy</c>, i.e. C-style: <c>_Bool</c>, <c>struct S</c>, <c>void (void)</c>, <c>"> >"</c>).
/// Upstream scip-clang hashes that string with wyhash to build function disambiguators, so reproducing it
/// reproduces the symbol names exactly. Types are parsed from a token slice and resolved against our own scope tree.
/// </summary>
internal sealed class TypeCanon
{
    internal abstract record Ty;

    internal sealed record Leaf(string S, bool Const = false, bool Volatile = false) : Ty;

    internal sealed record Ptr(Ty To, string Quals) : Ty;

    internal sealed record Ref(Ty To, bool RValue) : Ty;

    internal sealed record Arr(Ty Of, string N) : Ty;

    internal sealed record Fn(Ty Ret, List<Ty> Params, bool Variadic, string Tail) : Ty;

    private static readonly HashSet<string> Ignored =
    [
        "static", "extern", "inline", "constexpr", "consteval", "constinit", "virtual", "explicit", "friend", "mutable", "register", "thread_local",
        "typedef", "__inline", "_Noreturn", "__restrict", "restrict", "__extension__", "override", "final",
    ];

    private static readonly HashSet<string> BuiltinWords =
    [
        "void", "bool", "char", "short", "int", "long", "float", "double", "signed", "unsigned", "wchar_t", "char8_t", "char16_t", "char32_t", "__int128", "_Bool", "auto",
    ];

    private static readonly Dictionary<string, string> KnownTypedefs = new()
    {
        ["size_t"] = "unsigned long", ["std::size_t"] = "unsigned long", ["ssize_t"] = "long", ["ptrdiff_t"] = "long", ["std::ptrdiff_t"] = "long",
        ["intptr_t"] = "long", ["uintptr_t"] = "unsigned long", ["int8_t"] = "signed char", ["uint8_t"] = "unsigned char", ["int16_t"] = "short",
        ["uint16_t"] = "unsigned short", ["int32_t"] = "int", ["uint32_t"] = "unsigned int", ["int64_t"] = "long", ["uint64_t"] = "unsigned long",
        ["std::int8_t"] = "signed char", ["std::uint8_t"] = "unsigned char", ["std::int16_t"] = "short", ["std::uint16_t"] = "unsigned short",
        ["std::int32_t"] = "int", ["std::uint32_t"] = "unsigned int", ["std::int64_t"] = "long", ["std::uint64_t"] = "unsigned long",
        ["intmax_t"] = "long", ["uintmax_t"] = "unsigned long", ["off_t"] = "long", ["time_t"] = "long", ["wint_t"] = "unsigned int",
        ["FILE"] = "struct _IO_FILE", ["nullptr_t"] = "std::nullptr_t", ["std::nullptr_t"] = "std::nullptr_t",
        ["std::string"] = "class std::basic_string<char, struct std::char_traits<char>, class std::allocator<char> >",
        ["std::wstring"] = "class std::basic_string<wchar_t, struct std::char_traits<wchar_t>, class std::allocator<wchar_t> >",
        ["std::string_view"] = "class std::basic_string_view<char, struct std::char_traits<char> >",
    };

    private readonly Func<Scope, string, bool, List<Entity>?> _lookup;
    private int _depth;

    public TypeCanon(Func<Scope, string, bool, List<Entity>?> lookup) => _lookup = lookup;

    // ---- public API ---------------------------------------------------------------------------------------

    public Ty Parse(Scope sc, IReadOnlyList<Tok> toks)
    {
        if (_depth > 12)
        {
            return new Leaf("int");
        }

        _depth++;
        try
        {
            var arr = toks as Tok[] ?? [.. toks];
            return new Parser(this, sc, arr).ParseFull();
        }
        finally
        {
            _depth--;
        }
    }

    public static Ty AdjustParam(Ty t) => t switch
    {
        Arr a => new Ptr(a.Of, ""),
        Fn f => new Ptr(f, ""),
        Leaf l => l with { Const = false, Volatile = false },
        Ptr p => p with { Quals = "" },
        _ => t,
    };

    public static string Print(Ty t)
    {
        var sb = new StringBuilder();
        Before(t, true, sb);
        After(t, true, sb);
        return sb.ToString();
    }

    private static void Before(Ty t, bool phEmpty, StringBuilder sb)
    {
        switch (t)
        {
            case Leaf l:
                if (l.Const)
                {
                    sb.Append("const ");
                }

                if (l.Volatile)
                {
                    sb.Append("volatile ");
                }

                sb.Append(l.S);
                if (!phEmpty)
                {
                    sb.Append(' ');
                }

                break;
            case Ptr p:
                Before(p.To, false, sb);
                if (p.To is Arr)
                {
                    sb.Append('(');
                }

                sb.Append('*').Append(p.Quals);
                if (p.Quals.Length > 0 && !phEmpty)
                {
                    sb.Append(' ');
                }

                break;
            case Ref r:
                Before(r.To, false, sb);
                if (r.To is Arr)
                {
                    sb.Append('(');
                }

                sb.Append(r.RValue ? "&&" : "&");
                break;
            case Arr a:
                Before(a.Of, false, sb);
                break;
            case Fn f:
                Before(f.Ret, false, sb);
                if (!phEmpty)
                {
                    sb.Append('(');
                }

                break;
        }
    }

    private static void After(Ty t, bool phEmpty, StringBuilder sb)
    {
        switch (t)
        {
            case Ptr p:
                if (p.To is Arr)
                {
                    sb.Append(')');
                }

                After(p.To, false, sb);
                break;
            case Ref r:
                if (r.To is Arr)
                {
                    sb.Append(')');
                }

                After(r.To, false, sb);
                break;
            case Arr a:
                sb.Append('[').Append(a.N).Append(']');
                After(a.Of, false, sb);
                break;
            case Fn f:
                if (!phEmpty)
                {
                    sb.Append(')');
                }

                sb.Append('(');
                if (f.Params.Count == 0 && !f.Variadic)
                {
                    sb.Append("void");
                }

                for (var i = 0; i < f.Params.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(Print(f.Params[i]));
                }

                if (f.Variadic)
                {
                    sb.Append(f.Params.Count > 0 ? ", ..." : "...");
                }

                sb.Append(')').Append(f.Tail);
                After(f.Ret, false, sb);
                break;
        }
    }

    // ---- type parser --------------------------------------------------------------------------------------

    private sealed class Parser
    {
        private readonly TypeCanon _o;
        private readonly Scope _sc;
        private readonly Tok[] _t;
        private readonly int[] _m;

        public Parser(TypeCanon o, Scope sc, Tok[] t)
        {
            _o = o;
            _sc = sc;
            _t = t;
            _m = new int[t.Length];
            Array.Fill(_m, -1);
            var st = new List<int>();
            for (var i = 0; i < t.Length; i++)
            {
                if (t[i].Kind != TokKind.Punct)
                {
                    continue;
                }

                if (t[i].Text is "(" or "[" or "{")
                {
                    st.Add(i);
                }
                else if (t[i].Text is ")" or "]" or "}" && st.Count > 0)
                {
                    _m[st[^1]] = i;
                    _m[i] = st[^1];
                    st.RemoveAt(st.Count - 1);
                }
            }
        }

        private bool P(int i, string s) => i >= 0 && i < _t.Length && _t[i].Kind is TokKind.Punct or TokKind.Ident && _t[i].Text == s;

        private bool IsId(int i) => i >= 0 && i < _t.Length && _t[i].Kind == TokKind.Ident;

        public Ty ParseFull() => ParseRange(0, _t.Length);

        private Ty ParseRange(int a, int b)
        {
            var i = a;
            bool c = false, v = false;
            var words = new List<string>();
            Ty? basis = null;
            while (i < b)
            {
                var tk = _t[i];
                if (tk.Kind == TokKind.Punct && tk.Text == "[" && P(i + 1, "[") && _m[i] > 0)
                {
                    i = _m[i] + 1;
                    continue;
                }

                if (tk.Kind == TokKind.Ident)
                {
                    var s = tk.Text;
                    if (Ignored.Contains(s))
                    {
                        i++;
                        continue;
                    }

                    if (s is "__attribute__" or "alignas" or "__declspec" && P(i + 1, "(") && _m[i + 1] > 0)
                    {
                        i = _m[i + 1] + 1;
                        continue;
                    }

                    if (s == "const")
                    {
                        c = true;
                        i++;
                        continue;
                    }

                    if (s == "volatile")
                    {
                        v = true;
                        i++;
                        continue;
                    }

                    if (BuiltinWords.Contains(s))
                    {
                        words.Add(s);
                        i++;
                        continue;
                    }

                    if (s is "struct" or "class" or "union" or "enum" or "typename")
                    {
                        i++;
                        continue;
                    }

                    if (s == "decltype" && P(i + 1, "(") && _m[i + 1] > 0)
                    {
                        basis = new Leaf("decltype(" + string.Concat(_t[(i + 2).._m[i + 1]].Select(static x => x.Text)) + ")");
                        i = _m[i + 1] + 1;
                        continue;
                    }

                    if (basis is null && words.Count == 0)
                    {
                        basis = ParseChain(ref i, b);
                        continue;
                    }

                    break;
                }

                if (tk.Kind == TokKind.Punct && tk.Text == "::" && basis is null && words.Count == 0)
                {
                    basis = ParseChain(ref i, b);
                    continue;
                }

                break;
            }

            var ty = basis ?? new Leaf(BuiltinName(words));
            if (c || v)
            {
                ty = WithQuals(ty, c, v);
            }

            return Declarator(ty, i, b);
        }

        private static Ty WithQuals(Ty ty, bool c, bool v) => ty switch
        {
            Leaf l => l with { Const = l.Const || c, Volatile = l.Volatile || v },
            Ptr p => p with { Quals = Combine(p.Quals, c, v) },
            _ => ty,
        };

        private static string Combine(string existing, bool c, bool v)
        {
            c |= existing.Contains("const", StringComparison.Ordinal);
            v |= existing.Contains("volatile", StringComparison.Ordinal);
            return (c ? "const" : "") + (c && v ? " " : "") + (v ? "volatile" : "");
        }

        private static string BuiltinName(List<string> w)
        {
            if (w.Count == 0)
            {
                return "int";
            }

            var longs = w.Count(static x => x == "long");
            var uns = w.Contains("unsigned");
            var sgn = w.Contains("signed");
            if (w.Contains("void"))
            {
                return "void";
            }

            if (w.Contains("bool") || w.Contains("_Bool"))
            {
                return "_Bool";
            }

            foreach (var simple in new[] { "wchar_t", "char8_t", "char16_t", "char32_t" })
            {
                if (w.Contains(simple))
                {
                    return simple;
                }
            }

            if (w.Contains("auto"))
            {
                return "auto";
            }

            if (w.Contains("float"))
            {
                return "float";
            }

            if (w.Contains("double"))
            {
                return longs > 0 ? "long double" : "double";
            }

            if (w.Contains("__int128"))
            {
                return uns ? "unsigned __int128" : "__int128";
            }

            if (w.Contains("char"))
            {
                return uns ? "unsigned char" : sgn ? "signed char" : "char";
            }

            if (w.Contains("short"))
            {
                return uns ? "unsigned short" : "short";
            }

            if (longs >= 2)
            {
                return uns ? "unsigned long long" : "long long";
            }

            if (longs == 1)
            {
                return uns ? "unsigned long" : "long";
            }

            return uns ? "unsigned int" : "int";
        }

        // -- declarators

        private Ty Declarator(Ty ty, int i, int b)
        {
            // pointer operators
            var pack = false;
            while (i < b)
            {
                if (P(i, "*"))
                {
                    i++;
                    bool c = false, v = false;
                    while (i < b && IsId(i) && _t[i].Text is "const" or "volatile" or "__restrict" or "restrict" or "__restrict__")
                    {
                        c |= _t[i].Text == "const";
                        v |= _t[i].Text == "volatile";
                        i++;
                    }

                    ty = new Ptr(ty, Combine("", c, v));
                }
                else if (P(i, "&"))
                {
                    ty = new Ref(ty, false);
                    i++;
                }
                else if (P(i, "&&"))
                {
                    ty = new Ref(ty, true);
                    i++;
                }
                else if (P(i, "..."))
                {
                    pack = true;
                    i++;
                }
                else if (IsId(i) && P(i + 1, "::") && P(i + 2, "*"))
                {
                    var cls = _t[i].Text;
                    i += 3;
                    ty = new Leaf(Print(ty) + " " + cls + "::*");
                }
                else
                {
                    break;
                }
            }

            if (pack)
            {
                ty = new Leaf(Print(ty) + "...");
            }

            // direct declarator: optional grouping, optional name
            int gs = -1, ge = -1;
            if (P(i, "(") && _m[i] > i && i + 1 < b && (P(i + 1, "*") || P(i + 1, "&") || P(i + 1, "&&") || (IsId(i + 1) && P(i + 2, "::") && P(i + 3, "*")) || P(i + 1, "(")))
            {
                gs = i + 1;
                ge = _m[i];
                i = ge + 1;
            }
            else if (IsId(i) && _t[i].Text is not ("const" or "volatile" or "noexcept"))
            {
                i++;
            }

            var suffixes = new List<(bool IsArr, string N, int PA, int PB, string Tail, int TrailA, int TrailB)>();
            while (i < b)
            {
                if (P(i, "[") && _m[i] > i)
                {
                    var n = string.Concat(_t[(i + 1).._m[i]].Select(static x => x.Text));
                    suffixes.Add((true, n, 0, 0, "", 0, 0));
                    i = _m[i] + 1;
                }
                else if (P(i, "(") && _m[i] > i)
                {
                    var pa = i + 1;
                    var pb = _m[i];
                    i = pb + 1;
                    var tail = new StringBuilder();
                    int ta = -1, tb = -1;
                    while (i < b)
                    {
                        if (P(i, "const"))
                        {
                            tail.Append(" const");
                        }
                        else if (P(i, "volatile"))
                        {
                            tail.Append(" volatile");
                        }
                        else if (P(i, "&"))
                        {
                            tail.Append(" &");
                        }
                        else if (P(i, "&&"))
                        {
                            tail.Append(" &&");
                        }
                        else if (P(i, "noexcept"))
                        {
                            if (P(i + 1, "(") && _m[i + 1] > 0)
                            {
                                var inner = string.Concat(_t[(i + 2).._m[i + 1]].Select(static x => x.Text));
                                if (inner != "false")
                                {
                                    tail.Append(" noexcept");
                                }

                                i = _m[i + 1];
                            }
                            else
                            {
                                tail.Append(" noexcept");
                            }
                        }
                        else if (P(i, "->"))
                        {
                            ta = i + 1;
                            tb = b;
                            i = b;
                            break;
                        }
                        else
                        {
                            break;
                        }

                        i++;
                    }

                    suffixes.Add((false, "", pa, pb, tail.ToString(), ta, tb));
                }
                else
                {
                    break;
                }
            }

            for (var k = suffixes.Count - 1; k >= 0; k--)
            {
                var s = suffixes[k];
                if (s.IsArr)
                {
                    ty = new Arr(ty, s.N);
                }
                else
                {
                    var ret = s.TrailA >= 0 ? new Parser(_o, _sc, _t[s.TrailA..s.TrailB]).ParseFull() : ty;
                    ty = BuildFn(ret, s.PA, s.PB, s.Tail);
                }
            }

            if (gs >= 0)
            {
                ty = Declarator(ty, gs, ge);
            }

            return ty;
        }

        private Fn BuildFn(Ty ret, int pa, int pb, string tail)
        {
            var ps = new List<Ty>();
            var variadic = false;
            var start = pa;
            for (var k = pa; k <= pb; k++)
            {
                if (k < pb && _t[k].Kind == TokKind.Punct && _t[k].Text is "(" or "[" or "{" && _m[k] > k)
                {
                    k = _m[k];
                    continue;
                }

                if (k < pb && P(k, "<") && IsId(k - 1))
                {
                    var c = AngleEnd(k, pb);
                    if (c > 0)
                    {
                        k = c;
                        continue;
                    }
                }

                if (k == pb || P(k, ","))
                {
                    var seg = _t[start..k];
                    start = k + 1;
                    if (seg.Length == 0 || (seg.Length == 1 && seg[0].Text == "void"))
                    {
                        continue;
                    }

                    if (seg.Length == 1 && seg[0].Text == "...")
                    {
                        variadic = true;
                        continue;
                    }

                    var eq = Array.FindIndex(seg, static x => x.Kind == TokKind.Punct && x.Text == "=");
                    if (eq >= 0)
                    {
                        seg = seg[..eq];
                    }

                    ps.Add(AdjustParam(new Parser(_o, _sc, seg).ParseFull()));
                }
            }

            return new Fn(ret, ps, variadic, tail);
        }

        // -- names

        private int AngleEnd(int i, int limit)
        {
            var depth = 0;
            for (var k = i; k < limit; k++)
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
                    case ">>":
                        depth -= 2;
                        if (depth <= 0)
                        {
                            return k;
                        }

                        break;
                    case "(" or "[" or "{":
                        if (_m[k] < 0)
                        {
                            return -1;
                        }

                        k = _m[k];
                        break;
                }
            }

            return -1;
        }

        private Ty ParseChain(ref int i, int b)
        {
            var leading = false;
            if (P(i, "::"))
            {
                leading = true;
                i++;
            }

            var segs = new List<(string Name, string[]? Args)>();
            while (i < b && IsId(i))
            {
                var name = _t[i].Text;
                i++;
                string[]? args = null;
                if (P(i, "<") && AngleEnd(i, b) is var e and > 0)
                {
                    args = SplitArgs(i + 1, e);
                    i = e + 1;
                }

                segs.Add((name, args));
                if (P(i, "::") && IsId(i + 1))
                {
                    i++;
                    continue;
                }

                break;
            }

            return Resolve(segs, leading);
        }

        private string[] SplitArgs(int a, int e)
        {
            var res = new List<string>();
            var start = a;
            for (var k = a; k < e; k++)
            {
                var t = _t[k];
                if (t.Kind == TokKind.Punct && t.Text is "(" or "[" or "{" && _m[k] > k)
                {
                    k = _m[k];
                    continue;
                }

                if (t.Kind == TokKind.Punct && t.Text == "<" && IsId(k - 1) && AngleEnd(k, e + 1) is var c and > 0 && c < e + 1)
                {
                    k = c;
                    continue;
                }

                if (t.Kind == TokKind.Punct && t.Text == ",")
                {
                    res.Add(ArgText(start, k));
                    start = k + 1;
                }
            }

            if (e > a)
            {
                res.Add(ArgText(start, e));
            }

            return [.. res];
        }

        private string ArgText(int a, int b)
        {
            var seg = _t[a..b];
            if (seg.Length == 0)
            {
                return "";
            }

            if (seg.All(static x => x.Kind is TokKind.Number or TokKind.CharLiteral or TokKind.StringLiteral) || (seg.Length == 1 && seg[0].Text is "true" or "false"))
            {
                return string.Concat(seg.Select(static x => x.Text));
            }

            if (seg.Length == 2 && seg[0].Kind == TokKind.Punct && seg[0].Text == "-")
            {
                return "-" + seg[1].Text;
            }

            return Print(_o.Parse(_sc, seg));
        }

        private Ty Resolve(List<(string Name, string[]? Args)> segs, bool leading)
        {
            Entity? cur = null;
            var idx = 0;
            var depAt = -1;
            for (; idx < segs.Count; idx++)
            {
                var name = segs[idx].Name;
                List<Entity>? found;
                if (idx == 0)
                {
                    found = _o._lookup(_sc, name, leading);
                }
                else
                {
                    found = cur?.Own?.LookupHere(name);
                    if (found is null && cur is { Kind: EKind.Typedef } td && SymbolTable.ResolveType(td.Type)?.Own is { } tos)
                    {
                        found = tos.LookupHere(name);
                    }
                }

                var next = found?.FirstOrDefault(static e => e.Kind is EKind.Class or EKind.Enum or EKind.Typedef or EKind.TemplateParam or EKind.Namespace);
                if (next is null)
                {
                    break;
                }

                cur = next;
                if (cur.Kind == EKind.Class && segs[idx].Args is { } da && da.Any(static a => a.Contains("type-parameter", StringComparison.Ordinal)) && idx < segs.Count - 1)
                {
                    depAt = idx;
                    break;
                }

                if (cur.Kind == EKind.Typedef && idx < segs.Count - 1)
                {
                    var target = SymbolTable.ResolveType(cur.Type);
                    if (target is null)
                    {
                        break;
                    }

                    cur = target;
                }
            }

            if (cur is not null && idx == segs.Count)
            {
                return FromEntity(cur, segs[^1].Args);
            }

            if (depAt >= 0 && cur is not null)
            {
                // member of a dependent specialisation: printed as written, with canonical arguments
                var sbd = new StringBuilder(QualName(cur));
                AppendArgs(sbd, segs[depAt].Args);
                for (var k = depAt + 1; k < segs.Count; k++)
                {
                    sbd.Append("::").Append(segs[k].Name);
                    AppendArgs(sbd, segs[k].Args);
                }

                return new Leaf(sbd.ToString());
            }

            var textual = TextualName(segs, cur, idx);
            if (cur is null && KnownTypedefs.TryGetValue(textual, out var known))
            {
                return new Leaf(known);
            }

            if (cur is { Kind: EKind.TemplateParam })
            {
                return new Leaf("typename " + textual);
            }

            if (cur is null && segs.Count > 0 && segs[0].Name == "std")
            {
                return new Leaf("class " + textual);
            }

            return new Leaf(textual);
        }

        private static string TextualName(List<(string Name, string[]? Args)> segs, Entity? cur, int resolvedCount)
        {
            var sb = new StringBuilder();
            for (var k = 0; k < segs.Count; k++)
            {
                if (k > 0)
                {
                    sb.Append("::");
                }

                if (k == 0 && cur is { Kind: EKind.TemplateParam } && resolvedCount > 0)
                {
                    sb.Append(ParamName(cur));
                }
                else
                {
                    sb.Append(segs[k].Name);
                }

                AppendArgs(sb, segs[k].Args);
            }

            return sb.ToString();
        }

        private static void AppendArgs(StringBuilder sb, string[]? args)
        {
            if (args is null)
            {
                return;
            }

            sb.Append('<').Append(string.Join(", ", args));
            if (sb[^1] == '>')
            {
                sb.Append(' ');
            }

            sb.Append('>');
        }

        private Ty FromEntity(Entity e, string[]? args)
        {
            switch (e.Kind)
            {
                case EKind.TemplateParam:
                    if (args is not null)
                    {
                        var tsb = new StringBuilder(ParamName(e));
                        AppendArgs(tsb, args);
                        return new Leaf(tsb.ToString());
                    }

                    return new Leaf(ParamName(e));
                case EKind.Typedef:
                    if (e.AliasToks is { } toks && e.AliasScope is { } asc)
                    {
                        return _o.Parse(asc, toks);
                    }

                    return new Leaf(QualName(e));
                case EKind.Enum:
                    return new Leaf("enum " + QualName(e));
                default:
                    var kw = e.SymbolKind switch
                    {
                        SymbolInformation.Types.Kind.Struct => "struct",
                        SymbolInformation.Types.Kind.Union => "union",
                        _ => "class",
                    };
                    var dependent = args is not null && args.Any(static a => a.Contains("type-parameter", StringComparison.Ordinal));
                    if (args is null && e.TemplateScope is { } ts && InsideClass(e))
                    {
                        args = [.. ts.TemplateParamNames.Select((_, k) => ParamName(ts, k))];
                        dependent = true;
                    }

                    var sb = new StringBuilder();
                    if (!dependent)
                    {
                        sb.Append(kw).Append(' ');
                    }

                    sb.Append(QualName(e));
                    AppendArgs(sb, args);
                    return new Leaf(sb.ToString());
            }
        }

        private bool InsideClass(Entity e)
        {
            for (var s = _sc; s is not null; s = s.Parent)
            {
                if (s.Owner == e)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // ---- naming helpers -----------------------------------------------------------------------------------

    internal static string ParamName(Entity e)
    {
        var ts = e.DeclScope;
        return ParamName(ts, Math.Max(0, ts.TemplateParamNames.IndexOf(e.Name)), ts.TemplateTemplateNames.Contains(e.Name));
    }

    internal static string ParamName(Scope ts, int index, bool templateTemplate = false)
    {
        var depth = 0;
        for (var s = ts.Parent; s is not null; s = s.Parent)
        {
            if (s.Kind == SKind.Template && s.TemplateParamNames.Count > 0)
            {
                depth++;
            }
        }

        return $"{(templateTemplate ? "template-parameter" : "type-parameter")}-{depth}-{index}";
    }

    internal static string QualName(Entity e)
    {
        var name = e.Name;
        if (name.StartsWith("$anonymous_type", StringComparison.Ordinal))
        {
            name = e.Kind == EKind.Enum ? "(unnamed enum)" : "(unnamed struct)";
        }

        var parts = new List<string> { name };
        for (var s = e.DeclScope; s is not null; s = s.Parent)
        {
            if (s.Owner is { } o && s.Kind is SKind.Namespace or SKind.Class or SKind.Enum)
            {
                parts.Add(o.Name.StartsWith("$anonymous_namespace", StringComparison.Ordinal) ? "(anonymous namespace)" : o.Name);
            }
        }

        parts.Reverse();
        return string.Join("::", parts);
    }
}
