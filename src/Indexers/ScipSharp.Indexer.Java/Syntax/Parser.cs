namespace ScipSharp.Indexer.Java.Syntax;

public sealed class ParseException(string message, int offset) : Exception(message)
{
    public int Offset { get; } = offset;
}

public sealed class AnnotationExpr : Expr { public Annotation A { get; set; } = null!; }

/// <summary>Recursive-descent parser for Java 21 source files, producing the AST in <c>Ast.cs</c>.</summary>
public sealed class Parser
{
    private static readonly HashSet<string> Primitives = ["int", "long", "short", "byte", "char", "boolean", "float", "double", "void"];

    private static readonly HashSet<string> ModifierWords =
        ["public", "private", "protected", "static", "final", "abstract", "native", "synchronized", "transient", "volatile", "strictfp"];

    private static readonly HashSet<string> Reserved =
    [
        "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class", "const", "continue", "default", "do",
        "double", "else", "enum", "extends", "final", "finally", "float", "for", "goto", "if", "implements", "import",
        "instanceof", "int", "interface", "long", "native", "new", "package", "private", "protected", "public", "return",
        "short", "static", "strictfp", "super", "switch", "synchronized", "this", "throw", "throws", "transient", "try",
        "void", "volatile", "while", "true", "false", "null",
    ];

    private readonly string _src;
    private readonly List<Token> _t;
    private readonly Dictionary<int, string> _docs = [];

    private string? DocAt(int tokenStart) => _docs.GetValueOrDefault(tokenStart);
    private int _p;
    private int _lastEnd;
    private bool _noLambda;

    public Parser(string source)
    {
        _src = source;
        _t = Lexer.Lex(source, _docs);
    }

    public static CompilationUnit ParseFile(string source) => new Parser(source).ParseCompilationUnit();

    // ------------------------------------------------------------------ token helpers

    private Token Cur => _t[_p];

    private Token PeekTok(int n = 1) => _t[Math.Min(_p + n, _t.Count - 1)];

    private bool At(string s) => Cur.Is(s) && Cur.Kind != Tk.String && Cur.Kind != Tk.Char;

    private bool AtAt(int n, string s) => PeekTok(n).Is(s);

    private Token Next()
    {
        var t = _t[_p];
        if (_p < _t.Count - 1) _p++;
        _lastEnd = t.End;
        return t;
    }

    private bool Accept(string s)
    {
        if (!At(s)) return false;
        Next();
        return true;
    }

    private Token Expect(string s)
    {
        if (!At(s)) throw Fail($"expected '{s}' but found '{Cur.Text}'");
        return Next();
    }

    private ParseException Fail(string msg) => new(msg, Cur.Start);

    private bool AtIdent => Cur.Kind == Tk.Ident && !Reserved.Contains(Cur.Text);

    private Token ExpectIdent()
    {
        if (Cur.Kind != Tk.Ident || Reserved.Contains(Cur.Text)) throw Fail($"expected identifier but found '{Cur.Text}'");
        return Next();
    }

    /// <summary>
    /// Guard at the recursive entry points: throws <see cref="InsufficientExecutionStackException"/> (which speculative
    /// parses do not catch) before deeply nested input overflows the stack; callers skip the file.
    /// </summary>
    private static void EnsureStack() => System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();

    private T? Speculate<T>(Func<T?> f) where T : class
    {
        var save = (_p, _lastEnd);
        try
        {
            var r = f();
            if (r is null) (_p, _lastEnd) = save;
            return r;
        }
        catch (ParseException)
        {
            (_p, _lastEnd) = save;
            return null;
        }
    }

    private T Fin<T>(T node, int start) where T : Node
    {
        node.Start = start;
        node.End = _lastEnd;
        return node;
    }

    // ------------------------------------------------------------------ compilation unit

    public CompilationUnit ParseCompilationUnit()
    {
        var cu = new CompilationUnit { Source = _src, Start = 0 };
        var save = _p;
        var mods = ParseModifiers();
        if (At("package"))
        {
            Next();
            cu.PackageMods = mods;
            ReadQualified(cu.Package);
            Expect(";");
        }
        else
        {
            _p = save;
        }
        while (At("import"))
        {
            var start = Cur.Start;
            Next();
            var imp = new Import();
            if (At("static")) { Next(); imp.IsStatic = true; }
            else if (At("module") && PeekTok().Kind == Tk.Ident) Next();
            ReadQualified(imp.Parts);
            if (At(".") && AtAt(1, "*")) { Next(); Next(); imp.Wildcard = true; }
            Expect(";");
            cu.Imports.Add(Fin(imp, start));
        }
        while (Cur.Kind != Tk.Eof)
        {
            if (Accept(";")) continue;
            var doc = DocAt(Cur.Start);
            var m = ParseModifiers();
            if (!AtTypeKeyword()) throw Fail($"expected type declaration but found '{Cur.Text}'");
            cu.Types.Add(ParseTypeDecl(m, doc));
        }
        cu.End = _src.Length;
        return cu;
    }

    private void ReadQualified(List<(string, int, int)> parts)
    {
        var t = ExpectIdent();
        parts.Add((t.Text, t.Start, t.End));
        while (At(".") && PeekTok().Kind == Tk.Ident && !AtAt(1, "*"))
        {
            Next();
            t = Next();
            parts.Add((t.Text, t.Start, t.End));
        }
    }

    // ------------------------------------------------------------------ modifiers / annotations

