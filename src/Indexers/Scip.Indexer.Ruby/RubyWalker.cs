using System.Text.RegularExpressions;

namespace Scip.Indexer.Ruby;

/// <summary>One occurrence in a document. Symbols that need project-wide knowledge are resolved in a second pass.</summary>
internal sealed class Occ
{
    public int Line { get; init; }

    public int Col { get; init; }

    public int EndCol { get; init; }

    public string? Symbol { get; set; }

    public Func<RubyProject, string?>? Resolver { get; init; }

    public int Roles { get; init; }

    public int[]? Enclosing { get; set; }

    public SymbolInformation.Types.Kind Kind { get; init; }

    /// <summary>Hover markdown (without the ```ruby fence), resolved in the second pass.</summary>
    public Func<RubyProject, string?>? Markdown { get; set; }

    /// <summary>The doc comment above the definition.</summary>
    public string? Comment { get; set; }

    /// <summary>Instance variable occurrences: the owning type and name, for inheritance relationships.</summary>
    public (string[] Path, string Name)? Field { get; set; }

    /// <summary>Constant alias definition (`A = B`): resolves to the aliased symbol.</summary>
    public Func<RubyProject, string?>? AliasOf { get; set; }
}

internal sealed class DocModel(string relativePath, string text)
{
    public string RelativePath { get; } = relativePath;

    public string Text { get; } = text;

    public List<Occ> Occurrences { get; } = [];
}

internal sealed class LocalScope(LocalScope? parent)
{
    private readonly Dictionary<string, int> _ids = [];

    public int? Lookup(string name) =>
        _ids.TryGetValue(name, out var id) ? id : parent?.Lookup(name);

    public void Declare(string name, int id) => _ids[name] = id;

    public IEnumerable<(string Name, int Id)> Snapshot() => _ids.Select(kv => (kv.Key, kv.Value));
}

internal enum FrameKind { Class, Module, Singleton, Def, Block, Other, Brace }

internal sealed class Frame(FrameKind kind, LocalScope locals)
{
    public FrameKind Kind { get; } = kind;

    public LocalScope Locals { get; } = locals;

    public string[] Path { get; init; } = [];

    public string[][] Cref { get; init; } = [];

    public Occ? DefOcc { get; init; }

    public int StartLine { get; init; }

    public int StartCol { get; init; }

    public bool Endless { get; set; }

    /// <summary>Fields already assigned in this method/body: later assignments are write references, not definitions.</summary>
    public HashSet<string> AssignedFields { get; } = [];

    /// <summary>For conditionals: fields assigned before the branch point, and the union over finished branches.</summary>
    public HashSet<string>? BranchBase { get; set; }

    public HashSet<string>? BranchUnion { get; set; }

    public int ParenAtStart { get; set; }

    public bool AwaitDo { get; set; }

    /// <summary>For <see cref="FrameKind.Def"/>: the type that owns the method and whether it is a singleton method.</summary>
    public string[] OwnerPath { get; init; } = [];

    public bool OwnerSingleton { get; init; }

    public string MethodName { get; init; } = "";

    public SigInfo? Sig { get; init; }

    /// <summary>A <c>Struct.new(...) do ... end</c> block, which behaves like a class body.</summary>
    public bool StructBlock { get; init; }

    public bool IsFlatfile { get; init; }

    /// <summary>An <c>it "..." do</c> / <c>before do</c> body, modelled as a synthesized method.</summary>
    public bool ExampleBlock { get; init; }
}

/// <summary>
/// Single pass over a token stream that records definitions, local/field/constant/method occurrences and the
/// project-wide tables (types, methods, constants, ancestry) needed to resolve the rest afterwards.
/// </summary>
internal sealed partial class RubyWalker
{
    [GeneratedRegex(@"^\$[A-Za-z_]\w*$")]
    private static partial Regex NamedGlobal();

    private static readonly HashSet<string> ExprStartKeywords =
        ["then", "else", "do", "begin", "when", "elsif", "ensure", "and", "or", "not", "in", "if", "unless", "while", "until", "case"];

    private readonly RubyProject _project;
    private readonly DocModel _doc;
    private readonly List<Token> _t;
    private readonly List<Frame> _stack = [];
    private readonly List<(string Name, int Id)> _pendingBlockLocals = [];
    private int _i;
    private int _nextLocal;
    private int _parenDepth;
    private string[]? _structBlock;
    private bool _flatfileBlock;
    private (string[] Owner, string Name, Occ Def, int Line, int Col)? _exampleBlock;
    private int _lhsEnd = -1;
    private string[]? _instanceBlock;
    private int _lastChainEnd = -1;
    private RefSpec? _lastChain;
    private bool _rescueLine;

    private RubyWalker(RubyProject project, DocModel doc, List<Token> tokens)
    {
        _project = project;
        _doc = doc;
        _t = tokens;
        _stack.Add(new Frame(FrameKind.Other, new LocalScope(null)));
    }

    public static void Walk(RubyProject project, DocModel doc)
    {
        var walker = new RubyWalker(project, doc, RubyLexer.Lex(doc.Text));
        walker.Run();
    }

    private Token Tok(int index) => index >= 0 && index < _t.Count ? _t[index] : _t[^1];

    private Frame Top => _stack[^1];

    private LocalScope Scope => Top.Locals;

    private void Run()
    {
        while (_i < _t.Count && _t[_i].Kind != Tk.Eof)
        {
            var before = _i;
            Step();
            if (_i == before)
            {
                _i++;
            }
        }

        var last = _t.Count > 1 ? _t[^2] : default;
        while (_stack.Count > 1)
        {
            Close(last.Line, last.EndCol);
        }
    }

    // ---- context helpers ----------------------------------------------------------------------------------

    private string[][] CurCref
    {
        get
        {
            for (var k = _stack.Count - 1; k >= 0; k--)
            {
                if (_stack[k].Kind is FrameKind.Class or FrameKind.Module)
                {
                    return _stack[k].Cref;
                }
            }

            return [];
        }
    }

    private string[] CurNamespace => CurCref is [var first, ..] ? first : [];

    /// <summary>Owner for fields and implicit-self calls at the current position.</summary>
    private (string[] Path, bool Singleton) SelfContext()
    {
        for (var k = _stack.Count - 1; k >= 0; k--)
        {
            var f = _stack[k];
            switch (f.Kind)
            {
                case FrameKind.Def:
                    return (f.OwnerPath, f.OwnerSingleton);
                case FrameKind.Class or FrameKind.Module:
                    return (f.Path, true);
                case FrameKind.Singleton:
                    return (f.Path, true);
            }
        }

        return (["Object"], false);
    }

    /// <summary>Owner for a method defined at the current position.</summary>
    private (string[] Path, bool Singleton) DefOwner()
    {
        for (var k = _stack.Count - 1; k >= 0; k--)
        {
            var f = _stack[k];
            switch (f.Kind)
            {
                case FrameKind.Class or FrameKind.Module:
                    return (f.Path, false);
                case FrameKind.Singleton:
                    return (f.Path, true);
            }
        }

        return (["Object"], false);
    }

