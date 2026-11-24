namespace Scip.Indexer.Ruby;

/// <summary>
/// Ports of the Sorbet DSL rewriters scip-ruby sees through (<c>T::Struct</c>/<c>T::Props</c> <c>prop</c>/<c>const</c>,
/// ActiveSupport <c>mattr_*</c>/<c>cattr_*</c>/<c>delegate</c>, <c>Forwardable#def_delegator</c>, <c>Struct.new</c>).
/// </summary>
internal sealed partial class RubyWalker
{
    private readonly record struct Arg(int Start, int End);

    /// <summary>Splits the arguments of a command/paren call whose first argument token is at <paramref name="from"/>.</summary>
    private (List<Arg> Args, int LastToken) ReadArgs(int from)
    {
        var args = new List<Arg>();
        var j = from;
        var paren = false;
        if (Tok(j) is { Kind: Tk.Punct, Text: "(" } open && !open.SpaceBefore)
        {
            paren = true;
            j++;
        }

        var depth = 0;
        var start = j;
        var last = j - 1;
        var closer = -1;
        for (; j < _t.Count; j++)
        {
            var tk = _t[j];
            if (tk.Kind == Tk.Eof || (depth == 0 && (tk.Kind == Tk.Newline || tk is { Kind: Tk.Punct, Text: ";" }))
                || (depth == 0 && tk is { Kind: Tk.Keyword, Text: "do" or "if" or "unless" }))
            {
                break;
            }

            if (tk.Kind == Tk.Newline)
            {
                continue;
            }

            if (tk.Kind == Tk.Punct && tk.Text is "(" or "[" or "{")
            {
                depth++;
            }
            else if (tk.Kind == Tk.Punct && tk.Text is ")" or "]" or "}")
            {
                if (depth == 0)
                {
                    closer = paren ? j : -1;
                    break;
                }

                depth--;
            }
            else if (tk is { Kind: Tk.Punct, Text: "," } && depth == 0)
            {
                if (j > start)
                {
                    args.Add(new Arg(start, j - 1));
                }

                start = j + 1;
                continue;
            }

            last = j;
        }

        if (j > start && last >= start)
        {
            args.Add(new Arg(start, last));
        }

        return (args, closer >= 0 ? closer : last);
    }

    private bool IsSymbolArg(Arg a) => a.Start == a.End && _t[a.Start].Kind == Tk.Symbol;

    private Arg? LabelArg(List<Arg> args, string label) =>
        args.Cast<Arg?>().FirstOrDefault(a => _t[a!.Value.Start] is { Kind: Tk.Label } l && l.Text == label);

    private string[]? _lines;

    private string RawText(Token t)
    {
        _lines ??= _doc.Text.Split('\n');
        return t.Line < _lines.Length && t.EndCol <= _lines[t.Line].Length && t.EndCol > t.Col ? _lines[t.Line][t.Col..t.EndCol] : t.Text;
    }

    private string NameOf(Token t) => t.Kind == Tk.Str ? Unquote(RawText(t)) : t.Text;

    private static string Unquote(string raw) => raw.Length >= 2 && raw[0] is '"' or '\'' ? raw[1..^1] : raw;

    private Occ DefineMethodOcc(Token tok, string[] owner, bool singleton, string name, SymbolInformation.Types.Kind kind, int endLine, int endCol, bool colon = false,
        int? startCol = null, int? nameEndCol = null)
    {
        var info = _project.Type(owner);
        (singleton ? info.SingletonMethods : info.Methods).Add(name);
        _project.InvalidateAncestors();
        var occ = Emit(tok, _project.MethodSymbol(owner, singleton, name), (int)SymbolRole.Definition, null, kind, startCol: startCol ?? (colon ? tok.Col - 1 : tok.Col), endCol: nameEndCol);
        var setter = kind == SymbolInformation.Types.Kind.Setter && name.EndsWith('=');
        var synthMd = PrettyMethod(name, singleton, setter ? [new ParamSpec(name[..^1], ParamKind.Required)] : [], null);
        WithDocs(occ, _ => synthMd, comment: false);
        occ.Enclosing = [tok.Line, tok.Col, endLine, endCol];
        return occ;
    }