    private Modifiers ParseModifiers()
    {
        var m = new Modifiers();
        while (true)
        {
            var start = Cur.Start;
            if (At("@") && !AtAt(1, "interface"))
            {
                if (m.First < 0) m.First = start;
                m.Annotations.Add(ParseAnnotation());
                continue;
            }
            if (Cur.Kind != Tk.Ident) break;
            var w = Cur.Text;
            Mod f;
            if (ModifierWords.Contains(w) && !(w == "synchronized" && AtAt(1, "(")))
            {
                f = Enum.Parse<Mod>(w, ignoreCase: true);
                if (f == Mod.None) break;
            }
            else if (w == "default" && !AtAt(1, ":") && !AtAt(1, "->")) f = Mod.Default;
            else if (w == "sealed" && PeekTok().Kind == Tk.Ident) f = Mod.Sealed;
            else if (w == "non" && AtAt(1, "-") && AtAt(2, "sealed")) { Next(); Next(); f = Mod.NonSealed; }
            else break;
            if (m.First < 0) m.First = start;
            Next();
            m.Flags |= f;
            m.Keywords.Add(f.ToString().ToLowerInvariant());
        }
        m.Start = m.First < 0 ? Cur.Start : m.First;
        m.End = _lastEnd;
        return m;
    }

    private Annotation ParseAnnotation()
    {
        EnsureStack();
        var start = Expect("@").Start;
        var a = new Annotation { Name = ParseType(allowDims: false, allowArgs: false) };
        if (Accept("("))
        {
            if (!At(")"))
            {
                do
                {
                    if (Cur.Kind == Tk.Ident && AtAt(1, "="))
                    {
                        var n = Next();
                        Next();
                        a.Args.Add((n.Text, n.Start, ParseElementValue()));
                    }
                    else a.Args.Add((null, Cur.Start, ParseElementValue()));
                }
                while (Accept(","));
            }
            Expect(")");
        }
        return Fin(a, start);
    }

    private Expr ParseElementValue()
    {
        EnsureStack();
        var start = Cur.Start;
        if (At("@")) return Fin(new AnnotationExpr { A = ParseAnnotation() }, start);
        if (At("{"))
        {
            Next();
            var init = new ArrayInit();
            while (!At("}"))
            {
                init.Items.Add(ParseElementValue());
                if (!Accept(",")) break;
            }
            Expect("}");
            return Fin(init, start);
        }
        return ParseTernary();
    }

    // ------------------------------------------------------------------ types

    private bool AtTypeKeyword() =>
        At("class") || At("interface") || At("enum") || (At("@") && AtAt(1, "interface")) ||
        (At("record") && PeekTok().Kind == Tk.Ident && (AtAt(2, "(") || AtAt(2, "<")));

    public TypeRef ParseType(bool allowDims = true, bool allowArgs = true)
    {
        EnsureStack();
        var t = new TypeRef();
        while (At("@")) t.Annotations.Add(ParseAnnotation());
        var start = Cur.Start;
        if (At("?"))
        {
            Next();
            t.Wild = WildKind.Unbounded;
            if (At("extends") || At("super"))
            {
                t.Wild = Next().Text == "extends" ? WildKind.Extends : WildKind.Super;
                t.Bound = ParseType();
            }
            t.Text = _src[start.._lastEnd];
            return Fin(t, start);
        }
        if (Cur.Kind == Tk.Ident && Primitives.Contains(Cur.Text))
        {
            var p = Next();
            t.IsPrimitive = true;
            t.Segs.Add(new TypeSeg { Name = p.Text, Start = p.Start, End = p.End });
        }
        else
        {
            var firstSeg = true;
            do
            {
                if (!firstSeg) Next();
                firstSeg = false;
                while (At("@")) t.Annotations.Add(ParseAnnotation());
                var n = ExpectIdent();
                var seg = new TypeSeg { Name = n.Text, Start = n.Start, End = n.End };
                if (allowArgs && At("<"))
                {
                    // Failures are remembered: speculating a type at every identifier of `a < b < c < ...` re-parsed
                    // the rest of the chain as nested type arguments each time (quadratic, with deep exceptions).
                    var key = (_p, _inCastType, _noLambda);
                    if (_failedTypeArgs.Contains(key)) throw Fail("type arguments expected");
                    var ok = false;
                    try
                    {
                        Next();
                        if (At(">")) seg.Diamond = true;
                        else
                        {
                            do seg.Args.Add(ParseType());
                            while (Accept(","));
                        }
                        Expect(">");
                        ok = true;
                    }
                    finally
                    {
                        if (!ok) _failedTypeArgs.Add(key);
                    }
                }
                t.Segs.Add(seg);
            }
            while (At(".") && (PeekTok().Kind == Tk.Ident || AtAt(1, "@")) && !AtAt(1, "class") && !AtAt(1, "this") && !AtAt(1, "new"));
        }
        if (allowDims)
        {
            while (true)
            {
                while (At("@")) t.Annotations.Add(ParseAnnotation());
                if (At("[") && AtAt(1, "]")) { Next(); Next(); t.Dims++; }
                else break;
            }
        }
        if (At("&") && allowDims && allowArgs && _inCastType)
        {
            while (Accept("&")) t.Intersection.Add(ParseType());
        }
        t.Text = _src[start.._lastEnd];
        return Fin(t, start);
    }

    private bool _inCastType;
    private readonly HashSet<(int, bool, bool)> _failedTypeArgs = [];

