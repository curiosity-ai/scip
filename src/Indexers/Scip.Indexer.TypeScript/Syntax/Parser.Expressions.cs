namespace Scip.Indexer.TypeScript.Syntax;

public sealed partial class Parser
{
    private Node ParseExpression(bool noIn = false)
    {
        var left = ParseAssignment(noIn);
        while (IsP(","))
        {
            var n = new Node(SK.BinaryExpression, left.Start) { Left = left, Text = "," };
            _s.Next();
            n.Right = ParseAssignment(noIn);
            left = Fin(n);
        }

        return left;
    }

    private static readonly HashSet<string> AssignOps =
        ["=", "+=", "-=", "*=", "/=", "%=", "**=", "<<=", "&=", "|=", "^=", "&&=", "||=", "??="];

    private Node ParseAssignment(bool noIn = false)
    {
        var arrow = TryParseArrow();
        if (arrow is not null) { return arrow; }

        if (IsK("yield") && !IsP("=>"))
        {
            var y = Begin(SK.YieldExpression);
            _s.Next();
            if (EatP("*") || (!_s.NewlineBefore && !IsP(")") && !IsP("]") && !IsP("}") && !IsP(",") && !IsP(";") && !IsP(":") && _s.Kind != T.Eof))
            {
                y.Expression = ParseAssignment(noIn);
            }

            return Fin(y);
        }

        var left = ParseConditional(noIn);
        if (_s.Kind == T.Punct)
        {
            string? op = null;
            var extend = 0;
            if (AssignOps.Contains(_s.TokenText)) { op = _s.TokenText; }
            else if (IsP(">"))
            {
                var r = _s.RawAfterToken(3);
                if (r.StartsWith(">>=", StringComparison.Ordinal)) { op = ">>>="; extend = 3; }
                else if (r.StartsWith(">=", StringComparison.Ordinal)) { op = ">>="; extend = 2; }
            }

            if (op is not null)
            {
                var n = new Node(SK.BinaryExpression, left.Start) { Left = left, Text = op };
                if (extend > 0) { _s.ExtendToken(extend); }
                _s.Next();
                n.Right = ParseAssignment(noIn);
                return Fin(n);
            }
        }

        return left;
    }

    private Node? TryParseArrow()
    {
        var isAsync = false;
        var start = _s.TokenStart;
        var snap = Save();
        var doc = _s.JsDoc;
        try
        {
            if (IsK("async"))
            {
                _s.Next();
                if (_s.NewlineBefore) { Restore(snap); return null; }
                isAsync = true;
            }

            var n = new Node(SK.ArrowFunction, start) { JsDoc = doc, Modifiers = isAsync ? Mods.Async : Mods.None };
            if (_s.Kind == T.Ident && !Reserved.Contains(_s.TokenText) && PeekIsPunct("=>"))
            {
                var p = new Node(SK.Parameter, _s.TokenStart);
                p.Name = ParseIdentifier();
                n.Parameters.Add(Fin(p));
            }
            else if (IsP("(") || IsP("<"))
            {
                ParseTypeParameters(n);
                ParseParameters(n);
                if (EatP(":")) { n.Type = ParseReturnType(); }
                if (!IsP("=>")) { Restore(snap); return null; }
            }
            else
            {
                Restore(snap);
                return null;
            }

            if (!IsP("=>")) { Restore(snap); return null; }
            _s.Next();
            n.Body = IsP("{") ? ParseBlock() : ParseAssignment();
            return Fin(n);
        }
        catch (SyntaxException)
        {
            Restore(snap);
            return null;
        }
    }

    private Node ParseConditional(bool noIn)
    {
        var cond = ParseBinary(0, noIn);
        if (!IsP("?")) { return cond; }
        var n = new Node(SK.ConditionalExpression, cond.Start) { Condition = cond };
        _s.Next();
        n.Then = ParseAssignment();
        ExpectP(":");
        n.Else = ParseAssignment(noIn);
        return Fin(n);
    }

    private static int Precedence(string op) => op switch
    {
        "??" => 1, "||" => 2, "&&" => 3, "|" => 4, "^" => 5, "&" => 6,
        "==" or "!=" or "===" or "!==" => 7,
        "<" or ">" or "<=" or ">=" or "instanceof" or "in" or "as" or "satisfies" => 8,
        "<<" or ">>" or ">>>" => 9,
        "+" or "-" => 10,
        "*" or "/" or "%" => 11,
        "**" => 12,
        _ => -1,
    };

