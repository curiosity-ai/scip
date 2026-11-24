using System.Text;

namespace Scip.Indexer.Python.Syntax;

public sealed class ParseException(string message, int position) : Exception(message)
{
    public int Position { get; } = position;
}

/// <summary>
/// Recursive-descent parser for Python 3.x producing pyright-shaped nodes. It recovers from syntax errors at statement
/// granularity (the offending logical line, and its indented block, is skipped).
/// </summary>
public sealed class Parser
{
    private static readonly HashSet<string> Keywords =
    [
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del", "elif",
        "else", "except", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal", "not", "or",
        "pass", "raise", "return", "try", "while", "with", "yield",
    ];

    private readonly string _text;
    private readonly List<Token> _t;
    private int _i;
    private int _prevEnd;
    private int _errors;
    private readonly List<(int, string)> _errorList = [];

    private Parser(string text, List<Token> tokens)
    {
        _text = text;
        _t = tokens;
    }

    public static ModuleNode Parse(string text)
    {
        var parser = new Parser(text, new Tokenizer(text).Tokenize());
        var module = new ModuleNode { Start = 0, End = text.Length };
        while (parser.Peek.Kind != TokenKind.End)
        {
            if (parser.Peek.Kind is TokenKind.Indent or TokenKind.Dedent or TokenKind.Newline)
            {
                parser._i++;
                continue;
            }

            parser.ParseStatementRecovering(module.Statements);
        }

        module.SyntaxErrors = parser._errors;
        module.ErrorList.AddRange(parser._errorList);
        SetParents(module);
        return module;
    }

