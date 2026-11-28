using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Dart;

/// <summary>
/// A tolerant, declaration-level Dart parser. It recovers the declaration structure (types, members,
/// constructors, parameters, directives) from a token stream and skips function bodies and initializers.
/// It is not a full Dart parser: no expressions are analysed.
/// </summary>
internal sealed partial class DartParser
{
    private static readonly Token Eof = new(TokenKind.Punctuation, "", int.MaxValue, 0);

    private static readonly HashSet<string> Modifiers =
        ["static", "final", "const", "late", "covariant", "external", "abstract", "factory", "var", "required", "augment"];

    private static readonly HashSet<string> TypeModifiers = ["abstract", "base", "final", "sealed", "interface"];

    private readonly string _src;
    private readonly List<Token> _t;
    private readonly IReadOnlyList<string> _docs;
    private readonly FileModel _m;
    private int _i;

    private DartParser(string src, List<Token> tokens, IReadOnlyList<string> docs, FileModel model)
    {
        _src = src;
        _t = tokens;
        _docs = docs;
        _m = model;
    }

    public static FileModel Parse(string relativePath, string source, ExternalPackage? external = null)
    {
        var (tokens, docs, docRefs, docStarts) = DartLexer.LexFull(source);
        var model = new FileModel(relativePath) { Tokens = tokens, DocRefs = docRefs, External = external };
        model.DocStarts = docStarts;
        var parser = new DartParser(source, tokens, docs, model);
        parser.ParseFile();
        parser.ScanLocals();
        DemoteDuplicates(model);
        return model;
    }