    private List<TypeParam> ParseTypeParams()
    {
        var list = new List<TypeParam>();
        if (!At("<")) return list;
        Next();
        do
        {
            var tp = new TypeParam();
            var tpStart = Cur.Start;
            while (At("@")) tp.Annotations.Add(ParseAnnotation());
            var n = ExpectIdent();
            tp.Name = n.Text;
            tp.NameStart = n.Start;
            if (Accept("extends"))
            {
                do tp.Bounds.Add(ParseType());
                while (Accept("&"));
            }
            list.Add(Fin(tp, tpStart));
        }
        while (Accept(","));
        Expect(">");
        return list;
    }

    // ------------------------------------------------------------------ declarations

    private TypeDecl ParseTypeDecl(Modifiers mods, string? doc)
    {
        EnsureStack();
        var d = new TypeDecl { Mods = mods, Doc = doc };
        var start = mods.First >= 0 ? mods.First : Cur.Start;
        if (At("@")) { Next(); Next(); d.Kind = TypeKind.Annotation; }
        else
        {
            d.Kind = Next().Text switch
            {
                "class" => TypeKind.Class,
                "interface" => TypeKind.Interface,
                "enum" => TypeKind.Enum,
                _ => TypeKind.Record,
            };
        }
        var name = ExpectIdent();
        d.Name = name.Text;
        d.NameStart = name.Start;
        d.TypeParams.AddRange(ParseTypeParams());
        if (d.Kind == TypeKind.Record)
        {
            Expect("(");
            if (!At(")"))
            {
                do d.RecordComponents.Add(ParseParam());
                while (Accept(","));
            }
            Expect(")");
        }
        if (Accept("extends"))
        {
            do (d.Kind == TypeKind.Interface ? d.Implements : d.Extends).Add(ParseType());
            while (Accept(","));
        }
        if (Accept("implements"))
        {
            do d.Implements.Add(ParseType());
            while (Accept(","));
        }
        if (Accept("permits"))
        {
            do ParseType();
            while (Accept(","));
        }
        ParseClassBody(d);
        return Fin(d, start);
    }

    private void ParseClassBody(TypeDecl d)
    {
        Expect("{");
        if (d.Kind == TypeKind.Enum)
        {
            while (!At(";") && !At("}"))
            {
                var cstart = Cur.Start;
                var doc = DocAt(cstart);
                var mods = ParseModifiers();
                var n = ExpectIdent();
                var ec = new EnumConstant { Name = n.Text, NameStart = n.Start, Mods = mods, Doc = doc };
                if (At("(")) ec.Args.AddRange(ParseArgs());
                if (At("{"))
                {
                    var anon = new TypeDecl { Kind = TypeKind.Class, IsLocalOrAnon = true };
                    ParseClassBody(anon);
                    ec.Body = anon.Members;
                }
                d.EnumConstants.Add(Fin(ec, cstart));
                if (!Accept(",")) break;
            }
            Accept(";");
        }
        while (!At("}"))
        {
            if (Cur.Kind == Tk.Eof) throw Fail("unexpected end of file in class body");
            if (Accept(";")) continue;
            ParseMember(d);
        }
        Expect("}");
        d.End = _lastEnd;
    }

    private void ParseMember(TypeDecl owner)
    {
        var start = Cur.Start;
        var doc = DocAt(start);
        if (At("{"))
        {
            var ib = new InitBlock { Body = ParseBlock() };
            owner.Members.Add(Fin(ib, start));
            return;
        }
        if (At("static") && AtAt(1, "{"))
        {
            Next();
            var ib = new InitBlock { IsStatic = true, Body = ParseBlock() };
            owner.Members.Add(Fin(ib, start));
            return;
        }
        var mods = ParseModifiers();
        var mstart = mods.First >= 0 ? mods.First : Cur.Start;
        if (AtTypeKeyword())
        {
            owner.Members.Add(ParseTypeDecl(mods, doc));
            return;
        }
        var tps = ParseTypeParams();
        if (AtIdent && Cur.Text == owner.Name && AtAt(1, "(") && owner.Kind != TypeKind.Annotation)
        {
            var n = Next();
            var m = new MethodDecl { Mods = mods, Doc = doc, Name = n.Text, NameStart = n.Start, IsCtor = true };
            m.TypeParams.AddRange(tps);
            ParseMethodRest(m);
            owner.Members.Add(Fin(m, mstart));
            return;
        }
        if (owner.Kind == TypeKind.Record && AtIdent && Cur.Text == owner.Name && AtAt(1, "{"))
        {
            var n = Next();
            var m = new MethodDecl { Mods = mods, Doc = doc, Name = n.Text, NameStart = n.Start, IsCtor = true, CompactCtor = true };
            m.Body = ParseBlock();
            owner.Members.Add(Fin(m, mstart));
            return;
        }
        var type = ParseType();
        var name = ExpectIdent();
        if (At("("))
        {
            var m = new MethodDecl { Mods = mods, Doc = doc, Ret = type, Name = name.Text, NameStart = name.Start };
            m.TypeParams.AddRange(tps);
            ParseMethodRest(m);
            owner.Members.Add(Fin(m, mstart));
            return;
        }
        // fields
        var first = true;
        var cur = name;
        while (true)
        {
            var f = new FieldDecl { Mods = mods, Doc = doc, Type = type, Name = cur.Text, NameStart = cur.Start };
            while (At("[") && AtAt(1, "]")) { Next(); Next(); f.ExtraDims++; }
            if (Accept("=")) f.Init = ParseVarInit();
            if (At(",")) { f.Start = mstart; f.End = _lastEnd; owner.Members.Add(f); Next(); cur = ExpectIdent(); first = false; continue; }
            Expect(";");
            owner.Members.Add(Fin(f, first ? mstart : cur.Start));
            break;
        }
    }

