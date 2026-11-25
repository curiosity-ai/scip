namespace ScipSharp.Indexer.Ruby;

/// <summary>A parsed Sorbet <c>sig</c>: return type (null for <c>void</c>/unknown) and parameter types by name.</summary>
internal sealed record SigInfo(TypeFn? Ret, Dictionary<string, TypeFn> Params)
{
    /// <summary>Source text of each declared parameter type, for hover.</summary>
    public Dictionary<string, string> ParamText { get; init; } = [];

    /// <summary>Source text of the declared return type, or <c>void</c>.</summary>
    public string? RetText { get; init; }

    public bool Abstract { get; init; }

    public bool Override { get; init; }

    public bool Overridable { get; init; }

    public bool Final { get; init; }

    public string[] TypeParameters { get; init; } = [];
}

/// <summary>Bookkeeping for one open <c>(</c> / <c>[</c>: what the expression evaluates to once it closes.</summary>
internal sealed class ParenEntry
{
    public TypeFn? Call { get; init; }

    public Func<RubyProject, MethodTarget?>? Target { get; init; }

    public bool Passthrough { get; init; }

    public TypeFn? TypeArg { get; init; }

    public bool ArrayLiteral { get; init; }
}

internal readonly record struct AssignTarget(int LocalId, string? FieldKey, int ParenDepth);

/// <summary>
/// Local, flow-insensitive type tracking: just enough to resolve <c>x.foo</c> when <c>x</c> is a literal, a constant,
/// <c>self</c>, a local/ivar assigned from <c>Const.new</c>/a typed call/<c>T.let</c>, or a parameter with a <c>sig</c>.
/// Types are closures resolved in the second pass, so definition order across files does not matter.
/// </summary>
internal sealed partial class RubyWalker
{
    private static readonly HashSet<string> GenericCore = ["Array", "Hash", "Set", "Range", "Enumerator", "Enumerable"];

    private TypeFn? _exprType;
    private int _exprEnd = -1;
    private readonly Dictionary<int, TypeFn> _localTypes = [];
    private readonly Dictionary<int, string> _kwSymbols = [];
    private int _nextKw = -1;
    private readonly Stack<ParenEntry> _parens = [];
    private AssignTarget? _assign;
    private SigInfo? _pendingSig;
    private Func<RubyProject, MethodTarget?>? _lastCall;
    private Func<RubyProject, MethodTarget?>? _cmdTarget;
    private string? _pendingTCall;

    private void SetExpr(TypeFn? fn, int index)
    {
        _exprType = fn;
        _exprEnd = index;
    }

    private string LocalSym(int id) => id < 0 ? _kwSymbols[id] : $"local {id}";

    /// <summary>Marker return type for <c>T.attached_class</c>/<c>T.self_type</c>: resolved against the receiver at the call.</summary>
    internal static readonly TypeFn AttachedClass = _ => null;

    private static TypeFn CoreInstance(string name) => _ => new RType([name], false);

    private static TypeFn InstanceOf(RefSpec spec) =>
        p => p.ResolveType(spec) is { } t ? new RType(t.Path, false) : null;

    private static TypeFn ConstExpr(RefSpec spec, int count) =>
        p => p.Resolve(spec, count) switch
        {
            { IsType: true } c => new RType(c.Path, true),
            { } c when p.FieldTypes.TryGetValue("const:" + RubyProject.Key(c.Path), out var fn) => p.Guarded(() => fn(p)),
            _ => null,
        };

    private void BeginAssign(int localId, string? fieldKey) => _assign = new AssignTarget(localId, fieldKey, _parenDepth);

    private void FinishAssign()
    {
        if (_assign is not { } a || a.ParenDepth != _parenDepth)
        {
            return;
        }

        _assign = null;
        var typed = _exprEnd == _i - 1 && _exprType is not null;
        if (a.FieldKey is { } key)
        {
            if (typed)
            {
                _project.FieldTypes.TryAdd(key, _exprType!);
            }
        }
        else if (typed)
        {
            _localTypes[a.LocalId] = _exprType!;
        }
        else
        {
            _localTypes.Remove(a.LocalId);
        }
    }

    private TypeFn SelfType()
    {
        var (path, singleton) = SelfContext();
        var t = new RType(path, singleton);
        return _ => t;
    }

    // ---- brackets -----------------------------------------------------------------------------------------

