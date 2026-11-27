using ScipSharp.Indexer.Pascal.Syntax;
using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

internal sealed record ParamDecl(Token Name, string Modifier, PType? Type, string TypeKey, bool HasDefault, string Signature, bool IsBound);

internal sealed partial class Compiler
{
    private static readonly HashSet<string> DirectiveWords = new(StringComparer.Ordinal)
    {
        "forward", "external", "override", "overload", "virtual", "abstract", "static", "dynamic", "reintroduce",
        "message", "inline", "cdecl", "stdcall", "pascal", "register", "safecall", "cppdecl", "far", "near", "export",
        "assembler", "public", "alias", "deprecated", "platform", "experimental", "unimplemented", "final", "varargs",
        "noreturn", "interrupt", "iocheck", "local", "nostackframe", "compilerproc", "weakexternal", "syscall", "winapi",
        "mwpascal", "hardfloat", "softfloat", "vectorcall", "ms_abi_default", "sysv_abi_default", "ms_abi_cdecl",
        "sysv_abi_cdecl", "enumerator", "dispid", "rtlproc", "saveregisters", "oldfpccall", "internproc", "internconst",
        "noinline", "optional", "section", "far16", "cblock", "c",
    };

    private sealed class Heading
    {
        public RoutineKind Kind { get; set; }

        public bool IsClassMethod { get; set; }

        public required Token Start { get; init; }

        public Token Name { get; set; } = null!;

        public string DisplayName { get; set; } = "";

        public Sym? OwnerType { get; set; }

        public bool Qualified { get; set; }

        public List<Token>? TypeParams { get; set; }

        public Scope? TypeParamScope { get; set; }

        public List<ParamDecl> Params { get; set; } = [];

        public bool HasParamList { get; set; }

        public PType? Result { get; set; }

        public Token? ResultName { get; set; }

        public Token HeaderEnd { get; set; } = null!;

        public bool Forward { get; set; }

        public bool External { get; set; }

        public bool Override { get; set; }

        public bool Overload { get; set; }

        public bool Abstract { get; set; }

        public bool Virtual { get; set; }

        public bool VarArgs { get; set; }
    }

    private void RoutineDecl(Ctx ctx)
    {
        var mark = Mark();
        var h = ParseHeading(ctx);
        if (h is null)
        {
            Restore(mark);
            return;
        }

        var hasBody = ctx.MemberOf is null && ctx.AllowBodies && !h.Forward && !h.External;
        var (sym, parameters, occ) = BindRoutine(ctx, h, hasBody);
        if (hasBody)
        {
            Body(sym, parameters, h, occ);
        }

        Restore(mark);
    }