    private void ParseMethodRest(MethodDecl m)
    {
        Expect("(");
        if (!At(")"))
        {
            do
            {
                var p = ParseParam();
                if (p.Name != "this") m.Params.Add(p);
            }
            while (Accept(","));
        }
        Expect(")");
        while (At("[") && AtAt(1, "]")) { Next(); Next(); }
        if (Accept("throws"))
        {
            do m.Throws.Add(ParseType());
            while (Accept(","));
        }
        if (Accept("default")) m.DefaultValue = ParseElementValue();
        if (At("{")) m.Body = ParseBlock();
        else Expect(";");
    }

    private Param ParseParam()
    {
        var mods = ParseModifiers();
        var start = mods.First >= 0 ? mods.First : Cur.Start;
        var p = new Param { Mods = mods, Type = ParseType() };
        if (Accept("...")) { p.Varargs = true; }
        var n = Cur.Is("this") ? Next() : ExpectIdent();
        p.Name = n.Text;
        p.NameStart = n.Start;
        while (At("[") && AtAt(1, "]")) { Next(); Next(); p.Type!.Dims++; }
        return Fin(p, start);
    }

    private Expr ParseVarInit() => At("{") ? ParseArrayInit() : ParseExpr();

    private ArrayInit ParseArrayInit()
    {
        EnsureStack();
        var start = Expect("{").Start;
        var a = new ArrayInit();
        while (!At("}"))
        {
            a.Items.Add(ParseVarInit());
            if (!Accept(",")) break;
        }
        Expect("}");
        return Fin(a, start);
    }

    // ------------------------------------------------------------------ statements

    public Block ParseBlock()
    {
        EnsureStack();
        var start = Expect("{").Start;
        var b = new Block();
        while (!At("}"))
        {
            if (Cur.Kind == Tk.Eof) throw Fail("unexpected end of file in block");
            b.Stmts.Add(ParseStmt());
        }
        Expect("}");
        return Fin(b, start);
    }

    private VarDeclStmt? TryLocalVarDecl(bool requireSemi)
    {
        return Speculate(() =>
        {
            var start = Cur.Start;
            var mods = ParseModifiers();
            if (Cur.Kind != Tk.Ident || (Reserved.Contains(Cur.Text) && !Primitives.Contains(Cur.Text))) return null;
            var type = ParseType();
            if (!AtIdent) return null;
            if (!(AtAt(1, "=") || AtAt(1, ";") || AtAt(1, ",") || AtAt(1, ":") || AtAt(1, "["))) return null;
            var v = new VarDeclStmt { Mods = mods, Type = type };
            do
            {
                var n = ExpectIdent();
                var d = new Declarator { Name = n.Text, NameStart = n.Start };
                while (At("[") && AtAt(1, "]")) { Next(); Next(); d.ExtraDims++; }
                if (Accept("=")) d.Init = ParseVarInit();
                v.Vars.Add(Fin(d, n.Start));
            }
            while (Accept(","));
            if (requireSemi) Expect(";");
            return Fin(v, start);
        });
    }

    public Stmt ParseStmt()
    {
        EnsureStack();
        var start = Cur.Start;
        if (At("{")) return ParseBlock();
        if (Accept(";")) return Fin(new SimpleStmt(), start);
        if (Cur.Kind == Tk.Ident || At("@"))
        {
            switch (Cur.Text)
            {
                case "if":
                {
                    Next();
                    var s = new IfStmt();
                    Expect("(");
                    s.Cond = ParseExpr();
                    Expect(")");
                    s.Then = ParseStmt();
                    if (Accept("else")) s.Else = ParseStmt();
                    return Fin(s, start);
                }
                case "while":
                {
                    Next();
                    var s = new WhileStmt();
                    Expect("(");
                    s.Cond = ParseExpr();
                    Expect(")");
                    s.Body = ParseStmt();
                    return Fin(s, start);
                }
                case "do":
                {
                    Next();
                    var s = new WhileStmt { IsDo = true, Body = ParseStmt() };
                    Expect("while");
                    Expect("(");
                    s.Cond = ParseExpr();
                    Expect(")");
                    Expect(";");
                    return Fin(s, start);
                }
                case "for": return ParseFor();
                case "return":
                {
                    Next();
                    var s = new ReturnStmt();
                    if (!At(";")) s.E = ParseExpr();
                    Expect(";");
                    return Fin(s, start);
                }
                case "throw":
                {
                    Next();
                    var s = new ThrowStmt { E = ParseExpr() };
                    Expect(";");
                    return Fin(s, start);
                }
                case "break" or "continue":
                {
                    Next();
                    if (AtIdent) Next();
                    Expect(";");
                    return Fin(new SimpleStmt(), start);
                }
                case "assert":
                {
                    Next();
                    var s = new SimpleStmt { E = ParseExpr() };
                    if (Accept(":")) ParseExpr();
                    Expect(";");
                    return Fin(s, start);
                }
                case "try": return ParseTry();
                case "switch" when !IsSwitchExprStatement():
                {
                    var sw = ParseSwitch();
                    return sw;
                }
                case "synchronized" when AtAt(1, "("):
                {
                    Next();
                    Expect("(");
                    var s = new SyncStmt { Lock = ParseExpr() };
                    Expect(")");
                    s.Body = ParseBlock();
                    return Fin(s, start);
                }
                case "yield" when !(AtAt(1, "=") || AtAt(1, ".") || AtAt(1, "[") || AtAt(1, "++") || AtAt(1, "--")):
                {
                    Next();
                    var s = new SimpleStmt { E = ParseExpr() };
                    Expect(";");
                    return Fin(s, start);
                }
            }
            if (AtIdent && AtAt(1, ":") && !AtAt(1, "::"))
            {
                Next();
                Next();
                return Fin(new LabeledStmt { Body = ParseStmt() }, start);
            }
            // local class
            var save = _p;
            var mods = ParseModifiers();
            if (AtTypeKeyword())
            {
                var d = ParseTypeDecl(mods, DocAt(start));
                d.IsLocalOrAnon = true;
                return Fin(new LocalClassStmt { Decl = d }, start);
            }
            _p = save;
            var lv = TryLocalVarDecl(requireSemi: true);
            if (lv is not null) return lv;
        }
        var e = ParseExpr();
        Expect(";");
        return Fin(new ExprStmt { E = e }, start);
    }

