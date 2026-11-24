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
                    if (paren)
                    {
                        last = j;
                    }

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

        return (args, last);
    }

    private bool IsSymbolArg(Arg a) => a.Start == a.End && _t[a.Start].Kind == Tk.Symbol;

    private Arg? LabelArg(List<Arg> args, string label) =>
        args.Cast<Arg?>().FirstOrDefault(a => _t[a!.Value.Start] is { Kind: Tk.Label } l && l.Text == label);

    private static string Unquote(string raw) => raw.Length >= 2 && raw[0] is '"' or '\'' ? raw[1..^1] : raw;

    private Occ DefineMethodOcc(Token tok, string[] owner, bool singleton, string name, SymbolInformation.Types.Kind kind, int endLine, int endCol, bool colon = false)
    {
        var info = _project.Type(owner);
        (singleton ? info.SingletonMethods : info.Methods).Add(name);
        _project.InvalidateAncestors();
        var occ = Emit(tok, _project.MethodSymbol(owner, singleton, name), (int)SymbolRole.Definition, null, kind, startCol: colon ? tok.Col - 1 : tok.Col);
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
            case "alias_method":
                {
                    var (args, _) = ReadArgs(_i + 1);
                    if (args.Count >= 2 && _t[args[0].Start].Kind is Tk.Symbol or Tk.Str && _t[args[1].Start].Kind is Tk.Symbol or Tk.Str)
                    {
                        RegisterAlias(Unquote(_t[args[0].Start].Text), Unquote(_t[args[1].Start].Text));
                    }

                    return false;
                }

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
                Tk.Str => Unquote(v.Text),
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

    private void RegisterAlias(string newName, string oldName)
    {
        var (path, singleton) = DefOwner();
        if (!singleton)
        {
            _project.Type(path).Aliases[newName] = oldName;
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
