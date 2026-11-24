namespace Scip.Indexer.TypeScript.Syntax;

public sealed partial class Parser
{
    private Node ParseStatement()
    {
        var start = _s.TokenStart;
        var doc = _s.JsDoc;
        var decorators = new List<Node>();
        ParseDecorators(decorators);

        if (_s.Kind == T.Punct)
        {
            if (IsP("{")) { return ParseBlock(); }
            if (IsP(";")) { var e = Begin(SK.EmptyStatement); _s.Next(); return Fin(e); }
        }

        if (_s.Kind == T.Ident)
        {
            switch (_s.TokenText)
            {
                case "import" when !PeekIsPunct("(") && !PeekIsPunct("."):
                    return ParseImport(start, doc);
                case "export":
                    return ParseExport(start, doc, decorators);
                case "var" or "let" or "const" when !IsConstEnum() && (_s.TokenText != "let" || LetStartsDeclaration()):
                    return ParseVariableStatement(start, doc, Mods.None);
                case "using" or "await" when UsingStartsDeclaration():
                    return ParseVariableStatement(start, doc, Mods.None);
                case "function":
                    return FinishDecl(ParseFunction(start, Mods.None, SK.FunctionDeclaration), doc);
                case "class":
                    return FinishDecl(ParseClass(start, Mods.None, SK.ClassDeclaration, decorators), doc);
                case "if": return ParseIf();
                case "for": return ParseFor();
                case "while":
                {
                    var n = Begin(SK.WhileStatement); _s.Next(); ExpectP("("); n.Condition = ParseExpression(); ExpectP(")");
                    n.Body = ParseStatement(); return Fin(n);
                }

                case "do":
                {
                    var n = Begin(SK.DoStatement); _s.Next(); n.Body = ParseStatement(); ExpectK("while"); ExpectP("(");
                    n.Condition = ParseExpression(); ExpectP(")"); EatP(";"); return Fin(n);
                }

                case "return":
                {
                    var n = Begin(SK.ReturnStatement); _s.Next();
                    if (!IsP(";") && !IsP("}") && _s.Kind != T.Eof && !_s.NewlineBefore) { n.Expression = ParseExpression(); }
                    ConsumeSemicolon(); return Fin(n);
                }

                case "throw":
                {
                    var n = Begin(SK.ThrowStatement); _s.Next(); n.Expression = ParseExpression(); ConsumeSemicolon(); return Fin(n);
                }

                case "break" or "continue":
                {
                    var n = Begin(_s.TokenText == "break" ? SK.BreakStatement : SK.ContinueStatement); _s.Next();
                    if (_s.Kind == T.Ident && !_s.NewlineBefore && !Reserved.Contains(_s.TokenText)) { n.Name = ParseIdentifier(); }
                    ConsumeSemicolon(); return Fin(n);
                }

                case "debugger": { var n = Begin(SK.DebuggerStatement); _s.Next(); ConsumeSemicolon(); return Fin(n); }
                case "try": return ParseTry();
                case "switch": return ParseSwitch();
                case "interface" when PeekIsIdentNoNewline():
                    return FinishDecl(ParseInterface(start, Mods.None), doc);
                case "type" when PeekIsIdentNoNewline():
                    return FinishDecl(ParseTypeAlias(start, Mods.None), doc);
                case "enum":
                    return FinishDecl(ParseEnum(start, Mods.None), doc);
                case "namespace" or "module" when PeekIsNamespaceName():
                    return FinishDecl(ParseModule(start, Mods.None), doc);
                case "declare" or "abstract" or "async" when IsDeclarationAfterModifiers():
                    return ParseModifiedDeclaration(start, doc, decorators);
                case "const" when IsConstEnum():
                    return ParseModifiedDeclaration(start, doc, decorators);
            }

            // labeled statement
            if (!Reserved.Contains(_s.TokenText) && PeekIsPunct(":"))
            {
                var n = Begin(SK.LabeledStatement);
                n.Name = ParseIdentifier();
                ExpectP(":");
                n.Body = ParseStatement();
                return Fin(n);
            }
        }

        var stmt = Begin(SK.ExpressionStatement);
        stmt.Expression = ParseExpression();
        ConsumeSemicolon();
        return Fin(stmt);
    }

