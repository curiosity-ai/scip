namespace Scip.Indexer.TypeScript.Syntax;

public sealed partial class Parser
{
    private static readonly HashSet<string> KeywordTypes =
    [
        "any", "unknown", "never", "void", "undefined", "null", "string", "number", "boolean", "bigint", "symbol", "object", "intrinsic",
    ];

    private Node ParseReturnType()
    {
        // A return type may itself be a conditional type even inside the `extends` clause of another one.
        var savedNoConditional = _noConditional;
        _noConditional = false;
        try { return ParseReturnTypeCore(); }
        finally { _noConditional = savedNoConditional; }
    }

    private Node ParseReturnTypeCore()
    {
        // type predicates: `x is T`, `asserts x`, `asserts x is T`, `this is T`
        var snap = Save();
        var start = _s.TokenStart;
        var asserts = false;
        if (IsK("asserts") && !PeekIsPunct(",") && !PeekIsPunct(")") && !PeekIsPunct("=>")) { _s.Next(); asserts = true; }
        if (_s.Kind == T.Ident && (!Reserved.Contains(_s.TokenText) || _s.TokenText == "this"))
        {
            var isThis = _s.TokenText == "this";
            var id = isThis ? Begin(SK.This) : Begin(SK.Identifier);
            id.Text = _s.TokenText;
            _s.Next();
            Fin(id);
            if (IsK("is") && !_s.NewlineBefore)
            {
                _s.Next();
                var p = new Node(SK.TypePredicate, start) { Name = id, Type = ParseType() };
                return Fin(p);
            }

            if (asserts)
            {
                return Fin(new Node(SK.TypePredicate, start) { Name = id });
            }
        }

        Restore(snap);
        return ParseType();
    }

    private Node ParseType()
    {
        var start = _s.TokenStart;
        if (IsFunctionTypeStart(out var isCtor, out var isAbstract))
        {
            var n = new Node(isCtor ? SK.ConstructorType : SK.FunctionType, start);
            if (isAbstract) { _s.Next(); }
            if (isCtor) { _s.Next(); }
            ParseTypeParameters(n);
            ParseParameters(n);
            ExpectP("=>");
            n.Type = ParseReturnType();
            return Fin(n);
        }

        var type = ParseUnionType();
        if (!_s.NewlineBefore && IsK("extends") && !_noConditional)
        {
            var c = new Node(SK.ConditionalType, start) { Left = type };
            _s.Next();
            var saved = _noConditional;
            _noConditional = true;
            c.Right = ParseType();
            _noConditional = saved;
            ExpectP("?");
            c.Then = ParseType();
            ExpectP(":");
            c.Else = ParseType();
            return Fin(c);
        }

        return type;
    }

    private bool _noConditional;

    private bool IsFunctionTypeStart(out bool isCtor, out bool isAbstract)
    {
        isCtor = false;
        isAbstract = false;
        if (IsK("abstract") && PeekIsKeyword("new")) { isCtor = true; isAbstract = true; return true; }
        if (IsK("new")) { isCtor = true; return true; }
        if (!IsP("(") && !IsP("<")) { return false; }
        var snap = Save();
        try
        {
            var scratch = new Node(SK.FunctionType, 0);
            ParseTypeParameters(scratch);
            ParseParameters(scratch);
            return IsP("=>");
        }
        catch (SyntaxException)
        {
            return false;
        }
        finally
        {
            Restore(snap);
        }
    }

    private Node ParseUnionType()
    {
        var start = _s.TokenStart;
        EatP("|");
        var first = ParseIntersectionType();
        if (!IsP("|")) { return first; }
        var u = new Node(SK.UnionType, start);
        u.Elements.Add(first);
        while (EatP("|")) { u.Elements.Add(ParseIntersectionType()); }
        return Fin(u);
    }