    public static void SetParents(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var c in n.Children())
            {
                if (c is null)
                {
                    continue;
                }

                c.Parent = n;
                stack.Push(c);
            }
        }
    }

    private Token Peek => _t[_i];

    private Token PeekAt(int offset) => _t[Math.Min(_i + offset, _t.Count - 1)];

    private Token Next()
    {
        var tok = _t[_i];
        if (_i < _t.Count - 1)
        {
            _i++;
        }

        if (tok.Kind is not (TokenKind.Newline or TokenKind.Indent or TokenKind.Dedent or TokenKind.End))
        {
            _prevEnd = tok.End;
        }

        return tok;
    }

    private bool AcceptOp(string op)
    {
        if (Peek.IsOp(op))
        {
            Next();
            return true;
        }

        return false;
    }

    private bool AcceptKw(string kw)
    {
        if (Peek.IsKw(kw))
        {
            Next();
            return true;
        }

        return false;
    }

    private Token ExpectOp(string op) =>
        Peek.IsOp(op) ? Next() : throw new ParseException($"expected '{op}'", Peek.Start);

    private void ExpectKw(string kw)
    {
        if (!AcceptKw(kw))
        {
            throw new ParseException($"expected '{kw}'", Peek.Start);
        }
    }

    private T Fin<T>(T node, int start)
        where T : Node
    {
        node.Start = start;
        node.End = Math.Max(_prevEnd, start);
        return node;
    }

    // ------------------------------------------------------------------ statements

    private void ParseStatementRecovering(List<Node> into)
    {
        var startIndex = _i;
        try
        {
            ParseStatement(into);
        }
        catch (ParseException e)
        {
            _errors++;
            _errorList.Add((e.Position, e.Message));
            // Skip to the end of the logical line, and over an indented block following it.
            if (_i == startIndex)
            {
                Next();
            }

            while (Peek.Kind is not (TokenKind.Newline or TokenKind.End))
            {
                if (Peek.Kind == TokenKind.Dedent)
                {
                    return;
                }

                Next();
            }

            if (Peek.Kind == TokenKind.Newline)
            {
                Next();
            }

            if (Peek.Kind == TokenKind.Indent)
            {
                var depth = 0;
                while (Peek.Kind != TokenKind.End)
                {
                    if (Peek.Kind == TokenKind.Indent)
                    {
                        depth++;
                    }
                    else if (Peek.Kind == TokenKind.Dedent && --depth == 0)
                    {
                        Next();
                        break;
                    }

                    Next();
                }
            }
        }
    }

    private void ParseStatement(List<Node> into)
    {
        var tok = Peek;
        if (tok.Kind == TokenKind.Name)
        {
            switch (tok.Text)
            {
                case "if": into.Add(ParseIf()); return;
                case "while": into.Add(ParseWhile()); return;
                case "for": into.Add(ParseFor(tok.Start, false)); return;
                case "try": into.Add(ParseTry()); return;
                case "with": into.Add(ParseWith(tok.Start)); return;
                case "def": into.Add(ParseFunction([], tok.Start, false)); return;
                case "class": into.Add(ParseClass([], tok.Start)); return;
                case "async":
                    Next();
                    if (Peek.IsKw("def"))
                    {
                        into.Add(ParseFunction([], tok.Start, true));
                    }
                    else if (Peek.IsKw("for"))
                    {
                        into.Add(ParseFor(tok.Start, true));
                    }
                    else if (Peek.IsKw("with"))
                    {
                        into.Add(ParseWith(tok.Start));
                    }
                    else
                    {
                        throw new ParseException("bad async", Peek.Start);
                    }

                    return;
                case "match" when LooksLikeMatch():
                    into.Add(ParseMatch());
                    return;
            }
        }
        else if (tok.IsOp("@"))
        {
            into.Add(ParseDecorated());
            return;
        }

        ParseSimpleLine(into);
    }

    private void ParseSimpleLine(List<Node> into)
    {
        while (true)
        {
            var stmt = ParseSimpleStatement();
            if (stmt is not null)
            {
                into.Add(stmt);
            }

            if (AcceptOp(";"))
            {
                if (Peek.Kind is TokenKind.Newline or TokenKind.End)
                {
                    break;
                }

                continue;
            }

            break;
        }

        if (Peek.Kind == TokenKind.Newline)
        {
            Next();
        }
        else if (Peek.Kind is not (TokenKind.End or TokenKind.Dedent))
        {
            throw new ParseException("expected end of statement", Peek.Start);
        }
    }

    private Node? ParseSimpleStatement()
    {
        var tok = Peek;
        var start = tok.Start;
        if (tok.Kind == TokenKind.Name)
        {
            switch (tok.Text)
            {
                case "pass" or "break" or "continue":
                    Next();
                    return null;
                case "return":
                {
                    Next();
                    var r = new ReturnNode();
                    if (!AtStatementEnd())
                    {
                        r.Value = ParseTestListStarExpr();
                    }

                    return Fin(r, start);
                }

                case "raise":
                {
                    Next();
                    var r = new RaiseNode();
                    if (!AtStatementEnd())
                    {
                        r.Exception = ParseTest();
                        if (AcceptKw("from"))
                        {
                            r.From = ParseTest();
                        }
                    }

                    return Fin(r, start);
                }

                case "global" or "nonlocal":
                {
                    Next();
                    var g = new GlobalNode { IsNonlocal = tok.Text == "nonlocal" };
                    do
                    {
                        g.Names.Add(ParseName());
                    }
                    while (AcceptOp(","));

                    return Fin(g, start);
                }

                case "del":
                {
                    Next();
                    var d = new DelNode();
                    do
                    {
                        if (AtStatementEnd())
                        {
                            break;
                        }

                        d.Targets.Add(ParseBinary(5));
                    }
                    while (AcceptOp(","));

                    return Fin(d, start);
                }

                case "assert":
                {
                    Next();
                    var a = new AssertNode { Test = ParseTest() };
                    if (AcceptOp(","))
                    {
                        a.Message = ParseTest();
                    }

                    return Fin(a, start);
                }

                case "import":
                    return ParseImport();
                case "from":
                    return ParseFromImport();
                case "type" when PeekAt(1).Kind == TokenKind.Name && !Keywords.Contains(PeekAt(1).Text)
                    && (PeekAt(2).IsOp("=") || PeekAt(2).IsOp("[")):
                {
                    Next();
                    var name = ParseName();
                    if (Peek.IsOp("["))
                    {
                        SkipBalanced("[", "]");
                    }

                    ExpectOp("=");
                    var value = ParseTest();
                    var asg = new AssignmentNode { Value = value };
                    asg.Targets.Add(name);
                    return Fin(asg, start);
                }
            }
        }

        return ParseExpressionStatement();
    }

    private bool AtStatementEnd() => Peek.Kind is TokenKind.Newline or TokenKind.End or TokenKind.Dedent || Peek.IsOp(";");

    private void SkipBalanced(string open, string close)
    {
        var depth = 0;
        do
        {
            if (Peek.IsOp(open))
            {
                depth++;
            }
            else if (Peek.IsOp(close))
            {
                depth--;
            }
            else if (Peek.Kind == TokenKind.End)
            {
                throw new ParseException("unbalanced", Peek.Start);
            }

            Next();
        }
        while (depth > 0);
    }

    private Node ParseExpressionStatement()
    {
        var start = Peek.Start;
        var first = ParseTestListStarExpr();

        if (Peek.IsOp(":") && first.Kind is NodeKind.Name or NodeKind.MemberAccess or NodeKind.Index)
        {
            Next();
            var ann = new TypeAnnotationNode { ValueExpression = first, Annotation = ParseTest() };
            Fin(ann, start);
            if (AcceptOp("="))
            {
                var asg = new AssignmentNode();
                asg.Targets.Add(ann);
                asg.Value = Peek.IsKw("yield") ? ParseYield() : ParseTestListStarExpr();
                return Fin(asg, start);
            }

            return ann;
        }

        if (Peek.Kind == TokenKind.Op && Peek.Text.Length >= 2 && Peek.Text[^1] == '='
            && Peek.Text is not ("==" or "<=" or ">=" or "!=" or ":="))
        {
            Next();
            var aug = new AugAssignmentNode { Target = first };
            aug.Value = Peek.IsKw("yield") ? ParseYield() : ParseTestListStarExpr();
            return Fin(aug, start);
        }

        if (Peek.IsOp("="))
        {
            var asg = new AssignmentNode();
            var current = first;
            while (AcceptOp("="))
            {
                asg.Targets.Add(current);
                current = Peek.IsKw("yield") ? ParseYield() : ParseTestListStarExpr();
            }

            asg.Value = current;
            return Fin(asg, start);
        }

        return first;
    }

    private ImportNode ParseImport()
    {
        var start = Next().Start;
        var node = new ImportNode();
        do
        {
            var asStart = Peek.Start;
            var mod = ParseModuleName(false);
            var imp = new ImportAsNode { Module = mod };
            if (AcceptKw("as"))
            {
                imp.Alias = ParseName();
            }

            node.List.Add(Fin(imp, asStart));
        }
        while (AcceptOp(","));

        return Fin(node, start);
    }

    private ImportFromNode ParseFromImport()
    {
        var start = Next().Start;
        var node = new ImportFromNode { Module = ParseModuleName(true) };
        ExpectKw("import");
        if (Peek.IsOp("*"))
        {
            Next();
            node.IsWildcard = true;
            return Fin(node, start);
        }

        var paren = AcceptOp("(");
        do
        {
            if (paren && Peek.IsOp(")"))
            {
                break;
            }

            var s = Peek.Start;
            var imp = new ImportFromAsNode { Name = ParseName() };
            if (AcceptKw("as"))
            {
                imp.Alias = ParseName();
            }

            node.Imports.Add(Fin(imp, s));
        }
        while (AcceptOp(","));

        if (paren)
        {
            ExpectOp(")");
        }

        return Fin(node, start);
    }

    private ModuleNameNode ParseModuleName(bool allowRelative)
    {
        var start = Peek.Start;
        var node = new ModuleNameNode();
        if (allowRelative)
        {
            while (Peek.IsOp(".") || Peek.IsOp("..."))
            {
                node.LeadingDots += Next().Text.Length;
            }
        }

        if (Peek.Kind == TokenKind.Name && !Peek.IsKw("import"))
        {
            node.NameParts.Add(ParseName());
            while (Peek.IsOp(".") && PeekAt(1).Kind == TokenKind.Name)
            {
                Next();
                node.NameParts.Add(ParseName());
            }
        }
        else if (node.LeadingDots == 0)
        {
            throw new ParseException("expected module name", Peek.Start);
        }

        if (node.LeadingDots > 0 && node.NameParts.Count == 0)
        {
            // `from . import x`: the module node covers just the dots
            node.Start = start;
            node.End = _prevEnd;
            return node;
        }

        return Fin(node, start);
    }

    private NameNode ParseName()
    {
        var tok = Peek;
        if (tok.Kind != TokenKind.Name || Keywords.Contains(tok.Text))
        {
            throw new ParseException("expected name", tok.Start);
        }

        Next();
        return new NameNode { Value = tok.Text, Start = tok.Start, End = tok.End };
    }

    private SuiteNode ParseSuite()
    {
        ExpectOp(":");
        var suite = new SuiteNode { Start = Peek.Start };
        if (Peek.Kind == TokenKind.Newline)
        {
            Next();
            if (Peek.Kind != TokenKind.Indent)
            {
                throw new ParseException("expected indented block", Peek.Start);
            }

            Next();
            while (Peek.Kind is not (TokenKind.Dedent or TokenKind.End))
            {
                if (Peek.Kind == TokenKind.Newline)
                {
                    Next();
                    continue;
                }

                ParseStatementRecovering(suite.Statements);
            }

            if (Peek.Kind == TokenKind.Dedent)
            {
                Next();
            }
        }
        else
        {
            ParseSimpleLine(suite.Statements);
        }

        suite.Start = suite.Statements.Count > 0 ? suite.Statements[0].Start : suite.Start;
        suite.End = suite.Statements.Count > 0 ? suite.Statements[^1].End : _prevEnd;
        return suite;
    }

    private IfNode ParseIf()
    {
        var start = Next().Start; // `if` or `elif`
        var node = new IfNode { Test = ParseNamedExpr() };
        node.IfSuite = ParseSuite();
        if (Peek.IsKw("elif"))
        {
            node.ElseSuite = ParseIf();
        }
        else if (Peek.IsKw("else"))
        {
            Next();
            node.ElseSuite = ParseSuite();
        }

        return Fin(node, start);
    }

    private WhileNode ParseWhile()
    {
        var start = Next().Start;
        var node = new WhileNode { Test = ParseNamedExpr() };
        node.Suite = ParseSuite();
        if (AcceptKw("else"))
        {
            node.ElseSuite = ParseSuite();
        }

        return Fin(node, start);
    }

    private ForNode ParseFor(int start, bool isAsync)
    {
        _ = isAsync;
        ExpectKw("for");
        var node = new ForNode { Target = ParseTargetList() };
        ExpectKw("in");
        node.Iterable = ParseTestListStarExpr();
        node.Suite = ParseSuite();
        if (AcceptKw("else"))
        {
            node.ElseSuite = ParseSuite();
        }

        return Fin(node, start);
    }

    private Node ParseTargetList()
    {
        var start = Peek.Start;
        var first = ParseTarget();
        if (!Peek.IsOp(","))
        {
            return first;
        }

        var tuple = new SequenceNode(NodeKind.Tuple);
        tuple.Items.Add(first);
        while (AcceptOp(","))
        {
            if (Peek.IsKw("in") || Peek.IsOp("=") || AtStatementEnd())
            {
                break;
            }

            tuple.Items.Add(ParseTarget());
        }

        return Fin(tuple, start);
    }

    private Node ParseTarget()
    {
        if (Peek.IsOp("*"))
        {
            var s = Next().Start;
            var u = new WrapperNode(NodeKind.Unpack) { Expression = ParseBinary(5) };
            return Fin(u, s);
        }

        return ParseBinary(5);
    }

    private TryNode ParseTry()
    {
        var start = Next().Start;
        var node = new TryNode { TrySuite = ParseSuite() };
        while (Peek.IsKw("except"))
        {
            var s = Next().Start;
            AcceptOp("*");
            var ex = new ExceptNode();
            if (!Peek.IsOp(":"))
            {
                ex.TypeExpression = ParseTestListStarExpr();
                if (AcceptKw("as"))
                {
                    ex.Name = ParseName();
                }
            }

            ex.Suite = ParseSuite();
            node.Excepts.Add(Fin(ex, s));
        }

        if (AcceptKw("else"))
        {
            node.ElseSuite = ParseSuite();
        }

        if (AcceptKw("finally"))
        {
            node.FinallySuite = ParseSuite();
        }

        return Fin(node, start);
    }

    private WithNode ParseWith(int start)
    {
        ExpectKw("with");
        var node = new WithNode();
        if (Peek.IsOp("("))
        {
            var save = (_i, _prevEnd);
            try
            {
                Next();
                var items = new List<WithItemNode>();
                do
                {
                    if (Peek.IsOp(")"))
                    {
                        break;
                    }

                    items.Add(ParseWithItem());
                }
                while (AcceptOp(","));

                ExpectOp(")");
                if (!Peek.IsOp(":"))
                {
                    throw new ParseException("not a parenthesized with", Peek.Start);
                }

                node.Items.AddRange(items);
            }
            catch (ParseException)
            {
                (_i, _prevEnd) = save;
                node.Items.Clear();
            }
        }

        if (node.Items.Count == 0)
        {
            do
            {
                node.Items.Add(ParseWithItem());
            }
            while (AcceptOp(","));
        }

        node.Suite = ParseSuite();
        return Fin(node, start);
    }

    private WithItemNode ParseWithItem()
    {
        var s = Peek.Start;
        var item = new WithItemNode { Expression = ParseTest() };
        if (AcceptKw("as"))
        {
            item.Target = ParseTarget();
        }

        return Fin(item, s);
    }

    private Node ParseDecorated()
    {
        var decorators = new List<DecoratorNode>();
        var start = Peek.Start;
        while (Peek.IsOp("@"))
        {
            var s = Next().Start;
            var d = new DecoratorNode { Expression = ParseNamedExpr() };
            decorators.Add(Fin(d, s));
            if (Peek.Kind == TokenKind.Newline)
            {
                Next();
            }
        }

        if (Peek.IsKw("def"))
        {
            return ParseFunction(decorators, start, false);
        }

        if (Peek.IsKw("async") && PeekAt(1).IsKw("def"))
        {
            Next();
            return ParseFunction(decorators, start, true);
        }

        if (Peek.IsKw("class"))
        {
            return ParseClass(decorators, start);
        }

        throw new ParseException("expected def or class after decorator", Peek.Start);
    }

    private FunctionNode ParseFunction(List<DecoratorNode> decorators, int start, bool isAsync)
    {
        ExpectKw("def");
        var node = new FunctionNode { IsAsync = isAsync, Name = ParseName() };
        node.Decorators.AddRange(decorators);
        if (Peek.IsOp("["))
        {
            SkipBalanced("[", "]"); // PEP 695 type parameters are not modelled
        }

        ExpectOp("(");
        ParseParameters(node.Parameters, ")", true);
        ExpectOp(")");
        if (AcceptOp("->"))
        {
            node.ReturnAnnotation = ParseTest();
        }

        node.Suite = ParseSuite();
        return Fin(node, start);
    }

    private ClassNode ParseClass(List<DecoratorNode> decorators, int start)
    {
        ExpectKw("class");
        var node = new ClassNode { Name = ParseName() };
        node.Decorators.AddRange(decorators);
        if (Peek.IsOp("["))
        {
            SkipBalanced("[", "]");
        }

        if (AcceptOp("("))
        {
            ParseArguments(node.Arguments);
            ExpectOp(")");
        }

        node.Suite = ParseSuite();
        return Fin(node, start);
    }

    private void ParseParameters(List<ParameterNode> into, string terminator, bool allowAnnotations)
    {
        while (!Peek.IsOp(terminator) && Peek.Kind != TokenKind.End)
        {
            var start = Peek.Start;
            var p = new ParameterNode();
            if (AcceptOp("*"))
            {
                p.Category = ParamCategory.ArgsList;
            }
            else if (AcceptOp("**"))
            {
                p.Category = ParamCategory.KwargsDict;
            }
            else if (Peek.IsOp("/"))
            {
                Next();
                into.Add(Fin(p, start));
                if (!AcceptOp(","))
                {
                    break;
                }

                continue;
            }

            if (Peek.Kind == TokenKind.Name && !Keywords.Contains(Peek.Text))
            {
                p.Name = ParseName();
                if (allowAnnotations && AcceptOp(":"))
                {
                    p.Annotation = p.Category == ParamCategory.ArgsList && Peek.IsOp("*")
                        ? ParseTarget()
                        : ParseTest();
                }

                if (AcceptOp("="))
                {
                    p.Default = ParseTest();
                }
            }

            into.Add(Fin(p, start));
            if (!AcceptOp(","))
            {
                break;
            }
        }
    }

    private bool LooksLikeMatch()
    {
        var next = PeekAt(1);
        if (next.Kind == TokenKind.Op && next.Text is "=" or "." or "," or ")" or ":" or ":=" or "+=" or "-=" or "[" && next.Text != "[")
        {
            return false;
        }

        var j = _i + 1;
        while (j < _t.Count && _t[j].Kind is not (TokenKind.Newline or TokenKind.End))
        {
            j++;
        }

        return j > _i + 1 && _t[j - 1].IsOp(":") && next.Kind != TokenKind.Newline
               && PeekAt(2).Kind != TokenKind.End && _t[Math.Min(j + 1, _t.Count - 1)].Kind == TokenKind.Indent;
    }

    private MatchNode ParseMatch()
    {
        var start = Next().Start;
        var node = new MatchNode { Subject = ParseTestListStarExpr() };
        ExpectOp(":");
        if (Peek.Kind == TokenKind.Newline)
        {
            Next();
        }

        if (Peek.Kind != TokenKind.Indent)
        {
            throw new ParseException("expected indent", Peek.Start);
        }

        Next();
        while (Peek.IsKw("case"))
        {
            var cs = Next().Start;
            var c = new CaseNode { Pattern = ParsePattern() };
            if (AcceptKw("as"))
            {
                c.AsName = ParseName();
            }

            if (AcceptKw("if"))
            {
                c.Guard = ParseNamedExpr();
            }

            c.Suite = ParseSuite();
            node.Cases.Add(Fin(c, cs));
        }

        if (Peek.Kind == TokenKind.Dedent)
        {
            Next();
        }

        return Fin(node, start);
    }

    private Node ParsePattern()
    {
        var start = Peek.Start;
        Node One()
        {
            if (Peek.IsOp("*"))
            {
                var s = Next().Start;
                return Fin(new WrapperNode(NodeKind.Unpack) { Expression = ParseBinary(5) }, s);
            }

            return ParseBinary(1);
        }

        var first = One();
        if (!Peek.IsOp(","))
        {
            return first;
        }

        var tuple = new SequenceNode(NodeKind.Tuple);
        tuple.Items.Add(first);
        while (AcceptOp(","))
        {
            if (Peek.IsOp(":") || Peek.IsKw("if") || Peek.IsKw("as"))
            {
                break;
            }

            tuple.Items.Add(One());
        }

        return Fin(tuple, start);
    }

    // ------------------------------------------------------------------ expressions

    private Node ParseTestListStarExpr()
    {
        var start = Peek.Start;
        var first = ParseStarOrNamed();
        if (!Peek.IsOp(","))
        {
            return first;
        }

        var tuple = new SequenceNode(NodeKind.Tuple);
        tuple.Items.Add(first);
        while (AcceptOp(","))
        {
            if (AtExpressionEnd())
            {
                break;
            }

            tuple.Items.Add(ParseStarOrNamed());
        }

        return Fin(tuple, start);
    }

    private bool AtExpressionEnd() =>
        Peek.Kind is TokenKind.Newline or TokenKind.End or TokenKind.Dedent
        || Peek.Kind == TokenKind.Op && Peek.Text is ")" or "]" or "}" or "=" or ":" or ";" or "+=" or "-=" or "*=" or "/="
        || Peek.IsKw("in");

    private Node ParseStarOrNamed()
    {
        if (Peek.IsOp("*"))
        {
            var s = Next().Start;
            return Fin(new WrapperNode(NodeKind.Unpack) { Expression = ParseBinary(5) }, s);
        }

        return ParseNamedExpr();
    }

    private Node ParseNamedExpr()
    {
        if (Peek.Kind == TokenKind.Name && !Keywords.Contains(Peek.Text) && PeekAt(1).IsOp(":="))
        {
            var start = Peek.Start;
            var name = ParseName();
            Next();
            var node = new AssignmentExpressionNode { Name = name, Right = ParseTest() };
            return Fin(node, start);
        }

        return ParseTest();
    }

    private Node ParseYield()
    {
        var start = Next().Start;
        if (AcceptKw("from"))
        {
            return Fin(new WrapperNode(NodeKind.YieldFrom) { Expression = ParseTest() }, start);
        }

        var node = new WrapperNode(NodeKind.Yield);
        if (!AtExpressionEnd())
        {
            node.Expression = ParseTestListStarExpr();
        }

        return Fin(node, start);
    }

    private Node ParseTest()
    {
        if (Peek.IsKw("lambda"))
        {
            return ParseLambda();
        }

        var start = Peek.Start;
        var expr = ParseBinary(1);
        if (Peek.IsKw("if"))
        {
            Next();
            var test = ParseBinary(1);
            ExpectKw("else");
            var els = ParseTest();
            return Fin(new TernaryNode { IfExpression = expr, Test = test, ElseExpression = els }, start);
        }

        return expr;
    }

    private Node ParseLambda()
    {
        var start = Next().Start;
        var node = new LambdaNode();
        ParseParameters(node.Parameters, ":", false);
        ExpectOp(":");
        node.Body = ParseTest();
        return Fin(node, start);
    }

    private static int Precedence(Token t)
    {
        if (t.Kind == TokenKind.Name)
        {
            return t.Text switch
            {
                "or" => 1,
                "and" => 2,
                "in" or "is" => 4,
                "not" => 4, // only valid as `not in`; verified by the caller
                _ => 0,
            };
        }

        if (t.Kind != TokenKind.Op)
        {
            return 0;
        }

        return t.Text switch
        {
            "<" or ">" or "==" or ">=" or "<=" or "!=" or "<>" => 4,
            "|" => 5,
            "^" => 6,
            "&" => 7,
            "<<" or ">>" => 8,
            "+" or "-" => 9,
            "*" or "/" or "//" or "%" or "@" => 10,
            _ => 0,
        };
    }

    /// <summary>Precedence climbing. Levels: or=1, and=2, comparison=4, |=5, ^=6, &amp;=7, shift=8, arith=9, term=10.</summary>
    private Node ParseBinary(int minPrec)
    {
        var start = Peek.Start;
        Node left;
        if (minPrec <= 3 && Peek.IsKw("not"))
        {
            Next();
            left = Fin(new UnaryNode { Operand = ParseBinary(3) }, start);
        }
        else
        {
            left = ParseFactor();
        }

        while (true)
        {
            var tok = Peek;
            var prec = Precedence(tok);
            if (prec == 0 || prec < minPrec)
            {
                break;
            }

            string op = tok.Text;
            if (tok.IsKw("not"))
            {
                if (!PeekAt(1).IsKw("in"))
                {
                    break;
                }

                Next();
                op = "not in";
            }
            else if (tok.IsKw("is") && PeekAt(1).IsKw("not"))
            {
                Next();
                op = "is not";
            }

            Next();
            var right = ParseBinary(prec + 1);
            left = Fin(new BinaryNode { Left = left, Right = right, Operator = op }, start);
        }

        return left;
    }

    private Node ParseFactor()
    {
        var tok = Peek;
        if (tok.Kind == TokenKind.Op && tok.Text is "+" or "-" or "~")
        {
            Next();
            return Fin(new UnaryNode { Operand = ParseFactor() }, tok.Start);
        }

        return ParsePower();
    }

    private Node ParsePower()
    {
        var start = Peek.Start;
        Node left;
        if (Peek.IsKw("await"))
        {
            Next();
            left = Fin(new WrapperNode(NodeKind.Await) { Expression = ParsePrimary() }, start);
        }
        else
        {
            left = ParsePrimary();
        }

        if (AcceptOp("**"))
        {
            var right = ParseFactor();
            return Fin(new BinaryNode { Left = left, Right = right, Operator = "**" }, start);
        }

        return left;
    }

    private Node ParsePrimary()
    {
        var start = Peek.Start;
        var node = ParseAtom();
        while (true)
        {
            if (Peek.IsOp("("))
            {
                Next();
                var call = new CallNode { Left = node };
                ParseArguments(call.Arguments);
                ExpectOp(")");
                node = Fin(call, start);
            }
            else if (Peek.IsOp("["))
            {
                Next();
                var index = new IndexNode { Left = node };
                ParseSubscripts(index.Items);
                ExpectOp("]");
                node = Fin(index, start);
            }
            else if (Peek.IsOp(".") && PeekAt(1).Kind == TokenKind.Name)
            {
                Next();
                var ma = new MemberAccessNode { Left = node };
                var nameTok = Next();
                ma.Member = new NameNode { Value = nameTok.Text, Start = nameTok.Start, End = nameTok.End };
                node = Fin(ma, start);
            }
            else
            {
                return node;
            }
        }
    }

    private void ParseArguments(List<ArgumentNode> into)
    {
        while (!Peek.IsOp(")") && Peek.Kind != TokenKind.End)
        {
            var start = Peek.Start;
            var arg = new ArgumentNode();
            if (AcceptOp("*"))
            {
                arg.Category = 1;
                arg.Value = ParseTest();
            }
            else if (AcceptOp("**"))
            {
                arg.Category = 2;
                arg.Value = ParseTest();
            }
            else if (Peek.Kind == TokenKind.Name && !Keywords.Contains(Peek.Text) && PeekAt(1).IsOp("="))
            {
                arg.Name = ParseName();
                Next();
                arg.Value = ParseTest();
            }
            else
            {
                var v = ParseNamedExpr();
                arg.Value = Peek.IsKw("for") || Peek.IsKw("async") ? ParseComprehensionRest(v, start) : v;
            }

            into.Add(Fin(arg, start));
            if (!AcceptOp(","))
            {
                break;
            }
        }
    }

    private void ParseSubscripts(List<ArgumentNode> into)
    {
        while (!Peek.IsOp("]") && Peek.Kind != TokenKind.End)
        {
            var start = Peek.Start;
            var arg = new ArgumentNode();
            if (Peek.IsOp("*"))
            {
                var s = Next().Start;
                arg.Value = Fin(new WrapperNode(NodeKind.Unpack) { Expression = ParseBinary(5) }, s);
            }
            else
            {
                arg.Value = ParseSliceOrExpr();
            }

            into.Add(Fin(arg, start));
            if (!AcceptOp(","))
            {
                break;
            }
        }
    }

    private Node ParseSliceOrExpr()
    {
        var start = Peek.Start;
        Node? first = null;
        if (!Peek.IsOp(":"))
        {
            first = ParseNamedExpr();
            if (!Peek.IsOp(":"))
            {
                return first;
            }
        }

        var slice = new SliceNode { StartValue = first };
        ExpectOp(":");
        if (!Peek.IsOp(":") && !Peek.IsOp("]") && !Peek.IsOp(","))
        {
            slice.EndValue = ParseTest();
        }

        if (AcceptOp(":") && !Peek.IsOp("]") && !Peek.IsOp(","))
        {
            slice.Step = ParseTest();
        }

        return Fin(slice, start);
    }

    private Node ParseAtom()
    {
        var tok = Peek;
        switch (tok.Kind)
        {
            case TokenKind.Name:
                if (tok.Text is "None" or "True" or "False")
                {
                    Next();
                    return new ConstantNode { Text = tok.Text, Start = tok.Start, End = tok.End };
                }

                if (tok.Text == "yield")
                {
                    return ParseYield();
                }

                if (tok.Text == "lambda")
                {
                    return ParseLambda();
                }

                if (tok.Text == "__debug__")
                {
                    Next();
                    return new ConstantNode { Text = tok.Text, Start = tok.Start, End = tok.End };
                }

                return ParseName();
            case TokenKind.Number:
                Next();
                return new NumberNode { Start = tok.Start, End = tok.End };
            case TokenKind.String:
                return ParseStrings();
            case TokenKind.Op:
                switch (tok.Text)
                {
                    case "...":
                        Next();
                        return new ConstantNode { Text = "...", Start = tok.Start, End = tok.End };
                    case "(":
                        return ParseParenthesized();
                    case "[":
                        return ParseListDisplay();
                    case "{":
                        return ParseBraceDisplay();
                }

                break;
        }

        throw new ParseException($"unexpected token '{tok.Text}'", tok.Start);
    }

    private Node ParseParenthesized()
    {
        var start = Next().Start;
        if (AcceptOp(")"))
        {
            return Fin(new SequenceNode(NodeKind.Tuple) { Parenthesized = true }, start);
        }

        if (Peek.IsKw("yield"))
        {
            var y = ParseYield();
            ExpectOp(")");
            return y;
        }

        var first = ParseStarOrNamed();
        if (Peek.IsKw("for") || Peek.IsKw("async"))
        {
            var comp = ParseComprehensionRest(first, start);
            ExpectOp(")");
            comp.Start = start;
            comp.End = _prevEnd;
            return comp;
        }

        if (Peek.IsOp(","))
        {
            var tuple = new SequenceNode(NodeKind.Tuple) { Parenthesized = true };
            tuple.Items.Add(first);
            while (AcceptOp(","))
            {
                if (Peek.IsOp(")"))
                {
                    break;
                }

                tuple.Items.Add(ParseStarOrNamed());
            }

            ExpectOp(")");
            return Fin(tuple, start);
        }

        ExpectOp(")");
        // Pyright does not keep parenthesised-expression nodes, but widens none either; keep inner node.
        return first;
    }

    private Node ParseListDisplay()
    {
        var start = Next().Start;
        if (AcceptOp("]"))
        {
            return Fin(new SequenceNode(NodeKind.List), start);
        }

        var first = ParseStarOrNamed();
        if (Peek.IsKw("for") || Peek.IsKw("async"))
        {
            var comp = ParseComprehensionRest(first, start);
            ExpectOp("]");
            var list = new SequenceNode(NodeKind.List);
            list.Items.Add(comp);
            return Fin(list, start);
        }

        var node = new SequenceNode(NodeKind.List);
        node.Items.Add(first);
        while (AcceptOp(","))
        {
            if (Peek.IsOp("]"))
            {
                break;
            }

            node.Items.Add(ParseStarOrNamed());
        }

        ExpectOp("]");
        return Fin(node, start);
    }

    private Node ParseBraceDisplay()
    {
        var start = Next().Start;
        if (AcceptOp("}"))
        {
            return Fin(new DictionaryNode(), start);
        }

        if (Peek.IsOp("**"))
        {
            return ParseDictRest(start, null);
        }

        var first = ParseStarOrNamed();
        if (Peek.IsOp(":"))
        {
            return ParseDictRest(start, first);
        }

        if (Peek.IsKw("for") || Peek.IsKw("async"))
        {
            var comp = ParseComprehensionRest(first, start);
            ExpectOp("}");
            var set = new SequenceNode(NodeKind.Set);
            set.Items.Add(comp);
            return Fin(set, start);
        }

        var node = new SequenceNode(NodeKind.Set);
        node.Items.Add(first);
        while (AcceptOp(","))
        {
            if (Peek.IsOp("}"))
            {
                break;
            }

            node.Items.Add(ParseStarOrNamed());
        }

        ExpectOp("}");
        return Fin(node, start);
    }

    private Node ParseDictRest(int start, Node? firstKey)
    {
        var dict = new DictionaryNode();
        var entryStart = firstKey?.Start ?? Peek.Start;
        Node entry;
        if (firstKey is null)
        {
            Next(); // **
            entry = Fin(new DictExpandEntryNode { Expression = ParseBinary(5) }, entryStart);
        }
        else
        {
            ExpectOp(":");
            entry = Fin(new DictKeyEntryNode { Key = firstKey, Value = ParseTest() }, entryStart);
        }

        if (Peek.IsKw("for") || Peek.IsKw("async"))
        {
            var comp = ParseComprehensionRest(entry, entryStart);
            ExpectOp("}");
            dict.Entries.Add(comp);
            return Fin(dict, start);
        }

        dict.Entries.Add(entry);
        while (AcceptOp(","))
        {
            if (Peek.IsOp("}"))
            {
                break;
            }

            var s = Peek.Start;
            if (AcceptOp("**"))
            {
                dict.Entries.Add(Fin(new DictExpandEntryNode { Expression = ParseBinary(5) }, s));
            }
            else
            {
                var key = ParseTest();
                ExpectOp(":");
                dict.Entries.Add(Fin(new DictKeyEntryNode { Key = key, Value = ParseTest() }, s));
            }
        }

        ExpectOp("}");
        return Fin(dict, start);
    }

    private ComprehensionNode ParseComprehensionRest(Node expression, int start)
    {
        var comp = new ComprehensionNode { Expression = expression };
        while (true)
        {
            var s = Peek.Start;
            if (Peek.IsKw("async") && PeekAt(1).IsKw("for"))
            {
                Next();
            }

            if (Peek.IsKw("for"))
            {
                Next();
                var f = new ComprehensionForNode { Target = ParseTargetList() };
                ExpectKw("in");
                f.Iterable = ParseBinary(1); // or_test: no ternary, so a following `if` is a filter
                comp.Clauses.Add(Fin(f, s));
            }
            else if (Peek.IsKw("if"))
            {
                Next();
                var i = new ComprehensionIfNode { Test = ParseCondition() };
                comp.Clauses.Add(Fin(i, s));
            }
            else
            {
                break;
            }
        }

        return Fin(comp, start);
    }

    private Node ParseCondition()
    {
        if (Peek.Kind == TokenKind.Name && !Keywords.Contains(Peek.Text) && PeekAt(1).IsOp(":="))
        {
            return ParseNamedExpr();
        }

        return ParseBinary(1);
    }

    // ------------------------------------------------------------------ strings

    private Node ParseStrings()
    {
        var list = new StringListNode { Start = Peek.Start };
        while (Peek.Kind == TokenKind.String)
        {
            list.Strings.Add(MakeString(Next()));
        }

        list.End = _prevEnd;
        return list;
    }

    private StringNode MakeString(Token tok)
    {
        var text = tok.Text;
        var p = 0;
        while (text[p] is not ('"' or '\''))
        {
            p++;
        }

        var prefix = text[..p].ToLowerInvariant();
        var isRaw = prefix.Contains('r');
        var isF = prefix.Contains('f') || prefix.Contains('t');
        var q = text[p];
        var triple = text.Length >= p + 6 && text[p + 1] == q && text[p + 2] == q;
        var qlen = triple ? 3 : 1;
        var contentStart = p + qlen;
        var contentEnd = text.Length >= contentStart + qlen && text.AsSpan(text.Length - qlen).SequenceEqual(new string(q, qlen)) && text.Length - qlen >= contentStart
            ? text.Length - qlen
            : text.Length;
        var node = new StringNode { Start = tok.Start, End = tok.End, IsFormat = isF };
        var content = text[contentStart..contentEnd];
        node.Value = isRaw || !content.Contains('\\') ? content : Unescape(content);
        if (isF)
        {
            ParseFormatFields(node, tok.Start + contentStart, tok.Start + contentEnd);
        }

        return node;
    }

    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length)
            {
                sb.Append(s[i]);
                continue;
            }

            i++;
            switch (s[i])
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case '\\': sb.Append('\\'); break;
                case '\'': sb.Append('\''); break;
                case '"': sb.Append('"'); break;
                case '\n': break;
                default: sb.Append('\\').Append(s[i]); break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Finds the replacement fields of an f-string in <c>[start, end)</c> and parses each expression.</summary>
    private void ParseFormatFields(StringNode node, int start, int end)
    {
        var i = start;
        while (i < end)
        {
            var c = _text[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '{')
            {
                if (i + 1 < end && _text[i + 1] == '{')
                {
                    i += 2;
                    continue;
                }

                i = ParseOneField(node, i + 1, end);
                continue;
            }

            i++;
        }
    }

    /// <summary>Parses a replacement field whose expression begins at <paramref name="i"/>; returns the index after its <c>}</c>.</summary>
    private int ParseOneField(StringNode node, int i, int end)
    {
        var exprStart = i;
        var depth = 0;
        while (i < end)
        {
            var c = _text[i];
            if (c is '"' or '\'')
            {
                i = SkipNestedString(i, end);
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
            }
            else if (c == '}')
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }
            else if (depth == 0 && (c == '!' && i + 1 < end && _text[i + 1] != '=' || c == ':'))
            {
                break;
            }
            else if (depth == 0 && c == '=' && i + 1 < end && _text[i + 1] is '}' or '!' or ':' && _text[i - 1] is not ('=' or '!' or '<' or '>'))
            {
                break; // self-documenting `{x=}`
            }

            i++;
        }

        var exprEnd = i;
        TryParseExpression(node, exprStart, exprEnd);
        if (i < end && _text[i] == '=')
        {
            i++;
        }

        if (i < end && _text[i] == '!')
        {
            i++;
            while (i < end && _text[i] is not (':' or '}'))
            {
                i++;
            }
        }

        if (i < end && _text[i] == ':')
        {
            i++;
            while (i < end && _text[i] != '}')
            {
                if (_text[i] == '{')
                {
                    i = ParseOneField(node, i + 1, end);
                    continue;
                }

                i++;
            }
        }

        return i < end ? i + 1 : end;
    }

    private int SkipNestedString(int i, int end)
    {
        var q = _text[i];
        var triple = i + 2 < end && _text[i + 1] == q && _text[i + 2] == q;
        i += triple ? 3 : 1;
        while (i < end)
        {
            if (_text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (_text[i] == q && (!triple || i + 2 < end && _text[i + 1] == q && _text[i + 2] == q))
            {
                return i + (triple ? 3 : 1);
            }

            i++;
        }

        return end;
    }

    private void TryParseExpression(StringNode node, int start, int end)
    {
        if (end <= start || string.IsNullOrWhiteSpace(_text[start..end]))
        {
            return;
        }

        try
        {
            var sub = new Parser(_text, new Tokenizer(_text, start, end).Tokenize());
            var expr = sub.ParseTestListStarExpr();
            node.Expressions.Add(expr);
        }
        catch (ParseException)
        {
            // Ignore malformed replacement fields.
        }
    }
}