    private Node FinishDecl(Node n, string? doc)
    {
        n.JsDoc ??= doc;
        return n;
    }

    private bool PeekIsOptionalColon()
    {
        var snap = Save();
        _s.Next();
        var r = IsP("?");
        if (r) { _s.Next(); r = IsP(":"); }
        Restore(snap);
        return r;
    }

    private bool PeekIsPunct(string p)
    {
        var snap = Save();
        _s.Next();
        var r = IsP(p);
        Restore(snap);
        return r;
    }

    private bool PeekIsIdentNoNewline()
    {
        var snap = Save();
        _s.Next();
        var r = _s.Kind == T.Ident && !_s.NewlineBefore;
        Restore(snap);
        return r;
    }

    private bool PeekIsNamespaceName()
    {
        var snap = Save();
        _s.Next();
        var r = (_s.Kind is T.Ident or T.String) && !_s.NewlineBefore;
        Restore(snap);
        return r;
    }

    private bool IsConstEnum()
    {
        if (!IsK("const")) { return false; }
        var snap = Save();
        _s.Next();
        var r = IsK("enum");
        Restore(snap);
        return r;
    }

    /// <summary>`using x = ...` / `await using x = ...` explicit resource declarations.</summary>
    private bool UsingStartsDeclaration()
    {
        var snap = Save();
        try
        {
            if (IsK("await")) { _s.Next(); if (_s.NewlineBefore || !IsK("using")) { return false; } }
            _s.Next();
            return !_s.NewlineBefore && _s.Kind == T.Ident && !Reserved.Contains(_s.TokenText) && _s.TokenText is not ("of" or "in" or "as" or "satisfies" or "instanceof");
        }
        finally
        {
            Restore(snap);
        }
    }

    private bool LetStartsDeclaration()
    {
        var snap = Save();
        _s.Next();
        var r = _s.Kind == T.Ident || IsP("[") || IsP("{");
        Restore(snap);
        return r;
    }

    private bool IsDeclarationAfterModifiers()
    {
        var snap = Save();
        try
        {
            while (_s.Kind == T.Ident && _s.TokenText is "declare" or "abstract" or "async" or "export" or "default" or "const")
            {
                _s.Next();
                if (_s.NewlineBefore && false) { return false; }
            }

            return _s.Kind == T.Ident && _s.TokenText is "function" or "class" or "interface" or "type" or "enum" or "namespace" or "module"
                or "var" or "let" or "const" or "global";
        }
        finally
        {
            Restore(snap);
        }
    }

    private Node ParseModifiedDeclaration(int start, string? doc, List<Node> decorators)
    {
        var mods = ParseModifiers(allowAll: true);
        return ParseDeclarationBody(start, doc, mods, decorators) ?? throw new SyntaxException("declaration expected", _s.TokenStart);
    }

    private Node? ParseDeclarationBody(int start, string? doc, Mods mods, List<Node> decorators)
    {
        Node? n = null;
        if (_s.Kind == T.Ident)
        {
            switch (_s.TokenText)
            {
                case "var" or "let" or "const":
                    return ParseVariableStatement(start, doc, mods);
                case "function": n = ParseFunction(start, mods, SK.FunctionDeclaration); break;
                case "class": n = ParseClass(start, mods, SK.ClassDeclaration, decorators); break;
                case "interface": n = ParseInterface(start, mods); break;
                case "type": n = ParseTypeAlias(start, mods); break;
                case "enum": n = ParseEnum(start, mods); break;
                case "namespace" or "module" or "global": n = ParseModule(start, mods); break;
            }
        }

        if (n is not null) { n.JsDoc ??= doc; }
        return n;
    }

    // ---------------------------------------------------------------- blocks and simple statements
    private Node ParseBlock()
    {
        var n = Begin(SK.Block);
        ExpectP("{");
        while (!IsP("}") && _s.Kind != T.Eof) { ParseStatementRecovering(n.Elements); }
        ExpectP("}");
        return Fin(n);
    }

