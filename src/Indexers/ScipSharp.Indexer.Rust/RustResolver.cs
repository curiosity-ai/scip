using System.Runtime.CompilerServices;
using System.Text;

namespace ScipSharp.Indexer.Rust;

using Kind = SymbolInformation.Types.Kind;

/// <summary>Parses the tree of a <c>use</c> declaration into leaves.</summary>
internal sealed class UseParser(SourceFileResult file)
{
    private string Text(int i) => Encoding.UTF8.GetString(file.Bytes, file.Tokens[i].Start, file.Tokens[i].End - file.Tokens[i].Start);

    private bool IsP(int i, string s, int end) => i < end && file.Tokens[i].Kind == RustTokenKind.Punct && Text(i) == s;

    /// <summary>Parses tokens <c>[from, end)</c> (the part after the <c>use</c> keyword, including the <c>;</c>).</summary>
    public List<UseBinding> Parse(int from, int end)
    {
        List<UseBinding> result = [];
        var k = from;
        if (IsP(k, "::", end))
        {
            k++;
        }

        Tree([], ref k, end, result);
        return result;
    }

    private void Tree(List<Seg> prefix, ref int k, int end, List<UseBinding> result)
    {
        var path = new List<Seg>(prefix);
        while (k < end)
        {
            if (IsP(k, "{", end))
            {
                k++;
                while (k < end && !IsP(k, "}", end))
                {
                    var before = k;
                    Tree(path, ref k, end, result);
                    if (IsP(k, ",", end))
                    {
                        k++;
                    }
                    else if (k == before)
                    {
                        k++;
                    }
                }

                k++;
                return;
            }

            if (IsP(k, "*", end))
            {
                result.Add(new UseBinding(path, null, -1, glob: true, -1));
                k++;
                return;
            }

            if (file.Tokens[k].Kind != RustTokenKind.Ident)
            {
                k++;
                return;
            }

            var name = Text(k);
            var tok = k;
            k++;
            if (IsP(k, "::", end))
            {
                path.Add(new Seg(name, tok));
                k++;
                continue;
            }

            if (name == "self" && prefix.Count > 0)
            {
                result.Add(new UseBinding(path, path[^1].Name, -1, glob: false, tok));
            }
            else
            {
                path.Add(new Seg(name, tok));
                string? binding = name;
                var aliasTok = -1;
                if (k < end && file.Tokens[k].Kind == RustTokenKind.Ident && Text(k) == "as" && k + 1 < end)
                {
                    aliasTok = k + 1;
                    binding = Text(k + 1) == "_" ? null : Text(k + 1);
                    k += 2;
                }

                result.Add(new UseBinding(path, binding, aliasTok, glob: false, -1));
            }

            return;
        }
    }
}

/// <summary>
/// Second pass over one source file: resolves identifiers and paths to definitions (modules, imports, associated
/// items, std) and emits reference occurrences. Method calls and field accesses are resolved on a best-effort
/// type model of locals, <c>self</c> and fields (see <see cref="RustWorld"/>).
/// </summary>
internal sealed class RustResolver(RustWorld world, FileInfo fi, Dictionary<string, Item> externals)
{
    private readonly SourceFileResult _file = fi.File;
    private readonly List<RustToken> _t = fi.File.Tokens;
    private readonly byte[] _b = fi.File.Bytes;
    private readonly HashSet<int> _emitted = [];
    private readonly Dictionary<string, RType?> _localTypes = [];
    private readonly HashSet<string> _typing = [];
    private readonly Dictionary<int, (RType? Type, int Next)> _typeOnly = [];

    private static readonly HashSet<string> Keywords =
    [
        "as", "async", "await", "break", "const", "continue", "dyn", "else", "enum", "extern", "false", "fn", "for",
        "if", "impl", "in", "let", "loop", "match", "mod", "move", "mut", "pub", "ref", "return", "static", "struct",
        "trait", "true", "type", "unsafe", "use", "where", "while", "yield", "box", "union", "macro_rules", "default",
    ];