    private Heading? ParseHeading(Ctx ctx)
    {
        var h = new Heading { Start = Cur };
        if (AcceptWord("class"))
        {
            h.IsClassMethod = true;
        }

        AcceptWord("generic");
        var kw = Next();
        h.Kind = kw.Lower switch
        {
            "function" => RoutineKind.Function,
            "constructor" => RoutineKind.Constructor,
            "destructor" => RoutineKind.Destructor,
            "operator" => RoutineKind.Operator,
            _ => RoutineKind.Procedure,
        };

        if (h.Kind == RoutineKind.Operator)
        {
            if (AtEof || IsSym(";"))
            {
                return null;
            }

            // class operator TRec.Add(...) / TRec.:=(...) / TRange.in(...) in an implementation section.
            var parts = new List<Token>();
            while (IsIdent && Peek().IsSymbol(".") && ctx.MemberOf is null)
            {
                parts.Add(Next());
                Next();
            }

            h.Name = Next();
            h.DisplayName = h.Name.Text;
            if (parts.Count > 0)
            {
                parts.Add(h.Name);
                h.Qualified = true;
                ResolveQualifiers(h, parts);
            }
        }
        else
        {
            if (!IsNameLike)
            {
                SkipToSemicolon();
                return null;
            }

            var parts = new List<Token> { Next() };
            List<Token>? tps = null;
            if (IsSym("<") && GenericParamsFollow())
            {
                tps = TypeParamNames();
            }

            while (IsSym(".") && Peek().Kind is TokenKind.Identifier or TokenKind.Keyword)
            {
                Next();
                parts.Add(Next());
                tps = null;
                if (IsSym("<") && GenericParamsFollow())
                {
                    tps = TypeParamNames();
                }
            }

            h.Name = parts[^1];
            h.DisplayName = h.Name.Text;
            h.TypeParams = tps;
            if (parts.Count > 1)
            {
                if (ctx.MemberOf is not null)
                {
                    // Interface method resolution clause: procedure IFoo.Bar = MyBar;
                    SkipToSemicolon();
                    return null;
                }

                h.Qualified = true;
                ResolveQualifiers(h, parts);
            }
        }

        if (h.TypeParams is { Count: > 0 })
        {
            h.TypeParamScope = new Scope(null);
            for (var i = 0; i < h.TypeParams.Count; i++)
            {
                var tok = h.TypeParams[i];
                var tp = new Sym(tok.Text, SymKind.TypeParam, null) { ScipKind = ScipKind.TypeParameter, Index = i, HomeSource = tok.Source, Signature = tok.Text };
                tp.Type = new PType(TypeKind.TypeParam) { Decl = tp };
                h.TypeParamScope.Add(tp);
            }

            Push(h.TypeParamScope);
        }

        if (IsSym("("))
        {
            h.HasParamList = true;
            h.Params = ParseParams(ctx);
        }

        if (h.Kind == RoutineKind.Operator && IsIdent && Peek().IsSymbol(":"))
        {
            h.ResultName = Next();
        }

        if (Accept(":"))
        {
            h.Result = TypeSpec(null, ctx).Type;
        }

        h.HeaderEnd = Prev;
        ParseDirectives(h);
        return h;
    }