    private Node ParseIf()
    {
        var n = Begin(SK.IfStatement);
        _s.Next();
        ExpectP("(");
        n.Condition = ParseExpression();
        ExpectP(")");
        n.Then = ParseStatement();
        if (EatK("else")) { n.Else = ParseStatement(); }
        return Fin(n);
    }

    private Node ParseFor()
    {
        var start = _s.TokenStart;
        _s.Next();
        EatK("await");
        ExpectP("(");
        Node? init = null;
        if (IsP(";")) { /* no initializer */ }
        else if (IsK("var") || IsK("const") || (IsK("let") && LetStartsDeclaration()) || (IsK("using") || IsK("await")) && UsingStartsDeclaration())
        {
            init = ParseVariableDeclarationList(noIn: true);
        }
        else
        {
            init = ParseExpression(noIn: true);
        }

        Node n;
        if (IsK("of") || IsK("in"))
        {
            n = new Node(IsK("of") ? SK.ForOfStatement : SK.ForInStatement, start);
            _s.Next();
            n.Initializer = init;
            n.Expression = IsKOfPrev(n) ? ParseAssignment() : ParseExpression();
            ExpectP(")");
        }
        else
        {
            n = new Node(SK.ForStatement, start);
            n.Initializer = init;
            ExpectP(";");
            if (!IsP(";")) { n.Condition = ParseExpression(); }
            ExpectP(";");
            if (!IsP(")")) { n.Right = ParseExpression(); }
            ExpectP(")");
        }

        n.Body = ParseStatement();
        return Fin(n);
    }

    private static bool IsKOfPrev(Node n) => n.Kind == SK.ForOfStatement;

    private Node ParseTry()
    {
        var n = Begin(SK.TryStatement);
        _s.Next();
        n.Body = ParseBlock();
        if (IsK("catch"))
        {
            var c = Begin(SK.CatchClause);
            _s.Next();
            if (EatP("("))
            {
                var decl = Begin(SK.VariableDeclaration);
                decl.Name = ParseBindingName();
                if (EatP(":")) { decl.Type = ParseType(); }
                c.Initializer = Fin(decl);
                ExpectP(")");
            }

            c.Body = ParseBlock();
            n.Clause = Fin(c);
        }

        if (EatK("finally")) { n.Extra = ParseBlock(); }
        return Fin(n);
    }

    private Node ParseSwitch()
    {
        var n = Begin(SK.SwitchStatement);
        _s.Next();
        ExpectP("(");
        n.Expression = ParseExpression();
        ExpectP(")");
        var block = Begin(SK.CaseBlock);
        ExpectP("{");
        while (!IsP("}") && _s.Kind != T.Eof)
        {
            var clause = Begin(IsK("default") ? SK.DefaultClause : SK.CaseClause);
            if (EatK("case")) { clause.Expression = ParseExpression(); } else { ExpectK("default"); }
            ExpectP(":");
            while (!IsP("}") && !IsK("case") && !IsK("default") && _s.Kind != T.Eof) { ParseStatementRecovering(clause.Elements); }
            block.Elements.Add(Fin(clause));
        }

        ExpectP("}");
        n.Body = Fin(block);
        return Fin(n);
    }

    // ---------------------------------------------------------------- variables
    private Node ParseVariableStatement(int start, string? doc, Mods mods)
    {
        var n = new Node(SK.VariableStatement, start) { JsDoc = doc, Modifiers = mods };
        n.Initializer = ParseVariableDeclarationList(noIn: false);
        ConsumeSemicolon();
        return Fin(n);
    }

    private Node ParseVariableDeclarationList(bool noIn)
    {
        var list = Begin(SK.VariableDeclarationList);
        if (IsK("await")) { _s.Next(); }
        list.IsConst = IsK("const") || IsK("using");
        list.IsLet = IsK("let");
        _s.Next();
        do
        {
            var d = Begin(SK.VariableDeclaration);
            d.Name = ParseBindingName();
            if (EatP("!")) { /* definite assignment */ }
            if (EatP(":")) { d.Type = ParseType(); }
            if (EatP("=")) { d.Initializer = ParseAssignment(noIn); }
            list.Elements.Add(Fin(d));
        }
        while (EatP(","));
        return Fin(list);
    }