    private bool InClassBody()
    {
        for (var k = _stack.Count - 1; k >= 0; k--)
        {
            switch (_stack[k].Kind)
            {
                case FrameKind.Def: return false;
                case FrameKind.Class or FrameKind.Module or FrameKind.Singleton: return true;
            }
        }

        return false;
    }

    private bool ExprStartBefore(int index)
    {
        if (index <= 0)
        {
            return true;
        }

        var p = _t[index - 1];
        return p.Kind switch
        {
            Tk.Newline => true,
            Tk.Punct => p.Text is not (")" or "]" or "}"),
            Tk.Keyword => ExprStartKeywords.Contains(p.Text),
            _ => false,
        };
    }

    private bool StatementStartBefore(int index) => index <= 0 || _t[index - 1] is { Kind: Tk.Newline } or { Kind: Tk.Punct, Text: ";" };

    private void AddOcc(Occ o) => _doc.Occurrences.Add(o);

    private Occ Emit(Token tok, string? symbol, int roles = 0, Func<RubyProject, string?>? resolver = null,
        SymbolInformation.Types.Kind kind = SymbolInformation.Types.Kind.UnspecifiedKind, int? startCol = null, int? endCol = null)
    {
        var o = new Occ { Line = tok.Line, Col = startCol ?? tok.Col, EndCol = endCol ?? tok.EndCol, Symbol = symbol, Resolver = resolver, Roles = roles, Kind = kind };
        AddOcc(o);
        return o;
    }

    private int DefineLocal(string name, Token tok, int? startCol = null, int? endCol = null, string type = Untyped)
    {
        var id = _nextLocal++;
        Scope.Declare(name, id);
        FieldScopeFrame().AssignedFields.Add("#" + id);
        WithVarDocs(Emit(tok, $"local {id}", (int)SymbolRole.Definition, startCol: startCol, endCol: endCol), name, type);
        return id;
    }

    private void Close(int endLine, int endCol)
    {
        var f = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        if (f.DefOcc is { } o)
        {
            o.Enclosing = [f.StartLine, f.StartCol, endLine, endCol];
        }
    }

    private void CloseTopKeywordFrame(Token endTok)
    {
        // `end` closes the innermost keyword frame; stray brace frames above it (unbalanced input) are dropped.
        while (_stack.Count > 1 && Top.Kind == FrameKind.Brace)
        {
            _stack.RemoveAt(_stack.Count - 1);
        }

        if (_stack.Count > 1)
        {
            Close(endTok.Line, endTok.EndCol);
        }
    }

    // ---- main dispatch ------------------------------------------------------------------------------------

    private void Step()
    {
        var t = _t[_i];
        switch (t.Kind)
        {
            case Tk.Newline:
                // `while cond` ends at the line break: a `do` on a later line opens an ordinary block.
                Top.AwaitDo = false;
                FinishAssign();
                _cmdTarget = null;
                _rescueLine = false;
                _lhsEnd = -1;
                CloseEndlessDef();
                _i++;
                break;
            case Tk.Keyword:
                Keyword(t);
                break;
            case Tk.Const:
                ConstChain();
                break;
            case Tk.Ident:
                Identifier(t);
                break;
            case Tk.IVar or Tk.CVar:
                Field(t);
                break;
            case Tk.GVar:
                Global(t);
                break;
            case Tk.Punct:
                Punct(t);
                break;
            case Tk.Str:
                SetExpr(CoreInstance("String"), _i);
                _i++;
                break;
            case Tk.Num:
                SetExpr(CoreInstance(t.Text.Contains('.') || t.Text.Contains('e') && !t.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? "Float" : "Integer"), _i);
                _i++;
                break;
            case Tk.Symbol:
                SetExpr(CoreInstance("Symbol"), _i);
                _i++;
                break;
            case Tk.Label:
                CallLabel(t);
                _i++;
                break;
            default:
                _i++;
                break;
        }
    }

    private void CloseEndlessDef()
    {
        if (Top is { Kind: FrameKind.Def, Endless: true } f && _parenDepth <= f.ParenAtStart && _i > 0)
        {
            var prev = _t[_i - 1];
            Close(prev.Line, prev.EndCol);
        }
    }

    /// <summary>`foo(*)`, `foo(**)`, `foo(&)`: forwards the enclosing method's anonymous parameter.</summary>
    private bool AnonymousForward(Token t)
    {
        var prev = Tok(_i - 1);
        var next = Tok(_i + 1);
        if (prev is not { Kind: Tk.Punct, Text: "(" or "," } || next is not { Kind: Tk.Punct, Text: ")" or "," } || Scope.Lookup(t.Text) is not { } id || id < 0)
        {
            return false;
        }

        Emit(t, LocalSym(id), (int)SymbolRole.ReadAccess);
        _i++;
        return true;
    }

    private void Punct(Token t)
    {
        switch (t.Text)
        {
            case ";":
                FinishAssign();
                _lhsEnd = -1;
                CloseEndlessDef();
                _i++;
                break;
            case "*" or "**" or "&" when AnonymousForward(t):
                break;
            case "(" or "[":
                OpenParen(t);
                _parenDepth++;
                _i++;
                break;
            case ")" or "]":
                _parenDepth = Math.Max(0, _parenDepth - 1);
                CloseParen();
                _i++;
                break;
            case "{":
                OpenBrace();
                break;
            case "}":
                if (Top.Kind is FrameKind.Block or FrameKind.Brace || Top is { StructBlock: true } or { ExampleBlock: true })
                {
                    var hash = Top.Kind == FrameKind.Brace;
                    if (_stack.Count > 1)
                    {
                        Close(t.Line, t.EndCol);
                    }

                    if (hash)
                    {
                        SetExpr(CoreInstance("Hash"), _i);
                    }
                }

                _i++;
                break;
            case "->":
                Lambda();
                break;
            case "::" when Tok(_i + 1).Kind == Tk.Const && !PrevIsValueToken(_i):
                ConstChain();
                break;
            case "=>" when _rescueLine && Tok(_i + 1).Kind == Tk.Ident:
                _i++;
                DefineLocal(_t[_i].Text, _t[_i]);
                _i++;
                break;
            default:
                _i++;
                break;
        }
    }

    private bool PrevIsValueToken(int index)
    {
        if (index == 0)
        {
            return false;
        }

        var p = _t[index - 1];
        return p.Kind is Tk.Ident or Tk.Const or Tk.IVar or Tk.CVar or Tk.GVar or Tk.Num or Tk.Str
            || p is { Kind: Tk.Punct, Text: ")" or "]" or "}" } || p is { Kind: Tk.Keyword, Text: "self" or "end" };
    }

    private void OpenBrace()
    {
        var prev = _i > 0 ? _t[_i - 1] : default;
        var isBlock = _i > 0 && (prev.Kind is Tk.Ident or Tk.Const || prev is { Kind: Tk.Punct, Text: ")" or "->" } || prev is { Kind: Tk.Keyword, Text: "super" or "yield" });
        _i++;
        if (isBlock)
        {
            PushBlock();
        }
        else
        {
            _stack.Add(new Frame(FrameKind.Brace, Scope));
        }
    }