    private bool IsSwitchExprStatement() => false;

    private Stmt ParseFor()
    {
        var start = Next().Start;
        Expect("(");
        var fe = Speculate(() =>
        {
            var v = TryLocalVarDeclForEach();
            return v;
        });
        if (fe is not null)
        {
            var s = new ForEachStmt { Var = fe };
            s.Iterable = ParseExpr();
            Expect(")");
            s.Body = ParseStmt();
            return Fin(s, start);
        }
        var f = new ForStmt();
        if (!At(";"))
        {
            var lv = TryLocalVarDecl(requireSemi: false);
            if (lv is not null) f.Init.Add(lv);
            else
            {
                do
                {
                    var es = Cur.Start;
                    var ex = ParseExpr();
                    f.Init.Add(Fin(new ExprStmt { E = ex }, es));
                }
                while (Accept(","));
            }
        }
        Expect(";");
        if (!At(";")) f.Cond = ParseExpr();
        Expect(";");
        if (!At(")"))
        {
            do f.Update.Add(ParseExpr());
            while (Accept(","));
        }
        Expect(")");
        f.Body = ParseStmt();
        return Fin(f, start);
    }

    private VarDeclStmt? TryLocalVarDeclForEach()
    {
        var start = Cur.Start;
        var mods = ParseModifiers();
        if (Cur.Kind != Tk.Ident) return null;
        var type = ParseType();
        if (!AtIdent || !AtAt(1, ":")) return null;
        var n = Next();
        Next();
        var v = new VarDeclStmt { Mods = mods, Type = type };
        v.Vars.Add(new Declarator { Name = n.Text, NameStart = n.Start, Start = n.Start, End = n.End });
        return Fin(v, start);
    }

    private Stmt ParseTry()
    {
        var start = Next().Start;
        var t = new TryStmt();
        if (Accept("("))
        {
            while (!At(")"))
            {
                var rs = Cur.Start;
                var lv = TryLocalVarDecl(requireSemi: false);
                if (lv is not null) t.Resources.Add(lv);
                else
                {
                    var e = ParseExpr();
                    t.Resources.Add(Fin(new ExprStmt { E = e }, rs));
                }
                if (!Accept(";")) break;
            }
            Expect(")");
        }
        t.Body = ParseBlock();
        while (At("catch"))
        {
            var cs = Next().Start;
            Expect("(");
            var c = new CatchClause { Mods = ParseModifiers() };
            do c.Types.Add(ParseType());
            while (Accept("|"));
            var n = ExpectIdent();
            c.Name = n.Text;
            c.NameStart = n.Start;
            Expect(")");
            c.Body = ParseBlock();
            t.Catches.Add(Fin(c, cs));
        }
        if (Accept("finally")) t.Finally = ParseBlock();
        return Fin(t, start);
    }

    private SwitchStmt ParseSwitch()
    {
        var start = Next().Start;
        var sw = new SwitchStmt();
        Expect("(");
        sw.Selector = ParseExpr();
        Expect(")");
        Expect("{");
        while (!At("}"))
        {
            var cs = Cur.Start;
            var c = new SwitchCase();
            if (Accept("default")) c.IsDefault = true;
            else
            {
                Expect("case");
                do
                {
                    if (At("default")) { Next(); c.IsDefault = true; continue; }
                    var pat = Speculate(ParsePattern);
                    if (pat is not null) c.Patterns.Add(pat);
                    else
                    {
                        var old = _noLambda;
                        _noLambda = true;
                        c.Labels.Add(ParseTernary());
                        _noLambda = old;
                    }
                }
                while (Accept(","));
                if (At("when")) { Next(); var old = _noLambda; _noLambda = true; c.Guard = ParseTernary(); _noLambda = old; }
            }
            if (Accept("->"))
            {
                var bs = Cur.Start;
                if (At("{")) c.Body.Add(ParseBlock());
                else if (At("throw")) c.Body.Add(ParseStmt());
                else
                {
                    var e = ParseExpr();
                    Expect(";");
                    c.Body.Add(Fin(new ExprStmt { E = e }, bs));
                }
            }
            else
            {
                Expect(":");
                while (!At("case") && !At("default") && !At("}")) c.Body.Add(ParseStmt());
            }
            sw.Cases.Add(Fin(c, cs));
        }
        Expect("}");
        return Fin(sw, start);
    }