    private Node ParseBindingName()
    {
        if (IsP("{") || IsP("[")) { return ParseBindingPattern(); }
        return ParseIdentifier();
    }

    private Node ParseBindingPattern()
    {
        var isObject = IsP("{");
        var n = Begin(isObject ? SK.ObjectBindingPattern : SK.ArrayBindingPattern);
        _s.Next();
        var close = isObject ? "}" : "]";
        while (!IsP(close) && _s.Kind != T.Eof)
        {
            if (!isObject && IsP(",")) { var o = Begin(SK.OmittedExpression); n.Elements.Add(Fin(o)); _s.Next(); continue; }
            var el = Begin(SK.BindingElement);
            if (EatP("...")) { el.IsExtends = true; }
            if (isObject)
            {
                var first = ParsePropertyName();
                if (EatP(":")) { el.PropertyName = first; el.Name = ParseBindingName(); }
                else { el.Name = first; }
            }
            else
            {
                el.Name = ParseBindingName();
            }

            if (EatP("=")) { el.Initializer = ParseAssignment(); }
            n.Elements.Add(Fin(el));
            if (!EatP(",")) { break; }
        }

        ExpectP(close);
        return Fin(n);
    }

    // ---------------------------------------------------------------- functions
    private void ParseTypeParameters(Node owner)
    {
        if (!IsP("<")) { return; }
        _s.Next();
        while (!IsP(">") && _s.Kind != T.Eof)
        {
            var tp = Begin(SK.TypeParameter);
            while ((IsK("const") || IsK("in") || IsK("out")) && NextTokenIsModifierFollower()) { _s.Next(); }
            tp.Name = ParseIdentifier();
            if (EatK("extends")) { tp.Type = ParseType(); }
            if (EatP("=")) { tp.Initializer = ParseType(); }
            owner.TypeParameters.Add(Fin(tp));
            if (!EatP(",")) { break; }
        }

        ExpectP(">");
    }

    private Node ParseFunction(int start, Mods mods, SK kind)
    {
        var n = new Node(kind, start) { Modifiers = mods, JsDoc = _s.JsDoc };
        ExpectK("function");
        if (EatP("*")) { /* generator */ }
        if (_s.Kind == T.Ident && !IsP("(") && !IsP("<")) { n.Name = ParseIdentifier(); }
        ParseSignature(n);
        if (IsP("{")) { n.Body = ParseBlock(); } else { ConsumeSemicolon(); }
        return Fin(n);
    }

    /// <summary>Type parameters, parameters and return type.</summary>
    private void ParseSignature(Node n)
    {
        ParseTypeParameters(n);
        ParseParameters(n);
        if (EatP(":")) { n.Type = ParseReturnType(); }
    }

    private void ParseParameters(Node owner)
    {
        ExpectP("(");
        while (!IsP(")") && _s.Kind != T.Eof)
        {
            owner.Parameters.Add(ParseParameter());
            if (!EatP(",")) { break; }
        }

        ExpectP(")");
    }

    private Node ParseParameter()
    {
        var start = _s.TokenStart;
        var doc = _s.JsDoc;
        var p = new Node(SK.Parameter, start) { JsDoc = doc };
        ParseDecorators(p.Decorators);
        p.Modifiers = ParseModifiers(allowAll: false);
        if (EatP("...")) { p.IsExtends = true; }
        p.Name = IsP("{") || IsP("[") ? ParseBindingPattern() : ParseIdentifier(allowReserved: true);
        if (EatP("?")) { /* optional */ }
        if (EatP(":")) { p.Type = ParseType(); }
        if (EatP("=")) { p.Initializer = ParseAssignment(); }
        return Fin(p);
    }