    private string Text(int i) => i >= 0 && i < _t.Count ? Encoding.UTF8.GetString(_b, _t[i].Start, _t[i].End - _t[i].Start) : "";

    private bool Is(int i, string s) => i >= 0 && i < _t.Count && _t[i].Kind is RustTokenKind.Punct or RustTokenKind.Ident && Text(i) == s;

    private bool IsIdent(int i) => i >= 0 && i < _t.Count && _t[i].Kind == RustTokenKind.Ident;

    private RustWorld.Ctx At(int tok) => new(fi, tok);

    private int SkipBalanced(int i)
    {
        if (_file.SkipGroup(i) is { } after)
        {
            return after;
        }

        var depth = 0;
        for (; i < _t.Count; i++)
        {
            if (_t[i].Kind != RustTokenKind.Punct)
            {
                continue;
            }

            var s = Text(i);
            if (s is "(" or "[" or "{")
            {
                depth++;
            }
            else if (s is ")" or "]" or "}" && --depth == 0)
            {
                return i + 1;
            }
        }

        return _t.Count;
    }

    private int SkipAngles(int i)
    {
        var depth = 0;
        for (; i < _t.Count; i++)
        {
            if (_t[i].Kind != RustTokenKind.Punct)
            {
                continue;
            }

            var s = Text(i);
            if (s == "<")
            {
                depth++;
            }
            else if (s == ">" && --depth == 0)
            {
                return i + 1;
            }
            else if (s is "(" or "[" or "{")
            {
                i = SkipBalanced(i) - 1;
            }
            else if (s is ";" or "{")
            {
                return i;
            }
        }

        return i;
    }

    // ---- emission ------------------------------------------------------------------------------------

    private void Emit(int tok, Item item)
    {
        if (!_emitted.Add(tok))
        {
            return;
        }

        var t = _t[tok];
        _file.Occurrences.Add(new OccurrenceRecord(t.Start, t.End, item.Symbol, false, null));
        _file.Consumed.Add(tok);
        if (item.IsExternal)
        {
            externals.TryAdd(item.Symbol, item);
        }
    }

    private void EmitLocal(int tok, string symbol)
    {
        if (_emitted.Add(tok))
        {
            var t = _t[tok];
            _file.Occurrences.Add(new OccurrenceRecord(t.Start, t.End, symbol, false, null));
            _file.Consumed.Add(tok);
        }
    }

    // ---- main loop -----------------------------------------------------------------------------------