    private bool TryDsl(Token t)
    {
        switch (t.Text)
        {
            case "prop" or "const":
                return PropDsl(t);
            case "mattr_reader" or "mattr_writer" or "mattr_accessor" or "cattr_reader" or "cattr_writer" or "cattr_accessor"
                or "class_attribute":
                return AttrDsl(t);
            case "module_function":
                {
                    var (args, last) = ReadArgs(_i + 1);
                    var (path, _) = DefOwner();
                    var end = _t[Math.Max(last, args.Count > 0 ? args[^1].End : last)];
                    foreach (var a in args.Where(IsSymbolArg))
                    {
                        DefineMethodOcc(_t[a.Start], path, true, _t[a.Start].Text, SymbolInformation.Types.Kind.Method, end.Line, end.EndCol, true);
                    }

                    return false;
                }

            case "alias_method":
                {
                    var (args, _) = ReadArgs(_i + 1);
                    if (args.Count >= 2 && _t[args[0].Start].Kind is Tk.Symbol or Tk.Str && _t[args[1].Start].Kind is Tk.Symbol or Tk.Str)
                    {
                        RegisterAlias(NameOf(_t[args[0].Start]), NameOf(_t[args[1].Start]));
                    }

                    return false;
                }

            case "it" or "specify" or "xspecify" or "before" or "after" or "test":
                return CallThen(t, ExampleDsl(t));
            case "describe" or "context" or "shared_examples" or "shared_context":
                return CallThen(t, DescribeDsl(t));
            case "let" or "let!" or "subject":
                return CallThen(t, LetDsl(t));
            case "encrypted_prop":
                return CallThen(t, EncryptedPropDsl(t));
            case "token_prop" or "created_prop" or "timestamped_token_prop":
                return CallThen(t, OpusPropDsl(t));
            case "flatfile":
                if (Tok(_i + 1) is { Kind: Tk.Keyword, Text: "do" })
                {
                    _flatfileBlock = true;
                }

                return false;
            case "from" or "pattern" or "field" when _stack.Any(f => f.IsFlatfile):
                return CallThen(t, FlatfileDsl(t));
            case "delegate":
                return DelegateDsl(t);
            case "def_delegator" or "def_instance_delegator" or "def_delegators" or "def_instance_delegators":
                return DefDelegatorDsl(t);
            default:
                return false;
        }
    }

    private bool PropDsl(Token t)
    {
        var (args, last) = ReadArgs(_i + 1);
        if (args.Count == 0 || !IsSymbolArg(args[0]))
        {
            return false;
        }

        var (path, _) = DefOwner();
        var sym = _t[args[0].Start];
        var end = _t[Math.Max(last, args[0].End)];
        if (t.Text == "prop")
        {
            DefineMethodOcc(sym, path, false, sym.Text + "=", SymbolInformation.Types.Kind.Setter, end.Line, end.EndCol);
        }

        DefineMethodOcc(sym, path, false, sym.Text, SymbolInformation.Types.Kind.Getter, end.Line, end.EndCol);
        if (args.Count > 1 && ParseType(args[1].Start).Fn is { } fn)
        {
            _project.MethodRets[RubyProject.RetKey(path, false, sym.Text)] = fn;
            _project.FieldTypes.TryAdd(RubyProject.RetKey(path, false, "@" + sym.Text), fn);
        }

        _i++;
        return true;
    }