    private bool PeekBinaryOp(bool noIn, out string op, out int extend)
    {
        extend = 0;
        op = "";
        if (_s.Kind == T.Ident)
        {
            if (_s.TokenText is "instanceof") { op = "instanceof"; return true; }
            if (_s.TokenText == "in" && !noIn) { op = "in"; return true; }
            if (_s.TokenText is "as" or "satisfies" && !_s.NewlineBefore) { op = _s.TokenText; return true; }
            return false;
        }

        if (_s.Kind != T.Punct) { return false; }
        if (IsP(">"))
        {
            var r = _s.RawAfterToken(3);
            if (r.StartsWith(">>=", StringComparison.Ordinal)) { return false; } // >>>= assignment
            if (r.StartsWith(">>", StringComparison.Ordinal)) { op = ">>>"; extend = 2; return true; }
            if (r.StartsWith(">=", StringComparison.Ordinal)) { return false; } // >>= assignment
            if (r.StartsWith(">", StringComparison.Ordinal)) { op = ">>"; extend = 1; return true; }
            if (r.StartsWith("=", StringComparison.Ordinal)) { op = ">="; extend = 1; return true; }
            op = ">";
            return true;
        }

        op = _s.TokenText;
        return Precedence(op) >= 0;
    }

    private Node ParseBinary(int minPrec, bool noIn)
    {
        var left = ParseUnary();
        while (PeekBinaryOp(noIn, out var op, out var extend))
        {
            var prec = Precedence(op);
            if (prec < 0 || prec < minPrec) { break; }
            if (extend > 0) { _s.ExtendToken(extend); }
            if (op is "as" or "satisfies")
            {
                var asn = new Node(op == "as" ? SK.AsExpression : SK.SatisfiesExpression, left.Start) { Expression = left };
                _s.Next();
                if (IsK("const") && op == "as") { var k = Begin(SK.KeywordType); k.Text = "const"; _s.Next(); asn.Type = Fin(k); }
                else { asn.Type = ParseType(); }
                left = Fin(asn);
                continue;
            }

            var n = new Node(SK.BinaryExpression, left.Start) { Left = left, Text = op };
            _s.Next();
            n.Right = op == "**" ? ParseBinary(prec, noIn) : ParseBinary(prec + 1, noIn);
            left = Fin(n);
        }

        return left;
    }

    private Node ParseUnary()
    {
        if (_s.Kind == T.Punct)
        {
            if (_s.TokenText is "!" or "~" or "+" or "-" or "++" or "--")
            {
                var n = Begin(SK.PrefixUnaryExpression);
                n.Text = _s.TokenText;
                _s.Next();
                n.Expression = ParseUnary();
                return Fin(n);
            }

            if (IsP("<") && !JsxEnabled)
            {
                var ta = TryParse(() =>
                {
                    var n = Begin(SK.TypeAssertion);
                    _s.Next();
                    n.Type = ParseType();
                    ExpectP(">");
                    n.Expression = ParseUnary();
                    return Fin(n);
                });
                if (ta is not null) { return ta; }
            }
        }
        else if (_s.Kind == T.Ident)
        {
            switch (_s.TokenText)
            {
                case "typeof": return UnaryKeyword(SK.TypeOfExpression);
                case "void": return UnaryKeyword(SK.VoidExpression);
                case "delete": return UnaryKeyword(SK.DeleteExpression);
                case "await" when !PeekIsPunct(")") && !PeekIsPunct(",") && !PeekIsPunct(";") && !PeekIsPunct("=>") && !PeekIsPunct("."):
                    return UnaryKeyword(SK.AwaitExpression);
            }
        }

        var e = ParseLeftHandSide();
        while ((IsP("++") || IsP("--")) && !_s.NewlineBefore)
        {
            var p = new Node(SK.PostfixUnaryExpression, e.Start) { Expression = e, Text = _s.TokenText };
            _s.Next();
            e = Fin(p);
        }

        return e;
    }

    private Node UnaryKeyword(SK kind)
    {
        var n = Begin(kind);
        _s.Next();
        n.Expression = ParseUnary();
        return Fin(n);
    }