    public void Run()
    {
        // a `use` can be recorded twice (item re-scanned from a body in malformed input): keep the first
        Dictionary<int, int> uses = [];
        foreach (var (start, end) in fi.File.Uses)
        {
            uses.TryAdd(start, end);
        }

        for (var i = 0; i < _t.Count; i++)
        {
            var tk = _t[i];
            if (uses.TryGetValue(i, out var useEnd))
            {
                ProcessUse(i, useEnd);
                i = useEnd - 1;
                continue;
            }

            if (tk.Kind == RustTokenKind.Punct)
            {
                var p = Text(i);
                if (p == "#" && (Is(i + 1, "[") || (Is(i + 1, "!") && Is(i + 2, "["))))
                {
                    var open = Is(i + 1, "[") ? i + 1 : i + 2;
                    ProcessAttribute(open);
                    i = SkipBalanced(open) - 1;
                }

                continue;
            }

            if (tk.Kind == RustTokenKind.Lifetime)
            {
                if (!_file.Consumed.Contains(i) && At(i).GenericSymbol(Text(i)) is { } ls)
                {
                    EmitLocal(i, ls);
                }

                continue;
            }

            if (tk.Kind == RustTokenKind.Str && Is(i + 1, "."))
            {
                Chain(i + 1, RType.Of(world.StrItem), emit: true, out _);
                continue;
            }

            if (tk.Kind != RustTokenKind.Ident)
            {
                continue;
            }

            var name = Text(i);
            if (name is "crate" or "super" or "self" && Is(i - 1, "(") && Is(i - 2, "pub"))
            {
                // visibility `pub(crate)` / `pub(super)` refer to the module
                if (world.ResolveSegments(At(i), [name], NameSpaces.Type, null) is { } vis)
                {
                    Emit(i, vis);
                }

                continue;
            }

            if (name == "macro_rules" && Is(i + 1, "!"))
            {
                i = SkipBalanced(i + 3) - 1;
                continue;
            }

            if (fi.LocalAtTok.TryGetValue(i, out var local))
            {
                var isDef = fi.LocalDeclTok.TryGetValue(local, out var declTok) && declTok == i;
                if (!isDef && !Is(i - 1, ".") && !Is(i - 1, "::"))
                {
                    EvalExpr(i, emit: true, out _);
                }

                continue;
            }

            if (_file.Consumed.Contains(i) || _emitted.Contains(i) || Keywords.Contains(name) || Is(i - 1, ".") || Is(i - 1, "::"))
            {
                continue;
            }

            if (_t[i].Kind == RustTokenKind.Ident && i > 0 && IsIdent(i - 1) && Text(i - 1) is "fn" or "struct" or "enum" or "trait" or "mod" or "let" or "mut" or "ref"
                && !(Text(i - 1) is "let" or "ref" or "mut" && (char.IsUpper(name[0]) || Is(i + 1, "::") || Is(i + 1, "("))))
            {
                continue;
            }

            var (names, toks, after) = CollectPath(i);
            var lower = char.IsLower(names[0][0]) || names[0][0] == '_';
            if (names.Count == 1 && lower && !(Is(after, "(") || Is(after, "!") || Is(after, "::")) && At(i).GenericSymbol(names[0]) is null)
            {
                continue;
            }

            EvalExpr(i, emit: true, out _);
        }
    }

    /// <summary>
    /// <c>#[test]</c>, <c>#[bench]</c> and <c>#[derive(Debug, ..)]</c> refer to the built-in attribute macros of core
    /// (rust-analyzer emits an occurrence for the attribute and for every derived std macro).
    /// </summary>
    private void ProcessAttribute(int open)
    {
        var nameTok = open + 1;
        if (!IsIdent(nameTok) || Text(nameTok) is not ("test" or "bench" or "derive" or "global_allocator"))
        {
            return;
        }

        if (world.StdAttr(Text(nameTok)) is not { } attr || world.ResolveSegments(At(nameTok), [Text(nameTok)], NameSpaces.Macro, null) is { IsExternal: false, Kind: Kind.Macro })
        {
            return;
        }

        Emit(nameTok, attr);
        if (Text(nameTok) != "derive" || !Is(nameTok + 1, "("))
        {
            return;
        }

        var close = SkipBalanced(nameTok + 1) - 1;
        for (var k = nameTok + 2; k < close; k++)
        {
            if (IsIdent(k) && !Is(k - 1, "::") && !Is(k + 1, "::") && world.StdAttr(Text(k)) is { } derive
                && world.ResolveSegments(At(k), [Text(k)], NameSpaces.Macro, null) is not { IsExternal: false, Kind: Kind.Macro })
            {
                Emit(k, derive);
            }
        }
    }

    private (List<string> Names, List<int> Toks, int After) CollectPath(int i)
    {
        List<string> names = [];
        List<int> toks = [];
        var k = i;
        while (IsIdent(k))
        {
            names.Add(Text(k));
            toks.Add(k);
            k++;
            if (Is(k, "::") && Is(k + 1, "<"))
            {
                k = SkipAngles(k + 1);
            }

            if (Is(k, "::") && IsIdent(k + 1))
            {
                k++;
                continue;
            }

            break;
        }

        return (names, toks, k);
    }

    // ---- use declarations ---------------------------------------------------------------------------