    /// <summary>An index expression <c>recv[...]</c> is a call of <c>[]</c> (or <c>[]=</c> when assigned) on the receiver.</summary>
    private TypeFn? IndexCall(Token t)
    {
        if (_exprEnd != _i - 1 || t.SpaceBefore || _exprType is not { } recvFn)
        {
            return null;
        }

        var close = SkipBalanced(_i);
        var name = Tok(close) is { Kind: Tk.Punct, Text: "=" } ? "[]=" : "[]";
        Func<RubyProject, MethodTarget?> target = p => recvFn(p) is { } r ? p.LookupMethod(r, name) : null;
        Emit(t, null, 0, p => MethodSymbol(p, target(p)));
        return p => recvFn(p) is { } r ? p.ReturnOf(target(p), r, name) : null;
    }

    private void OpenParen(Token t)
    {
        var after = _exprEnd == _i - 1 && !t.SpaceBefore;
        var indexResult = t.Text == "[" ? IndexCall(t) : null;
        TypeFn? tArg = null;
        var passthrough = false;
        if (_pendingTCall is { } tc)
        {
            _pendingTCall = null;
            if (tc is "let" or "cast" or "assert_type!")
            {
                var k = SkipArgument(_i + 1);
                tArg = Tok(k) is { Kind: Tk.Punct, Text: "," } ? ParseType(k + 1).Fn : null;
            }
            else if (tc == "must")
            {
                passthrough = true;
            }
        }

        if (t.Text == "(" && !after && ExprStartBefore(_i))
        {
            passthrough = true;
        }

        _parens.Push(new ParenEntry
        {
            Call = t.Text == "[" ? indexResult : after ? _exprType : null,
            Target = after && t.Text == "(" ? _lastCall : null,
            TypeArg = tArg,
            Passthrough = passthrough,
            ArrayLiteral = t.Text == "[" && !after && IsArrayStart(),
        });
    }

    private bool IsArrayStart() => ExprStartBefore(_i) || (_i > 0 && _t[_i - 1].Kind == Tk.Ident && _t[_i].SpaceBefore);

    private void CloseParen()
    {
        if (_parens.Count == 0)
        {
            return;
        }

        var e = _parens.Pop();
        if (e.TypeArg is not null)
        {
            SetExpr(e.TypeArg, _i);
        }
        else if (e.Passthrough)
        {
            SetExpr(_exprEnd == _i - 1 ? _exprType : null, _i);
        }
        else if (e.ArrayLiteral)
        {
            SetExpr(CoreInstance("Array"), _i);
        }
        else if (e.Call is not null)
        {
            SetExpr(e.Call, _i);
        }
    }

    private int SkipBalanced(int i)
    {
        var depth = 0;
        for (var j = i; j < _t.Count && _t[j].Kind != Tk.Eof; j++)
        {
            var tk = _t[j];
            if (tk.Kind != Tk.Punct)
            {
                continue;
            }

            if (tk.Text is "(" or "[" or "{")
            {
                depth++;
            }
            else if (tk.Text is ")" or "]" or "}" && --depth <= 0)
            {
                return j + 1;
            }
        }

        return _t.Count - 1;
    }

    /// <summary>Index of the first top-level <c>,</c> or closing bracket at or after <paramref name="i"/>.</summary>
    private int SkipArgument(int i)
    {
        var depth = 0;
        for (var j = i; j < _t.Count && _t[j].Kind != Tk.Eof; j++)
        {
            var tk = _t[j];
            if (tk.Kind != Tk.Punct)
            {
                continue;
            }

            if (tk.Text is "(" or "[" or "{")
            {
                depth++;
            }
            else if (tk.Text is ")" or "]" or "}")
            {
                if (depth == 0)
                {
                    return j;
                }

                depth--;
            }
            else if (tk.Text == "," && depth == 0)
            {
                return j;
            }
        }

        return _t.Count - 1;
    }

    // ---- sorbet type expressions and sigs -----------------------------------------------------------------

    private (TypeFn? Fn, int End) ParseType(int i)
    {
        if (!TryReadChain(i, out var last, out var segs, out _, out var abs))
        {
            return (null, i);
        }

        var next = last + 1;
        if (segs is ["T"] && Tok(next) is { Kind: Tk.Punct, Text: "." } && Tok(next + 1).Kind == Tk.Ident)
        {
            var m = Tok(next + 1).Text;
            if (Tok(next + 2) is { Kind: Tk.Punct, Text: "(" })
            {
                var inner = ParseType(next + 3);
                var close = SkipBalanced(next + 2);
                if (m == "nilable")
                {
                    return (inner.Fn, close);
                }

                if (m == "class_of" && inner.Fn is { } f)
                {
                    return (p => f(p) is { } r ? new RType(r.Path, true) : null, close);
                }

                return (null, close);
            }

            return (m is "attached_class" or "self_type" ? AttachedClass : null, next + 2);
        }

        TypeFn fn = segs is ["T", var gen] && GenericCore.Contains(gen) ? CoreInstance(gen) : InstanceOf(new RefSpec(CurCref, segs, abs));
        if (Tok(next) is { Kind: Tk.Punct, Text: "[" } br && !br.SpaceBefore)
        {
            next = SkipBalanced(next);
        }

        return (fn, next);
    }