    private Node ParseIntersectionType()
    {
        var start = _s.TokenStart;
        EatP("&");
        var first = ParseTypeOperator();
        if (!IsP("&")) { return first; }
        var u = new Node(SK.IntersectionType, start);
        u.Elements.Add(first);
        while (EatP("&")) { u.Elements.Add(ParseTypeOperator()); }
        return Fin(u);
    }

    private Node ParseTypeOperator()
    {
        if (IsK("keyof") || IsK("unique") || (IsK("readonly") && !PeekIsPunct(",") && !PeekIsPunct(">")))
        {
            var n = Begin(SK.TypeOperator);
            n.Text = _s.TokenText;
            _s.Next();
            n.Type = ParseTypeOperator();
            return Fin(n);
        }

        if (IsK("infer"))
        {
            var n = Begin(SK.InferType);
            _s.Next();
            var tp = Begin(SK.TypeParameter);
            tp.Name = ParseIdentifier();
            if (IsK("extends"))
            {
                var snap = Save();
                _s.Next();
                var saved = _noConditional;
                _noConditional = true;
                try
                {
                    tp.Type = ParseType();
                    if (IsP("?") && !saved) { tp.Type = null; Restore(snap); }
                }
                catch (SyntaxException)
                {
                    tp.Type = null;
                    Restore(snap);
                }

                _noConditional = saved;
            }

            n.Name = Fin(tp);
            return Fin(n);
        }

        return ParsePostfixType();
    }

    private Node ParsePostfixType()
    {
        var t = ParsePrimaryType();
        while (!_s.NewlineBefore && IsP("["))
        {
            if (PeekIsPunct("]"))
            {
                var a = new Node(SK.ArrayType, t.Start) { Type = t };
                _s.Next(); _s.Next();
                t = Fin(a);
            }
            else
            {
                var ix = new Node(SK.IndexedAccessType, t.Start) { Left = t };
                _s.Next();
                ix.Right = ParseType();
                ExpectP("]");
                t = Fin(ix);
            }
        }

        return t;
    }

    private Node ParseEntityNameForType() => ParseEntityName();

    private Node ParsePrimaryType()
    {
        var start = _s.TokenStart;
        switch (_s.Kind)
        {
            case T.String or T.Number:
            {
                var lit = new Node(SK.LiteralType, start);
                lit.Expression = ParsePrimary();
                return Fin(lit);
            }

            case T.TemplateNoSub or T.TemplateHead:
            {
                var tl = new Node(SK.TemplateLiteralType, start);
                var noSub = _s.Kind == T.TemplateNoSub;
                _s.Next();
                while (!noSub)
                {
                    tl.Elements.Add(ParseType());
                    if (!IsP("}")) { Fail("'}' expected"); }
                    _s.RescanTemplateContinuation();
                    if (_s.Kind == T.TemplateTail) { _s.Next(); break; }
                    _s.Next();
                }

                return Fin(tl);
            }

            case T.Punct:
                switch (_s.TokenText)
                {
                    case "(":
                    {
                        var p = new Node(SK.ParenthesizedType, start);
                        _s.Next();
                        p.Type = ParseType();
                        ExpectP(")");
                        return Fin(p);
                    }

                    case "[": return ParseTupleType();
                    case "{": return ParseObjectType();
                    case "-":
                    {
                        var lit = new Node(SK.LiteralType, start);
                        lit.Expression = ParseUnary();
                        return Fin(lit);
                    }
                }

                break;
            case T.Ident:
                switch (_s.TokenText)
                {
                    case "this": { var n = Begin(SK.ThisType); _s.Next(); return Fin(n); }
                    case "true" or "false" or "null" when _s.TokenText != "null":
                    {
                        var lit = new Node(SK.LiteralType, start);
                        lit.Expression = ParsePrimary();
                        return Fin(lit);
                    }

                    case "typeof":
                    {
                        var q = Begin(SK.TypeQuery);
                        _s.Next();
                        if (IsK("import")) { q.Expression = ParseImportType(); }
                        else { q.Expression = ParseEntityName(); }
                        if (IsP("<") && !_s.NewlineBefore) { ParseTypeArguments(q); }
                        return Fin(q);
                    }

                    case "import": return ParseImportType();
                }

                if (KeywordTypes.Contains(_s.TokenText) && !PeekIsPunct("."))
                {
                    var k = Begin(SK.KeywordType);
                    k.Text = _s.TokenText;
                    _s.Next();
                    return Fin(k);
                }

                {
                    var tr = new Node(SK.TypeReference, start);
                    tr.Name = ParseEntityName();
                    if ((IsP("<") || IsP("<<")) && !_s.NewlineBefore) { ParseTypeArguments(tr); }
                    return Fin(tr);
                }
        }

        Fail("type expected");
        return null!;
    }