    private void ParseTypeArguments(Node owner)
    {
        if (IsP("<<")) { _s.SplitFirstChar(); }
        ExpectP("<");
        while (!IsP(">") && _s.Kind != T.Eof)
        {
            owner.TypeArguments.Add(ParseType());
            if (!EatP(",")) { break; }
        }

        ExpectP(">");
    }

    private void ParseArguments(Node call)
    {
        ExpectP("(");
        while (!IsP(")") && _s.Kind != T.Eof)
        {
            if (IsP("..."))
            {
                var sp = Begin(SK.SpreadElement);
                _s.Next();
                sp.Expression = ParseAssignment();
                call.Arguments.Add(Fin(sp));
            }
            else
            {
                call.Arguments.Add(ParseAssignment());
            }

            if (!EatP(",")) { break; }
        }

        ExpectP(")");
    }

    private Node ParseLeftHandSide(bool noCalls = false)
    {
        Node e;
        if (IsK("new")) { e = ParseNew(); }
        else if (IsK("super"))
        {
            e = Begin(SK.Super);
            _s.Next();
            Fin(e);
        }
        else if (IsK("import") && (PeekIsPunct("(") || PeekIsPunct(".")))
        {
            e = Begin(SK.Identifier);
            e.Text = "import";
            _s.Next();
            Fin(e);
            if (IsP("("))
            {
                var call = new Node(SK.ImportCall, e.Start);
                ParseArguments(call);
                e = Fin(call);
            }
            else
            {
                var meta = new Node(SK.MetaProperty, e.Start);
                _s.Next();
                meta.Name = ParseIdentifier(allowReserved: true);
                e = Fin(meta);
            }
        }
        else { e = ParsePrimary(); }

        while (true)
        {
            if (IsP("."))
            {
                var n = new Node(SK.PropertyAccessExpression, e.Start) { Expression = e };
                _s.Next();
                n.Name = _s.Kind == T.Private ? ParsePropertyName() : ParseIdentifier(allowReserved: true);
                e = Fin(n);
            }
            else if (IsP("?."))
            {
                _s.Next();
                if (IsP("("))
                {
                    var call = new Node(SK.CallExpression, e.Start) { Expression = e };
                    ParseArguments(call);
                    e = Fin(call);
                }
                else if (IsP("["))
                {
                    var n = new Node(SK.ElementAccessExpression, e.Start) { Expression = e };
                    _s.Next();
                    n.Right = ParseExpression();
                    ExpectP("]");
                    e = Fin(n);
                }
                else
                {
                    var n = new Node(SK.PropertyAccessExpression, e.Start) { Expression = e };
                    n.Name = _s.Kind == T.Private ? ParsePropertyName() : ParseIdentifier(allowReserved: true);
                    e = Fin(n);
                }
            }
            else if (IsP("["))
            {
                var n = new Node(SK.ElementAccessExpression, e.Start) { Expression = e };
                _s.Next();
                n.Right = ParseExpression();
                ExpectP("]");
                e = Fin(n);
            }
            else if (IsP("!") && !_s.NewlineBefore && !PeekIsPunct("="))
            {
                var n = new Node(SK.NonNullExpression, e.Start) { Expression = e };
                _s.Next();
                e = Fin(n);
            }
            else if (IsP("(") && !noCalls)
            {
                var call = new Node(SK.CallExpression, e.Start) { Expression = e };
                ParseArguments(call);
                e = Fin(call);
            }
            else if ((IsP("<") || IsP("<<")) && !noCalls)
            {
                var snap = Save();
                var call = new Node(SK.CallExpression, e.Start) { Expression = e };
                try
                {
                    ParseTypeArguments(call);
                    if (IsP("(")) { ParseArguments(call); e = Fin(call); continue; }
                    if (_s.Kind is T.TemplateNoSub or T.TemplateHead)
                    {
                        var tt = new Node(SK.TaggedTemplate, e.Start) { Expression = e };
                        tt.Right = ParseTemplate();
                        e = Fin(tt);
                        continue;
                    }

                    if (CanFollowInstantiation())
                    {
                        var inst = new Node(SK.ExpressionWithTypeArguments, e.Start) { Expression = call.Expression };
                        inst.TypeArguments.AddRange(call.TypeArguments);
                        e = Fin(inst);
                        continue;
                    }
                }
                catch (SyntaxException)
                {
                    // not type arguments
                }

                Restore(snap);
                break;
            }
            else if (_s.Kind is T.TemplateNoSub or T.TemplateHead)
            {
                var tt = new Node(SK.TaggedTemplate, e.Start) { Expression = e };
                tt.Right = ParseTemplate();
                e = Fin(tt);
            }
            else
            {
                break;
            }
        }

        return e;
    }

