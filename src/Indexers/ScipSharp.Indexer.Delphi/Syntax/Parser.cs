namespace ScipSharp.Indexer.Delphi.Syntax;

/// <summary>
/// Error-tolerant recursive-descent parser for Delphi units, programs, libraries and packages. It builds a declaration
/// tree (types, routines, properties, …); statement blocks and expressions are kept as token ranges that the resolver
/// walks later. Unexpected tokens are skipped rather than aborting the parse.
/// </summary>
internal sealed class Parser(IReadOnlyList<Token> tokens)
{
    private static readonly HashSet<string> RoutineDirectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "overload", "override", "virtual", "dynamic", "abstract", "final", "reintroduce", "static", "inline", "register",
        "pascal", "cdecl", "stdcall", "safecall", "winapi", "export", "external", "far", "near", "forward", "assembler",
        "message", "dispid", "platform", "deprecated", "library", "experimental", "varargs", "local", "unsafe", "delayed",
    };

    private static readonly HashSet<string> HintDirectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "platform", "deprecated", "library", "experimental",
    };

    private static readonly HashSet<string> PropertySpecifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "write", "stored", "default", "nodefault", "implements", "index", "readonly", "writeonly", "dispid", "add", "remove",
    };

    public int Pos { get; set; }

    private Token Cur => Tok(Pos);

    private Token Tok(int i) => tokens[Math.Clamp(i, 0, tokens.Count - 1)];

    private Token Peek(int n = 1) => Tok(Pos + n);

    private bool AtEnd => Cur.IsEof;

    private void Advance()
    {
        if (!AtEnd)
        {
            Pos++;
        }
    }

    private bool Accept(string symbol)
    {
        if (Cur.IsSymbol(symbol))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool AcceptKeyword(string keyword)
    {
        if (Cur.IsKeyword(keyword))
        {
            Advance();
            return true;
        }

        return false;
    }

    public static FileSyntax ParseFile(IReadOnlyList<Token> tokens) => new Parser(tokens).ParseFile();

    public FileSyntax ParseFile()
    {
        var f = new FileSyntax(tokens) { Doc = Cur.Doc };
        if (Cur.IsKeyword("unit"))
        {
            f.Kind = FileKind.Unit;
            f.HasHeader = true;
            Advance();
            f.Name = ParseDottedName(f.NameToks);
            SkipTo(";");
            Accept(";");
            if (AcceptKeyword("interface"))
            {
                ParseUses(f.InterfaceUses, "uses");
                ParseDeclarations(f.InterfaceDecls, DeclContext.Interface);
            }

            if (Cur.IsKeyword("implementation"))
            {
                f.ImplementationTok = Pos;
                Advance();
                ParseUses(f.ImplementationUses, "uses");
            }

            ParseDeclarations(f.ImplementationDecls, DeclContext.Implementation);
            ParseUnitEnd(f);
            return f;
        }

        if (Cur.IsKeyword("program") || Cur.IsKeyword("library"))
        {
            f.Kind = Cur.IsKeyword("program") ? FileKind.Program : FileKind.Library;
            f.HasHeader = true;
            Advance();
            f.Name = ParseDottedName(f.NameToks);
            SkipTo(";");
            Accept(";");
        }
        else if (Cur.IsWord("package"))
        {
            f.Kind = FileKind.Package;
            f.HasHeader = true;
            Advance();
            f.Name = ParseDottedName(f.NameToks);
            SkipTo(";");
            Accept(";");
            ParseUses(f.Requires, "requires");
            ParseUses(f.InterfaceUses, "contains");
            return f;
        }
        else
        {
            f.Kind = FileKind.Program;
        }

        ParseUses(f.InterfaceUses, "uses");
        ParseDeclarations(f.ImplementationDecls, DeclContext.Implementation);
        ParseUnitEnd(f);
        return f;
    }

    private void ParseUnitEnd(FileSyntax f)
    {
        while (!AtEnd)
        {
            if (Cur.IsKeyword("initialization") || Cur.IsKeyword("finalization"))
            {
                var start = Pos;
                Advance();
                var depth = 0;
                while (!AtEnd)
                {
                    if (depth == 0 && (Cur.IsKeyword("finalization") || (Cur.IsKeyword("end") && Peek().IsSymbol("."))))
                    {
                        break;
                    }

                    if (IsBlockOpener(Cur))
                    {
                        if (Cur.IsKeyword("asm"))
                        {
                            Pos = SkipAsm(Pos);
                            continue;
                        }

                        depth++;
                    }
                    else if (Cur.IsKeyword("end"))
                    {
                        depth--;
                    }

                    Advance();
                }

                f.Blocks.Add(new TokRange(start, Pos));
                continue;
            }

            if (Cur.IsKeyword("begin"))
            {
                var end = MatchEnd(Pos);
                f.Blocks.Add(new TokRange(Pos, end));
                Pos = end;
                continue;
            }

            if (Cur.IsKeyword("end"))
            {
                break;
            }

            // Stray tokens (e.g. declarations after a parse error): try to recover declarations.
            var before = Pos;
            ParseDeclarations(f.ImplementationDecls, DeclContext.Implementation);
            if (Pos == before)
            {
                Advance();
            }
        }
    }

    private static bool IsBlockOpener(Token t) =>
        t.Kind == TokenKind.Keyword && (t.IsKeyword("begin") || t.IsKeyword("try") || t.IsKeyword("case") || t.IsKeyword("asm"));

    private int SkipAsm(int i)
    {
        i++;
        while (!Tok(i).IsEof && !Tok(i).IsKeyword("end"))
        {
            i++;
        }

        return Tok(i).IsEof ? i : i + 1;
    }

    /// <summary>Given the index of <c>begin</c>/<c>try</c>/<c>case</c>/<c>asm</c>, returns the index just past its matching <c>end</c>.</summary>
    public int MatchEnd(int i)
    {
        if (Tok(i).IsKeyword("asm"))
        {
            return SkipAsm(i);
        }

        var depth = 0;
        while (!Tok(i).IsEof)
        {
            var t = Tok(i);
            if (t.IsKeyword("asm"))
            {
                i = SkipAsm(i);
                if (depth == 0)
                {
                    return i;
                }

                continue;
            }

            if (IsBlockOpener(t))
            {
                depth++;
            }
            else if (t.IsKeyword("end"))
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

    private string ParseDottedName(List<int> toks)
    {
        var parts = new List<string>();
        while (Cur.Kind is TokenKind.Identifier or TokenKind.Keyword && !Cur.IsSymbol(";"))
        {
            if (Cur.Kind == TokenKind.Keyword && toks.Count > 0 && !Tok(Pos - 1).IsSymbol("."))
            {
                break;
            }

            toks.Add(Pos);
            parts.Add(Cur.Text);
            Advance();
            if (!Accept("."))
            {
                break;
            }
        }

        return string.Join(".", parts);
    }

    private void ParseUses(List<UsesItem> list, string keyword)
    {
        if (!(Cur.IsKeyword(keyword) || Cur.IsWord(keyword)))
        {
            return;
        }

        Advance();
        while (!AtEnd && !Cur.IsSymbol(";"))
        {
            if (Cur.Kind is not (TokenKind.Identifier or TokenKind.Keyword))
            {
                Advance();
                continue;
            }

            var toks = new List<int>();
            var name = ParseDottedName(toks);
            int? inTok = null;
            string? inPath = null;
            if (Cur.IsKeyword("in"))
            {
                Advance();
                if (Cur.Kind == TokenKind.String)
                {
                    inTok = Pos;
                    inPath = Cur.Text;
                    Advance();
                }
            }

            if (toks.Count > 0)
            {
                list.Add(new UsesItem(toks, name, inTok, inPath));
            }

            if (!Accept(","))
            {
                SkipTo(";");
                break;
            }
        }

        Accept(";");
    }

    internal enum DeclContext
    {
        Interface,
        Implementation,
        Local,
    }

    private bool IsDeclTerminator() =>
        AtEnd || Cur.IsKeyword("implementation") || Cur.IsKeyword("initialization") || Cur.IsKeyword("finalization") ||
        Cur.IsKeyword("begin") || Cur.IsKeyword("end") || Cur.IsKeyword("asm");

    private static bool IsRoutineKeyword(Token t) =>
        t.IsKeyword("procedure") || t.IsKeyword("function") || t.IsKeyword("constructor") || t.IsKeyword("destructor") || t.IsWord("operator");

    public void ParseDeclarations(List<Decl> list, DeclContext ctx)
    {
        while (!IsDeclTerminator())
        {
            var start = Pos;
            var attrs = ParseAttributes();
            var t = Cur;
            if (t.IsKeyword("type"))
            {
                Advance();
                ParseTypeSection(list, attrs, Visibility.Default);
            }
            else if (t.IsKeyword("const") || t.IsKeyword("resourcestring"))
            {
                var rs = t.IsKeyword("resourcestring");
                Advance();
                while (Cur.IsIdentifier || Cur.IsSymbol("["))
                {
                    var s = Pos;
                    var a = ParseAttributes();
                    if (!Cur.IsIdentifier)
                    {
                        Pos = s;
                        break;
                    }

                    list.Add(ParseConstDecl(s, a, rs, false));
                }
            }
            else if (t.IsKeyword("var") || t.IsKeyword("threadvar"))
            {
                var kind = t.IsKeyword("threadvar") ? VarKind.ThreadVar : VarKind.Var;
                Advance();
                while (Cur.IsIdentifier || Cur.IsSymbol("["))
                {
                    var s = Pos;
                    var a = ParseAttributes();
                    if (!Cur.IsIdentifier)
                    {
                        Pos = s;
                        break;
                    }

                    list.Add(ParseVarDecl(s, a, kind, Visibility.Default));
                }
            }
            else if (t.IsKeyword("label"))
            {
                var d = new LabelDecl { Start = Pos };
                Advance();
                while (!AtEnd && !Cur.IsSymbol(";"))
                {
                    if (Cur.Kind is TokenKind.Identifier or TokenKind.Number)
                    {
                        d.NameToks.Add(Pos);
                    }

                    Advance();
                }

                Accept(";");
                d.End = Pos;
                list.Add(d);
            }
            else if (IsRoutineKeyword(t) || (t.IsKeyword("class") && IsRoutineKeyword(Peek())))
            {
                var r = ParseRoutine(start, attrs, allowBody: ctx != DeclContext.Interface, inClass: false);
                list.Add(r);
            }
            else if (t.IsKeyword("exports"))
            {
                SkipTo(";");
                Accept(";");
            }
            else if (attrs.Count > 0)
            {
                // Attributes not followed by a declaration we understand.
                continue;
            }
            else if (ctx == DeclContext.Local)
            {
                break;
            }
            else if (t.IsIdentifier && Peek().IsSymbol("=") && (Peek(2).IsKeyword("class") || Peek(2).IsKeyword("record") || Peek(2).IsKeyword("interface")))
            {
                // Recovery after a parse error inside a type section.
                ParseTypeSection(list, [], Visibility.Default);
            }
            else
            {
                Advance();
            }

            if (Pos == start)
            {
                Advance();
            }
        }
    }

    private void ParseTypeSection(List<Decl> list, List<AttributeNode> firstAttrs, Visibility vis)
    {
        var attrs = firstAttrs;
        while (true)
        {
            var s = Pos;
            if (attrs.Count == 0)
            {
                attrs = ParseAttributes();
            }

            if (Cur.IsIdentifier && (Peek().IsSymbol("=") || Peek().IsSymbol("<")))
            {
                list.Add(ParseTypeDecl(s, attrs, vis));
                attrs = [];
                continue;
            }

            Pos = attrs.Count > 0 && firstAttrs != attrs ? s : Pos;
            return;
        }
    }

    private TypeDecl ParseTypeDecl(int start, List<AttributeNode> attrs, Visibility vis)
    {
        var d = new TypeDecl { Start = start, NameTok = Pos, Doc = Tok(start).Doc ?? Cur.Doc, Visibility = vis };
        d.Attributes.AddRange(attrs);
        Advance();
        if (Cur.IsSymbol("<"))
        {
            d.TypeParams.AddRange(ParseTypeParams());
        }

        Accept("=");
        if (Cur.IsKeyword("type") && !Peek().IsKeyword("of"))
        {
            d.IsStrongAlias = true;
            Advance();
        }

        d.Type = ParseType();
        SkipHints();
        Accept(";");
        d.End = Pos;
        return d;
    }

    private void SkipHints()
    {
        while (HintDirectives.Contains(Cur.Text) && Cur.IsIdentifier || Cur.IsKeyword("library"))
        {
            Advance();
            if (Cur.Kind == TokenKind.String)
            {
                Advance();
            }
        }
    }

    private ConstDecl ParseConstDecl(int start, List<AttributeNode> attrs, bool resourceString, bool classMember)
    {
        var d = new ConstDecl { Start = start, NameTok = Pos, Doc = Tok(start).Doc ?? Cur.Doc, IsResourceString = resourceString, IsClassMember = classMember };
        d.Attributes.AddRange(attrs);
        Advance();
        if (Accept(":"))
        {
            d.Type = ParseType();
        }

        if (Accept("="))
        {
            var vs = Pos;
            SkipExpression();
            d.Value = new TokRange(vs, Pos);
        }

        Accept(";");
        d.End = Pos;
        return d;
    }

    private VarDecl ParseVarDecl(int start, List<AttributeNode> attrs, VarKind kind, Visibility vis)
    {
        var d = new VarDecl { Start = start, Doc = Tok(start).Doc ?? Cur.Doc, Kind = kind, Visibility = vis };
        d.Attributes.AddRange(attrs);
        while (Cur.IsIdentifier)
        {
            d.NameToks.Add(Pos);
            Advance();
            if (!Accept(","))
            {
                break;
            }
        }

        if (Accept(":"))
        {
            d.Type = ParseType();
        }

        SkipHints();
        if (Cur.IsWord("absolute"))
        {
            Advance();
            var s = Pos;
            SkipExpression();
            d.Absolute = new TokRange(s, Pos);
        }
        else if (Accept("="))
        {
            var s = Pos;
            SkipExpression();
            d.Init = new TokRange(s, Pos);
        }

        SkipHints();
        if (!Accept(";") && !Cur.IsSymbol(")") && !Cur.IsKeyword("end"))
        {
            SkipTo(";");
            Accept(";");
        }

        d.End = Pos;
        return d;
    }

    /// <summary>Skips an expression up to (not including) a <c>;</c> or an unbalanced closer at depth 0.</summary>
    private void SkipExpression()
    {
        var depth = 0;
        while (!AtEnd)
        {
            var t = Cur;
            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                depth++;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
            {
                if (depth == 0)
                {
                    return;
                }

                depth--;
            }
            else if (depth == 0 && (t.IsSymbol(";") || t.IsKeyword("end")))
            {
                return;
            }
            else if (t.IsKeyword("begin") || t.IsKeyword("case") || t.IsKeyword("try"))
            {
                // anonymous method / record constant with case is unusual; jump over blocks wholesale.
                Pos = MatchEnd(Pos);
                continue;
            }

            Advance();
        }
    }

    private void SkipTo(string symbol)
    {
        var depth = 0;
        while (!AtEnd)
        {
            if (depth == 0 && Cur.IsSymbol(symbol))
            {
                return;
            }

            if (Cur.IsSymbol("(") || Cur.IsSymbol("["))
            {
                depth++;
            }
            else if (Cur.IsSymbol(")") || Cur.IsSymbol("]"))
            {
                depth = Math.Max(0, depth - 1);
            }

            Advance();
        }
    }

    public List<AttributeNode> ParseAttributes()
    {
        var result = new List<AttributeNode>();
        while (Cur.IsSymbol("[") && (Peek().IsIdentifier || Peek().IsKeyword("unit")))
        {
            Advance();
            while (!AtEnd && !Cur.IsSymbol("]"))
            {
                // Optional target, e.g. [Result: Attr] or [unit: Attr].
                if ((Cur.IsIdentifier || Cur.Kind == TokenKind.Keyword) && Peek().IsSymbol(":"))
                {
                    Advance();
                    Advance();
                }

                if (!Cur.IsIdentifier)
                {
                    Advance();
                    continue;
                }

                var name = ParseNamedType();
                TokRange? args = null;
                if (Cur.IsSymbol("("))
                {
                    var s = Pos + 1;
                    Pos = SkipParens(Pos);
                    args = new TokRange(s, Pos - 1);
                }

                result.Add(new AttributeNode(name, args));
                if (!Accept(","))
                {
                    break;
                }
            }

            SkipTo("]");
            Accept("]");
        }

        return result;
    }

    /// <summary>Given the index of an opening bracket, returns the index just past the matching closer.</summary>
    public int SkipParens(int i)
    {
        var depth = 0;
        while (!Tok(i).IsEof)
        {
            var t = Tok(i);
            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                depth++;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
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

    public List<TypeParamNode> ParseTypeParams()
    {
        var result = new List<TypeParamNode>();
        if (!Accept("<"))
        {
            return result;
        }

        while (!AtEnd && !Cur.IsSymbol(">"))
        {
            var group = new List<int>();
            while (Cur.IsIdentifier)
            {
                group.Add(Pos);
                Advance();
                if (!Accept(","))
                {
                    break;
                }
            }

            var constraints = new List<TypeNode>();
            if (Accept(":"))
            {
                while (!AtEnd && !Cur.IsSymbol(";") && !Cur.IsSymbol(">"))
                {
                    if (Cur.IsKeyword("class") || Cur.IsKeyword("record") || Cur.IsKeyword("constructor") || Cur.IsWord("unmanaged"))
                    {
                        Advance();
                    }
                    else if (Cur.IsIdentifier)
                    {
                        constraints.Add(ParseNamedType());
                    }
                    else
                    {
                        Advance();
                    }

                    Accept(",");
                }
            }

            foreach (var g in group)
            {
                result.Add(new TypeParamNode(g, constraints));
            }

            if (!Accept(";") && !Accept(",") && !Cur.IsSymbol(">"))
            {
                if (group.Count == 0)
                {
                    Advance();
                }
            }
        }

        Accept(">");
        return result;
    }

    public NamedTypeNode ParseNamedType()
    {
        var n = new NamedTypeNode { Start = Pos };
        while (true)
        {
            if (!(Cur.IsIdentifier || Cur.IsKeyword("string") || Cur.IsKeyword("file")))
            {
                break;
            }

            var tok = Pos;
            Advance();
            List<TypeNode>? args = null;
            if (Cur.IsSymbol("<") && LooksLikeTypeArgs(Pos))
            {
                args = ParseTypeArgs();
            }

            n.Parts.Add(new NamePart(tok, args));
            if (Cur.IsSymbol(".") && (Peek().IsIdentifier))
            {
                Advance();
                continue;
            }

            break;
        }

        if (n.Parts.Count == 1 && Tok(n.Parts[0].Tok).IsKeyword("string") && Cur.IsSymbol("["))
        {
            var s = Pos + 1;
            Pos = SkipParens(Pos);
            n.Length = new TokRange(s, Pos - 1);
        }

        n.End = Pos;
        return n;
    }

    /// <summary>Whether the <c>&lt;</c> at <paramref name="i"/> opens a type argument list (vs. a less-than operator).</summary>
    public bool LooksLikeTypeArgs(int i)
    {
        var depth = 0;
        for (var j = i; j < tokens.Count && j < i + 64; j++)
        {
            var t = Tok(j);
            if (t.IsSymbol("<"))
            {
                depth++;
            }
            else if (t.IsSymbol(">"))
            {
                depth--;
                if (depth == 0)
                {
                    return true;
                }
            }
            else if (t.IsSymbol(">="))
            {
                return depth == 1;
            }
            else if (!(t.IsIdentifier || t.IsSymbol(",") || t.IsSymbol(".") || t.IsKeyword("string") || t.IsKeyword("array") ||
                         t.IsKeyword("of") || t.IsKeyword("record") || t.IsKeyword("class") || t.IsKeyword("procedure") ||
                         t.IsKeyword("function") || t.IsSymbol("^") || t.IsSymbol(":") || t.IsSymbol(";") || t.IsKeyword("const")))
            {
                return false;
            }
        }

        return false;
    }

    public List<TypeNode> ParseTypeArgs()
    {
        var args = new List<TypeNode>();
        Accept("<");
        while (!AtEnd && !Cur.IsSymbol(">"))
        {
            if (Cur.IsSymbol(">="))
            {
                break;
            }

            var before = Pos;
            args.Add(ParseType());
            if (!Accept(","))
            {
                if (Pos == before)
                {
                    Advance();
                }

                if (!Cur.IsSymbol(">"))
                {
                    break;
                }
            }
        }

        Accept(">");
        return args;
    }

    private bool LooksLikeSubrange()
    {
        var depth = 0;
        for (var j = Pos; j < tokens.Count; j++)
        {
            var t = Tok(j);
            if (t.IsEof)
            {
                return false;
            }

            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                depth++;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
            {
                if (depth == 0)
                {
                    return false;
                }

                depth--;
            }
            else if (depth == 0)
            {
                if (t.IsSymbol(".."))
                {
                    return true;
                }

                if (t.IsSymbol(";") || t.IsSymbol(",") || t.IsSymbol("=") || t.IsKeyword("of") || t.IsKeyword("end") ||
                    t.IsSymbol(":=") || t.Kind == TokenKind.Keyword && !(t.IsKeyword("div") || t.IsKeyword("mod") || t.IsKeyword("shl") || t.IsKeyword("shr") || t.IsKeyword("and") || t.IsKeyword("or") || t.IsKeyword("not")))
                {
                    return false;
                }
            }
        }

        return false;
    }

    public TypeNode ParseType()
    {
        var start = Pos;
        var t = Cur;
        TypeNode node;
        if (t.IsKeyword("packed"))
        {
            Advance();
            return ParseType();
        }

        if (t.IsKeyword("class"))
        {
            if (Peek().IsKeyword("of"))
            {
                Advance();
                Advance();
                node = new ClassOfTypeNode(ParseNamedType());
            }
            else
            {
                node = ParseStruct(StructKind.Class);
            }
        }
        else if (t.IsKeyword("object"))
        {
            node = ParseStruct(StructKind.Object);
        }
        else if (t.IsKeyword("record"))
        {
            node = ParseStruct(StructKind.Record);
        }
        else if (t.IsKeyword("interface"))
        {
            node = ParseStruct(StructKind.Interface);
        }
        else if (t.IsKeyword("dispinterface"))
        {
            node = ParseStruct(StructKind.DispInterface);
        }
        else if (t.IsSymbol("(") && !LooksLikeSubrange())
        {
            node = ParseEnum();
        }
        else if (t.IsKeyword("set"))
        {
            Advance();
            AcceptKeyword("of");
            node = new SetTypeNode(ParseType());
        }
        else if (t.IsKeyword("array"))
        {
            var a = new ArrayTypeNode();
            Advance();
            if (Accept("["))
            {
                while (!AtEnd && !Cur.IsSymbol("]"))
                {
                    var before = Pos;
                    a.Indexes.Add(ParseType());
                    if (!Accept(",") && Pos == before)
                    {
                        Advance();
                    }
                }

                Accept("]");
            }

            if (AcceptKeyword("of"))
            {
                if (Cur.IsKeyword("const"))
                {
                    Advance();
                }
                else
                {
                    a.Element = ParseType();
                }
            }

            node = a;
        }
        else if (t.IsSymbol("^"))
        {
            Advance();
            node = new PointerTypeNode(ParseType());
        }
        else if (t.IsKeyword("file") && !Peek().IsSymbol(".") )
        {
            Advance();
            node = new FileTypeNode(AcceptKeyword("of") ? ParseType() : null);
        }
        else if (t.IsKeyword("procedure") || t.IsKeyword("function"))
        {
            node = ParseProcType(false);
        }
        else if (t.IsWord("reference") && Peek().IsKeyword("to"))
        {
            Advance();
            Advance();
            node = ParseProcType(true);
        }
        else if ((t.IsIdentifier || t.IsKeyword("string")) && !LooksLikeSubrange())
        {
            node = ParseNamedType();
        }
        else
        {
            var s = Pos;
            SkipTypeExpression();
            if (Pos == s)
            {
                Advance();
            }

            node = new ExprTypeNode(new TokRange(s, Pos));
        }

        node.Start = start;
        node.End = Pos;
        return node;
    }

    private void SkipTypeExpression()
    {
        var depth = 0;
        while (!AtEnd)
        {
            var t = Cur;
            if (t.IsSymbol("(") || t.IsSymbol("["))
            {
                depth++;
            }
            else if (t.IsSymbol(")") || t.IsSymbol("]"))
            {
                if (depth == 0)
                {
                    return;
                }

                depth--;
            }
            else if (depth == 0 && (t.IsSymbol(";") || t.IsSymbol(",") || t.IsSymbol("=") || t.IsKeyword("of") || t.IsKeyword("end") || t.IsSymbol(":=")))
            {
                return;
            }

            Advance();
        }
    }

    private EnumTypeNode ParseEnum()
    {
        var e = new EnumTypeNode { Scoped = (Cur.Flags & TokenFlags.ScopedEnums) != 0 };
        Accept("(");
        while (!AtEnd && !Cur.IsSymbol(")"))
        {
            if (Cur.IsIdentifier)
            {
                var nameTok = Pos;
                Advance();
                TokRange? value = null;
                if (Accept("="))
                {
                    var s = Pos;
                    var depth = 0;
                    while (!AtEnd)
                    {
                        if (Cur.IsSymbol("("))
                        {
                            depth++;
                        }
                        else if (Cur.IsSymbol(")"))
                        {
                            if (depth == 0)
                            {
                                break;
                            }

                            depth--;
                        }
                        else if (depth == 0 && Cur.IsSymbol(","))
                        {
                            break;
                        }

                        Advance();
                    }

                    value = new TokRange(s, Pos);
                }

                e.Items.Add(new EnumItem(nameTok, value));
            }
            else if (Cur.IsSymbol(";"))
            {
                break; // malformed
            }
            else if (!Cur.IsSymbol(","))
            {
                Advance();
                continue;
            }

            Accept(",");
        }

        Accept(")");
        return e;
    }

    private ProcTypeNode ParseProcType(bool isReference)
    {
        var p = new ProcTypeNode { IsReference = isReference, Kind = Cur.IsKeyword("function") ? RoutineKind.Function : RoutineKind.Procedure };
        Advance();
        if (Cur.IsSymbol("("))
        {
            ParseParams(p.Params, ")");
        }

        if (Accept(":"))
        {
            p.Return = ParseType();
        }

        if (Cur.IsKeyword("of") && Peek().IsKeyword("object"))
        {
            Advance();
            Advance();
            p.OfObject = true;
        }

        // Calling conventions may follow, with or without a semicolon: `procedure; stdcall;`.
        while (true)
        {
            if (Cur.IsIdentifier && RoutineDirectives.Contains(Cur.Text))
            {
                Advance();
                continue;
            }

            if (Cur.IsSymbol(";") && Peek().IsIdentifier && RoutineDirectives.Contains(Peek().Text) && !Peek(2).IsSymbol(":") && !Peek(2).IsSymbol(",") && !Peek(2).IsSymbol("="))
            {
                Advance();
                continue;
            }

            break;
        }

        return p;
    }

    private StructTypeNode ParseStruct(StructKind kind)
    {
        var s = new StructTypeNode { Kind = kind, Start = Pos };
        Advance();
        if (kind == StructKind.Class)
        {
            while (Cur.IsWord("abstract") || Cur.IsWord("sealed"))
            {
                if (Cur.IsWord("abstract"))
                {
                    s.IsAbstract = true;
                }
                else
                {
                    s.IsSealed = true;
                }

                Advance();
            }
        }

        if (Cur.IsWord("helper") && kind is StructKind.Class or StructKind.Record)
        {
            s.Kind = kind == StructKind.Class ? StructKind.ClassHelper : StructKind.RecordHelper;
            Advance();
            if (Cur.IsSymbol("("))
            {
                ParseAncestors(s);
            }

            if (AcceptKeyword("for"))
            {
                s.HelperFor = ParseNamedType();
            }
        }
        else if (kind is StructKind.Class or StructKind.Interface or StructKind.DispInterface)
        {
            if (Cur.IsSymbol(";"))
            {
                s.IsForward = true;
                s.End = Pos;
                return s;
            }
        }

        if (Cur.IsSymbol("(") && kind != StructKind.Record)
        {
            ParseAncestors(s);
            if (Cur.IsSymbol(";") && kind is StructKind.Class or StructKind.Interface)
            {
                // `TFoo = class(TBase);` - a complete declaration without members.
                s.End = Pos;
                return s;
            }
        }

        if (kind is StructKind.Interface or StructKind.DispInterface && Cur.IsSymbol("[") && Peek().Kind == TokenKind.String)
        {
            Advance();
            s.GuidTok = Pos;
            SkipTo("]");
            Accept("]");
        }

        ParseMembers(s);
        if (Cur.IsKeyword("end"))
        {
            s.EndTok = Pos;
            Advance();
        }

        s.End = Pos;
        return s;
    }

    private void ParseAncestors(StructTypeNode s)
    {
        Accept("(");
        while (!AtEnd && !Cur.IsSymbol(")"))
        {
            if (Cur.IsIdentifier || Cur.IsKeyword("string"))
            {
                s.Ancestors.Add(ParseNamedType());
            }
            else if (!Cur.IsSymbol(","))
            {
                if (Cur.IsSymbol(";"))
                {
                    break;
                }

                Advance();
                continue;
            }

            Accept(",");
        }

        Accept(")");
    }

    private enum MemberMode
    {
        Field,
        ClassVar,
        Const,
        Type,
    }

    private void ParseMembers(StructTypeNode s)
    {
        var vis = Visibility.Default;
        var mode = MemberMode.Field;
        while (!AtEnd && !Cur.IsKeyword("end"))
        {
            var start = Pos;
            if (Cur.IsWord("strict"))
            {
                Advance();
                vis = Cur.IsWord("private") ? Visibility.StrictPrivate : Visibility.StrictProtected;
                Advance();
                mode = MemberMode.Field;
                continue;
            }

            if (Cur.IsIdentifier && !Peek().IsSymbol(":") && !Peek().IsSymbol(",") && !Peek().IsSymbol("=") && !Peek().IsSymbol("<") &&
                Cur.Text.ToLowerInvariant() is "private" or "protected" or "public" or "published" or "automated")
            {
                vis = Cur.Text.ToLowerInvariant() switch
                {
                    "private" => Visibility.Private,
                    "protected" => Visibility.Protected,
                    "public" => Visibility.Public,
                    "published" => Visibility.Published,
                    _ => Visibility.Automated,
                };
                Advance();
                mode = MemberMode.Field;
                continue;
            }

            var attrs = ParseAttributes();
            var isClass = false;
            if (Cur.IsKeyword("class") && (IsRoutineKeyword(Peek()) || Peek().IsKeyword("property") || Peek().IsKeyword("var") || Peek().IsKeyword("threadvar")))
            {
                isClass = true;
                Advance();
            }

            var t = Cur;
            if (t.IsKeyword("var") || t.IsKeyword("threadvar"))
            {
                Advance();
                mode = isClass ? MemberMode.ClassVar : MemberMode.Field;
                continue;
            }

            if (t.IsKeyword("const") || t.IsKeyword("resourcestring"))
            {
                Advance();
                mode = MemberMode.Const;
                continue;
            }

            if (t.IsKeyword("type"))
            {
                Advance();
                mode = MemberMode.Type;
                continue;
            }

            if (IsRoutineKeyword(t) && !(t.IsWord("operator") && (Peek().IsSymbol(":") || Peek().IsSymbol(","))))
            {
                var r = ParseRoutine(start, attrs, allowBody: false, inClass: true);
                r.IsClassMethod |= isClass;
                r.Visibility = vis;
                s.Members.Add(r);
                mode = MemberMode.Field;
                continue;
            }

            if (t.IsKeyword("property"))
            {
                var p = ParseProperty(start, attrs);
                p.IsClassProperty = isClass;
                p.Visibility = vis;
                s.Members.Add(p);
                continue;
            }

            if (t.IsKeyword("case") && s.Kind is StructKind.Record or StructKind.Object)
            {
                ParseVariantPart(s.Members, vis);
                continue;
            }

            if (t.IsIdentifier)
            {
                switch (mode)
                {
                    case MemberMode.Const:
                        s.Members.Add(ParseConstDecl(start, attrs, false, true));
                        break;
                    case MemberMode.Type:
                        if (Peek().IsSymbol("=") || Peek().IsSymbol("<"))
                        {
                            s.Members.Add(ParseTypeDecl(start, attrs, vis));
                        }
                        else
                        {
                            s.Members.Add(ParseVarDecl(start, attrs, VarKind.Field, vis));
                        }

                        break;
                    default:
                        s.Members.Add(ParseVarDecl(start, attrs, mode == MemberMode.ClassVar ? VarKind.ClassVar : VarKind.Field, vis));
                        break;
                }

                if (Pos == start)
                {
                    Advance();
                }

                continue;
            }

            if (Pos == start)
            {
                if (t.IsKeyword("implementation") || t.IsKeyword("begin"))
                {
                    return; // missing `end`
                }

                Advance();
            }
        }
    }

    private void ParseVariantPart(List<Decl> members, Visibility vis)
    {
        Advance(); // case
        if (Cur.IsIdentifier && Peek().IsSymbol(":"))
        {
            var d = new VarDecl { Start = Pos, Kind = VarKind.Field, Visibility = vis, Doc = Cur.Doc };
            d.NameToks.Add(Pos);
            Advance();
            Advance();
            d.Type = ParseNamedType();
            d.End = Pos;
            members.Add(d);
        }
        else
        {
            ParseType();
        }

        AcceptKeyword("of");
        while (!AtEnd && !Cur.IsKeyword("end") && !Cur.IsSymbol(")"))
        {
            // case labels
            SkipTo(":");
            if (!Accept(":"))
            {
                break;
            }

            if (!Accept("("))
            {
                break;
            }

            while (!AtEnd && !Cur.IsSymbol(")"))
            {
                if (Cur.IsKeyword("case"))
                {
                    ParseVariantPart(members, vis);
                    continue;
                }

                if (Cur.IsIdentifier)
                {
                    var before = Pos;
                    members.Add(ParseVarDecl(Pos, [], VarKind.Field, vis));
                    if (Pos == before)
                    {
                        Advance();
                    }

                    continue;
                }

                if (Cur.IsKeyword("end"))
                {
                    return;
                }

                Advance();
            }

            Accept(")");
            Accept(";");
        }
    }

    private PropertyDecl ParseProperty(int start, List<AttributeNode> attrs)
    {
        var p = new PropertyDecl { Start = start, Doc = Tok(start).Doc ?? Cur.Doc };
        p.Attributes.AddRange(attrs);
        Advance(); // property
        p.NameTok = Pos;
        if (Cur.IsIdentifier || Cur.Kind == TokenKind.Keyword)
        {
            Advance();
        }

        if (Cur.IsSymbol("["))
        {
            ParseParams(p.IndexParams, "]");
        }

        if (Accept(":"))
        {
            p.Type = ParseType();
        }

        while (!AtEnd && !Cur.IsSymbol(";"))
        {
            if (Cur.IsIdentifier && PropertySpecifiers.Contains(Cur.Text))
            {
                var kw = Cur.Text.ToLowerInvariant();
                var kwTok = Pos;
                Advance();
                var s = Pos;
                if (kw != "nodefault" && kw != "readonly" && kw != "writeonly")
                {
                    var depth = 0;
                    while (!AtEnd)
                    {
                        if (Cur.IsSymbol("(") || Cur.IsSymbol("["))
                        {
                            depth++;
                        }
                        else if (Cur.IsSymbol(")") || Cur.IsSymbol("]"))
                        {
                            depth--;
                        }
                        else if (depth == 0 && (Cur.IsSymbol(";") || (Cur.IsIdentifier && PropertySpecifiers.Contains(Cur.Text) && !Tok(Pos - 1).IsSymbol("."))))
                        {
                            break;
                        }

                        Advance();
                    }
                }

                p.Specs.Add(new PropertySpec(kw, kwTok, new TokRange(s, Pos)));
                continue;
            }

            Advance();
        }

        Accept(";");
        if (Cur.IsWord("default") && Peek().IsSymbol(";"))
        {
            p.IsDefault = true;
            Advance();
            Advance();
        }

        while (Cur.IsIdentifier && HintDirectives.Contains(Cur.Text) && (Peek().IsSymbol(";") || Peek().Kind == TokenKind.String))
        {
            SkipTo(";");
            Accept(";");
        }

        p.End = Pos;
        return p;
    }

    public void ParseParams(List<ParamGroup> list, string closer)
    {
        Advance(); // ( or [
        while (!AtEnd && !Cur.IsSymbol(closer))
        {
            if (IsParamListBreaker(Cur))
            {
                return; // unterminated parameter list
            }

            var g = new ParamGroup();
            g.Attributes.AddRange(ParseAttributes());
            if (Cur.IsKeyword("const") || Cur.IsKeyword("var") || Cur.IsWord("out") || Cur.IsWord("constref"))
            {
                g.Modifier = Cur.Text.ToLowerInvariant();
                Advance();
            }

            g.Attributes.AddRange(ParseAttributes());
            while (Cur.IsIdentifier)
            {
                g.NameToks.Add(Pos);
                Advance();
                if (!Accept(","))
                {
                    break;
                }
            }

            if (Accept(":"))
            {
                g.Type = ParseType();
            }

            if (Accept("="))
            {
                var s = Pos;
                var depth = 0;
                while (!AtEnd)
                {
                    if (Cur.IsSymbol("(") || Cur.IsSymbol("["))
                    {
                        depth++;
                    }
                    else if (Cur.IsSymbol(")") || Cur.IsSymbol("]"))
                    {
                        if (depth == 0)
                        {
                            break;
                        }

                        depth--;
                    }
                    else if (depth == 0 && Cur.IsSymbol(";"))
                    {
                        break;
                    }

                    Advance();
                }

                g.Default = new TokRange(s, Pos);
            }

            if (g.NameToks.Count > 0)
            {
                list.Add(g);
            }

            if (!Accept(";"))
            {
                if (!Cur.IsSymbol(closer))
                {
                    // Recover: skip to the next separator or the closer.
                    while (!AtEnd && !Cur.IsSymbol(";") && !Cur.IsSymbol(closer) && !IsParamListBreaker(Cur))
                    {
                        Advance();
                    }

                    if (!Accept(";"))
                    {
                        break;
                    }
                }
            }
        }

        Accept(closer);
    }

    private static bool IsParamListBreaker(Token t) =>
        t.Kind == TokenKind.Keyword && (t.IsKeyword("end") || t.IsKeyword("begin") || t.IsKeyword("implementation") || t.IsKeyword("procedure") ||
                                        t.IsKeyword("function") || t.IsKeyword("property") || t.IsKeyword("type") || t.IsKeyword("constructor") ||
                                        t.IsKeyword("destructor"));

    public RoutineDecl ParseRoutine(int start, List<AttributeNode> attrs, bool allowBody, bool inClass)
    {
        var r = new RoutineDecl { Start = start, Doc = Tok(start).Doc ?? Cur.Doc };
        r.Attributes.AddRange(attrs);
        if (Cur.IsKeyword("class"))
        {
            r.IsClassMethod = true;
            Advance();
        }

        r.Kind = Cur.Text.ToLowerInvariant() switch
        {
            "function" => RoutineKind.Function,
            "constructor" => RoutineKind.Constructor,
            "destructor" => RoutineKind.Destructor,
            "operator" => RoutineKind.Operator,
            _ => RoutineKind.Procedure,
        };
        Advance();

        while (Cur.IsIdentifier || (Cur.Kind == TokenKind.Keyword && r.NameParts.Count == 0 && !Cur.IsSymbol("(")))
        {
            if (Cur.Kind == TokenKind.Keyword && (Cur.IsKeyword("begin") || Cur.IsKeyword("var") || Cur.IsKeyword("const") || Cur.IsKeyword("of")))
            {
                break;
            }

            var tok = Pos;
            Advance();
            List<TypeNode>? args = null;
            if (Cur.IsSymbol("<"))
            {
                var tps = ParseTypeParams();
                args = tps.Select(tp => (TypeNode)ToNamed(tp.NameTok)).ToList();
                if (!Cur.IsSymbol("."))
                {
                    r.TypeParams.AddRange(tps);
                }
            }

            r.NameParts.Add(new NamePart(tok, args));
            if (Cur.IsSymbol(".") && (Peek().IsIdentifier || Peek().Kind == TokenKind.Keyword))
            {
                Advance();
                continue;
            }

            break;
        }

        if (r.NameParts.Count == 0)
        {
            // anonymous or malformed; synthesise a name part at the keyword
            r.NameParts.Add(new NamePart(Pos - 1, null));
        }

        if (inClass && Cur.IsSymbol("="))
        {
            Advance();
            if (Cur.IsIdentifier)
            {
                r.ResolutionTarget = Pos;
                Advance();
            }

            r.HeadingEnd = Pos;
            Accept(";");
            r.End = Pos;
            return r;
        }

        if (Cur.IsSymbol("("))
        {
            ParseParams(r.Params, ")");
        }

        if (Accept(":"))
        {
            r.Return = ParseType();
        }

        r.HeadingEnd = Pos;
        ParseRoutineDirectives(r);

        if (allowBody && !r.Directives.Contains("forward") && !r.Directives.Contains("external"))
        {
            r.Body = ParseBody();
        }

        r.End = Pos;
        return r;
    }

    private static NamedTypeNode ToNamed(int tok)
    {
        var n = new NamedTypeNode { Start = tok, End = tok + 1 };
        n.Parts.Add(new NamePart(tok, null));
        return n;
    }

    private void ParseRoutineDirectives(RoutineDecl r)
    {
        while (true)
        {
            if (Cur.IsSymbol(";"))
            {
                if (Peek().IsIdentifier && RoutineDirectives.Contains(Peek().Text) && !Peek(2).IsSymbol(":") && !Peek(2).IsSymbol(",") && !Peek(2).IsSymbol("."))
                {
                    Advance();
                    continue;
                }

                if (Peek().IsKeyword("inline") || Peek().IsKeyword("library"))
                {
                    Advance();
                    continue;
                }

                Advance();
                return;
            }

            if ((Cur.IsIdentifier && RoutineDirectives.Contains(Cur.Text)) || Cur.IsKeyword("inline") || Cur.IsKeyword("library"))
            {
                r.Directives.Add(Cur.Text.ToLowerInvariant());
                Advance();
                var s = Pos;
                while (!AtEnd && !Cur.IsSymbol(";") && !(Cur.IsIdentifier && RoutineDirectives.Contains(Cur.Text) && Tok(Pos - 1).Kind != TokenKind.Identifier))
                {
                    if (Cur.IsKeyword("begin") || Cur.IsKeyword("var") || Cur.IsKeyword("const") || Cur.IsKeyword("type"))
                    {
                        break;
                    }

                    Advance();
                }

                if (Pos > s)
                {
                    r.DirectiveArgs.Add(new TokRange(s, Pos));
                }

                continue;
            }

            return;
        }
    }

    public RoutineBody ParseBody()
    {
        var body = new RoutineBody();
        ParseDeclarations(body.Locals, DeclContext.Local);
        if (Cur.IsKeyword("begin") || Cur.IsKeyword("asm"))
        {
            body.IsAsm = Cur.IsKeyword("asm");
            var end = MatchEnd(Pos);
            body.Block = new TokRange(Pos, end);
            Pos = end;
            Accept(";");
        }
        else
        {
            body.Block = new TokRange(Pos, Pos);
        }

        return body;
    }
}
