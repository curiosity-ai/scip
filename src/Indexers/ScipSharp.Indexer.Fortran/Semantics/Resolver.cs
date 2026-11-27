using ScipSharp.Indexer.Fortran.Syntax;
using static ScipSharp.Indexer.Fortran.Syntax.Toks;
using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Fortran.Semantics;

/// <summary>
/// Pass 2: executes the work recorded by the collector against the complete program (all modules known), resolving
/// names through use and host association, typing <c>%</c> component chains, creating implicitly typed variables,
/// and emits occurrences and SymbolInformation into one <see cref="Document"/> per source file (included files get
/// their own documents).
/// </summary>
internal sealed class Resolver(Workspace ws, SymbolNamer namer)
{
    /// <summary>Statement keywords whose parenthesised operand is not an argument list (so <c>i=</c> inside is an implied DO).</summary>
    private static readonly HashSet<string> NonCallKeywords = new(StringComparer.Ordinal)
    {
        "forall", "concurrent", "data", "while", "if", "where", "case", "rank", "select",
    };

    private sealed class DocBuilder(SourceDoc file)
    {
        public SourceDoc File { get; } = file;

        public Document Document { get; } = new()
        {
            RelativePath = file.RelativePath,
            Language = "Fortran",
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
        };

        public HashSet<(int Line, int Start, int End, string Symbol, int Roles)> Seen { get; } = [];

        public Dictionary<string, SymbolInformation> Infos { get; } = new(StringComparer.Ordinal);

        public Dictionary<Entity, string> Locals { get; } = [];

        public int NextLocal { get; set; }
    }

    private readonly Dictionary<SourceDoc, DocBuilder> _docs = [];
    private readonly HashSet<string> _defined = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entity> _referenced = new(StringComparer.Ordinal);
    private readonly HashSet<string> _relationshipsEmitted = new(StringComparer.Ordinal);

    /// <summary>Expression names that resolved to nothing (diagnostics for tests; intrinsics excluded).</summary>
    public List<Token> Unresolved { get; } = [];

    /// <summary>Runs the recorded actions of every statement of one compilation unit file.</summary>
    public void Run(IEnumerable<Stmt> statements)
    {
        foreach (var st in statements)
        {
            foreach (var action in st.Actions)
            {
                try
                {
                    action(this);
                }
                catch (ArgumentOutOfRangeException)
                {
                }
                catch (IndexOutOfRangeException)
                {
                }
            }
        }
    }

    /// <summary>Finishes and removes the document of <paramref name="file"/> (null when nothing was emitted for it).</summary>
    public Document? Take(SourceDoc file)
    {
        if (!_docs.Remove(file, out var db))
        {
            return null;
        }

        db.Document.Symbols.AddRange(db.Infos.Values);
        return db.Document;
    }

    /// <summary>Documents still open (included files).</summary>
    public IEnumerable<SourceDoc> OpenDocuments => _docs.Keys.ToList();