    private void ProcessUse(int start, int end)
    {
        var parser = new UseParser(_file);
        foreach (var b in parser.Parse(start + 1, end))
        {
            var cx = At(b.Path.Count > 0 ? b.Path[0].Tok : start);
            List<Item?> steps = [];
            var names = b.Path.Select(s => s.Name).ToList();
            var target = names.Count == 0 ? null : world.ResolveSegments(cx, names, b.Glob ? NameSpaces.Type : NameSpaces.Any, steps);
            for (var i = 0; i < b.Path.Count && i < steps.Count; i++)
            {
                if (steps[i] is { } it)
                {
                    Emit(b.Path[i].Tok, it);
                }
            }

            if (target is not null)
            {
                if (b.SelfTok >= 0)
                {
                    Emit(b.SelfTok, target);
                }

                if (b.AliasTok >= 0)
                {
                    Emit(b.AliasTok, target);
                }
            }
        }
    }

    // ---- expressions: primary + postfix chain -------------------------------------------------------

    /// <summary>
    /// Evaluates the primary expression/path starting at <paramref name="i"/> (emitting references when
    /// <paramref name="emit"/>), then its postfix chain. Returns the type if known.
    /// </summary>
    private RType? EvalExpr(int i, bool emit, out int next)
    {
        // nested constructor arguments / local-type chains recurse: give up typing rather than overflow the stack
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            next = i + 1;
            return null;
        }

        // Typing-only evaluation has no side effects; memoise it (outside local-type cycles, whose results depend on
        // the cycle guard) so that `Some(Some(Some(..)))` is not re-typed from every nested token (quadratic).
        if (!emit && _typing.Count == 0)
        {
            if (_typeOnly.TryGetValue(i, out var memo))
            {
                next = memo.Next;
                return memo.Type;
            }

            var typed = EvalExprInner(i, emit: false, out next);
            _typeOnly[i] = (typed, next);
            return typed;
        }

        var ty = EvalExprInner(i, emit, out next);
        if (emit && ty?.Def is { } d && d == world.Primitive("bool"))
        {
            var j = i;
            var bang = -1;
            while (j < _t.Count && (Is(j, "&") || Is(j, "*") || Is(j, "!") || Is(j, "-") || Is(j, "mut")))
            {
                if (Is(j, "!"))
                {
                    bang = j;
                }

                j++;
            }

            if (bang < 0 && Is(i - 1, "!") && IsUnaryPosition(i - 1))
            {
                bang = i - 1;
            }

            if (bang >= 0 && IsUnaryPosition(bang))
            {
                EmitImplMember(bang, ty, "not");
            }
        }