    // `Foo<T>;` — an instantiation expression is valid when the next token cannot start an operand.
    private bool CanFollowInstantiation() =>
        _s.Kind == T.Eof || _s.NewlineBefore
        || (_s.Kind == T.Punct && _s.TokenText is ";" or ")" or "]" or "}" or "," or ":" or "." or "?." or "=" or "==" or "===" or "!=" or "!==" or "&&" or "||" or "??" or "?" or "=>" or "|" or "&" or "^")
        || IsK("as") || IsK("satisfies") || IsK("in") || IsK("of") || IsK("instanceof");

    private Node ParseNew()
    {
        var n = Begin(SK.NewExpression);
        _s.Next(); // new
        if (IsP("."))
        {
            _s.Next();
            var meta = new Node(SK.MetaProperty, n.Start);
            meta.Name = ParseIdentifier(allowReserved: true);
            return Fin(meta);
        }

        Node callee = IsK("new") ? ParseNew() : ParsePrimary();
        while (true)
        {
            if (IsP("."))
            {
                var pa = new Node(SK.PropertyAccessExpression, callee.Start) { Expression = callee };
                _s.Next();
                pa.Name = _s.Kind == T.Private ? ParsePropertyName() : ParseIdentifier(allowReserved: true);
                callee = Fin(pa);
            }
            else if (IsP("["))
            {
                var ea = new Node(SK.ElementAccessExpression, callee.Start) { Expression = callee };
                _s.Next();
                ea.Right = ParseExpression();
                ExpectP("]");
                callee = Fin(ea);
            }
            else
            {
                break;
            }
        }

        n.Expression = callee;
        if (IsP("<") || IsP("<<"))
        {
            var snap = Save();
            try
            {
                ParseTypeArguments(n);
            }
            catch (SyntaxException)
            {
                n.TypeArguments.Clear();
                Restore(snap);
            }
        }

        if (IsP("(")) { ParseArguments(n); }
        return Fin(n);
    }

    private Node ParseTemplate()
    {
        var n = Begin(SK.TemplateExpression);
        if (_s.Kind == T.TemplateNoSub) { _s.Next(); return Fin(n); }
        _s.Next(); // head
        while (true)
        {
            n.Elements.Add(ParseExpression());
            if (!IsP("}")) { Fail("'}' expected in template"); }
            _s.RescanTemplateContinuation();
            if (_s.Kind == T.TemplateTail) { _s.Next(); break; }
            _s.Next();
        }

        return Fin(n);
    }

    private Node ParsePrimary()
    {
        switch (_s.Kind)
        {
            case T.Number: { var n = Begin(SK.NumericLiteral); n.Text = _s.TokenText; _s.Next(); return Fin(n); }
            case T.String: { var n = Begin(SK.StringLiteral); n.Text = _s.TokenText; _s.Next(); return Fin(n); }
            case T.TemplateNoSub or T.TemplateHead: return ParseTemplate();
            case T.Private: { var n = Begin(SK.PrivateIdentifier); n.Text = _s.TokenText; _s.Next(); return Fin(n); }
            case T.Punct:
                switch (_s.TokenText)
                {
                    case "(":
                    {
                        var n = Begin(SK.ParenthesizedExpression);
                        _s.Next();
                        n.Expression = ParseExpression();
                        ExpectP(")");
                        return Fin(n);
                    }

                    case "<" when JsxEnabled: return ParseJsxElementOrFragment();
                    case "[": return ParseArrayLiteral();
                    case "{": return ParseObjectLiteral();
                    case "/" or "/=":
                    {
                        _s.RescanRegex();
                        var n = Begin(SK.RegexLiteral);
                        n.Text = _s.TokenText;
                        _s.Next();
                        return Fin(n);
                    }
                }

                break;
            case T.Ident:
                switch (_s.TokenText)
                {
                    case "this": { var n = Begin(SK.This); _s.Next(); return Fin(n); }
                    case "null" or "true" or "false": { var n = Begin(SK.KeywordLiteral); n.Text = _s.TokenText; _s.Next(); return Fin(n); }
                    case "function": return ParseFunction(_s.TokenStart, Mods.None, SK.FunctionExpression);
                    case "async" when PeekIsKeyword("function"):
                    {
                        var start = _s.TokenStart;
                        _s.Next();
                        return ParseFunction(start, Mods.Async, SK.FunctionExpression);
                    }

                    case "class": return ParseClass(_s.TokenStart, Mods.None, SK.ClassExpression, null);
                    case "@": break;
                }

                if (!Reserved.Contains(_s.TokenText)) { return ParseIdentifier(); }
                break;
        }

        if (IsP("@"))
        {
            var decorators = new List<Node>();
            var start = _s.TokenStart;
            ParseDecorators(decorators);
            return ParseClass(start, Mods.None, SK.ClassExpression, decorators);
        }

        Fail("expression expected");
        return null!;
    }

