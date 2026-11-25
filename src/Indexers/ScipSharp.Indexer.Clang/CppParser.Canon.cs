namespace ScipSharp.Indexer.Clang;

public sealed partial class CppParser
{
    /// <summary>Index of the <c>operator</c> keyword of an operator-function declarator in <c>[p, open)</c>, or -1.</summary>
    private int FindOperatorKeyword(int p, int open)
    {
        for (var x = open - 1; x >= p && x >= 0; x--)
        {
            if (_t[x].Kind == TokKind.Ident && _t[x].Text == "operator")
            {
                return x;
            }

            if (_t[x].Kind == TokKind.Punct && _t[x].Text is ";" or "{" or "}" or "=")
            {
                return -1;
            }
        }

        return -1;
    }

    /// <summary>Skips the name part of <c>operator ...</c> (not conversion type tokens) and returns the index of the token after it.</summary>
    private int SkipOperatorName(int k, int end)
    {
        var a = k + 1;
        if (a >= end)
        {
            return a;
        }

        if (P(a, "(") && P(a + 1, ")"))
        {
            return a + 2;
        }

        var t = _t[a];
        if (t.Kind == TokKind.Ident && t.Text is "new" or "delete" or "co_await")
        {
            return P(a + 1, "[") && P(a + 2, "]") ? a + 3 : a + 1;
        }

        if (t.Kind == TokKind.StringLiteral)
        {
            return t.Text.Length > 2 || !Id(a + 1) ? a + 1 : a + 2;
        }

        if (t.Kind == TokKind.Punct)
        {
            return t.Text == "[" && P(a + 1, "]") ? a + 2 : a + 1;
        }

        return a; // conversion function: the type tokens are scanned normally
    }

    /// <summary>Spelling of an operator function name as Clang prints it (conversion operators use the canonical type).</summary>
    private string OperatorName(Scope scope, int opIdx, int last)
    {
        var a = opIdx + 1;
        if (a > last)
        {
            return "operator";
        }

        if (P(a, "(") && P(a + 1, ")") && a + 1 == last)
        {
            return "operator()";
        }

        var t = _t[a];
        if (t.Kind == TokKind.Ident && t.Text is "new" or "delete" or "co_await")
        {
            return "operator " + t.Text + (P(a + 1, "[") && P(a + 2, "]") ? "[]" : "");
        }

        if (t.Kind == TokKind.StringLiteral)
        {
            var suffix = t.Text.Length > 2 && t.Text.StartsWith("\"\"", StringComparison.Ordinal) ? t.Text[2..] : a + 1 <= last && Id(a + 1) ? _t[a + 1].Text : "";
            return "operator\"\"" + suffix;
        }

        if (t.Kind == TokKind.Punct)
        {
            return "operator" + string.Concat(Enumerable.Range(a, last - a + 1).Select(i => _t[i].Text));
        }

        var ty = _canon.Parse(scope, _t.GetRange(a, last - a + 1));
        return "operator " + TypeCanon.Print(ty);
    }

    private static readonly Tok VoidTok = new(TokKind.Ident, "void", 0, 0, 0, 0, false, false);

    /// <summary>
    /// Builds the canonical function type string that upstream hashes into the disambiguator, and a parameter-only
    /// signature used to match redeclarations. Returns (parameter signature, full canonical type).
    /// </summary>
    private (string Sig, string Type, List<string> Params) CanonicalFunction(Scope sc, int p, int headEnd, int nameFirstIdx, int open, int close, int end, bool ctorOrDtor, string name)
    {
        var toks = new List<Tok>();
        if (ctorOrDtor)
        {
            toks.Add(VoidTok);
        }
        else if (name.StartsWith("operator ", StringComparison.Ordinal) && nameFirstIdx + 1 < open && Id(nameFirstIdx + 1) && _t[nameFirstIdx + 1].Text is not ("new" or "delete" or "co_await"))
        {
            // conversion operator: the return type follows the keyword
            for (var x = nameFirstIdx + 1; x < open; x++)
            {
                toks.Add(_t[x]);
            }
        }
        else
        {
            for (var x = p; x < headEnd && x < _t.Count; x++)
            {
                toks.Add(_t[x]);
            }
        }

        for (var x = open; x <= close; x++)
        {
            toks.Add(_t[x]);
        }

        var r = close + 1;
        while (r < end && r < _t.Count)
        {
            var t = _t[r];
            if (t.Kind == TokKind.Punct && t.Text is "{" or ";" or "=" or ":" or "}" or ",")
            {
                break;
            }

            if (t.Kind == TokKind.Ident && t.Text is "requires" or "override" or "final" or "__attribute__" or "try")
            {
                break;
            }

            if (t.Kind == TokKind.Punct && t.Text == "(" && M(r) > r)
            {
                for (var x = r; x <= M(r); x++)
                {
                    toks.Add(_t[x]);
                }

                r = M(r) + 1;
                continue;
            }

            toks.Add(t);
            r++;
        }

        var hasNoexcept = toks.Skip(close - open).Any(static t => t.Kind == TokKind.Ident && t.Text == "noexcept");
        var defaulted = r + 1 < _t.Count && P(r, "=") && P(r + 1, "default");
        if (!hasNoexcept && (name.StartsWith('~') || name.StartsWith("operator delete", StringComparison.Ordinal) || (defaulted && ctorOrDtor && !toks.Skip(open - open).Any(static t => t.Kind == TokKind.Ident && t.Text == "const"))))
        {
            toks.Add(new Tok(TokKind.Ident, "noexcept", 0, 0, 0, 0, false, false));
        }

        var ty = _canon.Parse(sc, toks);
        var full = TypeCanon.Print(ty);
        if (Environment.GetEnvironmentVariable("SCIP_CLANG_DEBUG_SIG") == "1") { Console.Error.WriteLine("SIG " + name + " :: " + full + " :: " + WyHash.HexOfText(full)); }
        if (ty is TypeCanon.Fn fn)
        {
            var ps = fn.Params.Select(TypeCanon.Print).ToList();
            var sig = string.Join(',', ps) + (fn.Variadic ? ",..." : "") + fn.Tail;
            return (sig, full, ps);
        }

        return (full, full, []);
    }