    private void PushBlock()
    {
        var frame = _structBlock is { } sb
            ? new Frame(FrameKind.Class, new LocalScope(null)) { Path = sb, Cref = [sb, .. CurCref], StructBlock = true }
            : _exampleBlock is { } ex
                ? new Frame(FrameKind.Def, new LocalScope(null))
                {
                    OwnerPath = ex.Owner, MethodName = ex.Name, DefOcc = ex.Def, StartLine = ex.Line, StartCol = ex.Col, ExampleBlock = true,
                }
                : _instanceBlock is { } inst
                    ? new Frame(FrameKind.Def, new LocalScope(null)) { OwnerPath = inst, MethodName = "setup", ExampleBlock = true }
                    : new Frame(FrameKind.Block, new LocalScope(Scope)) { IsFlatfile = _flatfileBlock };
        _exampleBlock = null;
        _instanceBlock = null;
        _structBlock = null;
        _flatfileBlock = false;
        foreach (var (name, id) in _pendingBlockLocals)
        {
            frame.Locals.Declare(name, id);
        }

        _pendingBlockLocals.Clear();
        _stack.Add(frame);
        if (Tok(_i) is { Kind: Tk.Punct, Text: "|" })
        {
            _i++;
            ParseParams(tk => tk is { Kind: Tk.Punct, Text: "|" }, false);
            if (Tok(_i) is { Kind: Tk.Punct, Text: "|" })
            {
                _i++;
            }
        }
    }

    private void Lambda()
    {
        _i++;
        _pendingBlockLocals.Clear();
        var scratch = new Frame(FrameKind.Other, new LocalScope(Scope));
        _stack.Add(scratch);
        if (Tok(_i) is { Kind: Tk.Punct, Text: "(" })
        {
            _i++;
            ParseParams(tk => tk is { Kind: Tk.Punct, Text: ")" }, false);
            if (Tok(_i) is { Kind: Tk.Punct, Text: ")" })
            {
                _i++;
            }
        }
        else
        {
            ParseParams(tk => tk is { Kind: Tk.Punct, Text: "{" } or { Kind: Tk.Keyword, Text: "do" } or { Kind: Tk.Newline }, false);
        }

        // Hand the parameters to the block that follows.
        _stack.Remove(scratch);
        foreach (var name in scratch.Locals.Snapshot())
        {
            _pendingBlockLocals.Add(name);
        }
    }

    // ---- parameters ---------------------------------------------------------------------------------------

    private void ParseParams(Func<Token, bool> isCloser, bool methodParams)
    {
        var atStart = true;
        var baseParen = _parenDepth;
        while (_i < _t.Count && _t[_i].Kind != Tk.Eof)
        {
            var t = _t[_i];
            if (_parenDepth == baseParen && isCloser(t))
            {
                return;
            }

            if (t.Kind == Tk.Newline && _parenDepth == baseParen)
            {
                _i++;
                continue;
            }

            if (atStart)
            {
                if (t is { Kind: Tk.Punct, Text: "*" or "**" or "&" })
                {
                    _i++;
                    if (Tok(_i) is { Kind: Tk.Punct, Text: "," or ")" or "|" } or { Kind: Tk.Newline })
                    {
                        // Anonymous splat/block parameter: upstream defines a local at the sigil itself.
                        DefineLocal(t.Text, t, type: t.Text switch { "*" => "T::Array[T.untyped]", "**" => "T::Hash[Symbol, T.untyped]", _ => Untyped });
                        atStart = false;
                    }

                    continue;
                }

                if (t.Kind == Tk.Ident)
                {
                    var pid = DefineLocal(t.Text, t, type: ParamDocType(t, methodParams));
                    if (methodParams && Top.Sig is { } sig && sig.Params.TryGetValue(t.Text, out var pfn))
                    {
                        _localTypes[pid] = pfn;
                    }

                    _i++;
                    atStart = false;
                    continue;
                }

                if (t.Kind == Tk.Label)
                {
                    if (methodParams && Top.Kind == FrameKind.Def)
                    {
                        DefineKeywordParam(t);
                    }
                    else
                    {
                        DefineLocal(t.Text, t, endCol: t.Col + t.Text.Length, type: ParamDocType(t, methodParams));
                    }

                    _i++;
                    atStart = false;
                    continue;
                }

                if (t is { Kind: Tk.Punct, Text: "..." })
                {
                    DefineLocal("...", t);
                    _i++;
                    atStart = false;
                    continue;
                }

                if (t is { Kind: Tk.Punct, Text: "(" })
                {
                    _i++;
                    ParseParams(tk => tk is { Kind: Tk.Punct, Text: ")" }, methodParams);
                    if (Tok(_i) is { Kind: Tk.Punct, Text: ")" })
                    {
                        _i++;
                    }

                    atStart = false;
                    continue;
                }

                atStart = false;
            }

            if (t.Kind == Tk.Punct && t.Text is "," or ";" && _parenDepth == baseParen)
            {
                atStart = true;
                _i++;
                continue;
            }

            Step();
        }
    }

    // ---- keywords -----------------------------------------------------------------------------------------

    private void Keyword(Token t)
    {
        switch (t.Text)
        {
            case "class":
                ClassOrModule(t, false);
                break;
            case "module":
                ClassOrModule(t, true);
                break;
            case "def":
                Def(t);
                break;
            case "do":
                if (Top.AwaitDo)
                {
                    Top.AwaitDo = false;
                    _i++;
                }
                else
                {
                    _i++;
                    PushBlock();
                }

                break;
            case "begin" or "case":
                _stack.Add(new Frame(FrameKind.Other, Scope));
                if (t.Text == "case")
                {
                    BeginBranches(Top);
                }

                _i++;
                break;
            case "if" or "unless" or "while" or "until":
                if (ExprStartBefore(_i))
                {
                    _stack.Add(new Frame(FrameKind.Other, Scope) { AwaitDo = t.Text is "while" or "until" });
                    if (t.Text is "if" or "unless")
                    {
                        BeginBranches(Top);
                    }
                }

                _i++;
                break;
            case "else" or "elsif" or "when":
                NextBranch();
                _i++;
                break;
            case "for":
                ForLoop();
                break;
            case "end":
                if (Top is { Kind: FrameKind.Def, Endless: false })
                {
                    ReturnedAssignment();
                }

                EndBranches();

                CloseTopKeywordFrame(t);
                _i++;
                break;
            case "rescue":
                _rescueLine = true;
                _i++;
                break;
            case "alias":
                if (Tok(_i + 1).Kind is Tk.Ident or Tk.Symbol or Tk.Const && Tok(_i + 2).Kind is Tk.Ident or Tk.Symbol or Tk.Const)
                {
                    RegisterAlias(Tok(_i + 1).Text, Tok(_i + 2).Text);
                }

                _i += 3;
                break;
            case "self":
                SetExpr(SelfType(), _i);
                _i++;
                break;
            case "true" or "false" or "nil":
                SetExpr(CoreInstance(t.Text switch { "true" => "TrueClass", "false" => "FalseClass", _ => "NilClass" }), _i);
                _i++;
                break;
            default:
                _i++;
                break;
        }
    }