    /// <summary>Parses <c>(name)</c> / <c>name</c> where name is a plain string or symbol, followed by a block opener.</summary>
    private bool NameThenBlock(out Token tok, out string text, out int startCol, out int endCol, bool allowConstant = false)
    {
        tok = default;
        text = "";
        startCol = endCol = 0;
        var j = _i + 1;
        Token? open = Tok(j) is { Kind: Tk.Punct, Text: "(", SpaceBefore: false } o ? o : null;
        if (open is not null)
        {
            j++;
        }

        var arg = Tok(j);
        if (arg.Line != _t[_i].Line)
        {
            return false;
        }

        if (arg.Kind == Tk.Str)
        {
            var raw = RawText(arg);
            if (raw.Length < 2 || raw[0] is not ('"' or '\'') || raw[^1] != raw[0] || raw.Contains("#{", StringComparison.Ordinal))
            {
                return false;
            }

            text = Unquote(raw);
            startCol = NameStart(arg, raw, text);
            endCol = arg.EndCol;
        }
        else if (arg.Kind == Tk.Symbol)
        {
            text = arg.Text;
            startCol = arg.Col - 1;
            endCol = arg.EndCol;
        }
        else if (allowConstant && arg.Kind == Tk.Const && TryReadChain(j, out var chainLast, out var chainSegs, out var chainToks, out var chainAbs) && !chainAbs)
        {
            // `describe Rack::MediaType do`: named after the constant, located at its last segment.
            text = string.Join("::", chainSegs);
            startCol = chainToks[^1].Col;
            endCol = chainToks[^1].EndCol;
            arg = chainToks[^1];
            j = chainLast;
        }
        else
        {
            return false;
        }

        tok = arg;
        j++;
        if (open is not null)
        {
            if (Tok(j) is not { Kind: Tk.Punct, Text: ")" } c)
            {
                return false;
            }

            startCol = arg.Kind == Tk.Str ? (text.Contains("::", StringComparison.Ordinal) ? startCol : open.Value.Col) : startCol;
            endCol = arg.Kind == Tk.Str ? c.EndCol : endCol;
            j++;
        }

        return Tok(j) is { Kind: Tk.Keyword, Text: "do" } or { Kind: Tk.Punct, Text: "{" };
    }

    /// <summary>
    /// Upstream locates a spec name that contains `::` after its last `::` (it treats the text as a constant path); otherwise
    /// the literal starts at its opening quote.
    /// </summary>
    private static int NameStart(Token str, string raw, string text)
    {
        var at = text.LastIndexOf("::", StringComparison.Ordinal);
        return at < 0 ? str.Col : str.Col + (raw.Length - text.Length) / 2 + at + 2;
    }

    private bool DescribeDsl(Token t)
    {
        if (Top.Kind is not (FrameKind.Class or FrameKind.Module or FrameKind.Other) && _stack.Count > 1 || InMethodBody() || !NameThenBlock(out var tok, out var text, out var start, out var end, allowConstant: true))
        {
            return false;
        }

        var (parent, singleton) = DefOwner();
        if (singleton)
        {
            return false;
        }

        if (_stack.Count == 1)
        {
            // Top-level `describe Foo do`: the group is not nested in anything.
            parent = [];
        }

        var kind = t.Text is "shared_context" ? "shared_examples" : t.Text;
        string[] path = [.. parent, $"<{kind} '{text}'>"];
        var info = _project.Type(path);
        info.Defined = true;
        info.ProjectDefined = true;
        info.IsSpec = true;
        if (parent.Length > 0)
        {
            // A nested `describe` is a subclass of the enclosing group.
            info.Super ??= new RefSpec([], parent, true);
        }

        _project.InvalidateAncestors();
        var sym = _project.TypeSymbol(path);
        var describeMd = $"class {RubyProject.Key(path)}" + (parent.Length > 0 ? $" < {RubyProject.Key(parent)}" : "");
        AddOcc(new Occ { Line = tok.Line, Col = start, EndCol = end, Symbol = sym, Roles = (int)SymbolRole.Definition, Kind = SymbolInformation.Types.Kind.Class, Markdown = _ => describeMd });
        if (parent.Length > 0)
        {
            AddOcc(new Occ { Line = tok.Line, Col = start, EndCol = end, Symbol = _project.TypeSymbol(parent) });
        }

        _structBlock = path;
        _i++;
        return true;
    }