        return ty;
    }

    /// <summary>True when the operator token at <paramref name="i"/> has no left operand (so it is a prefix operator).</summary>
    private bool IsUnaryPosition(int i)
    {
        if (i == 0)
        {
            return true;
        }

        var p = _t[i - 1];
        return p.Kind switch
        {
            RustTokenKind.Ident => Keywords.Contains(Text(i - 1)) && Text(i - 1) is not ("true" or "false"),
            RustTokenKind.Number or RustTokenKind.Str => false,
            RustTokenKind.Punct => Text(i - 1) is not (")" or "]"),
            _ => true,
        };
    }

    private RType? EvalExprInner(int i, bool emit, out int next)
    {
        next = i + 1;
        // unary prefixes
        while (i < _t.Count && (Is(i, "&") || Is(i, "*") || Is(i, "!") || Is(i, "-") || Is(i, "mut") || Is(i, "move")))
        {
            i++;
        }

        if (i >= _t.Count)
        {
            return null;
        }

        next = i + 1;
        if (_t[i].Kind == RustTokenKind.Str)
        {
            return Chain(i + 1, RType.Of(world.StrItem), emit, out next);
        }

        if (!IsIdent(i))
        {
            return null;
        }

        if (fi.LocalAtTok.TryGetValue(i, out var local))
        {
            return Chain(i + 1, TypeOfLocal(local), emit, out next);
        }

        var (names, toks, after) = CollectPath(i);
        next = after;
        // generic parameter
        if (At(i).GenericSymbol(names[0]) is { } gs)
        {
            if (emit)
            {
                EmitLocal(i, gs);
            }

            return null;
        }

        var lastName = names[^1];
        var want = Is(after, "!") && !Is(after + 1, "=") ? NameSpaces.Macro
            : Is(after, "(") ? NameSpaces.Value | NameSpaces.Type
            : NameSpaces.Type | NameSpaces.Value;
        List<Item?> steps = [];
        var item = world.ResolveSegments(At(i), names, want, steps);
        if (emit)
        {
            for (var s = 0; s < steps.Count && s < toks.Count; s++)
            {
                if (steps[s] is { } it && !(names[s] == "Self"))
                {
                    Emit(toks[s], it);
                }
            }
        }

        if (item is null)
        {
            return null;
        }

        if (item.Kind == Kind.Macro)
        {
            if (Is(after, "!"))
            {
                var open = after + 1;
                var close = Is(open, "(") || Is(open, "[") || Is(open, "{") ? SkipBalanced(open) : open;
                return item.Name switch
                {
                    "vec" => world.StdNamed("Vec") is { } v ? RType.Of(v, (RType?)null) : null,
                    "format" => world.StdNamed("String") is { } sItem ? RType.Of(sItem) : null,
                    _ => close < 0 ? null : null,
                };
            }

            return null;
        }

        RType? primary;
        var k = after;
        switch (item.Kind)
        {
            case Kind.Function or Kind.StaticMethod or Kind.Method or Kind.TraitMethod:
                {
                    if (!Is(k, "("))
                    {
                        return null;
                    }

                    var receiver = steps.Count >= 2 && steps[^2] is { IsType: true } st ? RType.Of(st) : null;
                    if (receiver is null && item.ParentImpl is { } pi)
                    {
                        receiver = world.ImplSelf(pi);
                    }

                    primary = world.ReturnType(item, receiver);
                    primary = ConstructorHint(item, primary, k + 1);
                    k = SkipBalanced(k);
                    break;
                }

            case Kind.Struct or Kind.Union or Kind.EnumMember:
                {
                    var typeItem = item.Kind == Kind.EnumMember && item.Parent is { } en ? en : item;
                    primary = RType.Of(typeItem, new RType?[Math.Max(0, typeItem.Generics?.Names.Count(n => !n.Key.StartsWith('\'')) ?? 0)]);
                    if (Is(k, "(") && !Is(k - 1, ">") )
                    {
                        primary = ConstructorHint(item, primary, k + 1);
                        k = SkipBalanced(k);
                    }
                    else if (Is(k, "{") && char.IsUpper(item.Name[0]))
                    {
                        var close = SkipBalanced(k);
                        if (LooksLikeFields(k, close))
                        {
                            FieldsInBraces(item, k, close, emit);
                            k = close;
                        }
                    }

                    break;
                }

            case Kind.Constant or Kind.StaticVariable:
                primary = world.DeclaredType(item);
                break;
            default:
                return null;
        }

        return Chain(k, primary, emit, out next);
    }

    /// <summary>Gives <c>Some(x)</c>, <c>Box::new(x)</c> ... the type of their first argument.</summary>
    private RType? ConstructorHint(Item item, RType? ty, int argStart)
    {
        if (ty?.Def is null || !item.Crate.IsStd)
        {
            return ty;
        }

        var wrapper = item.Kind == Kind.EnumMember ? item.Parent?.Name : item.Name == "new" ? ty.Def.Name : null;
        if (wrapper is "Option" or "Result" or "Box" or "Rc" or "Arc" or "RefCell" or "Cell")
        {
            var argType = EvalExpr(argStart, emit: false, out _);
            if (argType is not null)
            {
                var args = (RType?[])ty.Args.Clone();
                if (item.Name == "Err" && args.Length > 1)
                {
                    args[1] = argType;
                }
                else if (args.Length > 0)
                {
                    args[0] = argType;
                }

                return ty with { Args = args };
            }
        }

        return ty;
    }

    private bool LooksLikeFields(int open, int close)
    {
        // `{ }` or `{ ident: ...` / `{ ident, ...` / `{ ..base }`
        if (close - open <= 2)
        {
            return true;
        }

        return IsIdent(open + 1) && (Is(open + 2, ":") || Is(open + 2, ",") || Is(open + 2, "}")) || Is(open + 1, ".") || _t[open + 1].Kind == RustTokenKind.Number;
    }

    private void FieldsInBraces(Item structItem, int open, int close, bool emit)
    {
        if (!emit)
        {
            return;
        }

        var depth = 0;
        for (var k = open; k < close; k++)
        {
            if (_t[k].Kind == RustTokenKind.Punct)
            {
                var p = Text(k);
                if (p is "(" or "[" or "{")
                {
                    depth++;
                }
                else if (p is ")" or "]" or "}")
                {
                    depth--;
                }

                continue;
            }

            if (depth != 1 || !IsIdent(k) && _t[k].Kind != RustTokenKind.Number)
            {
                continue;
            }

            var prevOk = Is(k - 1, "{") || Is(k - 1, ",");
            var nextOk = Is(k + 1, ":") && !Is(k + 1, "::") || Is(k + 1, ",") || Is(k + 1, "}");
            if (!prevOk || !nextOk)
            {
                continue;
            }

            if (structItem.Members.TryGetValue(Text(k), out var l) && l.FirstOrDefault(x => x.Kind == Kind.Field) is { } field)
            {
                Emit(k, field);
            }
        }
    }

    // ---- postfix chain -------------------------------------------------------------------------------

    private RType? Chain(int k, RType? ty, bool emit, out int next)
    {
        next = k;
        while (k < _t.Count)
        {
            if (Is(k, "?"))
            {
                if (emit && ty?.Def is { Crate.IsStd: true, Name: "Option" or "Result" })
                {
                    EmitImplMember(k, ty, "branch");
                }

                ty = ty?.Def?.Name is "Option" or "Result" ? ty.Args.FirstOrDefault() : null;
                k++;
                continue;
            }

            if (Is(k, "[") && ty is not null)
            {
                var close = SkipBalanced(k) - 1;
                if (emit && ty.Def is { Crate.IsStd: true, Name: "Vec" })
                {
                    var after = close + 1;
                    var mutating = Is(after, "=") && !Is(after + 1, "=")
                        || (Is(after, "+") || Is(after, "-") || Is(after, "*") || Is(after, "/") || Is(after, "|") || Is(after, "&")) && Is(after + 1, "=") && !Is(after + 2, "=");
                    var member = mutating ? "index_mut" : "index";
                    if (EmitImplMember(k, ty, member) && _t[close].Kind == RustTokenKind.Punct)
                    {
                        EmitImplMember(close, ty, member);
                    }
                }

                ty = RustWorld.ElementType(ty);
                k = close + 1;
                continue;
            }

            if (Is(k, ".") && k + 1 < _t.Count)
            {
                if (Is(k + 1, "await"))
                {
                    k += 2;
                    continue;
                }

                if (_t[k + 1].Kind == RustTokenKind.Number)
                {
                    var txt = Text(k + 1);
                    var dot = txt.IndexOf('.');
                    var fieldName = dot < 0 ? txt : txt[..dot];
                    var f = FindField(ty, fieldName, out var recv);
                    if (f is not null && dot < 0 && emit)
                    {
                        Emit(k + 1, f);
                    }

                    ty = dot < 0 ? FieldType(f, recv) : null;
                    k += 2;
                    continue;
                }

                if (IsIdent(k + 1))
                {
                    var name = Text(k + 1);
                    var after = k + 2;
                    if (Is(after, "::") && Is(after + 1, "<"))
                    {
                        after = SkipAngles(after + 1);
                    }

                    if (Is(after, "("))
                    {
                        var method = world.FindMethod(ty, name);
                        if (method is not null)
                        {
                            if (emit)
                            {
                                Emit(k + 1, method);
                            }

                            ty = world.ReturnType(method, world.ReceiverAfterHops(ty, name));
                        }
                        else
                        {
                            if (emit && name is "to_string" or "into" && world.Blanket(name) is { } blanket)
                            {
                                Emit(k + 1, blanket);
                            }

                            ty = name == "to_string" && world.StdNamed("String") is { } str ? RType.Of(str) : null;
                        }

                        k = SkipBalanced(after);
                    }
                    else
                    {
                        var f = FindField(ty, name, out var recv);
                        if (f is not null && emit)
                        {
                            Emit(k + 1, f);
                        }

                        ty = FieldType(f, recv);
                        k = after;
                    }

                    continue;
                }
            }

            break;
        }

        if (emit && ty is not null)
        {
            Operator(k, ty);
        }

        next = k;
        return ty;
    }

    private Item? FindField(RType? ty, string name, out RType? receiver)
    {
        receiver = ty;
        for (var hops = 0; ty?.Def is not null && hops < 4; hops++)
        {
            if (ty.Def.Members.TryGetValue(name, out var l) && l.FirstOrDefault(x => x.Kind == Kind.Field) is { } f)
            {
                receiver = ty;
                return f;
            }

            ty = world.DerefOf(ty);
        }

        return null;
    }

    private RType? FieldType(Item? field, RType? receiver)
    {
        if (field is null)
        {
            return null;
        }

        var declared = world.DeclaredType(field);
        if (declared is null || receiver is null)
        {
            return declared;
        }

        var parent = field.Parent;
        if (parent?.Generics is null)
        {
            return declared;
        }

        Dictionary<string, RType?> map = [];
        var args = receiver.Args;
        var idx = 0;
        foreach (var gname in parent.Generics.Names.Keys)
        {
            if (gname.StartsWith('\'') || gname.Length == 0)
            {
                continue;
            }

            if (idx < args.Length)
            {
                map[gname] = args[idx];
            }

            idx++;
        }

        return RustWorld.Subst(declared, map);
    }

    private const string OperatorChars = "+-*/%&|^<>=!";

    /// <summary>
    /// Resolves a binary operator after an expression of known type to the operator trait impl. Like rust-analyzer the
    /// reference covers the operator token and the whitespace on each side of it.
    /// </summary>
    private void Operator(int k, RType ty)
    {
        if (ty.Def is null || k >= _t.Count || _t[k].Kind != RustTokenKind.Punct || Text(k).Length != 1 || !OperatorChars.Contains(Text(k)[0], StringComparison.Ordinal))
        {
            return;
        }

        // maximal run of adjacent operator characters
        var run = Text(k);
        var end = k + 1;
        while (end < _t.Count && _t[end].Kind == RustTokenKind.Punct && _t[end].Start == _t[end - 1].End && Text(end).Length == 1
            && OperatorChars.Contains(Text(end)[0], StringComparison.Ordinal))
        {
            run += Text(end);
            end++;
        }

        if (!RustStd.Operators.TryGetValue(run, out var info))
        {
            return;
        }

        foreach (var impl in ty.Def.Impls)
        {
            if (impl.TraitDef?.Name == info.Trait && impl.Members.TryGetValue(info.Method, out var m))
            {
                if (_emitted.Add(k))
                {
                    var start = _t[k].Start;
                    var stop = _t[end - 1].End;
                    AddWhitespace(k - 1, start, m[0].Symbol, before: true);
                    _file.Occurrences.Add(new OccurrenceRecord(start, stop, m[0].Symbol, false, null));
                    AddWhitespace(end, stop, m[0].Symbol, before: false);
                    if (m[0].IsExternal)
                    {
                        externals.TryAdd(m[0].Symbol, m[0]);
                    }
                }

                return;
            }
        }
    }

    /// <summary>The blank run (spaces/tabs, one line) right before/after an operator is part of its reference.</summary>
    private void AddWhitespace(int neighbour, int edge, string symbol, bool before)
    {
        if (neighbour < 0 || neighbour >= _t.Count)
        {
            return;
        }

        var from = before ? _t[neighbour].End : edge;
        var to = before ? edge : _t[neighbour].Start;
        if (to <= from)
        {
            return;
        }

        for (var q = from; q < to; q++)
        {
            if (_b[q] is not ((byte)' ' or (byte)'\t'))
            {
                return;
            }
        }

        _file.Occurrences.Add(new OccurrenceRecord(from, to, symbol, false, null));
    }

    /// <summary>Emits a reference at <paramref name="tok"/> to the member <paramref name="member"/> of a trait impl of <paramref name="ty"/>.</summary>
    private bool EmitImplMember(int tok, RType? ty, string member)
    {
        if (ty?.Def is null)
        {
            return false;
        }

        foreach (var impl in ty.Def.Impls)
        {
            if (impl.Members.TryGetValue(member, out var m))
            {
                Emit(tok, m[0]);
                return true;
            }
        }

        return false;
    }

    // ---- locals --------------------------------------------------------------------------------------

    private RType? TypeOfLocal(string symbol)
    {
        if (_localTypes.TryGetValue(symbol, out var cached))
        {
            return cached;
        }

        if (!_typing.Add(symbol) || !fi.LocalDeclTok.TryGetValue(symbol, out var tok) || !fi.LocalInfo.TryGetValue(symbol, out var info))
        {
            return null;
        }

        RType? result = null;
        try
        {
            result = ComputeLocalType(tok, info);
        }
        finally
        {
            _typing.Remove(symbol);
        }

        _localTypes[symbol] = result;
        return result;
    }

    private RType? ComputeLocalType(int tok, SymbolInformation info)
    {
        if (info.Kind == Kind.SelfParameter)
        {
            var cx = At(tok);
            return cx.Impl is { } impl ? world.ImplSelf(impl) : cx.Trait is { } tr ? RType.Of(tr) : null;
        }

        if (info.Kind == Kind.TypeParameter)
        {
            return null;
        }

        var k = tok + 1;
        if (Is(k, ":") )
        {
            var end = TypeEnd(k + 1);
            var p = k + 1;
            return world.ParseType(At(p), ref p, end);
        }

        var j = tok - 1;
        while (Is(j, "mut") || Is(j, "ref"))
        {
            j--;
        }

        if (Is(j, "let") && Is(tok + 1, "=") && !Is(tok + 2, "="))
        {
            return EvalExpr(tok + 2, emit: false, out _);
        }

        if (Is(j, "(") && IsIdent(j - 1) && Is(j - 2, "let") && Text(j - 1) is "Some" or "Ok" or "Err" && Is(tok + 1, ")") && Is(tok + 2, "="))
        {
            var scrutinee = EvalExpr(tok + 3, emit: false, out _);
            var idx = Text(j - 1) == "Err" ? 1 : 0;
            return scrutinee?.Args.Length > idx ? scrutinee.Args[idx] : null;
        }

        if (Is(j, "for") && Is(tok + 1, "in"))
        {
            return RustWorld.ElementType(EvalExpr(tok + 2, emit: false, out _));
        }

        return null;
    }

    private int TypeEnd(int from)
    {
        var angle = 0;
        for (var k = from; k < _t.Count; k++)
        {
            if (_t[k].Kind != RustTokenKind.Punct)
            {
                continue;
            }

            var s = Text(k);
            if (s == "<")
            {
                angle++;
            }
            else if (s == ">")
            {
                angle--;
            }
            else if (s is "(" or "[")
            {
                k = SkipBalanced(k) - 1;
            }
            else if (angle <= 0 && s is "," or ")" or "=" or ";" or "|" or "{" or "]" or "}")
            {
                return k;
            }
        }

        return _t.Count;
    }
}