    // ---------------------------------------------------------------- classes and members
    private Node ParseClass(int start, Mods mods, SK kind, List<Node>? decorators)
    {
        var n = new Node(kind, start) { Modifiers = mods, JsDoc = _s.JsDoc };
        if (decorators is not null) { n.Decorators.AddRange(decorators); }
        ExpectK("class");
        if (_s.Kind == T.Ident && !IsK("extends") && !IsK("implements")) { n.Name = ParseIdentifier(); }
        ParseTypeParameters(n);
        ParseHeritageClauses(n);
        ParseClassBody(n);
        return Fin(n);
    }

    private void ParseHeritageClauses(Node n)
    {
        while (IsK("extends") || IsK("implements"))
        {
            var h = Begin(SK.HeritageClause);
            h.IsExtends = IsK("extends");
            _s.Next();
            do
            {
                var e = Begin(SK.ExpressionWithTypeArguments);
                e.Expression = ParseLeftHandSide(noCalls: false);
                if (IsP("<") || IsP("<<")) { ParseTypeArguments(e); }
                h.Elements.Add(Fin(e));
            }
            while (EatP(","));
            n.HeritageClauses.Add(Fin(h));
        }
    }

    private void ParseClassBody(Node n)
    {
        ExpectP("{");
        while (!IsP("}") && _s.Kind != T.Eof)
        {
            if (EatP(";")) { continue; }
            n.Members.Add(ParseClassMember());
        }

        ExpectP("}");
    }

    private Node ParseClassMember()
    {
        var start = _s.TokenStart;
        var doc = _s.JsDoc;
        var decorators = new List<Node>();
        ParseDecorators(decorators);
        var mods = ParseModifiers(allowAll: false);
        if (IsK("static") is false && false) { }

        if ((mods & Mods.Static) != 0 && IsP("{"))
        {
            var sb = new Node(SK.ClassStaticBlock, start) { Modifiers = mods };
            sb.Body = ParseBlock();
            return Fin(sb);
        }

        var m = ParseMember(start, doc, mods, decorators, inClass: true);
        return m;
    }

    /// <summary>Parses a class element or an interface/type-literal member.</summary>
    private Node ParseMember(int start, string? doc, Mods mods, List<Node> decorators, bool inClass)
    {
        Node Mk(SK k)
        {
            var node = new Node(k, start) { Modifiers = mods, JsDoc = doc };
            node.Decorators.AddRange(decorators);
            return node;
        }

        // call / construct signatures (interfaces and type literals)
        if (!inClass && (IsP("(") || IsP("<")))
        {
            var cs = Mk(SK.CallSignature);
            ParseSignature(cs);
            ConsumeMemberEnd();
            return Fin(cs);
        }

        if (!inClass && IsK("new") && (PeekIsPunct("(") || PeekIsPunct("<")))
        {
            var cs = Mk(SK.ConstructSignature);
            _s.Next();
            ParseSignature(cs);
            ConsumeMemberEnd();
            return Fin(cs);
        }

        // index signature
        if (IsP("[") && LooksLikeIndexSignature())
        {
            var ix = Mk(SK.IndexSignature);
            _s.Next();
            ix.Parameters.Add(ParseParameter());
            ExpectP("]");
            if (EatP(":")) { ix.Type = ParseType(); }
            ConsumeMemberEnd();
            return Fin(ix);
        }

        // accessors
        if ((IsK("get") || IsK("set")) && NextStartsPropertyName())
        {
            var isGet = IsK("get");
            var acc = Mk(isGet ? SK.GetAccessor : SK.SetAccessor);
            _s.Next();
            acc.Name = ParsePropertyName();
            ParseSignature(acc);
            if (IsP("{")) { acc.Body = ParseBlock(); } else { ConsumeMemberEnd(); }
            return Fin(acc);
        }

        var isGenerator = EatP("*");
        if (inClass && IsK("constructor") && (PeekIsPunct("(") || PeekIsPunct("<")))
        {
            var c = Mk(SK.Constructor);
            _s.Next();
            ParseSignature(c);
            if (IsP("{")) { c.Body = ParseBlock(); } else { ConsumeMemberEnd(); }
            return Fin(c);
        }

        var name = ParsePropertyName();
        var optional = EatP("?");
        if (!optional && EatP("!")) { /* definite */ }
        if (IsP("(") || IsP("<"))
        {
            var method = Mk(inClass ? SK.MethodDeclaration : SK.MethodSignature);
            method.Name = name;
            ParseSignature(method);
            if (IsP("{")) { method.Body = ParseBlock(); } else { ConsumeMemberEnd(); }
            _ = isGenerator;
            return Fin(method);
        }

        var prop = Mk(inClass ? SK.PropertyDeclaration : SK.PropertySignature);
        prop.Name = name;
        if (EatP(":")) { prop.Type = ParseType(); }
        if (EatP("=")) { prop.Initializer = ParseAssignment(); }
        ConsumeMemberEnd();
        return Fin(prop);
    }