    private void ForLoop()
    {
        _stack.Add(new Frame(FrameKind.Other, Scope) { AwaitDo = true });
        _i++;
        while (_i < _t.Count && _t[_i] is not { Kind: Tk.Keyword, Text: "in" } && _t[_i].Kind is not (Tk.Newline or Tk.Eof))
        {
            if (_t[_i].Kind == Tk.Ident)
            {
                var name = _t[_i].Text;
                if (Scope.Lookup(name) is null)
                {
                    DefineLocal(name, _t[_i]);
                }
            }

            _i++;
        }
    }

    private bool TryReadChain(int from, out int last, out string[] segs, out Token[] toks, out bool absolute)
    {
        var s = new List<string>();
        var tk = new List<Token>();
        var j = from;
        absolute = false;
        last = from;
        if (Tok(j) is { Kind: Tk.Punct, Text: "::" })
        {
            absolute = true;
            j++;
        }

        while (Tok(j).Kind == Tk.Const)
        {
            s.Add(_t[j].Text);
            tk.Add(_t[j]);
            last = j;
            if (Tok(j + 1) is { Kind: Tk.Punct, Text: "::" } && Tok(j + 2).Kind == Tk.Const && !_t[j + 1].SpaceBefore && !_t[j + 2].SpaceBefore)
            {
                j += 2;
            }
            else
            {
                break;
            }
        }

        segs = [.. s];
        toks = [.. tk];
        return s.Count > 0;
    }

    private void ClassOrModule(Token kw, bool isModule)
    {
        if (!isModule && Tok(_i + 1) is { Kind: Tk.Punct, Text: "<<" })
        {
            _i += 2;
            var (owner, _) = DefOwner();
            _stack.Add(new Frame(FrameKind.Singleton, new LocalScope(null)) { Path = owner });
            if (Tok(_i) is { Kind: Tk.Keyword, Text: "self" } selfTok)
            {
                // Upstream currently emits a definition of the singleton class at `self`.
                WithDocs(Emit(selfTok, _project.Format(RubyProject.TypeDescriptors(owner, true)), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.SingletonClass),
                    p => SingletonHeader(p, owner), comment: false);
                _i++;
            }

            return;
        }

        if (!TryReadChain(_i + 1, out var lastIdx, out var segs, out var toks, out var absolute))
        {
            _i++;
            return;
        }

        var outerCref = CurCref;
        var ns = absolute ? [] : CurNamespace;
        string[] fullPath = [.. ns, .. segs];

        // Prefix segments of `class A::B` are references (resolved in the second pass).
        var spec = new RefSpec(outerCref, segs, absolute);
        for (var k = 0; k < segs.Length - 1; k++)
        {
            var count = k + 1;
            Emit(toks[k], null, NoRole, p => ConstSymbol(p, spec, count));
        }

        var nameTok = toks[^1];
        // `class A::B` presupposes that `A` exists, even when it is defined outside the indexed files.
        for (var k = 1; k < fullPath.Length; k++)
        {
            _project.Type(fullPath[..k]).Defined = true;
        }

        var info = _project.Type(fullPath);
        info.Defined = true;
        info.ProjectDefined = true;
        info.IsModule = isModule;
        _project.InvalidateAncestors();
        var defOcc = Emit(nameTok, _project.TypeSymbol(fullPath), (int)SymbolRole.Definition, null,
            isModule ? SymbolInformation.Types.Kind.Module : SymbolInformation.Types.Kind.Class);
        WithDocs(defOcc, p => TypeHeader(p, info, isModule));
        _i = lastIdx + 1;

        if (!isModule && Tok(_i) is { Kind: Tk.Punct, Text: "<" } && TryReadChain(_i + 1, out var superLast, out var superSegs, out _, out var superAbs))
        {
            _superStart = _i + 1;
            info.SuperIsExpression = Tok(superLast + 1) is { Kind: Tk.Punct, Text: "." or "&." or "(" or "::" };
            info.Super ??= new RefSpec(outerCref, superSegs, superAbs);
            if (superSegs is ["T", "Enum"])
            {
                info.Methods.Add("serialize");
                Emit(nameTok, _project.MethodSymbol(fullPath, false, "serialize"), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Method);
            }

            if (superSegs is ["T", "Struct"])
            {
                // Sorbet's T::Struct rewriter synthesises an `initialize` for the declared props.
                info.Methods.Add("initialize");
                Emit(nameTok, _project.MethodSymbol(fullPath, false, "initialize"), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Method);
            }
        }

        string[][] cref = [fullPath, .. outerCref];
        _stack.Add(new Frame(isModule ? FrameKind.Module : FrameKind.Class, new LocalScope(null))
        {
            Path = fullPath,
            Cref = cref,
            DefOcc = defOcc,
            StartLine = kw.Line,
            StartCol = kw.Col,
        });
    }