    private bool LetDsl(Token t)
    {
        if (!InClassBody() || InMethodBody() || !NameThenBlock(out var tok, out var text, out var start, out var end))
        {
            return false;
        }

        var (path, singleton) = DefOwner();
        if (singleton || FindSpec(path) is false)
        {
            return false;
        }

        _project.Type(path).Methods.Add(text);
        var occ = new Occ
        {
            Line = tok.Line, Col = start, EndCol = end, Symbol = _project.MethodSymbol(path, false, text), Roles = (int)SymbolRole.Definition,
            Kind = SymbolInformation.Types.Kind.Method,
        };
        var letMd = PrettyMethod(text, false, [], null);
        occ.Markdown = _ => letMd;
        AddOcc(occ);
        _exampleBlock = (path, text, occ, t.Line, t.Col);
        _i++;
        return true;
    }

    private bool FindSpec(string[] path) => _project.FindType(path) is { IsSpec: true };

    private bool ExampleDsl(Token t)
    {
        var (path, singleton) = DefOwner();
        if (singleton || Top.Kind != FrameKind.Class && Top.Kind != FrameKind.Module && Top.Kind != FrameKind.Block)
        {
            return false;
        }

        string name;
        int startCol, endCol, line = t.Line;
        if (t.Text is "before" or "after")
        {
            if (Tok(_i + 1) is not ({ Kind: Tk.Keyword, Text: "do" } or { Kind: Tk.Punct, Text: "{" }))
            {
                return false;
            }

            name = $"<{t.Text}>";
            startCol = t.Col;
            endCol = t.EndCol;
        }
        else
        {
            var j = _i + 1;
            Token? open = Tok(j) is { Kind: Tk.Punct, Text: "(", SpaceBefore: false } o ? o : null;
            if (open is not null)
            {
                j++;
            }

            var str = Tok(j);
            if (str.Kind != Tk.Str || str.Line != t.Line)
            {
                return false;
            }

            var raw = RawText(str);
            if (raw.Length < 2 || raw[0] is not ('"' or '\'') || raw[^1] != raw[0] || raw.Contains("#{", StringComparison.Ordinal))
            {
                return false;
            }

            j++;
            Token? close = null;
            if (open is not null)
            {
                if (Tok(j) is not { Kind: Tk.Punct, Text: ")" } c)
                {
                    return false;
                }

                close = c;
                j++;
            }

            if (Tok(j) is not ({ Kind: Tk.Keyword, Text: "do" } or { Kind: Tk.Punct, Text: "{" }))
            {
                return false;
            }

            name = $"<{t.Text} '{Unquote(raw)}'>";
            startCol = Unquote(raw).Contains("::", StringComparison.Ordinal) ? NameStart(str, raw, Unquote(raw)) : open?.Col ?? str.Col;
            endCol = close?.EndCol ?? str.EndCol;
            if (t.Text == "test")
            {
                // Minitest's `test "does x"` defines `test_does_x`; upstream locates it at the string.
                name = "test_" + System.Text.RegularExpressions.Regex.Replace(Unquote(raw), @"\s+", "_");
                startCol = str.Col;
                endCol = str.EndCol;
            }
        }

        var owner = _project.Type(path);
        owner.Methods.Add(name);
        var sym = _project.MethodSymbol(path, false, name);
        var occ = new Occ
        {
            Line = line, Col = startCol, EndCol = endCol, Roles = (int)SymbolRole.Definition, Kind = SymbolInformation.Types.Kind.Method,
            Resolver = p => p.IsSpecGroup(path) ? sym : null,
        };
        var exampleMd = PrettyMethod(name, false, [], null);
        occ.Markdown = _ => exampleMd;
        AddOcc(occ);
        _exampleBlock = (path, name, occ, t.Line, t.Col);
        _i++;
        return true;
    }

    /// <summary>Handled DSL calls whose helper is user-defined still reference that helper, as an ordinary call would.</summary>
    private bool CallThen(Token t, bool handled)
    {
        if (handled)
        {
            var (path, singleton) = SelfContext();
            EmitCall(t, t.Text, new RType(path, singleton), Tok(_i));
        }

        return handled;
    }