    private void ConsumeMemberEnd()
    {
        if (!EatP(";")) { EatP(","); }
    }

    private bool NextStartsPropertyName()
    {
        var snap = Save();
        _s.Next();
        var r = (_s.Kind is T.Ident or T.String or T.Number or T.Private) || IsP("[");
        Restore(snap);
        return r;
    }

    private bool LooksLikeIndexSignature()
    {
        var snap = Save();
        _s.Next();
        var r = _s.Kind == T.Ident;
        if (r)
        {
            _s.Next();
            r = IsP(":");
        }

        Restore(snap);
        return r;
    }

    // ---------------------------------------------------------------- interfaces, aliases, enums, modules
    private Node ParseInterface(int start, Mods mods)
    {
        var n = new Node(SK.InterfaceDeclaration, start) { Modifiers = mods, JsDoc = _s.JsDoc };
        ExpectK("interface");
        n.Name = ParseIdentifier();
        ParseTypeParameters(n);
        ParseHeritageClauses(n);
        ParseTypeMembers(n);
        return Fin(n);
    }

    private void ParseTypeMembers(Node owner)
    {
        ExpectP("{");
        while (!IsP("}") && _s.Kind != T.Eof)
        {
            var start = _s.TokenStart;
            var doc = _s.JsDoc;
            var mods = ParseModifiers(allowAll: false);
            owner.Members.Add(ParseMember(start, doc, mods, [], inClass: false));
        }

        ExpectP("}");
    }

    private Node ParseTypeAlias(int start, Mods mods)
    {
        var n = new Node(SK.TypeAliasDeclaration, start) { Modifiers = mods, JsDoc = _s.JsDoc };
        ExpectK("type");
        n.Name = ParseIdentifier();
        ParseTypeParameters(n);
        ExpectP("=");
        n.Type = ParseType();
        ConsumeSemicolon();
        return Fin(n);
    }

    private Node ParseEnum(int start, Mods mods)
    {
        var n = new Node(SK.EnumDeclaration, start) { Modifiers = mods, JsDoc = _s.JsDoc };
        if (IsK("const")) { _s.Next(); n.Modifiers |= Mods.Const; }
        ExpectK("enum");
        n.Name = ParseIdentifier();
        ExpectP("{");
        while (!IsP("}") && _s.Kind != T.Eof)
        {
            var m = Begin(SK.EnumMember);
            m.Name = ParsePropertyName();
            if (EatP("=")) { m.Initializer = ParseAssignment(); }
            n.Members.Add(Fin(m));
            if (!EatP(",")) { break; }
        }

        ExpectP("}");
        return Fin(n);
    }

    private Node ParseModule(int start, Mods mods)
    {
        var n = new Node(SK.ModuleDeclaration, start) { Modifiers = mods, JsDoc = _s.JsDoc };
        var isGlobal = IsK("global");
        _s.Next(); // namespace | module | global
        n.Name = isGlobal ? MakeIdentifier("global") : (_s.Kind == T.String ? ParsePropertyName() : ParseIdentifier());
        if (isGlobal) { n.Name.Start = _s.PrevEnd - 6; n.Name.End = _s.PrevEnd; }
        if (!isGlobal && EatP("."))
        {
            // namespace a.b.c {} => nested declarations
            var inner = new Node(SK.ModuleDeclaration, _s.TokenStart) { Modifiers = mods };
            inner.Name = ParseIdentifier();
            ParseModuleRest(inner);
            n.Body = Fin(inner);
            return Fin(n);
        }

        ParseModuleRest(n);
        return Fin(n);
    }