    private Pattern? ParsePattern()
    {
        var start = Cur.Start;
        var mods = ParseModifiers();
        if (Cur.Kind != Tk.Ident || Reserved.Contains(Cur.Text) && !Primitives.Contains(Cur.Text)) return null;
        var type = ParseType();
        var p = new Pattern { Mods = mods, Type = type };
        if (At("("))
        {
            Next();
            p.Sub = [];
            if (!At(")"))
            {
                do
                {
                    var sub = ParsePattern();
                    if (sub is null) return null;
                    p.Sub.Add(sub);
                }
                while (Accept(","));
            }
            Expect(")");
            if (AtIdent && !At("when")) { var n = Next(); p.Name = n.Text; p.NameStart = n.Start; }
            return Fin(p, start);
        }
        if (!AtIdent || At("when")) return null;
        var nm = Next();
        p.Name = nm.Text;
        p.NameStart = nm.Start;
        return Fin(p, start);
    }

    // ------------------------------------------------------------------ expressions

    private List<Expr> ParseArgs()
    {
        var args = new List<Expr>();
        Expect("(");
        if (!At(")"))
        {
            do args.Add(ParseExpr());
            while (Accept(","));
        }
        Expect(")");
        return args;
    }

    private static readonly HashSet<string> AssignOps = ["=", "+=", "-=", "*=", "/=", "&=", "|=", "^=", "%=", "<<="];

    public Expr ParseExpr()
    {
        EnsureStack();
        var start = Cur.Start;
        if (!_noLambda)
        {
            var lam = TryLambda();
            if (lam is not null) return lam;
        }
        var left = ParseTernary();
        string? op = null;
        var n = 1;
        if (Cur.Kind == Tk.Op)
        {
            if (AssignOps.Contains(Cur.Text)) op = Cur.Text;
            else if (Cur.Text == ">")
            {
                if (AdjacentOps(">>>=")) { op = ">>>="; n = 4; }
                else if (AdjacentOps(">>=")) { op = ">>="; n = 3; }
            }
        }
        if (op is null) return left;
        for (var i = 0; i < n; i++) Next();
        var right = ParseVarInitOrExpr();
        return Fin(new Assign { Op = op, L = left, R = right }, start);
    }

    private Expr ParseVarInitOrExpr() => At("{") ? ParseArrayInit() : ParseExpr();

    private bool AdjacentOps(string s)
    {
        var o = Cur.Start;
        return o + s.Length <= _src.Length && string.CompareOrdinal(_src, o, s, 0, s.Length) == 0;
    }

    private Expr? TryLambda()
    {
        var start = Cur.Start;
        if (AtIdent && AtAt(1, "->"))
        {
            var n = Next();
            Next();
            var lam = new Lambda();
            lam.Params.Add(new Param { Name = n.Text, NameStart = n.Start, Start = n.Start, End = n.End });
            ParseLambdaBodyAfterArrow(lam);
            return Fin(lam, start);
        }
        if (!At("(")) return null;
        var i = MatchingParen(_p);
        if (i + 1 >= _t.Count || !_t[i + 1].Is("->")) return null;
        Next();
        var l = new Lambda();
        if (!At(")"))
        {
            do
            {
                if (AtIdent && (AtAt(1, ",") || AtAt(1, ")")))
                {
                    var n = Next();
                    l.Params.Add(new Param { Name = n.Text, NameStart = n.Start, Start = n.Start, End = n.End });
                }
                else l.Params.Add(ParseParam());
            }
            while (Accept(","));
        }
        Expect(")");
        Expect("->");
        ParseLambdaBodyAfterArrow(l);
        return Fin(l, start);
    }

    private int[]? _matchingParen;

    /// <summary>
    /// Index of the <c>)</c> matching the <c>(</c> at token <paramref name="open"/> (<c>_t.Count</c> if unmatched).
    /// Computed once for the file: scanning ahead at every <c>(</c> was quadratic in the nesting depth.
    /// </summary>
    private int MatchingParen(int open)
    {
        if (_matchingParen is null)
        {
            _matchingParen = new int[_t.Count];
            Array.Fill(_matchingParen, _t.Count);
            var stack = new Stack<int>();
            for (var k = 0; k < _t.Count; k++)
            {
                var tk = _t[k];
                if (tk.Kind != Tk.Op) continue;
                if (tk.Text == "(") stack.Push(k);
                else if (tk.Text == ")" && stack.Count > 0) _matchingParen[stack.Pop()] = k;
            }
        }
        return _matchingParen[open];
    }

    private void ParseLambdaBody(Lambda l)
    {
        Expect("->");
        ParseLambdaBodyAfterArrow(l);
    }

    private void ParseLambdaBodyAfterArrow(Lambda l)
    {
        var old = _noLambda;
        _noLambda = false;
        if (At("{")) l.BodyBlock = ParseBlock();
        else l.BodyExpr = ParseExpr();
        _noLambda = old;
    }

    public Expr ParseTernary()
    {
        var start = Cur.Start;
        var c = ParseBinary(1);
        if (!At("?")) return c;
        Next();
        var old = _noLambda;
        _noLambda = false;
        var a = ParseExpr();
        _noLambda = old;
        Expect(":");
        var b = _noLambda ? ParseTernary() : ParseExprNoAssignLambda();
        return Fin(new Conditional { Cond = c, A = a, B = b }, start);
    }

    private Expr ParseExprNoAssignLambda()
    {
        var lam = TryLambda();
        return lam ?? ParseTernary();
    }