    private bool EncryptedPropDsl(Token t)
    {
        var (args, last) = ReadArgs(_i + 1);
        if (args.Count == 0 || !IsSymbolArg(args[0]))
        {
            return false;
        }

        var (path, _) = DefOwner();
        var sym = _t[args[0].Start];
        var end = _t[Math.Max(last, args[^1].End)];
        var immutable = LabelArg(args, "immutable") is { } im && _t[Math.Min(im.End, im.Start + 1)] is { Kind: Tk.Keyword, Text: "true" };
        foreach (var name in new[] { sym.Text, "encrypted_" + sym.Text })
        {
            if (!immutable)
            {
                DefineMethodOcc(sym, path, false, name + "=", SymbolInformation.Types.Kind.Setter, end.Line, end.EndCol);
            }

            DefineMethodOcc(sym, path, false, name, SymbolInformation.Types.Kind.Getter, end.Line, end.EndCol);
        }

        _i++;
        return true;
    }

    private bool OpusPropDsl(Token t)
    {
        var name = t.Text switch { "token_prop" or "timestamped_token_prop" => "token", _ => "created" };
        var (args, last) = ReadArgs(_i + 1);
        var immutable = LabelArg(args, "immutable") is { } im && _t[Math.Min(im.End, im.Start + 1)] is { Kind: Tk.Keyword, Text: "true" };
        var (path, _) = DefOwner();
        var end = _t[Math.Max(last, _i)];
        var start = t.Col + t.Text.Length - "_prop".Length - name.Length;
        if (!immutable)
        {
            DefineMethodOcc(t, path, false, name + "=", SymbolInformation.Types.Kind.Setter, end.Line, end.EndCol, false, start, start + name.Length);
        }

        DefineMethodOcc(t, path, false, name, SymbolInformation.Types.Kind.Getter, end.Line, end.EndCol, false, start, start + name.Length);
        _i++;
        return true;
    }

    private bool FlatfileDsl(Token t)
    {
        var (args, last) = ReadArgs(_i + 1);
        var sym = args.LastOrDefault(IsSymbolArg);
        if (sym.End == 0 && sym.Start == 0)
        {
            return false;
        }

        var (path, _) = DefOwner();
        var tok = _t[sym.Start];
        var end = _t[Math.Max(last, sym.End)];
        DefineMethodOcc(tok, path, false, tok.Text + "=", SymbolInformation.Types.Kind.Setter, end.Line, end.EndCol, true);
        DefineMethodOcc(tok, path, false, tok.Text, SymbolInformation.Types.Kind.Getter, end.Line, end.EndCol, true);
        _i++;
        return true;
    }

    private bool AttrDsl(Token t)
    {
        var (args, last) = ReadArgs(_i + 1);
        var names = args.Where(IsSymbolArg).Select(a => _t[a.Start]).ToList();
        if (names.Count == 0)
        {
            return false;
        }

        var (path, singletonCtx) = DefOwner();
        bool Off(string label) => LabelArg(args, label) is { } a && _t[Math.Min(a.End, a.Start + 1)] is { Kind: Tk.Keyword, Text: "false" };
        var kind = t.Text[(t.Text.IndexOf('_') + 1)..];
        var reader = kind is "reader" or "accessor" or "attribute";
        var writer = kind is "writer" or "accessor";
        var instanceAccessor = !Off("instance_accessor");
        var instanceReader = instanceAccessor && !Off("instance_reader");
        var instanceWriter = instanceAccessor && !Off("instance_writer");
        var end = _t[Math.Max(last, args[^1].End)];
        var isMattr = t.Text.StartsWith("mattr", StringComparison.Ordinal) || t.Text.StartsWith("cattr", StringComparison.Ordinal);
        foreach (var n in names)
        {
            if (isMattr)
            {
                if (reader)
                {
                    DefineMethodOcc(n, path, true, n.Text, SymbolInformation.Types.Kind.Getter, end.Line, end.EndCol, true);
                }

                if (writer)
                {
                    DefineMethodOcc(n, path, true, n.Text + "=", SymbolInformation.Types.Kind.Setter, end.Line, end.EndCol, true);
                }
            }

            if (reader && (instanceReader || !isMattr))
            {
                DefineMethodOcc(n, path, singletonCtx && !isMattr, n.Text, SymbolInformation.Types.Kind.Getter, end.Line, end.EndCol, true);
            }

            if (writer && instanceWriter)
            {
                DefineMethodOcc(n, path, singletonCtx && !isMattr, n.Text + "=", SymbolInformation.Types.Kind.Setter, end.Line, end.EndCol, true);
            }
        }

        _i++;
        return true;
    }