    private void ParseModuleRest(Node n)
    {
        if (EatP("."))
        {
            var inner = new Node(SK.ModuleDeclaration, _s.TokenStart) { Modifiers = n.Modifiers };
            inner.Name = ParseIdentifier();
            ParseModuleRest(inner);
            n.Body = Fin(inner);
            return;
        }

        if (IsP("{"))
        {
            var block = Begin(SK.ModuleBlock);
            _s.Next();
            while (!IsP("}") && _s.Kind != T.Eof) { ParseStatementRecovering(block.Elements); }
            ExpectP("}");
            n.Body = Fin(block);
        }
        else
        {
            ConsumeSemicolon();
        }
    }

    private Node MakeIdentifier(string text) => new(SK.Identifier, _s.TokenStart) { Text = text, End = _s.TokenEnd };

    // ---------------------------------------------------------------- imports / exports
    private Node ParseImport(int start, string? doc)
    {
        var n = new Node(SK.ImportDeclaration, start) { JsDoc = doc };
        _s.Next(); // import
        if (_s.Kind == T.String)
        {
            n.ModuleSpecifier = ParsePropertyName();
            SkipImportAttributes();
            ConsumeSemicolon();
            return Fin(n);
        }

        var isTypeOnly = false;
        if (IsK("type") && !PeekIsKeyword("from") && !PeekIsPunct("=") && !PeekIsPunct(",")) { isTypeOnly = true; _s.Next(); }

        var clauseStart = _s.TokenStart;
        Node? defaultName = null;
        if (_s.Kind == T.Ident)
        {
            var id = ParseIdentifier();
            if (IsP("="))
            {
                // import a = require("x") | import a = ns.b
                var eq = new Node(SK.ImportEqualsDeclaration, start) { JsDoc = doc, IsTypeOnly = isTypeOnly };
                _s.Next();
                eq.Name = id;
                if (IsK("require") && PeekIsPunct("("))
                {
                    var r = Begin(SK.ExternalModuleReference);
                    _s.Next();
                    ExpectP("(");
                    r.Expression = ParsePropertyName();
                    ExpectP(")");
                    eq.Expression = Fin(r);
                }
                else
                {
                    eq.Expression = ParseEntityName();
                }

                ConsumeSemicolon();
                return Fin(eq);
            }

            defaultName = id;
        }

        var clause = new Node(SK.ImportClause, clauseStart) { IsTypeOnly = isTypeOnly, Name = defaultName };
        if (defaultName is null || EatP(","))
        {
            if (IsP("*"))
            {
                var ns = Begin(SK.NamespaceImport);
                _s.Next();
                ExpectK("as");
                ns.Name = ParseIdentifier();
                clause.Clause = Fin(ns);
            }
            else if (IsP("{"))
            {
                var named = Begin(SK.NamedImports);
                _s.Next();
                while (!IsP("}") && _s.Kind != T.Eof)
                {
                    var spec = Begin(SK.ImportSpecifier);
                    if (IsK("type") && !PeekIsKeyword("as") && !PeekIsPunct(",") && !PeekIsPunct("}")) { _s.Next(); }
                    var first = ParseModuleExportName();
                    if (EatK("as")) { spec.PropertyName = first; spec.Name = ParseIdentifier(allowReserved: true); }
                    else { spec.Name = first; }
                    named.Elements.Add(Fin(spec));
                    if (!EatP(",")) { break; }
                }

                ExpectP("}");
                clause.Clause = Fin(named);
            }
        }

        n.Clause = Fin(clause);
        ExpectK("from");
        n.ModuleSpecifier = ParsePropertyName();
        SkipImportAttributes();
        ConsumeSemicolon();
        return Fin(n);
    }

    private Node ParseModuleExportName() => _s.Kind == T.String ? ParsePropertyName() : ParseIdentifier(allowReserved: true);