    private (string Op, int Prec, int Tokens)? PeekBinary()
    {
        var t = Cur;
        if (t.Kind == Tk.Ident) return t.Text == "instanceof" ? ("instanceof", 7, 1) : null;
        if (t.Kind != Tk.Op) return null;
        switch (t.Text)
        {
            case "||": return ("||", 1, 1);
            case "&&": return ("&&", 2, 1);
            case "|": return ("|", 3, 1);
            case "^": return ("^", 4, 1);
            case "&": return ("&", 5, 1);
            case "==" or "!=": return (t.Text, 6, 1);
            case "<=": return ("<=", 7, 1);
            case "<": return ("<", 7, 1);
            case "<<": return ("<<", 8, 1);
            case "+" or "-": return (t.Text, 9, 1);
            case "*" or "/" or "%": return (t.Text, 10, 1);
            case ">":
                if (AdjacentOps(">>>=") || AdjacentOps(">>=")) return null;
                if (AdjacentOps(">>>")) return (">>>", 8, 3);
                if (AdjacentOps(">>")) return (">>", 8, 2);
                if (AdjacentOps(">=")) return (">=", 7, 2);
                return (">", 7, 1);
        }
        return null;
    }

    private Expr ParseBinary(int minPrec)
    {
        var start = Cur.Start;
        var left = ParseUnary();
        while (true)
        {
            var op = PeekBinary();
            if (op is null || op.Value.Prec < minPrec) return left;
            var (o, prec, n) = op.Value;
            for (var i = 0; i < n; i++) Next();
            if (o == "instanceof")
            {
                var io = new InstanceOf { E = left };
                var pat = Speculate(ParsePattern);
                if (pat is not null && (pat.Name is not null || pat.Sub is not null)) io.Pattern = pat;
                else io.Type = ParseType();
                left = Fin(io, start);
                continue;
            }
            var right = ParseBinary(prec + 1);
            left = Fin(new Binary { Op = o, L = left, R = right }, start);
        }
    }

    private Expr ParseUnary()
    {
        EnsureStack();
        var start = Cur.Start;
        if (Cur.Kind == Tk.Op)
        {
            switch (Cur.Text)
            {
                case "+" or "-" or "!" or "~" or "++" or "--":
                {
                    var op = Next().Text;
                    return Fin(new Unary { Op = op, E = ParseUnary() }, start);
                }
                case "(":
                {
                    var cast = TryCast();
                    if (cast is not null) return cast;
                    break;
                }
            }
        }
        return ParsePostfix(ParsePrimary());
    }

    private Expr? TryCast()
    {
        var start = Cur.Start;
        // lambda?
        var lam = _noLambda ? null : TryLambda();
        if (lam is not null) return lam;
        return Speculate<Expr>(() =>
        {
            Expect("(");
            var prim = Cur.Kind == Tk.Ident && Primitives.Contains(Cur.Text);
            var old = _inCastType;
            _inCastType = true;
            var type = ParseType();
            _inCastType = old;
            Expect(")");
            if (prim && type.Dims == 0)
            {
                return Fin(new Cast { Type = type, E = ParseUnary() }, start);
            }
            var c = Cur;
            var ok = c.Kind is Tk.Number or Tk.String or Tk.Char
                     || (c.Kind == Tk.Ident && c.Text != "instanceof") || (c.Kind == Tk.Op && c.Text is "(" or "!" or "~");
            if (!ok) return null;
            var lamOperand = _noLambda ? null : TryLambda();
            return Fin(new Cast { Type = type, E = lamOperand ?? ParseUnary() }, start);
        });
    }

    private Expr ParsePrimary()
    {
        var start = Cur.Start;
        var t = Cur;
        switch (t.Kind)
        {
            case Tk.Number or Tk.String or Tk.Char:
                Next();
                return Fin(new Literal { Text = t.Text, Kind = t.Kind }, start);
            case Tk.Op when t.Text == "(":
            {
                Next();
                var old = _noLambda;
                _noLambda = false;
                var e = ParseExpr();
                _noLambda = old;
                Expect(")");
                return Fin(new Paren { E = e }, start);
            }
            case Tk.Op when t.Text == "@":
                return Fin(new AnnotationExpr { A = ParseAnnotation() }, start);
            case Tk.Ident:
                break;
            default:
                throw Fail($"unexpected token '{t.Text}'");
        }

        switch (t.Text)
        {
            case "true" or "false" or "null":
                Next();
                return Fin(new Literal { Text = t.Text, Kind = Tk.Ident }, start);
            case "new": return ParseNew(null);
            case "switch":
            {
                var sw = ParseSwitch();
                return Fin(new SwitchExpr { Switch = sw }, start);
            }
            case "this" or "super":
                Next();
                if (At("(")) return Fin(CallOf(null, t.Text, t.Start, ParseArgs()), start);
                return Fin(new NameExpr { Name = t.Text }, start);
        }

        if (Primitives.Contains(t.Text))
        {
            var ty = ParseType();
            if (At("::")) { Next(); Expect("new"); return Fin(new MethodRef { TargetType = ty, Name = "new", NameStart = _lastEnd - 3 }, start); }
            Expect(".");
            Expect("class");
            return Fin(new ClassLit { Type = ty }, start);
        }

        // array / generic type method refs and class literals: String[]::new, List<String>::new, int[].class
        if ((AtAt(1, "[") && AtAt(2, "]")) || AtAt(1, "<") || DottedGenericAhead())
        {
            var tr = Speculate<Expr>(() =>
            {
                var ty = ParseType();
                if (At("::"))
                {
                    Next();
                    SkipExplicitTypeArgs();
                    var n = At("new") ? Next() : ExpectIdent();
                    return Fin(new MethodRef { TargetType = ty, Name = n.Text, NameStart = n.Start }, start);
                }
                if (At(".") && AtAt(1, "class")) { Next(); Next(); return Fin(new ClassLit { Type = ty }, start); }
                return null;
            });
            if (tr is not null) return tr;
        }

        var id = ExpectIdent();
        if (At("(")) return Fin(CallOf(null, id.Text, id.Start, ParseArgs()), start);
        return Fin(new NameExpr { Name = id.Text }, start);
    }