    private bool DelegateDsl(Token t)
    {
        var (args, last) = ReadArgs(_i + 1);
        var names = args.Where(IsSymbolArg).Select(a => _t[a.Start]).ToList();
        if (names.Count == 0 || LabelArg(args, "to") is not { } to)
        {
            return false;
        }

        string prefix = "";
        if (LabelArg(args, "prefix") is { } pf)
        {
            var v = _t[Math.Min(pf.End, pf.Start + 1)];
            prefix = v.Kind switch
            {
                Tk.Symbol => v.Text,
                Tk.Str => Unquote(RawText(v)),
                Tk.Keyword when v.Text == "true" => _t[Math.Min(to.End, to.Start + 1)] is var tv ? Unquote(tv.Text) : "",
                _ => "",
            };
            prefix = prefix.Length > 0 ? prefix + "_" : "";
        }

        var (path, _) = DefOwner();
        var end = _t[Math.Max(last, args[^1].End)];
        foreach (var n in names)
        {
            DefineMethodOcc(n, path, false, prefix + n.Text, SymbolInformation.Types.Kind.Method, end.Line, end.EndCol, true);
        }

        _i++;
        return true;
    }

    private bool DefDelegatorDsl(Token t)
    {
        var (args, last) = ReadArgs(_i + 1);
        var (path, _) = DefOwner();
        var plural = t.Text.EndsWith('s');
        if (args.Count < 2)
        {
            return false;
        }

        var end = _t[Math.Max(last, args[^1].End)];
        if (plural)
        {
            foreach (var a in args.Skip(1).Where(IsSymbolArg))
            {
                DefineMethodOcc(_t[a.Start], path, false, _t[a.Start].Text, SymbolInformation.Types.Kind.Method, end.Line, end.EndCol, true);
            }
        }
        else
        {
            var target = args.Count > 2 && IsSymbolArg(args[2]) ? args[2] : args[1];
            if (IsSymbolArg(target))
            {
                DefineMethodOcc(_t[target.Start], path, false, _t[target.Start].Text, SymbolInformation.Types.Kind.Method, end.Line, end.EndCol, true);
            }
        }

        _i++;
        return true;
    }

    private void BeginBranches(Frame f)
    {
        f.BranchBase = [.. FieldScopeFrame().AssignedFields];
        f.BranchUnion = [];
    }

    private void NextBranch()
    {
        if (Top is { BranchBase: { } b, BranchUnion: { } u })
        {
            var cur = FieldScopeFrame().AssignedFields;
            u.UnionWith(cur);
            cur.Clear();
            cur.UnionWith(b);
        }
    }

    private void EndBranches()
    {
        if (Top is { BranchUnion: { } u })
        {
            FieldScopeFrame().AssignedFields.UnionWith(u);
        }
    }

    private Frame FieldScopeFrame()
    {
        for (var k = _stack.Count - 1; k >= 0; k--)
        {
            if (_stack[k].Kind is FrameKind.Def or FrameKind.Class or FrameKind.Module or FrameKind.Singleton)
            {
                return _stack[k];
            }
        }

        return _stack[0];
    }