    private bool PeekIsKeyword(string k)
    {
        var snap = Save();
        _s.Next();
        var r = IsK(k);
        Restore(snap);
        return r;
    }

    private void SkipImportAttributes()
    {
        if ((IsK("with") || IsK("assert")) && PeekIsPunct("{"))
        {
            _s.Next();
            var depth = 0;
            do
            {
                if (IsP("{")) { depth++; }
                if (IsP("}")) { depth--; }
                _s.Next();
            }
            while (depth > 0 && _s.Kind != T.Eof);
        }
    }

    private Node ParseEntityName()
    {
        Node left = ParseIdentifier(allowReserved: true);
        while (IsP("."))
        {
            var q = new Node(SK.QualifiedName, left.Start) { Left = left };
            _s.Next();
            q.Right = ParseIdentifier(allowReserved: true);
            left = Fin(q);
        }

        return left;
    }

    private Node ParseExport(int start, string? doc, List<Node> decorators)
    {
        var snap = Save();
        _s.Next(); // export
        if (IsP("{") || IsP("*") || (IsK("type") && (PeekIsPunct("{") || PeekIsPunct("*"))))
        {
            var n = new Node(SK.ExportDeclaration, start) { JsDoc = doc };
            if (IsK("type")) { n.IsTypeOnly = true; _s.Next(); }
            if (EatP("*"))
            {
                if (EatK("as"))
                {
                    var ns = Begin(SK.NamespaceExport);
                    ns.Name = ParseModuleExportName();
                    n.Clause = Fin(ns);
                }
            }
            else
            {
                var named = Begin(SK.NamedExports);
                _s.Next();
                while (!IsP("}") && _s.Kind != T.Eof)
                {
                    var spec = Begin(SK.ExportSpecifier);
                    if (IsK("type") && !PeekIsKeyword("as") && !PeekIsPunct(",") && !PeekIsPunct("}")) { _s.Next(); }
                    var first = ParseModuleExportName();
                    if (EatK("as")) { spec.PropertyName = first; spec.Name = ParseModuleExportName(); }
                    else { spec.Name = first; }
                    named.Elements.Add(Fin(spec));
                    if (!EatP(",")) { break; }
                }

                ExpectP("}");
                n.Clause = Fin(named);
            }

            if (EatK("from")) { n.ModuleSpecifier = ParsePropertyName(); SkipImportAttributes(); }
            ConsumeSemicolon();
            return Fin(n);
        }

        if (IsP("="))
        {
            var n = new Node(SK.ExportAssignment, start) { JsDoc = doc };
            _s.Next();
            n.Expression = ParseAssignment();
            ConsumeSemicolon();
            return Fin(n);
        }

        if (IsK("as") && PeekIsKeyword("namespace"))
        {
            var n = new Node(SK.ExportAssignment, start);
            _s.Next(); _s.Next();
            n.Name = ParseIdentifier();
            ConsumeSemicolon();
            return Fin(n);
        }

        if (IsK("import"))
        {
            var imp = ParseImport(_s.TokenStart, doc);
            imp.Modifiers |= Mods.Export;
            imp.Start = start;
            return imp;
        }

        if (IsK("default"))
        {
            var snap2 = Save();
            _s.Next();
            var isDecl = IsK("function") || IsK("class") || IsK("interface") || IsK("abstract") || (IsK("async") && PeekIsKeyword("function"));
            if (!isDecl)
            {
                var n = new Node(SK.ExportAssignment, start) { JsDoc = doc, Modifiers = Mods.Default };
                n.Expression = ParseAssignment();
                ConsumeSemicolon();
                return Fin(n);
            }

            Restore(snap2);
        }

        Restore(snap);
        _ = decorators;
        var mods = ParseModifiers(allowAll: true);
        if (!mods.HasFlag(Mods.Export)) { mods |= Mods.Export; }
        var decl = ParseDeclarationBody(start, doc, mods, decorators);
        if (decl is null) { Fail("declaration expected after export"); }
        decl.Modifiers |= mods;
        return decl;
    }
}