    /// <summary>True for <c>a.b.C&lt;</c>: a qualified name directly followed by type arguments (e.g. <c>Map.Entry&lt;K, V&gt;::new</c>).</summary>
    private bool DottedGenericAhead()
    {
        var i = 1;
        if (!AtAt(1, ".")) return false;
        while (AtAt(i, ".") && PeekTok(i + 1).Kind == Tk.Ident) i += 2;
        return i > 1 && AtAt(i, "<");
    }

    /// <summary>Skips the explicit type arguments of <c>Type::&lt;K, V&gt;method</c>.</summary>
    private void SkipExplicitTypeArgs()
    {
        if (!At("<")) return;
        Next();
        do ParseType(); while (Accept(","));
        Expect(">");
    }

    private static Call CallOf(Expr? target, string name, int nameStart, List<Expr> args)
    {
        var c = new Call { Target = target, Name = name, NameStart = nameStart };
        c.Args.AddRange(args);
        return c;
    }

    private Expr ParseNew(Expr? outer)
    {
        EnsureStack();
        var start = outer?.Start ?? Cur.Start;
        Expect("new");
        if (At("<")) { Next(); do ParseType(); while (Accept(",")); Expect(">"); }
        var type = ParseType(allowDims: false);
        if (At("[") )
        {
            var na = new NewArray { Elem = type };
            while (At("["))
            {
                Next();
                if (At("]")) { Next(); na.ExtraDims++; }
                else
                {
                    na.Dims.Add(ParseExpr());
                    Expect("]");
                }
            }
            if (At("{")) na.Init = ParseArrayInit();
            return Fin(na, start);
        }
        var ne = new NewExpr { Outer = outer, Type = type };
        ne.Args.AddRange(ParseArgs());
        if (At("{"))
        {
            var body = new TypeDecl { Kind = TypeKind.Class, IsLocalOrAnon = true, Name = "" };
            var bs = Cur.Start;
            ParseClassBody(body);
            body.Start = bs;
            ne.Body = body;
        }
        return Fin(ne, start);
    }

    private Expr ParsePostfix(Expr e)
    {
        var start = e.Start;
        while (true)
        {
            if (At("."))
            {
                Next();
                if (At("new")) { e = ParseNew(e); continue; }
                List<TypeRef>? targs = null;
                if (At("<"))
                {
                    Next();
                    targs = [];
                    do targs.Add(ParseType());
                    while (Accept(","));
                    Expect(">");
                }
                if (At("class"))
                {
                    Next();
                    e = Fin(new ClassLit { Type = ExprToType(e) }, start);
                    continue;
                }
                if (At("this") || At("super"))
                {
                    var th = Next();
                    e = Fin(new FieldAccess { Target = e, Name = th.Text, NameStart = th.Start }, start);
                    if (At("(")) e = Fin(CallOf(e, th.Text, th.Start, ParseArgs()), start);
                    continue;
                }
                var n = ExpectIdent();
                if (At("("))
                {
                    var call = CallOf(e, n.Text, n.Start, ParseArgs());
                    if (targs is not null) call.TypeArgs.AddRange(targs);
                    e = Fin(call, start);
                }
                else e = Fin(new FieldAccess { Target = e, Name = n.Text, NameStart = n.Start }, start);
            }
            else if (At("[") && AtAt(1, "]") && e is NameExpr or FieldAccess)
            {
                // qualified array type: Map.Entry[].class, Outer.Inner[]::new
                var ty = ExprToType(e);
                while (At("[") && AtAt(1, "]")) { Next(); Next(); ty.Dims++; }
                if (At("::"))
                {
                    Next();
                    var n = At("new") ? Next() : ExpectIdent();
                    e = Fin(new MethodRef { TargetType = ty, Name = n.Text, NameStart = n.Start }, start);
                }
                else
                {
                    Expect(".");
                    Expect("class");
                    e = Fin(new ClassLit { Type = ty }, start);
                }
            }
            else if (At("["))
            {
                Next();
                var old = _noLambda;
                _noLambda = false;
                var idx = ParseExpr();
                _noLambda = old;
                Expect("]");
                e = Fin(new IndexExpr { Target = e, Index = idx }, start);
            }
            else if (At("++") || At("--"))
            {
                var op = Next().Text;
                e = Fin(new Unary { Op = op, E = e, Postfix = true }, start);
            }
            else if (At("::"))
            {
                Next();
                SkipExplicitTypeArgs();
                var n = At("new") ? Next() : ExpectIdent();
                e = Fin(new MethodRef { Target = e, Name = n.Text, NameStart = n.Start }, start);
            }
            else return e;
        }
    }

    private TypeRef ExprToType(Expr e)
    {
        var t = new TypeRef { Start = e.Start, End = e.End, Text = _src[e.Start..e.End] };
        void Walk(Expr x)
        {
            switch (x)
            {
                case NameExpr n: t.Segs.Add(new TypeSeg { Name = n.Name, Start = n.Start, End = n.End }); break;
                case FieldAccess f:
                    Walk(f.Target);
                    t.Segs.Add(new TypeSeg { Name = f.Name, Start = f.NameStart, End = f.End });
                    break;
                default: throw new ParseException("not a type", x.Start);
            }
        }
        Walk(e);
        return t;
    }
}