    private bool IsTemplateTail() => false;

    private Node ParseImportType()
    {
        var n = Begin(SK.ImportType);
        ExpectK("import");
        ExpectP("(");
        n.Extra = ParseType();
        if (EatP(","))
        {
            // import attributes object
            var depth = 0;
            do
            {
                if (IsP("{")) { depth++; }
                if (IsP("}")) { depth--; }
                _s.Next();
            }
            while (depth > 0 && _s.Kind != T.Eof);
        }

        ExpectP(")");
        if (EatP(".")) { n.Name = ParseEntityName(); }
        if (IsP("<")) { ParseTypeArguments(n); }
        return Fin(n);
    }

    private Node ParseTupleType()
    {
        var n = Begin(SK.TupleType);
        _s.Next();
        while (!IsP("]") && _s.Kind != T.Eof)
        {
            var start = _s.TokenStart;
            var rest = EatP("...");
            Node element;
            var snap = Save();
            if (_s.Kind == T.Ident && !Reserved.Contains(_s.TokenText) && (PeekIsPunct(":") || PeekIsOptionalColon()))
            {
                var m = new Node(SK.NamedTupleMember, start);
                m.Name = ParseIdentifier();
                EatP("?");
                ExpectP(":");
                m.Type = ParseType();
                element = Fin(m);
            }
            else
            {
                Restore(snap);
                element = ParseType();
                if (EatP("?")) { element = Fin(new Node(SK.OptionalType, start) { Type = element }); }
            }

            if (rest) { element = Fin(new Node(SK.RestType, start) { Type = element }); }
            n.Elements.Add(element);
            if (!EatP(",")) { break; }
        }

        ExpectP("]");
        return Fin(n);
    }

    private Node ParseObjectType()
    {
        if (IsMappedTypeStart())
        {
            var m = Begin(SK.MappedType);
            _s.Next(); // {
            if (IsP("+") || IsP("-")) { _s.Next(); }
            EatK("readonly");
            ExpectP("[");
            var tp = Begin(SK.TypeParameter);
            tp.Name = ParseIdentifier();
            ExpectK("in");
            tp.Type = ParseType();
            m.Clause = Fin(tp);
            if (EatK("as")) { m.Extra = ParseType(); }
            ExpectP("]");
            if (IsP("+") || IsP("-")) { _s.Next(); }
            EatP("?");
            if (EatP(":")) { m.Type = ParseType(); }
            EatP(";");
            ExpectP("}");
            return Fin(m);
        }

        var n = Begin(SK.TypeLiteral);
        ParseTypeMembers(n);
        return Fin(n);
    }

    private bool IsMappedTypeStart()
    {
        var snap = Save();
        try
        {
            _s.Next(); // {
            if (IsP("+") || IsP("-")) { _s.Next(); }
            if (IsK("readonly")) { _s.Next(); }
            if (!IsP("[")) { return false; }
            _s.Next();
            if (_s.Kind != T.Ident) { return false; }
            _s.Next();
            return IsK("in");
        }
        finally
        {
            Restore(snap);
        }
    }
}