    /// <summary>
    /// <c>using Base::f;</c>: references every overload found through the qualifier and defines a shadow symbol for each
    /// function in the current scope (as Clang's <c>UsingShadowDecl</c>), so later lookups find the local name.
    /// </summary>
    private bool UsingDeclaration(Scope scope, int p, int e)
    {
        var a = p + 1;
        if (P(a, "typename"))
        {
            a++;
        }

        var segs = new List<int>();
        var x = a;
        if (P(x, "::"))
        {
            x++;
        }

        while (x < e && IsName(x))
        {
            segs.Add(x);
            x++;
            if (P(x, "::") && x + 1 < e)
            {
                x++;
                continue;
            }

            break;
        }

        if (segs.Count < 2 || x != e)
        {
            return false;
        }

        var qual = ResolveSegments(scope, segs.Take(segs.Count - 1).ToList(), emit: true);
        if (qual?.Own is not { } holder)
        {
            return false;
        }

        var nameTok = _t[segs[^1]];
        var cands = holder.LookupHere(nameTok.Text);
        if (cands is null)
        {
            return false;
        }

        var target = scope.Target;
        foreach (var c in cands.ToList())
        {
            Emit(nameTok, c, false);
            if (c.Kind == EKind.Func && target.Path is not null && !InTemplate(scope))
            {
                var desc = D(c.Name, SymbolDescriptor.Types.Suffix.Method, c.Disambiguator);
                var shadow = NewEntity(target, c.Name, EKind.Func, desc, nameTok, c.SymbolKind);
                shadow.Signature = c.Signature;
                shadow.MinArgs = c.MinArgs;
                shadow.MaxArgs = c.MaxArgs;
                shadow.ParamTypes = c.ParamTypes;
                shadow.Type = c.Type;
                shadow.IsStatic = c.IsStatic;
                shadow.Defined = true;
                Emit(nameTok, shadow, true);

                // Uses resolve to the underlying declaration, as in Clang: the shadow only exists as a definition.
                if (target.Names.TryGetValue(c.Name, out var named))
                {
                    named.Remove(shadow);
                }

                if (_members.TryGetValue(c.Name, out var mem))
                {
                    mem.Remove(shadow);
                }
            }
            else if (c.Kind != EKind.Func)
            {
                target.Add(c, nameTok.Text);
            }
        }

        return true;
    }

    private static bool InTemplate(Scope s)
    {
        for (var x = s; x is not null; x = x.Parent)
        {
            if (x.Kind == SKind.Template && x.TemplateParamNames.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Fills <see cref="Entity.Relationships"/> for classes (all transitive bases) and overriding virtual methods.</summary>
    public void ComputeRelationships()
    {
        foreach (var cls in _entities.Where(static e => e.Kind == EKind.Class && e.Own is not null))
        {
            var seen = new HashSet<Entity>();
            CollectBases(cls, seen);
            seen.Remove(cls);
            foreach (var b in seen.OrderBy(static b => b.Symbol, StringComparer.Ordinal))
            {
                cls.Relationships.Add((b.Symbol, false));
            }
        }

        // Virtual methods: direct overrides (nearest declaration along each base path)
        foreach (var cls in _entities.Where(static e => e.Kind == EKind.Class && e.Own is not null))
        {
            foreach (var m in cls.Own!.Names.Values.SelectMany(static l => l).Where(static f => f.Kind == EKind.Func && !f.IsStatic && f.Symbol.Length > 0).ToList())
            {
                var found = new List<Entity>();
                foreach (var b in cls.Bases)
                {
                    if (SymbolTable.ResolveType(b) is { Kind: EKind.Class } baseCls)
                    {
                        FindOverridden(baseCls, m, found, []);
                    }
                }

                if (found.Count > 0)
                {
                    m.IsVirtual = true;
                    foreach (var o in found.Select(static o => o.Symbol).Distinct().OrderBy(static s => s, StringComparer.Ordinal))
                    {
                        m.Relationships.Add((o, true));
                    }
                }
            }
        }
    }

    private static void CollectBases(Entity cls, HashSet<Entity> seen)
    {
        if (!seen.Add(cls))
        {
            return;
        }

        foreach (var b in cls.Bases)
        {
            if (SymbolTable.ResolveType(b) is { Kind: EKind.Class } baseCls)
            {
                CollectBases(baseCls, seen);
            }
        }
    }

    private static void FindOverridden(Entity cls, Entity method, List<Entity> found, HashSet<Entity> seen)
    {
        if (!seen.Add(cls) || cls.Own is null)
        {
            return;
        }

        var hit = cls.Own.Names.TryGetValue(method.Name, out var l) ? l.FirstOrDefault(f => f.Kind == EKind.Func && f.Signature == method.Signature && f.Symbol != method.Symbol && f.Path is not null && f.Path[^1].Name == method.Name) : null;
        if (hit is not null)
        {
            if (hit.IsVirtual)
            {
                found.Add(hit);
            }

            return;
        }

        foreach (var b in cls.Bases)
        {
            if (SymbolTable.ResolveType(b) is { Kind: EKind.Class } baseCls)
            {
                FindOverridden(baseCls, method, found, seen);
            }
        }
    }
}