    /// <summary>Parses the sig that starts right after the <c>sig</c> identifier at <paramref name="from"/>.</summary>
    private void CaptureSig(int from)
    {
        var j = from;
        if (Tok(j) is { Kind: Tk.Punct, Text: "(" })
        {
            j = SkipBalanced(j);
        }

        int end;
        if (Tok(j) is { Kind: Tk.Punct, Text: "{" })
        {
            end = SkipBalanced(j);
        }
        else if (Tok(j) is { Kind: Tk.Keyword, Text: "do" })
        {
            var depth = 0;
            end = j;
            for (var k = j; k < _t.Count && _t[k].Kind != Tk.Eof; k++)
            {
                if (_t[k] is { Kind: Tk.Keyword, Text: "do" })
                {
                    depth++;
                }
                else if (_t[k] is { Kind: Tk.Keyword, Text: "end" } && --depth == 0)
                {
                    end = k;
                    break;
                }
            }
        }
        else
        {
            return;
        }

        TypeFn? ret = null;
        Dictionary<string, TypeFn> parms = [];
        Dictionary<string, string> parmText = [];
        string? retText = null;
        bool isAbstract = false, isOverride = false, isOverridable = false, isFinal = from < j;
        List<string> typeParameters = [];
        for (var k = j; k < end; k++)
        {
            if (_t[k].Kind != Tk.Ident)
            {
                continue;
            }

            var hasParens = Tok(k + 1) is { Kind: Tk.Punct, Text: "(" };
            switch (_t[k].Text)
            {
                case "void" when !hasParens:
                    retText = "void";
                    continue;
                case "abstract" when !hasParens:
                    isAbstract = true;
                    continue;
                case "override":
                    isOverride = true;
                    break;
                case "overridable" when !hasParens:
                    isOverridable = true;
                    continue;
            }

            if (!hasParens)
            {
                continue;
            }

            var close = SkipBalanced(k + 1);
            if (_t[k].Text == "returns")
            {
                ret = ParseType(k + 2).Fn;
                retText = TokenText(k + 2, close - 1);
            }
            else if (_t[k].Text == "type_parameters")
            {
                for (var n = k + 2; n < close - 1; n++)
                {
                    if (_t[n].Kind == Tk.Symbol)
                    {
                        typeParameters.Add(_t[n].Text.StartsWith(':') ? _t[n].Text : ":" + _t[n].Text);
                    }
                }
            }
            else if (_t[k].Text == "params")
            {
                var n = k + 2;
                while (n < close - 1)
                {
                    if (_t[n].Kind == Tk.Label || _t[n].Kind == Tk.Str && Tok(n + 1) is { Kind: Tk.Punct, Text: ":" })
                    {
                        var (fn, nx) = ParseType(n + 1);
                        if (fn is not null)
                        {
                            parms[_t[n].Text] = fn;
                        }

                        // Text of the type: up to the next top-level comma.
                        var e = n + 1;
                        var depth = 0;
                        while (e < close - 1)
                        {
                            if (_t[e] is { Kind: Tk.Punct, Text: "(" or "[" or "{" })
                            {
                                depth++;
                            }
                            else if (_t[e] is { Kind: Tk.Punct, Text: ")" or "]" or "}" })
                            {
                                depth--;
                            }
                            else if (depth == 0 && _t[e] is { Kind: Tk.Punct, Text: "," })
                            {
                                break;
                            }

                            e++;
                        }

                        parmText[_t[n].Text.Trim('"')] = TokenText(n + 1, e);
                        n = Math.Max(Math.Max(nx, e), n + 1);
                    }
                    else
                    {
                        n++;
                    }
                }
            }

            k = close - 1;
        }

        _pendingSig = new SigInfo(ret, parms)
        {
            ParamText = parmText,
            RetText = retText,
            Abstract = isAbstract,
            Override = isOverride,
            Overridable = isOverridable,
            Final = isFinal,
            TypeParameters = [.. typeParameters],
        };
    }
}