    private bool GenericParamsFollow()
    {
        if (!IsSym("<"))
        {
            return false;
        }

        for (var k = 1; k < 64; k++)
        {
            var t = Peek(k);
            if (t.IsSymbol(">"))
            {
                return true;
            }

            if (!(t.IsIdentifier || t.IsSymbol(",") || t.IsSymbol(":") || t.IsSymbol(";") || t.IsWord("class") || t.IsWord("record") || t.IsWord("constructor") || t.IsSymbol(".")))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Resolves <c>TOuter.TInner</c> of <c>procedure TOuter.TInner.Method</c> and opens their member scopes.</summary>
    private void ResolveQualifiers(Heading h, List<Token> parts)
    {
        Sym? owner = null;
        for (var i = 0; i < parts.Count - 1; i++)
        {
            var q = parts[i];
            List<Sym>? found = owner is null ? Lookup(q.Text) : owner.Type is { } ot ? Types.FindMember(ot, q.Text) : null;
            var typeSym = found?.FirstOrDefault(static s => s.Kind == SymKind.Type);
            if (typeSym is null)
            {
                h.OwnerType = null;
                return;
            }

            Emit.Reference(q, typeSym);
            owner = typeSym;
            if (typeSym.TypeParams is { Count: > 0 } tps)
            {
                var tpScope = new Scope(typeSym);
                foreach (var tp in tps)
                {
                    tpScope.Add(tp);
                }

                Push(tpScope);
            }

            if (typeSym.Type is { } t)
            {
                Push(new MemberLayer(t, Types));
            }
        }

        h.OwnerType = owner;
    }

    /// <summary>Parses a parameter list in parentheses (or brackets for indexed properties).</summary>
    private List<ParamDecl> ParseParams(Ctx ctx, string close = ")")
    {
        var result = new List<ParamDecl>();
        Next(); // ( or [
        while (!AtEof && !IsSym(close))
        {
            var before = _i;
            var modifier = "";
            if (Cur.Lower is "const" or "var" or "out" or "constref" && Cur.Kind is TokenKind.Keyword or TokenKind.Identifier && (Peek().IsIdentifier || Peek().IsSymbol("[")))
            {
                modifier = Next().Lower;
            }

            if (IsSym("["))
            {
                SkipBalanced(); // attributes / constref modifier [ref]
            }

            if (IsKw("procedure") || IsKw("function"))
            {
                // ISO procedural / functional parameter.
                var isFunction = IsKw("function");
                Next();
                if (IsIdent)
                {
                    var pname = Next();
                    var pt = new PType(TypeKind.Proc);
                    if (IsSym("("))
                    {
                        foreach (var inner in ParseParams(ctx))
                        {
                            Emit.Definition(inner.Name, new Sym(inner.Name.Text, SymKind.Param, ctx.Owner)
                            {
                                ScipKind = ScipKind.Parameter,
                                Type = inner.Type,
                                HomeSource = inner.Name.Source,
                                Signature = inner.Signature,
                            });
                        }
                    }

                    if (isFunction && Accept(":"))
                    {
                        pt.Result = TypeSpec(null, ctx).Type;
                    }

                    result.Add(new ParamDecl(pname, isFunction ? "function" : "procedure", pt, isFunction ? "function" : "procedure", false, $"{(isFunction ? "function" : "procedure")} {pname.Text}", false));
                }

                Accept(";");
                continue;
            }

            var names = new List<Token>();
            while (IsIdent || DemoteKeywordName())
            {
                names.Add(Next());
                if (!Accept(","))
                {
                    break;
                }
            }

            PType? type = null;
            var typeText = "";
            var bounds = new List<ParamDecl>();
            if (Accept(":"))
            {
                var typeStart = Cur;
                type = IsConformantArray() ? ConformantArray(ctx, bounds) : TypeSpec(null, ctx).Type;
                typeText = Slice(typeStart, Prev, 200);
            }

            var hasDefault = false;
            if (Accept("="))
            {
                hasDefault = true;
                Expr();
            }

            var key = (modifier + ":" + typeText).ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal);
            foreach (var n in names)
            {
                var sig = (modifier.Length > 0 ? modifier + " " : "") + n.Text + (typeText.Length > 0 ? ": " + typeText : "");
                result.Add(new ParamDecl(n, modifier, type, key, hasDefault, sig, false));
            }

            result.AddRange(bounds);
            if (!Accept(";") && _i == before)
            {
                Next();
            }
        }

        Accept(close);
        return result;
    }

    private bool IsConformantArray()
    {
        var k = IsKw("packed") ? 1 : 0;
        return Peek(k).IsKeyword("array") && Peek(k + 1).IsSymbol("[") && Peek(k + 2).IsIdentifier && Peek(k + 3).IsSymbol("..")
            && Peek(k + 4).IsIdentifier && Peek(k + 5).IsSymbol(":");
    }

    /// <summary>ISO conformant array schema <c>array [lo..hi: T] of E</c>; the bound identifiers become parameters.</summary>
    private PType ConformantArray(Ctx ctx, List<ParamDecl> bounds)
    {
        if (IsKw("packed"))
        {
            Next();
        }

        Next(); // array
        Next(); // [
        while (!AtEof && !IsSym("]"))
        {
            var before = _i;
            if (IsIdent && Peek().IsSymbol(".."))
            {
                var lo = Next();
                Next();
                var hi = AcceptIdent();
                Accept(":");
                var ts = Cur;
                var bt = TypeSpec(null, ctx).Type;
                var text = Slice(ts, Prev, 100);
                bounds.Add(new ParamDecl(lo, "bound", bt, "", true, $"{lo.Text}: {text}", true));
                if (hi is not null)
                {
                    bounds.Add(new ParamDecl(hi, "bound", bt, "", true, $"{hi.Text}: {text}", true));
                }
            }

            if (!Accept(";") && _i == before)
            {
                Next();
            }
        }

        Accept("]");
        if (IsKw("of"))
        {
            Next();
        }

        var elem = IsConformantArray() ? ConformantArray(ctx, bounds) : TypeSpec(null, ctx).Type;
        return new PType(TypeKind.Array) { Elem = elem };
    }

    private bool IsDirectiveAt(int offset)
    {
        var t = Peek(offset);
        if (t.Kind != TokenKind.Identifier || !DirectiveWords.Contains(t.Lower))
        {
            return false;
        }

        var n = Peek(offset + 1);
        return !(n.IsSymbol(":") || n.IsSymbol(",") || n.IsSymbol("=") || n.IsSymbol(":="));
    }

    private void ParseDirectives(Heading h)
    {
        while (!AtEof)
        {
            if (IsSym(";") && IsDirectiveAt(1))
            {
                Next();
                continue;
            }

            if (IsDirectiveAt(0))
            {
                var w = Next().Lower;
                switch (w)
                {
                    case "forward":
                        h.Forward = true;
                        break;
                    case "external" or "weakexternal":
                        h.External = true;
                        while (!IsSym(";") && !AtEof && (Cur.Kind is TokenKind.String or TokenKind.Number || IsWord("name") || IsWord("index") || IsWord("delayed") || (IsIdent && !IsDirectiveAt(0) && !Peek().IsSymbol(":"))))
                        {
                            if (IsWord("name") || IsWord("index"))
                            {
                                Next();
                                if (!IsSym(";"))
                                {
                                    Expr();
                                }
                            }
                            else
                            {
                                Next();
                            }
                        }

                        break;
                    case "override":
                        h.Override = true;
                        break;
                    case "overload":
                        h.Overload = true;
                        break;
                    case "abstract":
                        h.Abstract = true;
                        break;
                    case "varargs":
                        h.VarArgs = true;
                        break;
                    case "message" or "dispid":
                        if (!IsSym(";"))
                        {
                            Expr();
                        }

                        break;
                    case "public":
                        if (IsWord("name"))
                        {
                            Next();
                            Expr();
                        }

                        break;
                    case "alias":
                        Accept(":");
                        if (Cur.Kind == TokenKind.String)
                        {
                            Next();
                        }

                        break;
                    case "deprecated" or "experimental":
                        if (Cur.Kind == TokenKind.String)
                        {
                            Next();
                        }

                        break;
                    case "enumerator":
                        AcceptIdent();
                        break;
                    case "virtual" or "dynamic":
                        h.Virtual = true;
                        if (!IsSym(";") && !IsDirectiveAt(0))
                        {
                            Expr();
                        }

                        break;
                }

                continue;
            }

            if (IsSym("[") && ReferenceEquals(h.HeaderEnd, Prev))
            {
                SkipBalanced(); // FPC [public, alias: 'x'] modifiers
                continue;
            }

            break;
        }

        Accept(";");
    }

    private static string SignatureKey(Heading h) => string.Join(";", h.Params.Where(static p => !p.IsBound).Select(static p => p.TypeKey));

    private (Sym Sym, List<Sym> Params, Occurrence? Occurrence) BindRoutine(Ctx ctx, Heading h, bool hasBody)
    {
        var name = h.DisplayName;
        Scope declScope;
        Sym owner;
        var candidates = new List<Sym>();
        if (h.Qualified && h.OwnerType is { Type.Members: { } members } ownerType)
        {
            declScope = members;
            owner = ownerType;
            candidates.AddRange(members.Find(name) ?? []);
        }
        else
        {
            declScope = ctx.Scope;
            owner = ctx.Owner;
            candidates.AddRange(ctx.Scope.Find(name) ?? []);
            if (ReferenceEquals(ctx.Scope, _unit.Implementation))
            {
                candidates.AddRange(_unit.Interface.Find(name) ?? []);
            }
        }

        candidates.RemoveAll(static c => c.Kind != SymKind.Routine || c.Routine is null);
        var key = SignatureKey(h);
        Sym? existing = null;
        if (hasBody)
        {
            var open = candidates.Where(static c => !c.Routine!.HasBody && !c.External).ToList();
            existing = open.FirstOrDefault(c => c.Routine!.SignatureKey == key && h.HasParamList)
                ?? (!h.HasParamList ? open.FirstOrDefault() : null)
                ?? (open.Count == 1 && !open[0].Routine!.IsOverload && !h.Overload ? open[0] : null);
        }

        Sym sym;
        if (existing is not null)
        {
            sym = existing;
        }
        else
        {
            var overload = owner.Symbol is null ? 0 : _ws.NextOverload(owner.Symbol, name);
            var isMethod = owner.Kind == SymKind.Type;
            sym = new Sym(name, SymKind.Routine, owner)
            {
                Symbol = ChildSymbol(owner, Names.Method(name, overload)),
                HomeSource = h.Name.Source,
                IsStatic = h.IsClassMethod,
                ScipKind = h.Kind switch
                {
                    RoutineKind.Constructor => ScipKind.Constructor,
                    RoutineKind.Operator => ScipKind.Operator,
                    _ when isMethod && h.IsClassMethod => ScipKind.StaticMethod,
                    _ when isMethod && h.Abstract => ScipKind.AbstractMethod,
                    _ when isMethod => ScipKind.Method,
                    _ => ScipKind.Function,
                },
            };
            if (ctx.LocalData && owner.Symbol is null)
            {
                sym.Symbol = null;
            }

            sym.Routine = new RoutineInfo(h.Kind) { SignatureKey = key, IsOverload = h.Overload, IsOverride = h.Override, IsForward = h.Forward };
            sym.Signature = Slice(h.Start, h.HeaderEnd, 400);
            sym.Documentation = DocFor(h.Start);
            declScope.Add(sym);
            // override; TP objects re-declare virtual methods with 'virtual' instead.
            if ((h.Override || (h.Virtual && owner.Type?.Kind == TypeKind.Object)) && owner.Type is { } ot)
            {
                foreach (var a in Types.AncestorsOf(ot))
                {
                    if (a.Members?.Find(name)?.FirstOrDefault(static s => s.Kind == SymKind.Routine) is { } overridden)
                    {
                        AddRelationship(sym, overridden, implementation: true);
                        break;
                    }
                }
            }
        }

        var info = sym.Routine!;
        info.Result ??= h.Result;
        if (h.HasParamList || existing is null)
        {
            var real = h.Params.Where(static p => !p.IsBound).ToList();
            info.MinArgs = real.Count(static p => !p.HasDefault);
            info.MaxArgs = h.VarArgs ? int.MaxValue : real.Count;
        }

        if (h.TypeParamScope is { } tps)
        {
            sym.TypeParams = [];
            foreach (var tp in tps.All.OrderBy(static t => t.Index))
            {
                tp.Symbol = ChildSymbol(sym, Names.TypeParameter(tp.Name));
                sym.TypeParams.Add(tp);
            }

            foreach (var tok in h.TypeParams!)
            {
                if (tps.Find(tok.Text) is { } tpl)
                {
                    Emit.Definition(tok, tpl[0]);
                }
            }
        }

        // Parameters: reuse the declaration's locals when the body repeats them in the same document.
        List<Sym> parameters;
        if (!h.HasParamList && existing is not null)
        {
            parameters = info.Params;
        }
        else
        {
            parameters = [];
            var reuse = existing is not null && info.Params.Count == h.Params.Count && ReferenceEquals(info.ParamSource, h.Name.Source);
            for (var i = 0; i < h.Params.Count; i++)
            {
                var p = h.Params[i];
                Sym ps;
                if (reuse && string.Equals(info.Params[i].Name, p.Name.Text, StringComparison.OrdinalIgnoreCase))
                {
                    ps = info.Params[i];
                }
                else
                {
                    ps = new Sym(p.Name.Text, SymKind.Param, sym)
                    {
                        ScipKind = ScipKind.Parameter,
                        Type = p.Type,
                        HomeSource = p.Name.Source,
                        Signature = p.Signature,
                    };
                }

                Emit.Definition(p.Name, ps);
                parameters.Add(ps);
            }

            if (existing is null || info.Params.Count == 0)
            {
                info.Params = parameters;
                info.ParamSource = h.Name.Source;
            }
        }

        if (!hasBody && h.ResultName is { } resultName && info.Result is { } resultType)
        {
            Emit.Definition(resultName, new Sym(resultName.Text, SymKind.Result, sym)
            {
                ScipKind = ScipKind.Variable,
                Type = resultType,
                HomeSource = resultName.Source,
                Signature = $"{resultName.Text}: result",
            });
        }

        var occ = Emit.Definition(h.Name, sym, forward: !hasBody && info.HasBody);
        if (hasBody)
        {
            foreach (var o in info.DeclarationOccurrences)
            {
                o.SymbolRoles = (int)SymbolRole.ForwardDefinition;
            }

            info.DeclarationOccurrences.Clear();
            info.HasBody = true;
            if (existing is not null)
            {
                Emitter.UpdateDocumentation(sym, DocFor(h.Start));
            }
        }
        else if (occ is not null && !info.HasBody)
        {
            info.DeclarationOccurrences.Add(occ);
        }

        return (sym, parameters, occ);
    }

    private void Body(Sym sym, List<Sym> parameters, Heading h, Occurrence? occ)
    {
        var bodyScope = new Scope(sym);
        foreach (var p in parameters)
        {
            bodyScope.Add(p);
        }

        var info = sym.Routine!;
        if (info.Result is { } result)
        {
            if (h.ResultName is { } rn)
            {
                var r = new Sym(rn.Text, SymKind.Result, sym) { ScipKind = ScipKind.Variable, Type = result, HomeSource = rn.Source, Signature = $"{rn.Text}: result" };
                Emit.Definition(rn, r);
                bodyScope.Add(r);
            }
            else if (_mode is not (PascalMode.Iso or PascalMode.ExtendedPascal or PascalMode.Tp))
            {
                bodyScope.Add(new Sym("Result", SymKind.Result, sym) { ScipKind = ScipKind.Variable, Type = result, HomeSource = h.Name.Source, Signature = "Result" });
            }
        }

        var ownerType = sym.Owner is { Kind: SymKind.Type } ot ? ot : null;
        if (ownerType?.Type is { } classType)
        {
            bodyScope.Add(new Sym("Self", SymKind.Self, sym) { Type = classType });
        }

        Push(bodyScope);
        var savedMethod = _currentMethod;
        if (ownerType is not null)
        {
            _currentMethod = sym;
        }

        var bctx = new Ctx { Owner = sym, Scope = bodyScope, LocalData = true, AllowBodies = true };
        Declarations(bctx);
        if (IsKw("begin"))
        {
            Next();
            StatementList("end");
            if (IsKw("end"))
            {
                Next();
            }
        }
        else if (IsKw("asm"))
        {
            SkipAsm();
        }

        var end = Prev;
        Accept(";");
        if (occ is not null && ReferenceEquals(h.Start.Source, end.Source) && end.End > h.Start.Start)
        {
            occ.SetEnclosingSourceRange(h.Start.Source.RangeOf(h.Start.Start, end.End));
        }

        _currentMethod = savedMethod;
    }

    private void SkipAsm()
    {
        Next(); // asm
        while (!AtEof && !IsKw("end"))
        {
            Next();
        }

        if (IsKw("end"))
        {
            Next();
        }

        // optional register list: asm ... end ['eax', 'ebx'];
        if (IsSym("["))
        {
            SkipBalanced();
        }
    }
}