    private void Def(Token kw)
    {
        _i++;
        var (ownerPath, ownerSingleton) = DefOwner();
        if (Tok(_i) is { Kind: Tk.Keyword, Text: "self" } && Tok(_i + 1) is { Kind: Tk.Punct, Text: "." })
        {
            ownerSingleton = true;
            _i += 2;
        }
        else if (Tok(_i).Kind == Tk.Const && Tok(_i + 1) is { Kind: Tk.Punct, Text: "." } && !_t[_i + 1].SpaceBefore)
        {
            TryReadChain(_i, out var lastIdx, out var segs, out _, out var abs);
            var spec = new RefSpec(CurCref, segs, abs);
            string[] guess = [.. (abs ? [] : CurNamespace), .. segs];
            var resolved = _project.Resolve(spec, segs.Length) is { IsType: true } r ? r.Path : guess;
            EmitConstRefs(_i, lastIdx, spec);
            ownerPath = resolved;
            ownerSingleton = true;
            _i = lastIdx + 2;
        }
        else if (Tok(_i).Kind == Tk.Ident && Tok(_i + 1) is { Kind: Tk.Punct, Text: "." } && !_t[_i + 1].SpaceBefore)
        {
            _i += 2;
        }
        else if (Tok(_i) is { Kind: Tk.Punct, Text: "(" })
        {
            // `def (expr).name`: skip the parenthesised receiver (unknown object).
            var depth = 0;
            var j = _i;
            for (; j < _t.Count && _t[j].Kind != Tk.Eof; j++)
            {
                if (_t[j] is { Kind: Tk.Punct, Text: "(" })
                {
                    depth++;
                }
                else if (_t[j] is { Kind: Tk.Punct, Text: ")" } && --depth == 0)
                {
                    break;
                }
            }

            if (Tok(j + 1) is { Kind: Tk.Punct, Text: "." })
            {
                _i = j + 2;
            }
            else
            {
                return;
            }
        }

        var nameTok = Tok(_i);
        var name = nameTok.Text;
        var nameEnd = nameTok.EndCol;
        if (nameTok.Kind is Tk.Ident or Tk.Const or Tk.Keyword or Tk.Label)
        {
            if (nameTok.Kind == Tk.Label)
            {
                // `def foo:` is not valid Ruby; bail out gracefully.
                return;
            }

            _i++;
            if (Tok(_i) is { Kind: Tk.Punct, Text: "=" } eq && !eq.SpaceBefore && Tok(_i + 1) is { Kind: Tk.Punct, Text: "(" } or { Kind: Tk.Newline })
            {
                name += "=";
                nameEnd = eq.EndCol;
                _i++;
            }
        }
        else if (nameTok.Kind == Tk.Punct)
        {
            var sb = new System.Text.StringBuilder();
            var end = nameTok.EndCol;
            while (Tok(_i).Kind == Tk.Punct && Tok(_i).Text != "(" && (sb.Length == 0 || !_t[_i].SpaceBefore))
            {
                sb.Append(_t[_i].Text);
                end = _t[_i].EndCol;
                _i++;
            }

            name = sb.ToString();
            nameEnd = end;
            if (name.Length == 0)
            {
                return;
            }
        }
        else
        {
            return;
        }

        var owner = _project.Type(ownerPath);
        (ownerSingleton ? owner.SingletonMethods : owner.Methods).Add(name);
        var sigInfo = _pendingSig;
        _pendingSig = null;
        if (sigInfo?.Ret is { } retFn)
        {
            _project.MethodRets[RubyProject.RetKey(ownerPath, ownerSingleton, name)] = retFn;
        }

        var defOcc = Emit(nameTok, _project.MethodSymbol(ownerPath, ownerSingleton, name), (int)SymbolRole.Definition, null,
            SymbolInformation.Types.Kind.Method, endCol: nameEnd);
        var frame = new Frame(FrameKind.Def, new LocalScope(null))
        {
            DefOcc = defOcc,
            StartLine = kw.Line,
            StartCol = kw.Col,
            OwnerPath = ownerPath,
            OwnerSingleton = ownerSingleton,
            MethodName = name,
            Sig = sigInfo,
        };
        _stack.Add(frame);

        var paramStart = _i;
        var paramEnd = _i;
        var occsBefore = _doc.Occurrences.Count;
        if (Tok(_i) is { Kind: Tk.Punct, Text: "(" })
        {
            _i++;
            paramStart = _i;
            ParseParams(tk => tk is { Kind: Tk.Punct, Text: ")" }, true);
            paramEnd = _i;
            if (Tok(_i) is { Kind: Tk.Punct, Text: ")" })
            {
                _i++;
            }
        }
        else if (Tok(_i).Kind is not (Tk.Newline or Tk.Eof) && Tok(_i) is not { Kind: Tk.Punct, Text: "=" or ";" })
        {
            ParseParams(tk => tk.Kind is Tk.Newline or Tk.Eof || tk is { Kind: Tk.Punct, Text: ";" or "=" }, true);
            paramEnd = _i;
        }

        var specs = ExtractParams(paramStart, paramEnd);
        if (!frame.Endless && IsEmptyBody(_i))
        {
            // Sorbet drops the CFG of an empty method, and with it the definitions of its (necessarily unused) parameters.
            for (var k = _doc.Occurrences.Count - 1; k >= occsBefore; k--)
            {
                var o = _doc.Occurrences[k];
                if ((o.Roles & (int)SymbolRole.Definition) != 0 && o.Symbol?.StartsWith("local ", StringComparison.Ordinal) == true)
                {
                    _doc.Occurrences.RemoveAt(k);
                }
            }
        }

        var methodMd = PrettyMethod(name, ownerSingleton, specs, sigInfo);
        WithDocs(defOcc, _ => methodMd);

        if (Tok(_i) is { Kind: Tk.Punct, Text: "=" })
        {
            frame.Endless = true;
            frame.ParenAtStart = _parenDepth;
            _i++;
        }
    }

    // ---- constants ----------------------------------------------------------------------------------------

    private static string? ConstSymbolOrNull(RubyProject p, ConstTarget? target) =>
        target is not { } t ? null : t.IsType ? p.TypeSymbol(t.Path) : p.ConstantSymbol(t.Path);

    private static string? ConstSymbol(RubyProject p, RefSpec spec, int count) => ConstSymbolOrNull(p, p.Resolve(spec, count));

    /// <summary>Marks an occurrence that upstream emits with no role at all (scope qualifiers, superclasses, ...).</summary>
    internal const int NoRole = 1 << 30;

    private void EmitConstRefs(int firstIdx, int lastIdx, RefSpec spec, bool lastNoRole = false)
    {
        var k = 0;
        for (var j = firstIdx; j <= lastIdx; j++)
        {
            if (_t[j].Kind != Tk.Const)
            {
                continue;
            }

            var count = ++k;
            // Upstream gives scope qualifiers of `A::B` (everything but the last segment) no role.
            var role = j < lastIdx || lastNoRole ? NoRole : 0;
            Emit(_t[j], null, role, p => ConstSymbol(p, spec, count));
        }
    }

    private void ConstChain()
    {
        if (_i > 0 && _t[_i - 1] is { Kind: Tk.Punct, Text: "." or "&." })
        {
            _i++;
            return;
        }

        var start = _i;
        if (!TryReadChain(start, out var lastIdx, out var segs, out var toks, out var absolute))
        {
            _i++;
            return;
        }

        var next = Tok(lastIdx + 1);
        // Constant assignment `NAME = value` at statement start (no `::` path).
        if (segs.Length == 1 && !absolute && StatementStartBefore(start) && next is { Kind: Tk.Punct, Text: "=" or "||=" }
            && !InMethodBody())
        {
            var ns = CurNamespace;
            if (IsStructNew(lastIdx + 2))
            {
                DefineStruct(toks[0], [.. ns, segs[0]], lastIdx + 2);
                _i = lastIdx + 1;
                return;
            }

            var owner = _project.Type(ns);
            owner.Constants.Add(segs[0]);
            _project.InvalidateAncestors();
            var constOcc = WithVarDocs(Emit(toks[0], _project.ConstantSymbol([.. ns, segs[0]]), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Constant),
                segs[0], ConstType(lastIdx + 2), comment: true);
            if (next is { Kind: Tk.Punct, Text: "=" } && TryReadChain(lastIdx + 2, out var aliasLast, out var aliasSegs, out _, out var aliasAbs)
                && Tok(aliasLast + 1).Kind is Tk.Newline or Tk.Eof)
            {
                var aliasSpec = new RefSpec(CurCref, aliasSegs, aliasAbs);
                constOcc.AliasOf = p => ConstSymbol(p, aliasSpec, aliasSegs.Length);
            }
            BeginAssign(0, "const:" + RubyProject.Key([.. ns, segs[0]]));
            _i = lastIdx + 1;
            return;
        }

        if (segs.Length > 1 && !absolute && StatementStartBefore(start) && next is { Kind: Tk.Punct, Text: "=" or "||=" } && !InMethodBody())
        {
            // `Rack::NAME = value` defines NAME inside Rack; the qualifiers are ordinary (role-less) references.
            string[] scopePath = [.. CurNamespace, .. segs[..^1]];
            _project.Type(scopePath).Constants.Add(segs[^1]);
            _project.InvalidateAncestors();
            var scopeSpec = new RefSpec(CurCref, segs[..^1], absolute);
            for (var k = 0; k < segs.Length - 1; k++)
            {
                var count = k + 1;
                Emit(toks[k], null, NoRole, p => ConstSymbol(p, scopeSpec, count));
            }

            WithVarDocs(Emit(toks[^1], _project.ConstantSymbol([.. scopePath, segs[^1]]), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Constant),
                segs[^1], ConstType(lastIdx + 2), comment: true);
            BeginAssign(0, "const:" + RubyProject.Key([.. scopePath, segs[^1]]));
            _i = lastIdx + 1;
            return;
        }

        var spec = new RefSpec(CurCref, segs, absolute);
        EmitConstRefs(start, lastIdx, spec, start == _superStart || IsCallStyleConstant(lastIdx));
        _lastChain = spec;
        _lastChainEnd = lastIdx;
        SetExpr(ConstExpr(spec, segs.Length), lastIdx);
        _i = lastIdx + 1;
    }