    /// <summary>
    /// Local functions (and their parameters), or other tolerantly parsed declarations, can share a symbol path with an
    /// earlier declaration of the same file. Every later duplicate becomes a <c>local N</c> symbol so each global symbol
    /// is defined exactly once per document.
    /// </summary>
    private static void DemoteDuplicates(FileModel model)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in model.AllDecls)
        {
            if (d.IsLocal)
            {
                continue;
            }

            var key = string.Join('\u0001', d.Path.Select(x => x.Name + (int)x.Suffix));
            if (!seen.Add(key) || (d.Kind is DeclKind.Parameter or DeclKind.TypeParameter && d.Parent is { IsLocal: true, Kind: DeclKind.Function } && !model.IsTopLevel(d.Parent)))
            {
                d.IsLocal = true;
            }
        }
    }

    private Token At(int i) => i >= 0 && i < _t.Count ? _t[i] : Eof;

    private Token Cur => At(_i);

    private bool IsIdent(int i) => At(i).Kind == TokenKind.Identifier;

    private void ParseFile()
    {
        while (_i < _t.Count)
        {
            var before = _i;
            ParseTopLevel();
            if (_i == before)
            {
                _i++;
            }
        }
    }

    private void SkipAnnotations()
    {
        while (Cur.Is("@"))
        {
            _i++;
            if (IsIdent(_i))
            {
                _i++;
            }

            while (Cur.Is(".") && IsIdent(_i + 1))
            {
                _i += 2;
            }

            if (Cur.Is("("))
            {
                _i = SkipBalanced(_i);
            }
        }
    }

    private string? DocOf(int tokenIndex) => At(tokenIndex).DocIndex is >= 0 and var d ? _docs[d] : null;

    private void ParseTopLevel()
    {
        if (Cur.Is(";"))
        {
            _i++;
            return;
        }

        var start = _i;
        SkipAnnotations();
        var tk = Cur;
        if (tk.Kind == TokenKind.Identifier)
        {
            switch (tk.Text)
            {
                case "import" or "export":
                    ParseDirective();
                    return;
                case "part" when At(_i + 1).Kind == TokenKind.String || At(_i + 1).Is("of"):
                    ParseDirective();
                    return;
                case "library":
                    _i = SkipToSemicolon(_i);
                    return;
            }
        }

        var j = _i;
        while (At(j).Kind == TokenKind.Identifier && TypeModifiers.Contains(At(j).Text) && IsIdent(j + 1))
        {
            j++;
        }

        if (At(j).Is("mixin") && At(j + 1).Is("class"))
        {
            j++;
        }

        var kw = At(j);
        if (kw.Kind == TokenKind.Identifier && kw.Text is "class" or "mixin" or "enum" or "extension" or "typedef" && IsIdent(j + 1))
        {
            ParseTypeLike(start, j);
            return;
        }

        if (kw.Is("extension") && (At(j + 1).Is("on") || At(j + 1).Is("<")))
        {
            ParseTypeLike(start, j);
            return;
        }

        ParseMember(null, start);
    }

    private void ParseDirective()
    {
        var kw = Cur.Text;
        _i++;
        if (kw == "part" && Cur.Is("of"))
        {
            _i++;
        }

        DirectiveRef? directive = null;
        if (Cur.Kind == TokenKind.String)
        {
            directive = new DirectiveRef(Cur, kw);
            _m.Directives.Add(directive);
        }

        List<string>? combinator = null;
        while (_i < _t.Count && !Cur.Is(";"))
        {
            if (Cur.Is("as") && IsIdent(_i + 1))
            {
                var prefix = At(_i + 1);
                if (directive is not null)
                {
                    directive.Prefix = prefix.Text;
                }

                _m.ImportPrefixes.Add(prefix.Text);
                var pd = NewDecl(DeclKind.ImportPrefix, prefix.Text, null, prefix, [(prefix.Text, SymbolDescriptor.Types.Suffix.Term)], prefix.Offset, null);
                _m.TopLevel.Remove(pd);
                _m.PrefixDecls[prefix.Text] = pd;
                combinator = null;
            }
            else if (directive is not null && (Cur.Is("show") || Cur.Is("hide")) && IsIdent(_i + 1))
            {
                combinator = [];
                if (Cur.Is("show"))
                {
                    directive.Show = combinator;
                }
                else
                {
                    directive.Hide = combinator;
                }
            }
            else if (combinator is not null && Cur.Kind == TokenKind.Identifier)
            {
                combinator.Add(Cur.Text);
            }
            else if (Cur.Is("if") && At(_i + 1).Is("("))
            {
                _i = SkipBalanced(_i + 1);
                if (At(_i).Kind == TokenKind.String)
                {
                    _i++;
                }

                continue;
            }

            _i++;
        }

        _i++;
    }

    // ---- type-like declarations -------------------------------------------------------------------------

    private void ParseTypeLike(int start, int kwIdx)
    {
        var startTok = _t[start];
        var doc = DocOf(start);
        var kw = _t[kwIdx].Text;
        var p = kwIdx + 1;

        if (kw == "typedef")
        {
            ParseTypedef(start, p, doc);
            return;
        }

        if (kw == "extension" && At(p).Is("type"))
        {
            p++;
            if (At(p).Is("const"))
            {
                p++;
            }
        }

        Token? nameTok = null;
        if (!At(p).Is("on") && !At(p).Is("<"))
        {
            nameTok = At(p);
            p++;
        }

        var typeParamsAt = At(p).Is("<") ? p : -1;
        if (typeParamsAt >= 0)
        {
            p = SkipAngle(p);
        }

        var decl = nameTok is { } nt
            ? NewDecl(kw switch
            {
                "mixin" => DeclKind.Mixin,
                "enum" => DeclKind.Enum,
                "extension" => DeclKind.Extension,
                _ => DeclKind.Class,
            }, nt.Text, null, nt, [(nt.Text, SymbolDescriptor.Types.Suffix.Type)], startTok.Offset, doc)
            : null;

        // Upstream quirk: members of an unnamed extension are symbolised under the name `null`.
        var holder = decl ?? new Decl(DeclKind.Extension, "", null, Eof, _m.RelativePath)
        {
            Path = [("null", SymbolDescriptor.Types.Suffix.Type)],
            EnclosingStart = startTok.Offset,
        };
        if (decl is null)
        {
            _m.TopLevel.Add(holder);
        }

        if (typeParamsAt >= 0)
        {
            ParseTypeParams(typeParamsAt, holder);
        }

        // header: extends / with / implements / on
        string? state = null;
        var expectType = false;
        while (p < _t.Count && !At(p).Is("{") && !At(p).Is(";"))
        {
            var tk = At(p);
            if (tk.Is("("))
            {
                p = SkipBalanced(p);
                continue;
            }

            if (tk.Kind == TokenKind.Identifier && tk.Text is "extends" or "with" or "implements" or "on")
            {
                state = tk.Text;
                expectType = true;
                p++;
                continue;
            }

            if (tk.Is("="))
            {
                state = "extends";
                expectType = true;
                p++;
                continue;
            }

            if (tk.Is(","))
            {
                expectType = true;
                p++;
                continue;
            }

            if (expectType && state is not null && tk.Kind == TokenKind.Identifier)
            {
                var name = tk.Text;
                while (At(p + 1).Is(".") && IsIdent(p + 2))
                {
                    name = At(p + 2).Text;
                    p += 2;
                }

                decl?.Supers.Add(name);
                expectType = false;
                p++;
                if (At(p).Is("<"))
                {
                    p = SkipAngle(p);
                }

                continue;
            }

            p++;
        }

        _i = p;
        if (decl is not null)
        {
            decl.Signature = Collapse(startTok.Offset, At(p).Offset == int.MaxValue ? _src.Length : At(p).Offset);
        }

        if (Cur.Is("{"))
        {
            ParseBody(holder, kw == "enum");
            holder.EnclosingEnd = At(_i - 1).End;
        }
        else
        {
            if (Cur.Is(";"))
            {
                _i++;
            }

            if (decl is not null)
            {
                decl.EnclosingEnd = At(_i - 1).End;
            }
        }
    }

    private void ParseTypedef(int start, int p, string? doc)
    {
        Token? nameTok = null;
        if (IsIdent(p) && (At(p + 1).Is("<") || At(p + 1).Is("=")))
        {
            nameTok = At(p);
        }
        else
        {
            for (var k = p; k < _t.Count && !At(k).Is(";"); k++)
            {
                if (At(k).Is("(") && IsIdent(k - 1))
                {
                    nameTok = At(k - 1);
                    break;
                }
            }
        }

        var end = SkipToSemicolon(p);
        _i = end;
        if (nameTok is { } nt)
        {
            var d = NewDecl(DeclKind.TypeAlias, nt.Text, null, nt, [(nt.Text, SymbolDescriptor.Types.Suffix.Type)], _t[start].Offset, doc);
            d.EnclosingEnd = At(end - 1).End;
            d.Signature = Collapse(d.EnclosingStart, d.EnclosingEnd);
            if (At(p + 1).Is("<"))
            {
                ParseTypeParams(p + 1, d);
            }
        }
    }

    private void ParseBody(Decl owner, bool isEnum)
    {
        owner.BodyStart = Cur.End;
        _i++;
        if (isEnum)
        {
            ParseEnumConstants(owner);
        }

        while (_i < _t.Count && !Cur.Is("}"))
        {
            var before = _i;
            ParseMember(owner, _i);
            if (_i == before)
            {
                _i++;
            }
        }

        owner.BodyEnd = Cur.Offset == int.MaxValue ? _src.Length : Cur.Offset;
        _i++;
    }

    private void ParseEnumConstants(Decl owner)
    {
        while (_i < _t.Count && !Cur.Is("}"))
        {
            if (Cur.Is(";"))
            {
                _i++;
                return;
            }

            var start = _i;
            SkipAnnotations();
            if (IsIdent(_i))
            {
                var nameTok = Cur;
                _i++;
                if (Cur.Is("<"))
                {
                    _i = SkipAngle(_i);
                }

                if (Cur.Is(".") && IsIdent(_i + 1))
                {
                    _i += 2;
                }

                if (Cur.Is("("))
                {
                    _i = SkipBalanced(_i);
                }

                var d = NewDecl(DeclKind.Field, nameTok.Text, owner, nameTok, [.. owner.Path, (nameTok.Text, SymbolDescriptor.Types.Suffix.Term)],
                    _t[start].Offset, DocOf(start));
                d.IsStatic = true;
                d.TypeName = owner.Name;
                d.EnclosingEnd = At(_i - 1).End;
                d.Signature = Collapse(d.EnclosingStart, d.EnclosingEnd);
            }

            if (Cur.Is(","))
            {
                _i++;
            }
            else if (!Cur.Is(";") && !Cur.Is("}"))
            {
                _i++;
            }
        }
    }

    // ---- members ------------------------------------------------------------------------------------------

    private void ParseMember(Decl? parent, int start)
    {
        if (Cur.Is(";"))
        {
            _i++;
            return;
        }

        _i = start;
        SkipAnnotations();
        var startOffset = _t[Math.Min(start, _t.Count - 1)].Offset;
        var doc = DocOf(start);

        var header = new List<Token>();
        string? opName = null;
        Token opTok = default;
        var term = "";
        var angles = new List<int>();
        var j = _i;
        while (j < _t.Count)
        {
            var tk = _t[j];
            if (tk.Is("operator") && At(j + 1).Kind == TokenKind.Punctuation && !At(j + 1).Is("("))
            {
                var k = j + 1;
                var sb = new System.Text.StringBuilder();
                while (k < _t.Count && !At(k).Is("("))
                {
                    sb.Append(At(k).Text);
                    k++;
                }

                opName = sb.ToString();
                opTok = new Token(TokenKind.Punctuation, opName, At(j + 1).Offset, At(k - 1).End - At(j + 1).Offset);
                j = k;
                term = "(";
                break;
            }

            if (tk.Is("<") && IsIdent(j - 1))
            {
                angles.Add(j);
                j = SkipAngle(j);
                continue;
            }

            if (tk.Is("("))
            {
                if (At(j - 1).Is("Function") || (At(j - 1).Kind != TokenKind.Identifier && !At(j - 1).Is(">")))
                {
                    // function type parameters or a record type such as `(int, {String name}) f()`
                    if (At(j - 1).Is("?") || At(j - 1).Kind == TokenKind.Punctuation)
                    {
                        header.Add(tk);
                    }

                    j = SkipBalanced(j);
                    continue;
                }

                term = "(";
                break;
            }

            if (tk.Kind == TokenKind.Punctuation && tk.Text is "=" or ";" or "{" or "=>" or "," or "}")
            {
                term = tk.Text;
                break;
            }

            header.Add(tk);
            j++;
        }

        var core = header.Where(h => h.Kind == TokenKind.Identifier && !Modifiers.Contains(h.Text)).ToList();
        if (term is "=" or ";" or "," && header.Count >= 2 && header[^1].Kind == TokenKind.Identifier && Modifiers.Contains(header[^1].Text)
            && ((header[^2].Kind == TokenKind.Identifier && !Modifiers.Contains(header[^2].Text)) || header[^2].Is(">") || header[^2].Is("?")))
        {
            core.Add(header[^1]); // a variable named like a modifier: `const Object factory = ...;`
        }

        var isStatic = header.Any(h => h.Is("static"));
        var accessorIdx = core.FindIndex(h => h.Is("get") || h.Is("set"));
        if (accessorIdx >= core.Count - 1)
        {
            accessorIdx = -1;
        }

        if (term == "(" && (opName is not null || core.Count > 0))
        {
            ParseCallable(parent, startOffset, doc, header, core, opName, opTok, accessorIdx, isStatic, j, angles);
            return;
        }

        if (term is "" or "}" || core.Count == 0)
        {
            _i = term == "{" ? SkipBalanced(j) : Math.Max(j, _i);
            if (_i == start)
            {
                _i++;
            }

            return;
        }

        if (accessorIdx >= 0 && term is "{" or "=>" or ";")
        {
            var nameTok = core[accessorIdx + 1];
            _i = j;
            var d = NewAccessor(parent, nameTok, core[accessorIdx].Is("get"), startOffset, doc, isStatic);
            d.TypeName = TypeNameBefore(header, header.IndexOf(core[accessorIdx]));
            SkipBody();
            d.EnclosingEnd = At(_i - 1).End;
            d.Signature = Collapse(startOffset, SignatureEnd(d.EnclosingEnd, j));
            return;
        }

        if (term is "{" or "=>")
        {
            _i = term == "{" ? SkipBalanced(j) : SkipToSemicolon(j);
            return;
        }

        // field(s) / top-level variable(s)
        var nt = core[^1];
        var p = j;
        var kind = parent is null ? DeclKind.Variable : DeclKind.Field;
        while (true)
        {
            if (At(p).Is("="))
            {
                p = SkipExpr(p + 1);
            }

            var end = At(p - 1).End;
            var name = nt.Text;
            var path = parent is null
                ? new List<(string, SymbolDescriptor.Types.Suffix)> { (name, SymbolDescriptor.Types.Suffix.Term) }
                : [.. parent.Path, (name, SymbolDescriptor.Types.Suffix.Term)];
            var d = NewDecl(kind, name, parent, nt, path, nt.Offset, doc);
            d.IsStatic = isStatic;
            d.TypeName = TypeNameBefore(header, header.LastIndexOf(nt));
            if (d.TypeName is null && At(j).Is("="))
            {
                d.TypeName = InferType(j + 1);
            }

            d.EnclosingEnd = end;
            d.Signature = Collapse(startOffset, nt.End);
            if (At(p).Is(",") && IsIdent(p + 1))
            {
                nt = At(p + 1);
                p += 2;
                continue;
            }

            if (At(p).Is(";"))
            {
                p++;
            }

            break;
        }

        _i = p;
    }

    private int SignatureEnd(int end, int termIdx) => Math.Min(end, At(termIdx).Offset == int.MaxValue ? end : At(termIdx).Offset);

    private Decl NewAccessor(Decl? parent, Token nameTok, bool getter, int startOffset, string? doc, bool isStatic)
    {
        var accessorName = (getter ? "<get>" : "<set>") + nameTok.Text;
        List<(string, SymbolDescriptor.Types.Suffix)> path = parent is null or { Kind: DeclKind.Extension, Name: "" }
            ? [(accessorName, SymbolDescriptor.Types.Suffix.Term)]
            : [.. parent.Path, (accessorName, SymbolDescriptor.Types.Suffix.Term)];
        var d = NewDecl(DeclKind.Property, nameTok.Text, parent, nameTok, path, startOffset, doc);
        d.IsGetter = getter;
        d.IsStatic = isStatic;
        return d;
    }

    private void ParseCallable(Decl? parent, int startOffset, string? doc, List<Token> header, List<Token> core,
        string? opName, Token opTok, int accessorIdx, bool isStatic, int parenIdx, List<int> angles)
    {
        Decl decl;
        var isFactory = header.Any(h => h.Is("factory"));
        if (opName is not null)
        {
            decl = NewDecl(DeclKind.Method, opName, parent, opTok, MemberPath(parent, opName, SymbolDescriptor.Types.Suffix.Method), startOffset, doc);
        }
        else if (accessorIdx >= 0)
        {
            decl = NewAccessor(parent, core[accessorIdx + 1], core[accessorIdx].Is("get"), startOffset, doc, isStatic);
        }
        else
        {
            var nameTok = core[^1];
            var nameIdxInHeader = header.LastIndexOf(nameTok);
            var prev = nameIdxInHeader > 0 ? header[nameIdxInHeader - 1] : default;
            if (parent is { IsTypeLike: true, Kind: not DeclKind.Extension } && parent.Name.Length > 0
                && prev.Is(".") && nameIdxInHeader > 1 && header[nameIdxInHeader - 2].Text == parent.Name)
            {
                var n = nameTok.Text == "new" ? "" : nameTok.Text;
                decl = NewDecl(DeclKind.Constructor, n, parent, nameTok,
                    MemberPath(parent, n.Length == 0 ? "<constructor>" : n, SymbolDescriptor.Types.Suffix.Method), startOffset, doc);
            }
            else if (parent is { IsTypeLike: true, Kind: not DeclKind.Extension } && parent.Name.Length > 0
                && nameTok.Text == parent.Name && core.Count == 1)
            {
                decl = NewDecl(DeclKind.Constructor, "", parent, nameTok,
                    MemberPath(parent, "<constructor>", SymbolDescriptor.Types.Suffix.Method), startOffset, doc);
            }
            else if (parent is null)
            {
                decl = NewDecl(DeclKind.Function, nameTok.Text, null, nameTok,
                    [(nameTok.Text, SymbolDescriptor.Types.Suffix.Method)], startOffset, doc);
            }
            else
            {
                decl = NewDecl(DeclKind.Method, nameTok.Text, parent, nameTok,
                    MemberPath(parent, nameTok.Text, SymbolDescriptor.Types.Suffix.Method), startOffset, doc);
            }

            _ = isFactory;
        }

        decl.IsStatic = isStatic;
        if (decl.Kind == DeclKind.Constructor)
        {
            decl.TypeName = parent?.Name;
        }
        else if (opName is null)
        {
            var nameIdx = header.IndexOf(decl.NameToken);
            decl.TypeName = accessorIdx >= 0 ? TypeNameBefore(header, header.IndexOf(core[accessorIdx])) : nameIdx > 0 ? TypeNameBefore(header, nameIdx) : null;
        }

        foreach (var a in angles)
        {
            if (At(a - 1).Offset == decl.NameToken.Offset)
            {
                ParseTypeParams(a, decl);
            }
        }

        var close = ParseParams(parenIdx, decl);
        _i = close;
        var sigEnd = At(close - 1).End;
        var bodyStart = _i;
        SkipBody();
        ParseLocalFunctions(bodyStart, _i, decl);
        decl.EnclosingEnd = At(_i - 1).End;
        decl.Signature = Collapse(startOffset, sigEnd);
    }

    private static List<(string, SymbolDescriptor.Types.Suffix)> MemberPath(Decl? parent, string name, SymbolDescriptor.Types.Suffix suffix) =>
        parent is null ? [(name, suffix)] : [.. parent.Path, (name, suffix)];

    /// <summary>Parses the parameter list starting at the <c>(</c> at <paramref name="open"/>; returns the index after <c>)</c>.</summary>
    private int ParseParams(int open, Decl owner)
    {
        var after = SkipBalanced(open);
        var close = after - 1;
        var slice = new List<Token>();
        var depth = 0;
        var angle = 0;
        var named = false;
        for (var k = open + 1; k < close; k++)
        {
            var tk = _t[k];
            if (depth == 0 && angle == 0)
            {
                if ((tk.Is("{") || tk.Is("[")) && slice.Count == 0)
                {
                    named = tk.Is("{");
                    continue;
                }

                if ((tk.Is("}") || tk.Is("]")) && slice.Count == 0)
                {
                    continue;
                }

                if (tk.Is(","))
                {
                    FlushParam(slice, named, owner);
                    slice.Clear();
                    continue;
                }

                if ((tk.Is("}") || tk.Is("]")) && k + 1 == close)
                {
                    continue;
                }
            }

            if (tk.Is("(") || tk.Is("[") || tk.Is("{"))
            {
                depth++;
            }
            else if (tk.Is(")") || tk.Is("]") || tk.Is("}"))
            {
                depth--;
            }
            else if (tk.Is("<") && IsIdent(k - 1))
            {
                angle++;
            }
            else if (tk.Is(">") && angle > 0)
            {
                angle--;
            }

            slice.Add(tk);
        }

        FlushParam(slice, named, owner);
        return after;
    }

    private static readonly HashSet<string> NotFunctionNames = ["if", "for", "while", "switch", "catch", "on", "assert", "return", "await", "super", "this"];

    /// <summary>
    /// Finds local function declarations (<c>void f(int a) {}</c>) in a body. Upstream indexes these as functions of
    /// the file namespace, so they get the same symbol shape as top-level functions.
    /// </summary>
    private void ParseLocalFunctions(int from, int to, Decl owner)
    {
        for (var k = from + 1; k < to - 1 && k < _t.Count; k++)
        {
            var name = _t[k];
            if (name.Kind != TokenKind.Identifier || !At(k + 1).Is("(") || NotFunctionNames.Contains(name.Text))
            {
                continue;
            }

            var prev = At(k - 1);
            if (!((prev.Kind == TokenKind.Identifier && !NotFunctionNames.Contains(prev.Text) && prev.Text is not ("new" or "else" or "do" or "throw" or "in" or "is" or "as"))
                || prev.Is(">") || prev.Is("?")))
            {
                continue;
            }

            var after = SkipBalanced(k + 1);
            var next = At(after);
            if (!(next.Is("{") || next.Is("=>") || next.Is("async")))
            {
                continue;
            }

            // statement start: the previous-previous token must end a statement or open a block
            var before = At(k - 2);
            if (prev.Is(">") || prev.Is("?"))
            {
                var typeStart = TypeStart(k - 1);
                before = At(typeStart - 1);
            }

            if (before.Kind == TokenKind.Identifier && before.Text is not ("static" or "final" or "external"))
            {
                continue;
            }

            var d = NewDecl(DeclKind.Function, name.Text, null, name, [(name.Text, SymbolDescriptor.Types.Suffix.Method)], At(k - 1).Offset, null);
            _m.TopLevel.Remove(d);
            ParseParams(k + 1, d);
            var savedI = _i;
            _i = after;
            SkipBody();
            var bodyEnd = _i;
            _i = savedI;
            d.EnclosingEnd = Math.Max(At(after - 1).End, At(bodyEnd - 1).End);
            d.Signature = Collapse(d.EnclosingStart, At(after - 1).End);
        }

        _ = owner;
    }

    /// <summary>Registers the type parameters of the <c>&lt;...&gt;</c> list starting at <paramref name="open"/>.</summary>
    private void ParseTypeParams(int open, Decl owner)
    {
        var end = SkipAngle(open);
        var depth = 0;
        var expectName = true;
        for (var k = open + 1; k < end - 1; k++)
        {
            var tk = _t[k];
            if (tk.Is("<") || tk.Is("(") || tk.Is("["))
            {
                depth++;
            }
            else if (tk.Is(">") || tk.Is(")") || tk.Is("]"))
            {
                depth--;
            }
            else if (depth == 0 && tk.Is(","))
            {
                expectName = true;
            }
            else if (depth == 0 && expectName && tk.Kind == TokenKind.Identifier)
            {
                expectName = false;
                var d = NewDecl(DeclKind.TypeParameter, tk.Text, owner, tk, [.. owner.Path, (tk.Text, SymbolDescriptor.Types.Suffix.TypeParameter)],
                    tk.Offset, null);
                // enclosing range: up to the next top-level comma
                var e = k + 1;
                var dd = 0;
                while (e < end - 1 && !(dd == 0 && _t[e].Is(",")))
                {
                    dd += _t[e].Is("<") ? 1 : _t[e].Is(">") ? -1 : 0;
                    e++;
                }

                d.EnclosingEnd = _t[e - 1].End;
                d.Signature = Collapse(d.EnclosingStart, d.EnclosingEnd);
                owner.TypeParameters.Add(d);
            }
        }
    }

    private void FlushParam(List<Token> slice, bool named, Decl owner)
    {
        if (slice.Count == 0)
        {
            return;
        }

        var idx = 0;
        while (idx < slice.Count && slice[idx].Is("@"))
        {
            idx += 2;
            while (idx + 1 < slice.Count && slice[idx].Is("."))
            {
                idx += 2;
            }

            if (idx < slice.Count && slice[idx].Is("("))
            {
                var depth = 0;
                do
                {
                    depth += slice[idx].Is("(") ? 1 : slice[idx].Is(")") ? -1 : 0;
                    idx++;
                }
                while (idx < slice.Count && depth > 0);
            }
        }

        if (idx >= slice.Count)
        {
            return;
        }

        // cut at the default value
        var end = slice.Count;
        var d = 0;
        var a = 0;
        for (var k = idx; k < slice.Count; k++)
        {
            var tk = slice[k];
            if (tk.Is("(") || tk.Is("[") || tk.Is("{"))
            {
                d++;
            }
            else if (tk.Is(")") || tk.Is("]") || tk.Is("}"))
            {
                d--;
            }
            else if (tk.Is("<") && k > 0 && slice[k - 1].Kind == TokenKind.Identifier)
            {
                a++;
            }
            else if (tk.Is(">") && a > 0)
            {
                a--;
            }
            else if (d == 0 && a == 0 && (tk.Is("=") || tk.Is(":")))
            {
                end = k;
                break;
            }
        }

        Token? nameTok = null;
        for (var k = idx; k + 2 < end; k++)
        {
            if ((slice[k].Is("this") || slice[k].Is("super")) && slice[k + 1].Is(".") && slice[k + 2].Kind == TokenKind.Identifier)
            {
                nameTok = slice[k + 2];
                if (slice[k].Is("this") && owner.Parent is { } cls)
                {
                    _m.ThisFieldRefs.Add(new ThisFieldRef(nameTok.Value, cls));
                    if (!named)
                    {
                        // a positional field formal parameter is a `local` parameter that is referenced, never defined
                        var synthetic = new Decl(DeclKind.Parameter, nameTok.Value.Text, owner, nameTok.Value, _m.RelativePath)
                        {
                            External = _m.External,
                            IsLocal = true,
                            IsSynthetic = true,
                            EnclosingStart = slice[idx].Offset,
                            EnclosingEnd = slice[^1].End,
                        };
                        owner.Parameters.Add(synthetic);
                        _m.SyntheticParams.Add(synthetic);
                        return;
                    }
                }

                break;
            }
        }

        if (nameTok is null)
        {
            d = 0;
            a = 0;
            for (var k = idx; k < end; k++)
            {
                var tk = slice[k];
                if (tk.Is("(") && d == 0 && a == 0 && k > idx && slice[k - 1].Kind == TokenKind.Identifier && !slice[k - 1].Is("Function"))
                {
                    nameTok = slice[k - 1];
                    break;
                }

                if (tk.Is("(") || tk.Is("[") || tk.Is("{"))
                {
                    d++;
                }
                else if (tk.Is(")") || tk.Is("]") || tk.Is("}"))
                {
                    d--;
                }
                else if (tk.Is("<") && k > 0 && slice[k - 1].Kind == TokenKind.Identifier)
                {
                    a++;
                }
                else if (tk.Is(">") && a > 0)
                {
                    a--;
                }
                else if (d == 0 && a == 0 && tk.Kind == TokenKind.Identifier && !Modifiers.Contains(tk.Text)
                    && !(k > idx && slice[k - 1].Is(".") ) && !tk.Is("Function"))
                {
                    nameTok = tk;
                }
            }
        }

        if (nameTok is not { } nt)
        {
            return;
        }

        var p = NewDecl(DeclKind.Parameter, nt.Text, owner, nt, [.. owner.Path, (nt.Text, SymbolDescriptor.Types.Suffix.Parameter)],
            slice[idx].Offset, null, isLocal: !named || nt.Text.StartsWith('_'));
        p.EnclosingEnd = slice[^1].End;
        p.Signature = Collapse(p.EnclosingStart, slice[end - 1].End);
        p.TypeName = TypeNameBefore(slice, slice.IndexOf(nt));
        owner.Parameters.Add(p);
    }

    // ---- skipping helpers ---------------------------------------------------------------------------------

    /// <summary>Skips constructor initializers and the body (block, expression or semicolon).</summary>
    private void SkipBody()
    {
        while (_i < _t.Count)
        {
            var tk = Cur;
            if (tk.Is("{"))
            {
                _i = SkipBalanced(_i);
                return;
            }

            if (tk.Is("=>") || tk.Is("="))
            {
                _i = SkipToSemicolon(_i);
                return;
            }

            if (tk.Is(";"))
            {
                _i++;
                return;
            }

            if (tk.Is(":"))
            {
                // initializer list
                _i++;
                while (_i < _t.Count && !Cur.Is("{") && !Cur.Is(";"))
                {
                    _i = Cur.Is("(") || Cur.Is("[") ? SkipBalanced(_i) : _i + 1;
                }

                continue;
            }

            if (tk.Is("}"))
            {
                return;
            }

            _i++;
        }
    }

    // SkipBalanced results for every opener (any closer closes any opener, exactly as the scan below counts depth):
    // the parser skips the same nested argument lists many times (`const A(child: const A(...))`), which was quadratic.
    private int[]? _balancedEnd;

    /// <summary>Index after the bracket matching the opener at <paramref name="i"/>.</summary>
    private int SkipBalanced(int i)
    {
        if (i >= 0 && i < _t.Count && IsOpener(_t[i]))
        {
            if (_balancedEnd is null)
            {
                _balancedEnd = new int[_t.Count];
                var open = new Stack<int>();
                for (var k = 0; k < _t.Count; k++)
                {
                    if (IsOpener(_t[k]))
                    {
                        _balancedEnd[k] = _t.Count; // unmatched
                        open.Push(k);
                    }
                    else if ((_t[k].Is(")") || _t[k].Is("]") || _t[k].Is("}")) && open.Count > 0)
                    {
                        _balancedEnd[open.Pop()] = k + 1;
                    }
                }
            }

            return _balancedEnd[i];
        }

        var depth = 0;
        while (i < _t.Count)
        {
            var tk = _t[i];
            if (tk.Is("(") || tk.Is("[") || tk.Is("{"))
            {
                depth++;
            }
            else if (tk.Is(")") || tk.Is("]") || tk.Is("}"))
            {
                depth--;
                if (depth <= 0)
                {
                    return i + 1;
                }
            }

            i++;
        }

        return i;
    }

    private static bool IsOpener(Token tk) => tk.Is("(") || tk.Is("[") || tk.Is("{");

    private int SkipAngle(int i)
    {
        var depth = 0;
        while (i < _t.Count)
        {
            var tk = _t[i];
            if (tk.Is("<"))
            {
                depth++;
            }
            else if (tk.Is(">"))
            {
                depth--;
                if (depth <= 0)
                {
                    return i + 1;
                }
            }
            else if (tk.Is(";") || tk.Is("{") || tk.Is("}"))
            {
                return i;
            }

            i++;
        }

        return i;
    }

    private int SkipToSemicolon(int i)
    {
        var depth = 0;
        while (i < _t.Count)
        {
            var tk = _t[i];
            if (tk.Is("(") || tk.Is("[") || tk.Is("{"))
            {
                depth++;
            }
            else if (tk.Is(")") || tk.Is("]") || tk.Is("}"))
            {
                depth--;
            }
            else if (tk.Is(";") && depth <= 0)
            {
                return i + 1;
            }

            i++;
        }

        return i;
    }

    /// <summary>Skips an initializer expression; returns the index of the terminating <c>,</c>, <c>;</c> or <c>}</c>.</summary>
    private int SkipExpr(int i)
    {
        var depth = 0;
        while (i < _t.Count)
        {
            var tk = _t[i];
            if (tk.Is("(") || tk.Is("[") || tk.Is("{"))
            {
                depth++;
            }
            else if (tk.Is(")") || tk.Is("]") || tk.Is("}"))
            {
                if (depth == 0)
                {
                    return i;
                }

                depth--;
            }
            else if (depth == 0 && (tk.Is(",") || tk.Is(";")))
            {
                return i;
            }

            i++;
        }

        return i;
    }

    // ---- declaration registry ------------------------------------------------------------------------------

    private Decl NewDecl(DeclKind kind, string name, Decl? parent, Token nameTok,
        List<(string, SymbolDescriptor.Types.Suffix)> path, int enclosingStart, string? doc, bool? isLocal = null)
    {
        var d = new Decl(kind, name, parent, nameTok, _m.RelativePath)
        {
            External = _m.External,
            Path = path,
            IsLocal = isLocal ?? name.StartsWith('_'),
            EnclosingStart = enclosingStart,
            EnclosingEnd = nameTok.End,
            Doc = doc,
        };
        _m.AllDecls.Add(d);
        _m.DeclTokenOffsets.Add(nameTok.Offset);
        if (parent is null)
        {
            _m.TopLevel.Add(d);
        }
        else if (kind is not (DeclKind.Parameter or DeclKind.TypeParameter))
        {
            parent.Members.Add(d);
        }

        return d;
    }

    private string Collapse(int start, int end) =>
        end <= start || start < 0 ? "" : Whitespace().Replace(_src[start..Math.Min(end, _src.Length)], " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