    /// <summary>SymbolInformation for symbols that are referenced but defined nowhere in the project.</summary>
    public List<SymbolInformation> ExternalSymbols()
    {
        var result = new List<SymbolInformation>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var pending = _referenced.Where(kv => !_defined.Contains(kv.Key) && !done.Contains(kv.Key))
                .OrderBy(static kv => kv.Key, StringComparer.Ordinal).ToList();
            if (pending.Count == 0)
            {
                return result;
            }

            foreach (var (symbol, e) in pending)
            {
                done.Add(symbol);
                result.Add(BuildInfo(Canon(e), symbol, local: false));
            }
        }
    }

    // ---------------- actions ----------------

    public void Define(Token tok, Entity e)
    {
        var target = Canon(e);
        var roles = (int)SymbolRole.Definition;
        if (e.IsForward)
        {
            roles = (int)SymbolRole.ForwardDefinition;
        }
        else if (target.IsExternalDecl)
        {
            roles = 0; // EXTERNAL f / typed external function: a reference to the global procedure
        }
        else if (target.IsProcedureLike && target.DefToken is not null && tok != target.DefToken && e.DefToken != tok)
        {
            roles = 0; // e.g. `real :: f` inside function f, a generic continued in a second interface block
        }

        Emit(tok, e, roles);
    }

    public void RefEntity(Token tok, Entity e) => Emit(tok, e, 0);

    public void RefKey(Token tok, Scope scope, string key, LookupCtx ctx, bool allowImplicit)
    {
        if (tok.File.Macros.Contains(key))
        {
            return;
        }

        var e = ws.Lookup(scope, key, ctx);
        if (e is null && allowImplicit && !Workspace.ImplicitNone(scope) && !ws.HasOpaqueUse(scope))
        {
            e = ImplicitVar(tok, scope);
        }

        if (e is not null && e.Kind != EntityKind.Intrinsic)
        {
            Emit(tok, e, e.DefToken == tok ? (int)SymbolRole.Definition : 0);
        }
    }

    public void RefProc(Token tok, Scope scope)
    {
        var name = tok.Lower;
        if (tok.File.Macros.Contains(name))
        {
            return;
        }

        var e = ws.Lookup(scope, name, LookupCtx.Proc);
        if (e is null)
        {
            if (Intrinsics.IsSubroutine(name))
            {
                return;
            }

            e = ws.ExternalProc(name, tok.Text);
        }

        if (e.Kind != EntityKind.Intrinsic)
        {
            Emit(tok, e, 0);
        }
    }

    public void RefLabel(Token tok, Scope unit)
    {
        if (unit.Labels.TryGetValue(Collector.NormalizeLabel(tok.Text), out var e))
        {
            Emit(tok, e, 0);
        }
    }

    public void RefModule(Token tok, UseInfo u)
    {
        if (ws.Modules.TryGetValue(u.Module, out var m))
        {
            Emit(tok, m, 0);
        }
        else if (!u.Intrinsic && !Workspace.IntrinsicModules.Contains(u.Module))
        {
            Emit(tok, ws.UnknownModule(u.Module, tok.Text), 0);
        }
    }

    public void RefSubmodule(Token tok, string ancestor, string name)
    {
        if (ws.Submodules.TryGetValue(ancestor + ":" + name, out var s))
        {
            Emit(tok, s, 0);
        }
    }

    public void RefUseItem(Token tok, UseInfo u, string remote)
    {
        var e = ws.Export(u, remote, LookupCtx.Expr);
        if (e is not null && e.Kind != EntityKind.Intrinsic)
        {
            Emit(tok, e, (int)SymbolRole.Import);
        }
    }

    public void RefMember(Token tok, Entity type, string name)
    {
        if (ws.FindMember(type, name) is { } m)
        {
            Emit(tok, m, 0);
        }
    }

    public void RefCommon(Token tok)
    {
        if (ws.Commons.TryGetValue(tok.Lower, out var c))
        {
            Emit(tok, c, 0);
        }
    }

    public void SetGuard(Entity assoc, Token? typeTok, Scope scope) =>
        assoc.GuardType = typeTok is null ? null : ws.Lookup(scope, typeTok.Lower, LookupCtx.Type);

    public void InferAssociate(Entity assoc, Stmt st, int from, int to, Scope scope)
    {
        var t = st.Tokens;
        if (!IsName(t, from))
        {
            return;
        }

        var e = ws.Lookup(scope, t[from].Lower, LookupCtx.Expr);
        var j = from + 1;
        while (e is not null && j < to)
        {
            while (j < to && (t[j].Is("(") || t[j].Is("[")))
            {
                j = Match(t, j) + 1;
            }

            if (j + 1 < to && t[j].Is("%") && t[j + 1].IsName)
            {
                var type = ws.TypeOf(e);
                e = type is null ? null : ws.FindMember(type, t[j + 1].Lower);
                j += 2;
                continue;
            }

            break;
        }

        if (e is not null && e != assoc)
        {
            assoc.TypeSource = e;
        }
    }

    /// <summary>Resolves every name in the expression tokens [a, b).</summary>
    public void Walk(Stmt st, int a, int b, Scope scope)
    {
        var t = st.Tokens;
        b = Math.Min(b, t.Count);
        var parens = new Stack<bool>();
        for (var k = a; k < b; k++)
        {
            var tok = t[k];
            switch (tok.Kind)
            {
                case TokenKind.Op:
                    if (tok.Text == "(")
                    {
                        parens.Push(k > 0 && t[k - 1].IsName && !NonCallKeywords.Contains(t[k - 1].Lower));
                    }
                    else if (tok.Text == "[")
                    {
                        parens.Push(false);
                    }
                    else if (tok.Text is ")" or "]" && parens.Count > 0)
                    {
                        parens.Pop();
                    }

                    continue;
                case TokenKind.DotOp:
                    RefOperator(tok, scope);
                    continue;
                case TokenKind.Name:
                    break;
                default:
                    continue;
            }

            if (k > 0 && t[k - 1].Is("%"))
            {
                continue; // component names are resolved by the chain of their base
            }

            if (parens.Count > 0 && parens.Peek() && IsOp(t, k + 1, "=") && k > 0 && (t[k - 1].Is("(") || t[k - 1].Is(",")))
            {
                continue; // keyword argument / I/O specifier / type parameter keyword
            }

            if (tok.File.Macros.Contains(tok.Lower))
            {
                continue;
            }

            var e = ResolveExprName(tok, scope, IsOp(t, k + 1, "("));
            if (e is null)
            {
                if (!Intrinsics.IsFunction(tok.Lower) && !Intrinsics.IsSubroutine(tok.Lower) && ws.Lookup(scope, tok.Lower, LookupCtx.Expr) is null
                    && !ws.HasOpaqueUse(scope))
                {
                    Unresolved.Add(tok);
                }

                continue;
            }

            Emit(tok, e, e.DefToken == tok ? (int)SymbolRole.Definition : 0);
            Chain(t, k, e);
        }
    }

    private void Chain(List<Token> t, int k, Entity e)
    {
        Entity? cur = e;
        var j = k + 1;
        var guard = 0;
        while (cur is not null && guard++ < 64)
        {
            while (j < t.Count && (t[j].Is("(") || t[j].Is("[")))
            {
                j = Match(t, j) + 1;
            }

            if (j + 1 < t.Count && t[j].Is("%") && t[j + 1].IsName)
            {
                var type = ws.TypeOf(cur);
                var member = type is null ? null : ws.FindMember(type, t[j + 1].Lower);
                if (member is not null)
                {
                    Emit(t[j + 1], member, 0);
                }

                cur = member;
                j += 2;
                continue;
            }

            break;
        }
    }

    private void RefOperator(Token tok, Scope scope)
    {
        if (tok.Lower is ".and." or ".or." or ".not." or ".eqv." or ".neqv.")
        {
            if (ws.Lookup(scope, "operator(" + tok.Lower + ")", LookupCtx.Expr) is { Kind: EntityKind.Generic } builtin)
            {
                Emit(tok, builtin, 0);
            }

            return;
        }

        if (ws.Lookup(scope, Collector.OperatorKey(tok), LookupCtx.Expr) is { Kind: EntityKind.Generic } g)
        {
            Emit(tok, g, 0);
        }
    }

    private Entity? ResolveExprName(Token tok, Scope scope, bool paren)
    {
        var name = tok.Lower;
        var e = ws.Lookup(scope, name, LookupCtx.Expr);
        if (e is not null)
        {
            if (e.Kind == EntityKind.Intrinsic)
            {
                return null;
            }

            // FORTRAN 77: a scalar typed name used with an argument list is an external function.
            if (paren && e.Kind == EntityKind.Variable && !e.IsArray && !e.Implicit && e.TypeName is null && e.AliasOf is null
                && e.TypeSource is null && e.Scope.Kind is ScopeKind.Procedure or ScopeKind.Program
                && e.TypeText?.StartsWith("character", StringComparison.OrdinalIgnoreCase) != true && e.Common is null)
            {
                return Intrinsics.IsFunction(name) ? null : ws.ExternalProc(name, tok.Text);
            }

            return e;
        }

        if (tok.IsKindParam)
        {
            return null;
        }

        if (paren)
        {
            return Intrinsics.IsFunction(name) || ws.HasOpaqueUse(scope) ? null : ws.ExternalProc(name, tok.Text);
        }

        if (Workspace.ImplicitNone(scope) || ws.HasOpaqueUse(scope))
        {
            return null;
        }

        return ImplicitVar(tok, scope);
    }

    private Entity? ImplicitVar(Token tok, Scope scope)
    {
        var unit = scope.Unit;
        if (unit.Kind is ScopeKind.File or ScopeKind.DerivedType)
        {
            return null;
        }

        var e = ws.Track(new Entity(tok.Lower, tok.Text, EntityKind.Variable, unit)
        {
            DefToken = tok, Implicit = true, TypeText = Workspace.ImplicitType(unit, tok.Lower),
        });
        unit.Entities[e.Name] = e;
        return e;
    }

    // ---------------- emission ----------------

    private static Entity Canon(Entity e)
    {
        var cur = e;
        for (var i = 0; i < 16 && cur.Canonical is { } c && c != cur; i++)
        {
            cur = c;
        }

        return cur;
    }

    private DocBuilder Doc(SourceDoc file)
    {
        if (!_docs.TryGetValue(file, out var db))
        {
            db = new DocBuilder(file);
            _docs[file] = db;
        }

        return db;
    }

    private void Emit(Token tok, Entity e, int roles)
    {
        if (e.AliasOf is { } alias)
        {
            var outer = e.Scope.Parent is { } p ? ws.Lookup(p, alias, LookupCtx.Expr) : null;
            if (outer is null || outer == e)
            {
                return;
            }

            e = outer;
        }

        var target = Canon(e);
        if (target.Kind == EntityKind.Intrinsic)
        {
            return;
        }

        var db = Doc(tok.File);
        var symbol = namer.Symbol(target);
        var local = symbol is null;
        if (symbol is null)
        {
            var home = target.DefToken?.File ?? target.Scope.File;
            if (home != db.File)
            {
                return; // a local of another document (e.g. an includer's variable used inside an included file)
            }

            if (!db.Locals.TryGetValue(target, out symbol))
            {
                symbol = "local " + db.NextLocal++;
                db.Locals[target] = symbol;
                db.Infos[symbol] = BuildInfo(target, symbol, local: true);
            }
        }

        if (!db.Seen.Add((tok.Line, tok.Col, tok.EndCol, symbol, roles)))
        {
            return;
        }

        var occ = new Occurrence
        {
            Symbol = symbol,
            SymbolRoles = roles,
            SingleLineRange = new SingleLineRange { Line = tok.Line, StartCharacter = tok.Col, EndCharacter = tok.EndCol },
        };
        var isDefinition = (roles & (int)(SymbolRole.Definition | SymbolRole.ForwardDefinition)) != 0;
        if (isDefinition && (target.Body ?? e.Body) is { Start: { } start, End: { } end } && start.File == tok.File && end.File == tok.File
            && (tok == target.DefToken || tok == e.DefToken))
        {
            if (start.Line == end.Line)
            {
                occ.SingleLineEnclosingRange = new SingleLineRange { Line = start.Line, StartCharacter = start.Col, EndCharacter = end.EndCol };
            }
            else
            {
                occ.MultiLineEnclosingRange = new MultiLineRange
                {
                    StartLine = start.Line, StartCharacter = start.Col, EndLine = end.Line, EndCharacter = end.EndCol,
                };
            }
        }

        db.Document.Occurrences.Add(occ);
        if (local)
        {
            return;
        }

        if (isDefinition)
        {
            if (!db.Infos.ContainsKey(symbol))
            {
                db.Infos[symbol] = BuildInfo(target, symbol, local: false);
            }

            _defined.Add(symbol);
        }
        else
        {
            _referenced.TryAdd(symbol, target);
        }
    }

    private SymbolInformation BuildInfo(Entity e, string symbol, bool local)
    {
        var info = new SymbolInformation { Symbol = symbol, DisplayName = DisplayName(e), Kind = KindOf(e) };
        if (Docs.Clean(e.Docs) is { Length: > 0 } doc)
        {
            info.Documentation.Add(doc);
        }
        else if (e.Kind == EntityKind.Unknown)
        {
            info.Documentation.Add(e.IsExternalProc ? "External procedure (not defined in this project)." : "Not defined in this project.");
        }
        else if (e.Implicit)
        {
            info.Documentation.Add("Implicitly typed.");
        }

        if (Signature(e) is { Length: > 0 } sig)
        {
            info.SignatureDocumentation = new Signature { Language = "Fortran", Text = sig };
        }

        if (local && e.Scope.Unit.Owner is { } owner && namer.Symbol(Canon(owner)) is { } enclosing)
        {
            info.EnclosingSymbol = enclosing;
        }

        // Relationships are attached once per symbol (lint warns about repeats across documents).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (target, impl, reference) in local || _relationshipsEmitted.Add(symbol) ? e.Rels : [])
        {
            var c = Canon(target);
            if (namer.Symbol(c) is not { } rs || rs == symbol || !seen.Add(rs))
            {
                continue;
            }

            info.Relationships.Add(new Relationship { Symbol = rs, IsImplementation = impl, IsReference = reference });
            _referenced.TryAdd(rs, c);
        }

        return info;
    }

    private static string DisplayName(Entity e) => e.Kind switch
    {
        EntityKind.CommonBlock => "/" + e.DisplayName + "/",
        _ => e.DisplayName,
    };

    private static Kind KindOf(Entity e) => e.Kind switch
    {
        EntityKind.Program or EntityKind.BlockData => Kind.Namespace,
        EntityKind.Module or EntityKind.Submodule => Kind.Module,
        EntityKind.Subroutine or EntityKind.Function or EntityKind.StatementFunction => Kind.Function,
        EntityKind.Generic => e.Name.StartsWith("operator(", StringComparison.Ordinal) || e.Name.StartsWith("assignment(", StringComparison.Ordinal)
            ? Kind.Operator
            : Kind.Interface,
        EntityKind.AbstractInterface => Kind.Interface,
        EntityKind.DerivedType => e.ParentTypeName is not null || e.Body?.Entities.Values.Any(static m => m.Kind is EntityKind.Binding) == true
            ? Kind.Class
            : Kind.Struct,
        EntityKind.TypeParam => Kind.TypeParameter,
        EntityKind.Component => Kind.Field,
        EntityKind.Binding => e.Attrs.Contains("deferred") ? Kind.AbstractMethod : Kind.Method,
        EntityKind.GenericBinding => e.Name.StartsWith("operator(", StringComparison.Ordinal) ? Kind.Operator : Kind.Method,
        EntityKind.Variable or EntityKind.Namelist or EntityKind.ProcPointer => Kind.Variable,
        EntityKind.NamedConstant => Kind.Constant,
        EntityKind.Dummy => Kind.Parameter,
        EntityKind.CommonBlock => Kind.Struct,
        EntityKind.CommonMember => Kind.StaticField,
        EntityKind.Enumerator => Kind.EnumMember,
        EntityKind.Unknown => e.IsExternalProc ? Kind.Function : e.UnknownModule is { Length: 0 } ? Kind.Module : Kind.Variable,
        _ => Kind.UnspecifiedKind,
    };

    private static string? Signature(Entity e)
    {
        switch (e.Kind)
        {
            case EntityKind.Program or EntityKind.Module or EntityKind.Submodule or EntityKind.BlockData or EntityKind.DerivedType
                or EntityKind.StatementFunction:
                return e.Header;
            case EntityKind.Subroutine or EntityKind.Function or EntityKind.AbstractInterface:
            {
                if (e.Header is null)
                {
                    return null;
                }

                var lines = new List<string> { e.Header };
                foreach (var a in e.Args)
                {
                    lines.Add("  " + Decl(a));
                }

                if (e.Result is { } r)
                {
                    lines.Add("  " + Decl(r));
                }
                else if (e.Kind == EntityKind.Function && e.TypeText is not null && !e.Header.Contains(e.TypeText, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add("  " + e.TypeText + " :: " + e.DisplayName);
                }

                return string.Join('\n', lines);
            }

            case EntityKind.Generic:
            {
                var sig = e.Signature ?? "interface " + e.DisplayName;
                return e.Specifics.Count == 0 ? sig : sig + "\n" + string.Join('\n', e.Specifics.Select(static s => "  procedure :: " + s.Name).Distinct());
            }

            case EntityKind.Binding or EntityKind.GenericBinding or EntityKind.CommonBlock or EntityKind.Label or EntityKind.Namelist:
                return e.Signature;
            case EntityKind.Variable when e.Signature is not null && e.TypeText is null:
                return e.Signature;
            case EntityKind.Variable or EntityKind.Component or EntityKind.NamedConstant or EntityKind.Dummy or EntityKind.CommonMember
                or EntityKind.TypeParam or EntityKind.ProcPointer or EntityKind.Enumerator:
                return Decl(e);
            default:
                return null;
        }
    }

    private static string Decl(Entity e)
    {
        var type = e.TypeText ?? (e.Kind == EntityKind.Enumerator ? "enumerator" : Workspace.ImplicitType(e.Scope, e.Name));
        var head = e.Attrs.Count == 0 ? type : type + ", " + string.Join(", ", e.Attrs);
        var decl = head + " :: " + e.DisplayName + (e.Dims ?? "");
        if (e.Init is { Length: > 0 } init)
        {
            decl += init.StartsWith('=') ? " " + init : " = " + init;
        }

        return decl;
    }
}