    private int _superStart = -1;

    /// <summary>True when only newlines or `;` separate the end of a `def` header from its closing `end`.</summary>
    private bool IsEmptyBody(int from)
    {
        var j = from;
        while (Tok(j).Kind == Tk.Newline || Tok(j) is { Kind: Tk.Punct, Text: ";" })
        {
            j++;
        }

        return Tok(j) is { Kind: Tk.Keyword, Text: "end" };
    }

    /// <summary>`class T.class_of(A) &lt; T.class_of(B)`: hover of the singleton class defined at `class &lt;&lt; self`.</summary>
    private static string? SingletonHeader(RubyProject p, string[] owner)
    {
        var name = $"T.class_of({RubyProject.Key(owner)})";
        var info = p.FindType(owner);
        if (info is { IsModule: true })
        {
            return $"class {name} < Module";
        }

        if (info?.Super is { } spec && !info.SuperIsExpression)
        {
            var super = p.ResolveType(spec) is { } t && (t.Defined || t.Core is not null) ? RubyProject.Key(t.Path) : null;
            return super is null ? $"class {name}" : $"class {name} < T.class_of({super})";
        }

        return $"class {name} < T.class_of(Object)";
    }

    private const string StubSuper = "Sorbet::Private::Static::<StubModule>";

    /// <summary>`class A::B &lt; C`, `module M`: the first line of a class hover.</summary>
    private static string TypeHeader(RubyProject p, TypeInfo info, bool isModule)
    {
        var name = RubyProject.Key(info.Path);
        if (isModule || info.Super is not { } spec)
        {
            return $"{(isModule ? "module" : "class")} {name}";
        }

        if (info.SuperIsExpression)
        {
            return $"class {name}";
        }

        var super = p.ResolveType(spec) is { } t && (t.Defined || t.Core is not null) ? RubyProject.Key(t.Path) : StubSuper;
        return super == "Object" ? $"class {name}" : $"class {name} < {super}";
    }

    /// <summary>For `attr_writer :x`: the setter's single parameter takes the attribute's declared type.</summary>
    private static SigInfo? SetterSig(SigInfo? sig, string param) =>
        sig is null ? null : sig with { ParamText = new Dictionary<string, string> { [param] = sig.RetText ?? Untyped }, RetText = null };

    private static readonly HashSet<string> BuiltinGlobals =
        ["$VERBOSE", "$DEBUG", "$stdout", "$stderr", "$stdin", "$PROGRAM_NAME", "$LOAD_PATH", "$LOADED_FEATURES", "$FILENAME", "$0", "$;", "$,", "$/", "$\\"];

    /// <summary>`@x = @x.merge(..)`: upstream evaluates the right-hand side first, so the assignment is only a write.</summary>
    private bool ReadsFieldOnRightHandSide(Token field)
    {
        if (Tok(_i + 1) is not { Kind: Tk.Punct, Text: "=" })
        {
            return false;
        }

        for (var j = _i + 2; j < _t.Count && _t[j].Kind is not (Tk.Newline or Tk.Eof); j++)
        {
            if (_t[j].Kind == field.Kind && _t[j].Text == field.Text)
            {
                return true;
            }
        }

        return false;
    }

    private int TouchLocal(int id)
    {
        FieldScopeFrame().AssignedFields.Add("#" + id);
        return id < 0 ? (int)SymbolRole.ReadAccess : 0;
    }

    /// <summary>
    /// Upstream emits a definition for the first assignment of a variable along each control-flow path (a sibling
    /// branch starts afresh) and a write for any assignment once the variable was assigned or read on the path.
    /// </summary>
    private int LocalAssignRole(int id, bool compound)
    {
        if (id < 0)
        {
            return (int)SymbolRole.WriteAccess;
        }

        var seen = !FieldScopeFrame().AssignedFields.Add("#" + id);
        return seen || compound ? (int)SymbolRole.WriteAccess : (int)SymbolRole.Definition;
    }

    /// <summary>`Array(x)` / `Integer x`: a constant used as a method name gets no access role upstream.</summary>
    private bool IsCallStyleConstant(int lastIdx)
    {
        var n = Tok(lastIdx + 1);
        if (n is { Kind: Tk.Punct, Text: "(" } && !n.SpaceBefore)
        {
            return true;
        }

        return n.SpaceBefore && n.Kind is Tk.Ident or Tk.IVar or Tk.Str or Tk.Num or Tk.Symbol && n.Line == _t[lastIdx].Line;
    }

    private bool InMethodBody()
    {
        foreach (var f in _stack)
        {
            if (f.Kind == FrameKind.Def)
            {
                return true;
            }
        }

        return false;
    }

    // ---- identifiers --------------------------------------------------------------------------------------

    private void AncestryCall(string kind, int argStart)
    {
        var target = CurTypeFrame();
        var j = argStart;
        while (TryReadChain(j, out var last, out var segs, out _, out var abs))
        {
            if (target is not null)
            {
                var spec = new RefSpec(CurCref, segs, abs);
                var info = _project.Type(target.Path);
                var singletonCtx = Top.Kind == FrameKind.Singleton;
                (kind switch
                {
                    "prepend" => info.Prepends,
                    "extend" => info.Extends,
                    _ => singletonCtx ? info.Extends : info.Includes,
                }).Add(spec);
                _project.InvalidateAncestors();
            }

            if (Tok(last + 1) is { Kind: Tk.Punct, Text: "," })
            {
                j = last + 2;
            }
            else
            {
                break;
            }
        }
    }