    private void RegisterAlias(string newName, string oldName)
    {
        var (path, singleton) = DefOwner();
        if (!singleton)
        {
            _project.Type(path).Aliases[newName] = oldName;
            _project.InvalidateAncestors();
        }
        else if (_project.Type(path).SingletonMethods.Add(newName))
        {
            // `class << self; alias_method :it, :test; end` makes `it` available on the class itself.
            _project.InvalidateAncestors();
        }
    }

    /// <summary>
    /// A method whose last statement is <c>@x = value</c> returns that value: upstream reports a reference to the
    /// field over the whole assignment expression (single-line only).
    /// </summary>
    private void ReturnedAssignment()
    {
        var j = _i - 1;
        while (j >= 0 && (_t[j].Kind == Tk.Newline || _t[j] is { Kind: Tk.Punct, Text: ";" }))
        {
            j--;
        }

        var last = j;
        var depth = 0;
        for (; j >= 0; j--)
        {
            var tk = _t[j];
            if (tk.Kind == Tk.Punct && tk.Text is ")" or "]" or "}")
            {
                depth++;
            }
            else if (tk.Kind == Tk.Punct && tk.Text is "(" or "[" or "{")
            {
                if (--depth < 0)
                {
                    return;
                }
            }
            else if (depth == 0 && (tk.Kind == Tk.Newline || tk is { Kind: Tk.Punct, Text: ";" } || tk is { Kind: Tk.Keyword, Text: "def" }))
            {
                break;
            }
        }

        var start = j + 1;
        if (last <= start || _t[start].Kind is not (Tk.IVar or Tk.CVar) || _t[start + 1] is not { Kind: Tk.Punct, Text: "=" }
            || _t[start].Line != _t[last].Line)
        {
            return;
        }

        var (path, singleton) = SelfContext();
        if (_t[start].Kind == Tk.CVar)
        {
            singleton = true;
        }

        Emit(_t[start], _project.FieldSymbol(path, singleton, _t[start].Text), 0, null, SymbolInformation.Types.Kind.UnspecifiedKind,
            endCol: _t[last].EndCol);
    }

    // ---- Struct.new ---------------------------------------------------------------------------------------

    private bool IsStructNew(int i) =>
        Tok(i) is { Kind: Tk.Const, Text: "Struct" } && Tok(i + 1) is { Kind: Tk.Punct, Text: "." } && Tok(i + 2) is { Kind: Tk.Ident, Text: "new" };

    private void DefineStruct(Token constTok, string[] path, int structIdx)
    {
        var info = _project.Type(path);
        info.Defined = true;
        info.ProjectDefined = true;
        info.Super ??= new RefSpec([], ["Struct"], true);
        _project.Type(path[..^1]).Constants.Add(path[^1]);
        _project.InvalidateAncestors();
        Emit(constTok, _project.TypeSymbol(path), 0);
        var def = Emit(constTok, _project.TypeSymbol(path), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Class);
        var (args, last) = ReadArgs(structIdx + 3);
        var endTok = _t[Math.Max(last, structIdx + 2)];
        info.Methods.Add("initialize");
        Emit(constTok, _project.MethodSymbol(path, false, "initialize"), (int)SymbolRole.Definition, null, SymbolInformation.Types.Kind.Method);
        foreach (var a in args.Where(IsSymbolArg))
        {
            var n = _t[a.Start];
            DefineMethodOcc(n, path, false, n.Text + "=", SymbolInformation.Types.Kind.Setter, n.Line, n.EndCol);
            DefineMethodOcc(n, path, false, n.Text, SymbolInformation.Types.Kind.Getter, n.Line, n.EndCol);
        }

        // A trailing `do`/`{` block is a class body for `def`s.
        var after = Tok(last + 1);
        var hasBlock = after is { Kind: Tk.Keyword, Text: "do" } || (after is { Kind: Tk.Punct, Text: "{" });
        if (hasBlock)
        {
            _structBlock = path;
        }

        def.Enclosing = null;
        _ = endTok;
    }
}