    private Node ParseArrayLiteral()
    {
        var n = Begin(SK.ArrayLiteralExpression);
        _s.Next();
        while (!IsP("]") && _s.Kind != T.Eof)
        {
            if (IsP(",")) { var o = Begin(SK.OmittedExpression); n.Elements.Add(Fin(o)); _s.Next(); continue; }
            if (IsP("..."))
            {
                var sp = Begin(SK.SpreadElement);
                _s.Next();
                sp.Expression = ParseAssignment();
                n.Elements.Add(Fin(sp));
            }
            else
            {
                n.Elements.Add(ParseAssignment());
            }

            if (!EatP(",")) { break; }
        }

        ExpectP("]");
        return Fin(n);
    }

    private Node ParseObjectLiteral()
    {
        var n = Begin(SK.ObjectLiteralExpression);
        _s.Next();
        while (!IsP("}") && _s.Kind != T.Eof)
        {
            var start = _s.TokenStart;
            var doc = _s.JsDoc;
            if (IsP("..."))
            {
                var sp = Begin(SK.SpreadAssignment);
                _s.Next();
                sp.Expression = ParseAssignment();
                n.Elements.Add(Fin(sp));
            }
            else
            {
                n.Elements.Add(ParseObjectMember(start, doc));
            }

            if (!EatP(",")) { break; }
        }

        ExpectP("}");
        return Fin(n);
    }

    private Node ParseObjectMember(int start, string? doc)
    {
        var mods = Mods.None;
        if (IsK("async") && NextStartsPropertyNameOrStar()) { _s.Next(); mods |= Mods.Async; }
        if ((IsK("get") || IsK("set")) && NextStartsPropertyName())
        {
            var acc = new Node(IsK("get") ? SK.GetAccessor : SK.SetAccessor, start) { JsDoc = doc };
            _s.Next();
            acc.Name = ParsePropertyName();
            ParseSignature(acc);
            acc.Body = ParseBlock();
            return Fin(acc);
        }

        var gen = EatP("*");
        var name = ParsePropertyName();
        if (IsP("(") || IsP("<"))
        {
            var m = new Node(SK.MethodDeclaration, start) { JsDoc = doc, Modifiers = mods, Name = name };
            ParseSignature(m);
            m.Body = ParseBlock();
            _ = gen;
            return Fin(m);
        }

        if (EatP(":"))
        {
            var pa = new Node(SK.PropertyAssignment, start) { JsDoc = doc, Name = name };
            pa.Initializer = ParseAssignment();
            return Fin(pa);
        }

        var sh = new Node(SK.ShorthandPropertyAssignment, start) { JsDoc = doc, Name = name };
        if (EatP("=")) { sh.Initializer = ParseAssignment(); }
        return Fin(sh);
    }

    private bool NextStartsPropertyNameOrStar()
    {
        var snap = Save();
        _s.Next();
        var r = ((_s.Kind is T.Ident or T.String or T.Number or T.Private) || IsP("[") || IsP("*")) && !_s.NewlineBefore;
        Restore(snap);
        return r;
    }
}