    private Frame? CurTypeFrame()
    {
        for (var k = _stack.Count - 1; k >= 0; k--)
        {
            if (_stack[k].Kind is FrameKind.Class or FrameKind.Module)
            {
                return _stack[k];
            }

            if (_stack[k].Kind == FrameKind.Def)
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsAssignOp(Token t) =>
        t.Kind == Tk.Punct && t.Text is "=" or "||=" or "&&=" or "+=" or "-=" or "*=" or "/=" or "%=" or "**=" or "|=" or "&=" or "^=" or "<<=" or ">>=";

    /// <summary>Multiple assignment `a, b = ...` (statement-start lookahead): sets <c>_lhsEnd</c> to the `=` token.</summary>
    private void DetectMultiAssign(Token next)
    {
        if (_lhsEnd < 0 && next is { Kind: Tk.Punct, Text: "," } && StatementStartBefore(_i))
        {
            for (var j = _i; j < _t.Count; j++)
            {
                var tk = _t[j];
                if (tk is { Kind: Tk.Punct, Text: "=" })
                {
                    _lhsEnd = j;
                    break;
                }

                if (!(tk.Kind is Tk.Ident or Tk.IVar || tk is { Kind: Tk.Punct, Text: "," or "*" or "(" or ")" }))
                {
                    break;
                }
            }
        }
    }

    private void Identifier(Token t)
    {
        var prev = _i > 0 ? _t[_i - 1] : default;
        var next = Tok(_i + 1);

        if (_i > 0 && prev is { Kind: Tk.Punct, Text: "." or "&." })
        {
            MethodCallWithReceiver(t);
            _i++;
            return;
        }

        DetectMultiAssign(next);

        if (_i < _lhsEnd)
        {
            if (Scope.Lookup(t.Text) is { } existing)
            {
                Emit(t, LocalSym(existing), LocalAssignRole(existing, false));
            }
            else
            {
                DefineLocal(t.Text, t);
            }

            _i++;
            return;
        }

        var isCall = next is { Kind: Tk.Punct, Text: "(" } && !next.SpaceBefore;
        if (!isCall && Scope.Lookup(t.Text) is { } id)
        {
            var chained = _i >= 2 && _t[_i - 1] is { Kind: Tk.Punct, Text: "=" } && _t[_i - 2].Text == t.Text && next is { Kind: Tk.Punct, Text: "=" };
            var role = IsAssignOp(next) ? LocalAssignRole(id, next.Text is not ("=" or "||=")) : TouchLocal(id);
            Emit(t, LocalSym(id), chained && id >= 0 ? (int)SymbolRole.Definition : role);
            if (next is { Kind: Tk.Punct, Text: "=" or "||=" })
            {
                BeginAssign(id, null);
            }
            else
            {
                SetExpr(_localTypes.GetValueOrDefault(id), _i);
            }

            _i++;
            return;
        }

        if (!isCall && IsAssignOp(next) && next.Text != "=" || (!isCall && next is { Kind: Tk.Punct, Text: "=" }))
        {
            var newId = DefineLocal(t.Text, t, type: AssignedType(_i));
            if (next is { Kind: Tk.Punct, Text: "=" or "||=" })
            {
                BeginAssign(newId, null);
            }

            _i++;
            return;
        }

        if (t.Text == "sig" && !isCall && next is { Kind: Tk.Punct, Text: "{" } or { Kind: Tk.Keyword, Text: "do" })
        {
            CaptureSig(_i + 1);
        }

        if (!isCall && (InClassBody() || _stack.Count == 1 && t.Text is "describe" or "context") && StatementStartBefore(_i) && TryDsl(t))
        {
            return;
        }

        if (t.Text is "attr_accessor" or "attr_reader" or "attr_writer" or "attr" && InClassBody())
        {
            Attrs(t);
            return;
        }

        if (t.Text is "include" or "extend" or "prepend" && !isCall)
        {
            AncestryCall(t.Text, _i + 1);
        }

        // `setup do ... end` / `teardown do ... end` bodies run as instance methods of the test class.
        if (!isCall && t.Text is "setup" or "teardown" or "before" or "after" && InClassBody() && StatementStartBefore(_i)
            && CurTypeFrame() is { } typeFrame && (t.Text is "setup" or "teardown" || FindSpec(typeFrame.Path))
            && Tok(_i + 1) is { Kind: Tk.Punct, Text: "{" } or { Kind: Tk.Keyword, Text: "do" })
        {
            _instanceBlock = typeFrame.Path;
        }

        // Implicit-self method call.
        var (path, singleton) = SelfContext();
        var name = t.Text;
        var self = new RType(path, singleton);
        EmitCall(t, name, self, next);
        _i++;
    }

    private static string? MethodSymbol(RubyProject p, MethodTarget? m) =>
        m is { } t ? p.MethodSymbol(t.Owner, t.Singleton, t.Name, t.Core) : null;

    /// <summary>Emits a (possibly typed) method reference and records its return type for chaining.</summary>
    private void EmitCall(Token t, string name, RType? recv, Token next, TypeFn? recvFn = null)
    {
        if (recv is null && recvFn is null)
        {
            SetExpr(null, _i);
            _lastCall = null;
            return;
        }

        if (next is { Kind: Tk.Punct, Text: "=" } && Tok(_i + 2) is not { Kind: Tk.Punct, Text: "=" })
        {
            // `recv.foo = value` calls the setter.
            name += "=";
        }

        var captured = name;
        Func<RubyProject, MethodTarget?> target = p =>
        {
            var r = recvFn is null ? recv : recvFn(p);
            return r is { } rt ? p.LookupMethod(rt, captured) : null;
        };
        Emit(t, null, 0, p => MethodSymbol(p, target(p)));
        SetExpr(p =>
        {
            var r = recvFn is null ? recv : recvFn(p);
            return r is null ? null : p.ReturnOf(target(p), r, captured);
        }, _i);
        _lastCall = target;
        if (Tok(_i + 1) is { Kind: Tk.Label, SpaceBefore: true })
        {
            _cmdTarget = target;
        }
    }

    private void CallLabel(Token label)
    {
        var target = _parens.Count > 0 ? _parens.Peek().Target : null;
        target ??= _cmdTarget;
        if (target is null)
        {
            return;
        }

        var name = label.Text;
        Emit(label, null, 0, p =>
        {
            if (target(p) is not { Core: false } m || !p.KwParams.Contains(RubyProject.RetKey(m.Owner, m.Singleton, m.Name) + "(" + name))
            {
                return null;
            }

            return p.ParamSymbol(m.Owner, m.Singleton, m.Name, name);
        }, endCol: label.Col + label.Text.Length);
    }

    private void DefineKeywordParam(Token t)
    {
        var frame = Top;
        var sym = _project.ParamSymbol(frame.OwnerPath, frame.OwnerSingleton, frame.MethodName, t.Text);
        var id = _nextKw--;
        Scope.Declare(t.Text, id);
        _kwSymbols[id] = sym;
        _project.KwParams.Add(RubyProject.RetKey(frame.OwnerPath, frame.OwnerSingleton, frame.MethodName) + "(" + t.Text);
        if (frame.Sig is { } sig && sig.Params.TryGetValue(t.Text, out var fn))
        {
            _localTypes[id] = fn;
        }

        WithVarDocs(Emit(t, sym, (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Parameter, endCol: t.Col + t.Text.Length), t.Text, ParamDocType(t, true));
    }

    /// <summary>The type shown in the hover of a parameter: the declared type of the sig, wrapped for splats.</summary>
    private string ParamDocType(Token t, bool methodParams)
    {
        var declared = methodParams && Top.Sig is { } sig && sig.ParamText.TryGetValue(t.Text, out var text) ? text : Untyped;
        var prev = Tok(_i - 1);
        return prev switch
        {
            { Kind: Tk.Punct, Text: "*" } => $"T::Array[{declared}]",
            { Kind: Tk.Punct, Text: "**" } => $"T::Hash[Symbol, {declared}]",
            _ => declared,
        };
    }

    private void MethodCallWithReceiver(Token t)
    {
        var name = t.Text;
        var recvFn = _exprEnd == _i - 2 ? _exprType : null;
        if (_i >= 2 && _t[_i - 2] is { Kind: Tk.Const, Text: "T" } && (_i < 3 || _t[_i - 3] is not { Kind: Tk.Punct, Text: "::" }))
        {
            _pendingTCall = name;
        }

        if (recvFn is null)
        {
            EmitCall(t, name, null, Tok(_i + 1));
            return;
        }

        EmitCall(t, name, null, Tok(_i + 1), recvFn);
    }

    private void Attrs(Token t)
    {
        var (path, _) = DefOwner();
        var owner = _project.Type(path);
        var (singletonCtx, _) = (Top.Kind == FrameKind.Singleton, 0);
        var reader = t.Text is "attr_accessor" or "attr_reader" or "attr";
        var writer = t.Text is "attr_accessor" or "attr_writer";
        var sigInfo = _pendingSig;
        _pendingSig = null;
        _i++;
        while (Tok(_i).Kind == Tk.Symbol || Tok(_i) is { Kind: Tk.Punct, Text: "," } || Tok(_i).Kind == Tk.Str)
        {
            var s = _t[_i];
            if (s.Kind == Tk.Symbol)
            {
                if (writer)
                {
                    (singletonCtx ? owner.SingletonMethods : owner.Methods).Add(s.Text + "=");
                    var setterMd = PrettyMethod(s.Text + "=", singletonCtx, [new ParamSpec(s.Text, ParamKind.Required)], SetterSig(sigInfo, s.Text));
                    WithDocs(Emit(s, _project.MethodSymbol(path, singletonCtx, s.Text + "="), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Setter), _ => setterMd);
                }

                if (reader)
                {
                    (singletonCtx ? owner.SingletonMethods : owner.Methods).Add(s.Text);
                    var getterMd = PrettyMethod(s.Text, singletonCtx, [], sigInfo);
                    WithDocs(Emit(s, _project.MethodSymbol(path, singletonCtx, s.Text), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Getter), _ => getterMd);
                    if (sigInfo?.Ret is { } attrRet)
                    {
                        _project.MethodRets[RubyProject.RetKey(path, singletonCtx, s.Text)] = attrRet;
                        _project.FieldTypes.TryAdd(RubyProject.RetKey(path, singletonCtx, "@" + s.Text), attrRet);
                    }
                }
            }

            _i++;
        }
    }

    // ---- fields -------------------------------------------------------------------------------------------

    private void Field(Token t)
    {
        var next = Tok(_i + 1);
        DetectMultiAssign(next);
        var assign = IsAssignOp(next) || _i < _lhsEnd;
        var (path, singleton) = SelfContext();
        if (t.Kind == Tk.CVar)
        {
            // Sorbet models class variables as fields of the singleton class.
            singleton = true;
        }

        var symbol = _project.FieldSymbol(path, singleton, t.Text);
        var fieldKey0 = t.Kind == Tk.IVar && !singleton ? (path, t.Text) : ((string[], string)?)null;
        if (fieldKey0 is { } fk)
        {
            _project.Type(fk.Item1).Fields.Add(fk.Item2);
        }

        // A read earlier in the same method makes a later assignment a plain write (upstream's CFG already has the local).
        var prev0 = _i > 0 ? _t[_i - 1] : default;
        var bareCondition = !assign
            && (prev0 is { Kind: Tk.Keyword, Text: "if" or "unless" or "elsif" or "while" or "until" }
                    && next.Kind is Tk.Newline or Tk.Eof or Tk.Keyword && (next.Kind != Tk.Keyword || next.Text == "then")
                || next is { Kind: Tk.Punct, Text: "?", SpaceBefore: true });
        // A bare condition (`x if @flag`) is not a read of the CFG local, so it does not make a later assignment a write.
        var again = (!bareCondition && !FieldScopeFrame().AssignedFields.Add(t.Text) && assign)
            || assign && (next.Text is not ("=" or "||=") && _i >= _lhsEnd || ReadsFieldOnRightHandSide(t));
        if (again)
        {
            Emit(t, symbol, (int)SymbolRole.WriteAccess).Field = fieldKey0;
        }
        else
        {
            // A bare condition (`x if @flag`, `@flag ? a : b`) is emitted without a role by upstream.
            var fieldOcc = Emit(t, symbol, assign ? (int)SymbolRole.Definition : bareCondition ? NoRole : 0, null, assign ? SymbolInformation.Types.Kind.Field : SymbolInformation.Types.Kind.UnspecifiedKind);
            fieldOcc.Field = fieldKey0;
            if (assign && t.Kind == Tk.IVar && path is not ["Object"])
            {
                WithVarDocs(fieldOcc, t.Text, AssignedType(_i), comment: true);
            }
        }

        var fieldKey = RubyProject.RetKey(path, singleton, t.Text);
        if (next is { Kind: Tk.Punct, Text: "=" or "||=" })
        {
            BeginAssign(0, fieldKey);
        }
        else
        {
            SetExpr(p => p.FieldTypes.TryGetValue(fieldKey, out var fn) ? p.Guarded(() => fn(p)) : null, _i);
        }

        _i++;
    }

    private void Global(Token t)
    {
        if (NamedGlobal().IsMatch(t.Text))
        {
            var assign = IsAssignOp(Tok(_i + 1));
            var write = assign && (Tok(_i + 1).Text != "=" || BuiltinGlobals.Contains(t.Text) || ReadsFieldOnRightHandSide(t));
            var globalOcc = Emit(t, _project.GlobalSymbol(t.Text), write ? (int)SymbolRole.WriteAccess : assign ? (int)SymbolRole.Definition : 0, null,
                assign ? SymbolInformation.Types.Kind.Variable : SymbolInformation.Types.Kind.UnspecifiedKind);
            if (assign && !write)
            {
                WithVarDocs(globalOcc, t.Text, AssignedType(_i), comment: true);
            }
        }

        _i++;
    }
}
