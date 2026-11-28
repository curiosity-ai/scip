namespace ScipSharp.Indexer.Php.Syntax;

/// <summary>Result of parsing one PHP file.</summary>
internal sealed class ParsedFile(string source, List<Stmt> stmts)
{
    /// <summary>Source decoded as Latin-1 (one char per byte).</summary>
    public string Source { get; } = source;

    public List<Stmt> Stmts { get; } = stmts;

    /// <summary>All nodes in PHP-Parser traversal (post-order) order.</summary>
    public IEnumerable<Node> PostOrder()
    {
        var stack = new Stack<(Node Node, List<Node> Kids, int Index)>();
        foreach (var root in Stmts)
        {
            var kids = new List<Node>();
            root.Children(kids);
            stack.Push((root, kids, 0));
            while (stack.Count > 0)
            {
                var (node, ks, i) = stack.Pop();
                if (i < ks.Count)
                {
                    stack.Push((node, ks, i + 1));
                    var child = ks[i];
                    var grand = new List<Node>();
                    child.Children(grand);
                    stack.Push((child, grand, 0));
                }
                else
                {
                    yield return node;
                }
            }
        }
    }
}

/// <summary>
/// Recursive-descent PHP 8.x parser producing the subset of nikic/php-parser's AST that scip-php consumes. Names are
/// resolved while parsing (the equivalent of php-parser's <c>NameResolver</c> visitor).
/// </summary>
internal sealed class PhpParser
{
    private readonly string _src;
    private readonly List<Token> _toks;
    private int _p;
    private int _prevEnd;

    // Nesting guard: nested expressions and statements recurse here (several frames per level), so input nested deeper
    // than this, or deep enough to exhaust the stack, is a parse error (the file is skipped) rather than a process crash.
    // Left-associative chains (`$a->b()->c()`, `'a' . 'b' . ...`, elseif) are parsed in loops and do not count.
    private const int MaxNesting = 2000;
    private int _nesting;

    // Name resolution context.
    private string? _ns;
    private Dictionary<string, string> _classAliases = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _funcAliases = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _constAliases = new(StringComparer.Ordinal);

    private PhpParser(string src, List<Token> toks, PhpParser? parent)
    {
        _src = src;
        _toks = toks;
        if (parent != null)
        {
            _nesting = parent._nesting;
            _ns = parent._ns;
            _classAliases = parent._classAliases;
            _funcAliases = parent._funcAliases;
            _constAliases = parent._constAliases;
        }
    }

    /// <summary>Parses PHP source text (raw file bytes decoded as Latin-1).</summary>
    public static ParsedFile Parse(string latin1Source)
    {
        var toks = new Lexer(latin1Source).Tokenize();
        var parser = new PhpParser(latin1Source, toks, null);
        var stmts = parser.ParseTop();
        var file = new ParsedFile(latin1Source, stmts);
        foreach (var s in stmts)
        {
            Connect(s);
        }

        return file;
    }

    private static void Connect(Node root)
    {
        // Iterative (long chains make deep trees), assigning parents in the same pre-order as a recursive walk.
        var stack = new Stack<(Node Node, Node? Parent)>();
        var kids = new List<Node>();
        stack.Push((root, null));
        while (stack.Count > 0)
        {
            var (n, parent) = stack.Pop();
            if (parent != null)
            {
                n.Parent = parent;
            }

            kids.Clear();
            n.Children(kids);
            for (var i = kids.Count - 1; i >= 0; i--)
            {
                stack.Push((kids[i], n));
            }
        }
    }

    private void EnterNesting()
    {
        if (_nesting >= MaxNesting || !System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw Error("nesting too deep");
        }

        _nesting++;
    }

    // ---------------------------------------------------------------- token helpers

    private Token Cur => _toks[_p];

    private Token Peek(int n = 1) => _toks[Math.Min(_p + n, _toks.Count - 1)];

    private Token Next()
    {
        var t = _toks[_p];
        if (_p < _toks.Count - 1)
        {
            _p++;
        }

        _prevEnd = t.End;
        return t;
    }

    private bool IsOp(string op) => Cur.IsOp(op);

    private bool IsKw(string kw) => Cur.IsKeyword(kw);

    private bool AcceptOp(string op)
    {
        if (IsOp(op))
        {
            Next();
            return true;
        }

        return false;
    }

    private bool AcceptKw(string kw)
    {
        if (IsKw(kw))
        {
            Next();
            return true;
        }

        return false;
    }

    private Token ExpectOp(string op)
    {
        if (!IsOp(op))
        {
            throw Error($"expected '{op}' but found '{Cur.Text}'");
        }

        return Next();
    }

    private void ExpectKw(string kw)
    {
        if (!IsKw(kw))
        {
            throw Error($"expected '{kw}' but found '{Cur.Text}'");
        }

        Next();
    }

    private ParseException Error(string msg) => new($"{msg} at offset {Cur.Start}", Cur.Start);

    private T Fin<T>(T node, Token start)
        where T : Node
    {
        node.Start = start.Start;
        node.End = Math.Max(_prevEnd, start.End);
        node.TokPos = start.Pos;
        node.Doc = start.Doc;
        return node;
    }

    private void EndStatement()
    {
        if (Cur.Kind == TK.CloseTag)
        {
            Next();
            return;
        }

        if (Cur.Kind == TK.Eof)
        {
            return;
        }

        ExpectOp(";");
    }

    // ---------------------------------------------------------------- name resolution

    private static readonly HashSet<string> BuiltinTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "float", "string", "bool", "array", "callable", "iterable", "object", "mixed", "void", "never", "null", "false", "true",
    };

    private Name MakeName(Token t, int type, bool resolve = true)
    {
        var text = t.Text.StartsWith('\\') ? t.Text[1..] : t.Text;
        var n = new Name { Kind = t.NameKind, Text = text, Start = t.Start, End = t.End, TokPos = t.Pos, Doc = t.Doc };
        if (!resolve || t.NameKind == NameKind.FullyQualified)
        {
            return n;
        }

        if (t.NameKind == NameKind.Relative)
        {
            var rest = text[(text.IndexOf('\\') + 1)..];
            n.Text = _ns == null ? rest : _ns + "\\" + rest;
            n.Kind = NameKind.FullyQualified;
            return n;
        }

        if (type == 1 && n.IsSpecialClassName)
        {
            return n;
        }

        if (t.NameKind == NameKind.Qualified)
        {
            var slash = text.IndexOf('\\');
            if (_classAliases.TryGetValue(text[..slash], out var target))
            {
                n.Text = target + text[slash..];
                n.Kind = NameKind.FullyQualified;
                return n;
            }
        }
        else
        {
            var table = type switch { 2 => _funcAliases, 3 => _constAliases, _ => _classAliases };
            if (table.TryGetValue(text, out var target))
            {
                n.Text = target;
                n.Kind = NameKind.FullyQualified;
                return n;
            }

            if (type != 1)
            {
                if (_ns == null)
                {
                    n.Kind = NameKind.FullyQualified;
                }

                return n;
            }
        }

        if (_ns != null)
        {
            n.Text = _ns + "\\" + text;
        }

        n.Kind = NameKind.FullyQualified;
        return n;
    }

    // ---------------------------------------------------------------- top level / statements

    private List<Stmt> ParseTop()
    {
        var result = new List<Stmt>();
        NamespaceStmt? current = null;
        while (Cur.Kind != TK.Eof)
        {
            if (IsKw("namespace") && Peek().Kind != TK.Op || IsKw("namespace") && Peek().IsOp("{"))
            {
                var start = Next();
                Name? name = null;
                if (Cur.Kind == TK.Name)
                {
                    name = MakeName(Next(), 1, resolve: false);
                }

                ResetNamespace(name);
                var ns = new NamespaceStmt { Name = name };
                if (AcceptOp("{"))
                {
                    while (!IsOp("}") && Cur.Kind != TK.Eof)
                    {
                        ParseStatementInto(ns.Stmts);
                    }

                    ExpectOp("}");
                    current = null;
                }
                else
                {
                    EndStatement();
                    current = ns;
                }

                Fin(ns, start);
                result.Add(ns);
                continue;
            }

            if (current != null)
            {
                ParseStatementInto(current.Stmts);
                current.End = Math.Max(current.End, _prevEnd);
            }
            else
            {
                ParseStatementInto(result);
            }
        }

        return result;
    }

    private void ResetNamespace(Name? name)
    {
        _ns = name?.Text;
        _classAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _funcAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _constAliases = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private void ParseStatementInto(List<Stmt> into)
    {
        var s = ParseStatement();
        if (s != null)
        {
            into.Add(s);
        }
    }

    private List<Stmt> ParseBlockBody()
    {
        ExpectOp("{");
        var list = new List<Stmt>();
        while (!IsOp("}") && Cur.Kind != TK.Eof)
        {
            ParseStatementInto(list);
        }

        ExpectOp("}");
        return list;
    }

    private Stmt? ParseStatement()
    {
        EnterNesting();
        try
        {
            return ParseStatementCore();
        }
        finally
        {
            _nesting--;
        }
    }

    private Stmt? ParseStatementCore()
    {
        var t = Cur;
        switch (t.Kind)
        {
            case TK.InlineHtml:
                Next();
                return Fin(new InlineHtmlStmt(), t);
            case TK.CloseTag:
                Next();
                return null;
            case TK.OpenTagEcho:
            {
                Next();
                var e = ParseExpr(0);
                var kids = new List<Node?> { e };
                while (AcceptOp(","))
                {
                    kids.Add(ParseExpr(0));
                }

                EndStatement();
                return Fin(new GenericStmt { Kind = "echo", Kids = kids }, t);
            }

            case TK.Op when t.Text == "{":
            {
                var body = ParseBlockBody();
                return Fin(new GenericStmt { Kind = "block", Kids = [.. body] }, t);
            }

            case TK.Op when t.Text == ";":
                Next();
                return null;
            case TK.Op when t.Text == "#[":
                return ParseAttributedStatement();
            case TK.Name when t.NameKind == NameKind.Unqualified:
                return ParseKeywordStatement(t) ?? ParseExpressionStatement();
        }

        return ParseExpressionStatement();
    }

    private Stmt ParseExpressionStatement()
    {
        var start = Cur;
        var e = ParseExpr(0);
        EndStatement();
        return Fin(new ExprStmt { Expr = e }, start);
    }

    private Stmt ParseAttributedStatement()
    {
        var start = Cur;
        var attrs = ParseAttributes();
        var t = Cur;
        if (t.Kind == TK.Name)
        {
            if (IsKw("function") && (Peek().Kind == TK.Name || (Peek().IsOp("&") && Peek(2).Kind == TK.Name)))
            {
                return ParseFunctionDecl(start, attrs);
            }

            if (IsKw("class") || IsKw("abstract") || IsKw("final") || IsKw("interface") || IsKw("trait") || IsKw("enum") || IsKw("readonly"))
            {
                return ParseClassLikeDecl(start, attrs);
            }
        }

        // An attributed closure / arrow function expression statement.
        _attrsForExpr = attrs;
        _attrsStart = start;
        var e = ParseExpr(0);
        EndStatement();
        return Fin(new ExprStmt { Expr = e }, start);
    }

    private List<AttrGroup>? _attrsForExpr;
    private Token? _attrsStart;

    private Stmt? ParseKeywordStatement(Token t)
    {
        var kw = t.Text.ToLowerInvariant();
        switch (kw)
        {
            case "use":
                return ParseUse();
            case "const":
                return ParseConstStmt();
            case "function":
                if (Peek().Kind == TK.Name || (Peek().IsOp("&") && Peek(2).Kind == TK.Name))
                {
                    return ParseFunctionDecl(t, []);
                }

                return null;
            case "abstract":
            case "final":
            case "class":
            case "interface":
            case "trait":
                return ParseClassLikeDecl(t, []);
            case "readonly":
                return Peek().Kind == TK.Name ? ParseClassLikeDecl(t, []) : null;
            case "enum":
                return Peek().Kind == TK.Name && Peek().NameKind == NameKind.Unqualified && !Peek().IsKeyword("extends") && !Peek().IsKeyword("implements")
                    ? ParseClassLikeDecl(t, [])
                    : null;
            case "if":
                return ParseIf();
            case "while":
                return ParseWhile();
            case "do":
                return ParseDoWhile();
            case "for":
                return ParseFor();
            case "foreach":
                return ParseForeach();
            case "switch":
                return ParseSwitch();
            case "try":
                return ParseTry();
            case "declare":
                return ParseDeclare();
            case "return":
            {
                Next();
                var kids = new List<Node?>();
                if (!IsOp(";") && Cur.Kind != TK.CloseTag && Cur.Kind != TK.Eof)
                {
                    kids.Add(ParseExpr(0));
                }

                EndStatement();
                return Fin(new GenericStmt { Kind = "return", Kids = kids }, t);
            }

            case "echo":
            {
                Next();
                var kids = new List<Node?> { ParseExpr(0) };
                while (AcceptOp(","))
                {
                    kids.Add(ParseExpr(0));
                }

                EndStatement();
                return Fin(new GenericStmt { Kind = "echo", Kids = kids }, t);
            }

            case "global":
            {
                Next();
                var kids = new List<Node?> { ParseExpr(0) };
                while (AcceptOp(","))
                {
                    kids.Add(ParseExpr(0));
                }

                EndStatement();
                return Fin(new GenericStmt { Kind = "global", Kids = kids }, t);
            }

            case "static" when Peek().Kind == TK.Variable:
            {
                Next();
                var kids = new List<Node?>();
                do
                {
                    kids.Add(ParseVariableToken());
                    if (AcceptOp("="))
                    {
                        kids.Add(ParseExpr(0));
                    }
                }
                while (AcceptOp(","));

                EndStatement();
                return Fin(new GenericStmt { Kind = "static", Kids = kids }, t);
            }

            case "unset" when Peek().IsOp("("):
            {
                Next();
                ExpectOp("(");
                var kids = new List<Node?>();
                while (!IsOp(")"))
                {
                    kids.Add(ParseExpr(0));
                    if (!AcceptOp(","))
                    {
                        break;
                    }
                }

                ExpectOp(")");
                EndStatement();
                return Fin(new GenericStmt { Kind = "unset", Kids = kids }, t);
            }

            case "break":
            case "continue":
            {
                Next();
                var kids = new List<Node?>();
                if (!IsOp(";") && Cur.Kind != TK.CloseTag)
                {
                    kids.Add(ParseExpr(0));
                }

                EndStatement();
                return Fin(new GenericStmt { Kind = kw, Kids = kids }, t);
            }

            case "goto" when Peek().Kind == TK.Name:
                Next();
                Next();
                EndStatement();
                return Fin(new GenericStmt { Kind = "goto" }, t);
            case "__halt_compiler":
                _p = _toks.Count - 1;
                return null;
        }

        // label:
        if (Peek().IsOp(":") && !Peek().IsOp("::") && !IsReserved(kw))
        {
            Next();
            Next();
            return Fin(new GenericStmt { Kind = "label" }, t);
        }

        return null;
    }

    private static bool IsReserved(string kw) => kw is "default" or "else" or "case" or "parent" or "self" or "static";

    private Stmt ParseUse()
    {
        var start = Next();
        var type = 1;
        if (IsKw("function") && Peek().Kind == TK.Name)
        {
            Next();
            type = 2;
        }
        else if (IsKw("const") && Peek().Kind == TK.Name)
        {
            Next();
            type = 3;
        }

        var stmt = new UseStmt { Type = type };
        do
        {
            if (Cur.Kind != TK.Name)
            {
                throw Error("expected name in use statement");
            }

            var nameTok = Next();
            if (IsOp("\\") && Peek().IsOp("{"))
            {
                Next();
                Next();
                stmt.Prefix = MakeName(nameTok, 1, resolve: false);
                var prefix = stmt.Prefix.Text;
                while (!IsOp("}"))
                {
                    var itemStart = Cur;
                    var itemType = 0;
                    if (IsKw("function") && Peek().Kind == TK.Name)
                    {
                        Next();
                        itemType = 2;
                    }
                    else if (IsKw("const") && Peek().Kind == TK.Name)
                    {
                        Next();
                        itemType = 3;
                    }

                    var item = new UseItem { Name = MakeName(Next(), 1, resolve: false), Type = itemType };
                    if (AcceptKw("as"))
                    {
                        item.Alias = MakeIdent(Next());
                    }

                    Fin(item, itemStart);
                    stmt.Uses.Add(item);
                    AddAlias(type | itemType, prefix + "\\" + item.Name.Text, item.AliasName);
                    if (!AcceptOp(","))
                    {
                        break;
                    }
                }

                ExpectOp("}");
            }
            else
            {
                var item = new UseItem { Name = MakeName(nameTok, 1, resolve: false) };
                if (AcceptKw("as"))
                {
                    item.Alias = MakeIdent(Next());
                }

                Fin(item, nameTok);
                stmt.Uses.Add(item);
                AddAlias(type, item.Name.Text, item.AliasName);
            }
        }
        while (AcceptOp(","));

        EndStatement();
        return Fin(stmt, start);
    }

    private void AddAlias(int type, string fullName, string alias)
    {
        switch (type)
        {
            case 2:
                _funcAliases[alias] = fullName;
                break;
            case 3:
                _constAliases[alias] = fullName;
                break;
            default:
                _classAliases[alias] = fullName;
                break;
        }
    }

    private Ident MakeIdent(Token t) => new() { Name = t.Text, Start = t.Start, End = t.End, TokPos = t.Pos, Doc = t.Doc };

    private Stmt ParseConstStmt()
    {
        var start = Next();
        var stmt = new ConstStmt();
        do
        {
            var nameTok = Next();
            ExpectOp("=");
            var item = new ConstItem { Name = MakeIdent(nameTok), Value = ParseExpr(0) };
            item.NamespacedName = _ns == null ? nameTok.Text : _ns + "\\" + nameTok.Text;
            Fin(item, nameTok);
            stmt.Consts.Add(item);
        }
        while (AcceptOp(","));

        EndStatement();
        return Fin(stmt, start);
    }

    private Stmt ParseDeclare()
    {
        var start = Next();
        ExpectOp("(");
        var depth = 1;
        while (depth > 0 && Cur.Kind != TK.Eof)
        {
            if (IsOp("("))
            {
                depth++;
            }
            else if (IsOp(")"))
            {
                depth--;
            }

            Next();
        }

        var kids = new List<Node?>();
        if (IsOp("{"))
        {
            kids.AddRange(ParseBlockBody());
        }
        else if (AcceptOp(":"))
        {
            while (!IsKw("enddeclare") && Cur.Kind != TK.Eof)
            {
                ParseStatementInto(kids);
            }

            Next();
            EndStatement();
        }
        else
        {
            EndStatement();
        }

        return Fin(new GenericStmt { Kind = "declare", Kids = kids }, start);
    }

    private void ParseStatementInto(List<Node?> into)
    {
        var s = ParseStatement();
        if (s != null)
        {
            into.Add(s);
        }
    }

    /// <summary>Parses a statement or an alt-syntax block (<c>: ... endX;</c>) of a control structure.</summary>
    private void ParseBody(List<Node?> into, params string[] terminators)
    {
        if (AcceptOp(":"))
        {
            while (Cur.Kind != TK.Eof && !terminators.Any(IsKw))
            {
                ParseStatementInto(into);
            }

            return;
        }

        ParseStatementInto(into);
    }

    private Stmt ParseIf()
    {
        var start = Next();
        var kids = new List<Node?>();
        ExpectOp("(");
        kids.Add(ParseExpr(0));
        ExpectOp(")");
        if (IsOp(":"))
        {
            ParseBody(kids, "elseif", "else", "endif");
            while (IsKw("elseif") || (IsKw("else") && Peek().IsKeyword("if")))
            {
                if (IsKw("else"))
                {
                    Next();
                }

                Next();
                ExpectOp("(");
                kids.Add(ParseExpr(0));
                ExpectOp(")");
                ParseBody(kids, "elseif", "else", "endif");
            }

            if (AcceptKw("else"))
            {
                ParseBody(kids, "endif");
            }

            ExpectKw("endif");
            EndStatement();
            return Fin(new GenericStmt { Kind = "if", Kids = kids }, start);
        }

        ParseStatementInto(kids);
        while (true)
        {
            if (IsKw("elseif") || (IsKw("else") && Peek().IsKeyword("if")))
            {
                if (IsKw("else"))
                {
                    Next();
                }

                Next();
                ExpectOp("(");
                kids.Add(ParseExpr(0));
                ExpectOp(")");
                ParseStatementInto(kids);
                continue;
            }

            if (AcceptKw("else"))
            {
                ParseStatementInto(kids);
            }

            break;
        }

        return Fin(new GenericStmt { Kind = "if", Kids = kids }, start);
    }

    private Stmt ParseWhile()
    {
        var start = Next();
        var kids = new List<Node?>();
        ExpectOp("(");
        kids.Add(ParseExpr(0));
        ExpectOp(")");
        var alt = IsOp(":");
        ParseBody(kids, "endwhile");
        if (alt)
        {
            ExpectKw("endwhile");
            EndStatement();
        }

        return Fin(new GenericStmt { Kind = "while", Kids = kids }, start);
    }

    private Stmt ParseDoWhile()
    {
        var start = Next();
        var kids = new List<Node?>();
        ParseStatementInto(kids);
        ExpectKw("while");
        ExpectOp("(");
        kids.Add(ParseExpr(0));
        ExpectOp(")");
        EndStatement();
        return Fin(new GenericStmt { Kind = "do", Kids = kids }, start);
    }

    private Stmt ParseFor()
    {
        var start = Next();
        var kids = new List<Node?>();
        ExpectOp("(");
        for (var part = 0; part < 3; part++)
        {
            while (!IsOp(part == 2 ? ")" : ";"))
            {
                kids.Add(ParseExpr(0));
                if (!AcceptOp(","))
                {
                    break;
                }
            }

            ExpectOp(part == 2 ? ")" : ";");
        }

        var alt = IsOp(":");
        ParseBody(kids, "endfor");
        if (alt)
        {
            ExpectKw("endfor");
            EndStatement();
        }

        return Fin(new GenericStmt { Kind = "for", Kids = kids }, start);
    }

    private Stmt ParseForeach()
    {
        var start = Next();
        var kids = new List<Node?>();
        ExpectOp("(");
        kids.Add(ParseExpr(0));
        ExpectKw("as");
        AcceptOp("&");
        kids.Add(ParseExpr(0));
        if (AcceptOp("=>"))
        {
            AcceptOp("&");
            kids.Add(ParseExpr(0));
        }

        ExpectOp(")");
        var alt = IsOp(":");
        ParseBody(kids, "endforeach");
        if (alt)
        {
            ExpectKw("endforeach");
            EndStatement();
        }

        return Fin(new GenericStmt { Kind = "foreach", Kids = kids }, start);
    }

    private Stmt ParseSwitch()
    {
        var start = Next();
        var kids = new List<Node?>();
        ExpectOp("(");
        kids.Add(ParseExpr(0));
        ExpectOp(")");
        var alt = false;
        if (AcceptOp(":"))
        {
            alt = true;
        }
        else
        {
            ExpectOp("{");
        }

        while (Cur.Kind != TK.Eof && !(alt ? IsKw("endswitch") : IsOp("}")))
        {
            if (AcceptKw("case"))
            {
                kids.Add(ParseExpr(0));
            }
            else if (AcceptKw("default"))
            {
            }
            else
            {
                throw Error("expected case or default");
            }

            if (!AcceptOp(":"))
            {
                ExpectOp(";");
            }

            while (!IsKw("case") && !IsKw("default") && !IsOp("}") && !IsKw("endswitch") && Cur.Kind != TK.Eof)
            {
                ParseStatementInto(kids);
            }
        }

        if (alt)
        {
            ExpectKw("endswitch");
            EndStatement();
        }
        else
        {
            ExpectOp("}");
        }

        return Fin(new GenericStmt { Kind = "switch", Kids = kids }, start);
    }

    private Stmt ParseTry()
    {
        var start = Next();
        var kids = new List<Node?>();
        kids.AddRange(ParseBlockBody());
        while (IsKw("catch"))
        {
            var cs = Next();
            ExpectOp("(");
            var ckids = new List<Node?>();
            do
            {
                ckids.Add(MakeName(Next(), 1));
            }
            while (AcceptOp("|"));

            if (Cur.Kind == TK.Variable)
            {
                ckids.Add(ParseVariableToken());
            }

            ExpectOp(")");
            ckids.AddRange(ParseBlockBody());
            kids.Add(Fin(new GenericStmt { Kind = "catch", Kids = ckids }, cs));
        }

        if (IsKw("finally"))
        {
            Next();
            kids.AddRange(ParseBlockBody());
        }

        return Fin(new GenericStmt { Kind = "try", Kids = kids }, start);
    }

    // ---------------------------------------------------------------- declarations

    private List<AttrGroup> ParseAttributes()
    {
        var groups = new List<AttrGroup>();
        while (IsOp("#["))
        {
            var start = Next();
            var g = new AttrGroup();
            while (!IsOp("]") && Cur.Kind != TK.Eof)
            {
                var nameTok = Next();
                var item = new AttrItem { Name = MakeName(nameTok, 1) };
                if (IsOp("("))
                {
                    item.Args = ParseArgs(out _);
                }

                Fin(item, nameTok);
                g.Attrs.Add(item);
                if (!AcceptOp(","))
                {
                    break;
                }
            }

            ExpectOp("]");
            groups.Add(Fin(g, start));
        }

        return groups;
    }

    private Mod ParseModifiers()
    {
        var m = Mod.None;
        while (Cur.Kind == TK.Name && Cur.NameKind == NameKind.Unqualified)
        {
            var f = Cur.Text.ToLowerInvariant() switch
            {
                "public" => Mod.Public,
                "protected" => Mod.Protected,
                "private" => Mod.Private,
                "static" => Mod.Static,
                "abstract" => Mod.Abstract,
                "final" => Mod.Final,
                "readonly" => Mod.ReadOnly,
                "var" => Mod.Public,
                _ => Mod.None,
            };
            if (f == Mod.None)
            {
                break;
            }

            // `static`/`readonly`/... used as a name (e.g. `static::foo()`) is not a modifier here.
            Next();

            // PHP 8.4 asymmetric visibility: `private(set)`, `protected(set)`, `public(set)` restrict writes only.
            if (f is Mod.Public or Mod.Protected or Mod.Private && IsOp("(") && Peek().Kind == TK.Name
                && Peek().Text.Equals("set", StringComparison.OrdinalIgnoreCase) && Peek(2).IsOp(")"))
            {
                Next();
                Next();
                Next();
                if ((m & Mod.Visibility) == Mod.None)
                {
                    m |= Mod.Public; // `private(set) string $x` is publicly readable
                }

                continue;
            }

            m |= f;
        }

        return m;
    }

    private Stmt ParseFunctionDecl(Token start, List<AttrGroup> attrs)
    {
        var fnTok = Next();
        _ = fnTok;
        var f = new FunctionStmt { AttrGroups = attrs };
        f.ByRef = AcceptOp("&");
        var nameTok = Next();
        f.Name = MakeIdent(nameTok);
        f.NamespacedName = _ns == null ? nameTok.Text : _ns + "\\" + nameTok.Text;
        f.Params = ParseParams();
        if (AcceptOp(":"))
        {
            f.ReturnType = ParseType();
        }

        f.Stmts = ParseBlockBody();
        return Fin(f, start);
    }

    private List<Param> ParseParams()
    {
        ExpectOp("(");
        var list = new List<Param>();
        while (!IsOp(")") && Cur.Kind != TK.Eof)
        {
            var start = Cur;
            var p = new Param { AttrGroups = ParseAttributes() };
            p.Flags = ParseModifiers();
            if (Cur.Kind != TK.Variable && !IsOp("&") && !IsOp("..."))
            {
                p.Type = ParseType();
            }

            p.ByRef = AcceptOp("&");
            p.Variadic = AcceptOp("...");
            p.Var = ParseVariableToken();
            if (AcceptOp("="))
            {
                p.Default = ParseExpr(0);
            }

            if (IsOp("{"))
            {
                SkipBraces();
            }

            list.Add(Fin(p, start));
            if (!AcceptOp(","))
            {
                break;
            }
        }

        ExpectOp(")");
        return list;
    }

    private void SkipBraces()
    {
        var depth = 0;
        do
        {
            if (IsOp("{"))
            {
                depth++;
            }
            else if (IsOp("}"))
            {
                depth--;
            }

            Next();
        }
        while (depth > 0 && Cur.Kind != TK.Eof);
    }

    private Variable ParseVariableToken()
    {
        var t = Cur;
        if (t.Kind != TK.Variable)
        {
            throw Error($"expected variable but found '{t.Text}'");
        }

        Next();
        return Fin(new Variable { Name = t.Text[1..] }, t);
    }

    private Node ParseType()
    {
        var start = Cur;
        if (AcceptOp("?"))
        {
            var inner = ParseSimpleType();
            return Fin(new NullableType { Type = inner }, start);
        }

        var first = ParseSimpleType();
        if (IsOp("|"))
        {
            var u = new UnionType { Types = [first] };
            while (AcceptOp("|"))
            {
                u.Types.Add(ParseSimpleType());
            }

            return Fin(u, start);
        }

        if (IsOp("&") && (Peek().Kind == TK.Name || Peek().IsOp("(")))
        {
            var u = new IntersectionType { Types = [first] };
            while (IsOp("&") && (Peek().Kind == TK.Name || Peek().IsOp("(")))
            {
                Next();
                u.Types.Add(ParseSimpleType());
            }

            return Fin(u, start);
        }

        return first;
    }

    private Node ParseSimpleType()
    {
        var start = Cur;
        if (AcceptOp("("))
        {
            var u = new IntersectionType();
            u.Types.Add(ParseSimpleType());
            while (AcceptOp("&"))
            {
                u.Types.Add(ParseSimpleType());
            }

            ExpectOp(")");
            return Fin(u, start);
        }

        if (Cur.Kind != TK.Name)
        {
            throw Error($"expected type but found '{Cur.Text}'");
        }

        var t = Next();
        if (t.NameKind == NameKind.Unqualified && BuiltinTypes.Contains(t.Text))
        {
            return new Ident { Name = t.Text.ToLowerInvariant(), Start = t.Start, End = t.End, TokPos = t.Pos, Doc = t.Doc };
        }

        return MakeName(t, 1);
    }

    private Stmt ParseClassLikeDecl(Token start, List<AttrGroup> attrs)
    {
        var flags = Mod.None;
        while (true)
        {
            if (AcceptKw("abstract"))
            {
                flags |= Mod.Abstract;
            }
            else if (AcceptKw("final"))
            {
                flags |= Mod.Final;
            }
            else if (AcceptKw("readonly"))
            {
                flags |= Mod.ReadOnly;
            }
            else
            {
                break;
            }
        }

        var kwTok = Next();
        var kw = kwTok.Text.ToLowerInvariant();
        var nameTok = Next();
        var name = MakeIdent(nameTok);
        ClassLike cl;
        switch (kw)
        {
            case "interface":
            {
                var i = new InterfaceStmt();
                if (AcceptKw("extends"))
                {
                    do
                    {
                        i.Extends.Add(MakeName(Next(), 1));
                    }
                    while (AcceptOp(","));
                }

                cl = i;
                break;
            }

            case "trait":
                cl = new TraitStmt();
                break;
            case "enum":
            {
                var e = new EnumStmt();
                if (AcceptOp(":"))
                {
                    e.ScalarType = ParseType();
                }

                if (AcceptKw("implements"))
                {
                    do
                    {
                        e.Implements.Add(MakeName(Next(), 1));
                    }
                    while (AcceptOp(","));
                }

                cl = e;
                break;
            }

            default:
            {
                var c = new ClassStmt();
                ParseExtendsImplements(c);
                cl = c;
                break;
            }
        }

        cl.Name = name;
        cl.Flags = flags;
        cl.AttrGroups = attrs;
        cl.Stmts = ParseClassBody();
        return Fin(cl, start);
    }

    private void ParseExtendsImplements(ClassStmt c)
    {
        if (AcceptKw("extends"))
        {
            c.Extends = MakeName(Next(), 1);
        }

        if (AcceptKw("implements"))
        {
            do
            {
                c.Implements.Add(MakeName(Next(), 1));
            }
            while (AcceptOp(","));
        }
    }

    private List<Stmt> ParseClassBody()
    {
        ExpectOp("{");
        var list = new List<Stmt>();
        while (!IsOp("}") && Cur.Kind != TK.Eof)
        {
            list.Add(ParseMember());
        }

        ExpectOp("}");
        return list;
    }

    private Stmt ParseMember()
    {
        var start = Cur;
        var attrs = ParseAttributes();
        if (IsKw("use"))
        {
            return ParseTraitUse(start);
        }

        if (IsKw("case") && Peek().Kind == TK.Name)
        {
            Next();
            var nameTok = Next();
            var ec = new EnumCase { AttrGroups = attrs, Name = MakeIdent(nameTok) };
            if (AcceptOp("="))
            {
                ec.Value = ParseExpr(0);
            }

            EndStatement();
            return Fin(ec, start);
        }

        var flags = ParseModifiers();
        if (IsKw("const"))
        {
            Next();
            var cc = new ClassConst { Flags = flags, AttrGroups = attrs };
            if (Cur.Kind == TK.Name && !Peek().IsOp("="))
            {
                cc.Type = ParseType();
            }
            else if (IsOp("?") || IsOp("("))
            {
                cc.Type = ParseType();
            }

            do
            {
                var nameTok = Next();
                ExpectOp("=");
                var item = new ConstItem { Name = MakeIdent(nameTok), Value = ParseExpr(0) };
                Fin(item, nameTok);
                cc.Consts.Add(item);
            }
            while (AcceptOp(","));

            EndStatement();
            return Fin(cc, start);
        }

        if (IsKw("function"))
        {
            Next();
            var m = new ClassMethod { Flags = flags, AttrGroups = attrs };
            m.ByRef = AcceptOp("&");
            var nameTok = Next();
            m.Name = MakeIdent(nameTok);
            m.Params = ParseParams();
            if (AcceptOp(":"))
            {
                m.ReturnType = ParseType();
            }

            if (IsOp("{"))
            {
                m.Stmts = ParseBlockBody();
            }
            else
            {
                EndStatement();
            }

            return Fin(m, start);
        }

        var prop = new PropertyStmt { Flags = flags, AttrGroups = attrs };
        if (Cur.Kind != TK.Variable)
        {
            prop.Type = ParseType();
        }

        do
        {
            var vt = Cur;
            if (vt.Kind != TK.Variable)
            {
                throw Error($"unexpected '{vt.Text}' in class body");
            }

            Next();
            var item = new PropertyItem { Name = new Ident { Name = vt.Text[1..], Start = vt.Start, End = vt.End, TokPos = vt.Pos } };
            if (AcceptOp("="))
            {
                item.Default = ParseExpr(0);
            }

            Fin(item, vt);
            prop.Props.Add(item);
        }
        while (AcceptOp(","));

        if (IsOp("{"))
        {
            SkipBraces();
        }
        else
        {
            EndStatement();
        }

        return Fin(prop, start);
    }

    private Stmt ParseTraitUse(Token start)
    {
        Next();
        var tu = new TraitUse();
        do
        {
            tu.Traits.Add(MakeName(Next(), 1));
        }
        while (AcceptOp(","));

        if (AcceptOp("{"))
        {
            while (!IsOp("}") && Cur.Kind != TK.Eof)
            {
                // [Trait::]method (as [visibility] [alias] | insteadof T1, T2);
                var kids = new List<Node>();
                if (Cur.Kind == TK.Name && Peek().IsOp("::"))
                {
                    kids.Add(MakeName(Next(), 1));
                    Next();
                }

                Next(); // method name
                if (AcceptKw("as"))
                {
                    ParseModifiers();
                    if (Cur.Kind == TK.Name)
                    {
                        Next();
                    }
                }
                else if (AcceptKw("insteadof"))
                {
                    do
                    {
                        kids.Add(MakeName(Next(), 1));
                    }
                    while (AcceptOp(","));
                }

                ExpectOp(";");
                tu.Adaptations.AddRange(kids);
            }

            ExpectOp("}");
        }
        else
        {
            EndStatement();
        }

        return Fin(tu, start);
    }

    // ---------------------------------------------------------------- expressions

    private static int BinaryPrec(Token t, out bool rightAssoc)
    {
        rightAssoc = false;
        if (t.Kind == TK.Op)
        {
            switch (t.Text)
            {
                case "??":
                    rightAssoc = true;
                    return 6;
                case "||":
                    return 7;
                case "&&":
                    return 8;
                case "|":
                    return 9;
                case "^":
                    return 10;
                case "&":
                    return 11;
                case "==" or "!=" or "===" or "!==" or "<>" or "<=>":
                    return 12;
                case "<" or "<=" or ">" or ">=":
                    return 13;
                case ".":
                    return 14;
                case "<<" or ">>":
                    return 15;
                case "+" or "-":
                    return 16;
                case "*" or "/" or "%":
                    return 17;
                case "**":
                    rightAssoc = true;
                    return 21;
            }

            return -1;
        }

        if (t.Kind == TK.Name && t.NameKind == NameKind.Unqualified)
        {
            switch (t.Text.ToLowerInvariant())
            {
                case "or":
                    return 1;
                case "xor":
                    return 2;
                case "and":
                    return 3;
                case "instanceof":
                    return 19;
            }
        }

        return -1;
    }

    private static readonly HashSet<string> AssignOps = ["=", "+=", "-=", "*=", "/=", ".=", "%=", "&=", "|=", "^=", "<<=", ">>=", "**=", "??="];

    private Expr ParseExpr(int minPrec)
    {
        EnterNesting();
        try
        {
            return ParseExprCore(minPrec);
        }
        finally
        {
            _nesting--;
        }
    }

    private Expr ParseExprCore(int minPrec)
    {
        var start = Cur;
        var left = ParseUnary();
        while (true)
        {
            var t = Cur;
            if (t.IsOp("?") && minPrec <= 5)
            {
                Next();
                Expr? mid = null;
                if (!IsOp(":"))
                {
                    mid = ParseExpr(0);
                    // allow assignment / nested ternary in the middle
                }

                ExpectOp(":");
                var els = ParseExpr(5 + 1);
                left = Fin(new Ternary { Cond = left, If = mid, Else = els }, start);
                continue;
            }

            var prec = BinaryPrec(t, out var right);
            if (prec < 0 || prec < minPrec)
            {
                break;
            }

            Next();
            if (t.Kind == TK.Name && t.Text.Equals("instanceof", StringComparison.OrdinalIgnoreCase))
            {
                Node cls;
                if (Cur.Kind == TK.Name && !IsKw("new"))
                {
                    cls = MakeName(Next(), 1);
                }
                else
                {
                    cls = ParseExpr(20);
                }

                left = Fin(new GenericExpr { Kind = "instanceof", Kids = [left, cls] }, start);
                continue;
            }

            var rhs = ParseExpr(right ? prec : prec + 1);
            left = Fin(new BinaryOp { Op = t.Text.ToLowerInvariant(), Left = left, Right = rhs }, start);
        }

        return left;
    }

    private Expr ParseUnary()
    {
        var t = Cur;
        if (t.Kind == TK.Op)
        {
            switch (t.Text)
            {
                case "!":
                {
                    Next();
                    var e = ParseExpr(18);
                    return Fin(new GenericExpr { Kind = "!", Kids = [e] }, t);
                }

                case "-" or "+" or "~" or "@":
                {
                    Next();
                    var e = ParseExpr(20);
                    return Fin(new GenericExpr { Kind = t.Text, Kids = [e] }, t);
                }

                case "++" or "--":
                {
                    Next();
                    var e = ParseExpr(20);
                    return Fin(new GenericExpr { Kind = t.Text, Kids = [e] }, t);
                }

                case "&":
                {
                    Next();
                    var e = ParseExpr(20);
                    return Fin(new GenericExpr { Kind = "&", Kids = [e] }, t);
                }
            }
        }

        if (t.Kind == TK.Cast)
        {
            Next();
            var e = ParseExpr(20);
            return Fin(new GenericExpr { Kind = "cast", Kids = [e] }, t);
        }

        if (t.Kind == TK.Name && t.NameKind == NameKind.Unqualified)
        {
            switch (t.Text.ToLowerInvariant())
            {
                case "clone":
                {
                    Next();
                    var e = ParseExpr(20);
                    return Fin(new CloneExpr { Value = e }, t);
                }

                case "print":
                {
                    Next();
                    var e = ParseExpr(4);
                    return Fin(new GenericExpr { Kind = "print", Kids = [e] }, t);
                }

                case "yield":
                {
                    Next();
                    var kids = new List<Node?>();
                    if (AcceptKw("from"))
                    {
                        kids.Add(ParseExpr(4));
                    }
                    else if (!IsOp(";") && !IsOp(")") && !IsOp(",") && !IsOp("]") && Cur.Kind != TK.CloseTag)
                    {
                        var v = ParseExpr(5);
                        if (AcceptOp("=>"))
                        {
                            kids.Add(v);
                            v = ParseExpr(5);
                        }

                        kids.Add(v);
                    }

                    return Fin(new GenericExpr { Kind = "yield", Kids = kids }, t);
                }

                case "throw":
                {
                    Next();
                    var e = ParseExpr(0);
                    return Fin(new GenericExpr { Kind = "throw", Kids = [e] }, t);
                }

                case "include" or "include_once" or "require" or "require_once":
                {
                    Next();
                    var e = ParseExpr(4);
                    return Fin(new GenericExpr { Kind = "include", Kids = [e] }, t);
                }
            }
        }

        var primary = ParsePostfix();
        if (Cur.Kind == TK.Op && AssignOps.Contains(Cur.Text) && IsLvalue(primary))
        {
            var op = Next();
            if (op.Text == "=" && IsOp("&") && !(Peek().Kind == TK.Op && Peek().Text is "&"))
            {
                // $a = &$b;
                Next();
                var r = ParseExpr(4);
                return Fin(new GenericExpr { Kind = "assignref", Kids = [primary, r] }, t);
            }

            var value = ParseExpr(4);
            if (op.Text == "=")
            {
                return Fin(new Assign { Var = primary, Value = value }, t);
            }

            return Fin(new GenericExpr { Kind = "assignop", Kids = [primary, value] }, t);
        }

        return primary;
    }

    private static bool IsLvalue(Expr e) =>
        e is Variable or ArrayDimFetch or PropertyFetch or StaticPropertyFetch or ArrayLit || e is GenericExpr { Kind: "list" };

    private Expr ParsePostfix()
    {
        var start = Cur;
        var e = ParsePrimary();
        while (true)
        {
            var t = Cur;
            if (t.IsOp("["))
            {
                Next();
                Expr? dim = null;
                if (!IsOp("]"))
                {
                    dim = ParseExpr(0);
                }

                ExpectOp("]");
                e = Fin(new ArrayDimFetch { Var = e, Dim = dim }, start);
                continue;
            }

            if (t.IsOp("->") || t.IsOp("?->"))
            {
                Next();
                var nullsafe = t.Text == "?->";
                Node nameNode;
                if (Cur.Kind == TK.Name)
                {
                    nameNode = MakeIdent(Next());
                }
                else if (Cur.Kind == TK.Variable)
                {
                    nameNode = ParseVariableToken();
                }
                else if (AcceptOp("{"))
                {
                    nameNode = ParseExpr(0);
                    ExpectOp("}");
                }
                else if (Cur.IsOp("$"))
                {
                    nameNode = ParsePrimary();
                }
                else
                {
                    throw Error($"unexpected '{Cur.Text}' after '->'");
                }

                if (IsOp("("))
                {
                    var args = ParseArgs(out _);
                    e = Fin(new MethodCall { Var = e, NameNode = nameNode, Args = args, Nullsafe = nullsafe }, start);
                }
                else
                {
                    e = Fin(new PropertyFetch { Var = e, NameNode = nameNode, Nullsafe = nullsafe }, start);
                }

                continue;
            }

            if (t.IsOp("::"))
            {
                Next();
                e = ParseStaticMember(e is ClassRefExpr cr ? cr.Name : e, start);
                continue;
            }

            if (t.IsOp("("))
            {
                var args = ParseArgs(out _);
                e = Fin(new FuncCall { Callee = e, Args = args }, start);
                continue;
            }

            if (t.IsOp("++") || t.IsOp("--"))
            {
                Next();
                e = Fin(new GenericExpr { Kind = "postfix", Kids = [e] }, start);
                continue;
            }

            break;
        }

        return e;
    }

    private Expr ParseStaticMember(Node cls, Token start)
    {
        if (Cur.Kind == TK.Variable)
        {
            var vt = Next();
            Node nameNode = new Ident { Name = vt.Text[1..], Start = vt.Start, End = vt.End, TokPos = vt.Pos };
            if (IsOp("("))
            {
                // A::$f() calls the value of the static property.
                var v = new Variable { Name = vt.Text[1..], Start = vt.Start, End = vt.End, TokPos = vt.Pos };
                var args = ParseArgs(out _);
                return Fin(new StaticCall { Class = cls, NameNode = v, Args = args }, start);
            }

            return Fin(new StaticPropertyFetch { Class = cls, NameNode = nameNode }, start);
        }

        if (Cur.Kind == TK.Name)
        {
            var nt = Next();
            var ident = MakeIdent(nt);
            if (IsOp("("))
            {
                var args = ParseArgs(out _);
                return Fin(new StaticCall { Class = cls, NameNode = ident, Args = args }, start);
            }

            return Fin(new ClassConstFetch { Class = cls, NameNode = ident }, start);
        }

        if (AcceptOp("{"))
        {
            var ne = ParseExpr(0);
            ExpectOp("}");
            var args = ParseArgs(out _);
            return Fin(new StaticCall { Class = cls, NameNode = ne, Args = args }, start);
        }

        if (IsOp("$"))
        {
            var dyn = ParsePrimary();
            return Fin(new StaticPropertyFetch { Class = cls, NameNode = dyn }, start);
        }

        throw Error($"unexpected '{Cur.Text}' after '::'");
    }

    private List<Arg> ParseArgs(out bool firstClassCallable)
    {
        firstClassCallable = false;
        ExpectOp("(");
        var args = new List<Arg>();
        if (IsOp("...") && Peek().IsOp(")"))
        {
            Next();
            Next();
            firstClassCallable = true;
            return args;
        }

        while (!IsOp(")") && Cur.Kind != TK.Eof)
        {
            var start = Cur;
            var a = new Arg();
            if (AcceptOp("..."))
            {
                a.Unpack = true;
            }
            else if (Cur.Kind == TK.Name && Peek().IsOp(":") && !Peek().IsOp("::"))
            {
                a.Label = MakeIdent(Next());
                Next();
            }

            a.Value = ParseExpr(0);
            args.Add(Fin(a, start));
            if (!AcceptOp(","))
            {
                break;
            }
        }

        ExpectOp(")");
        return args;
    }

    private Expr ParsePrimary()
    {
        var t = Cur;
        switch (t.Kind)
        {
            case TK.Variable:
                Next();
                return Fin(new Variable { Name = t.Text[1..] }, t);
            case TK.Number:
                Next();
                return Fin(new NumberLit { Text = t.Text }, t);
            case TK.String:
                Next();
                return Fin(MakeString(t), t);
            case TK.Template:
                Next();
                return ParseTemplate(t);
            case TK.Op:
                return ParseOpPrimary(t);
            case TK.Name:
                return ParseNamePrimary(t);
        }

        throw Error($"unexpected token '{t.Text}'");
    }

    private static StringLit MakeString(Token t)
    {
        var raw = t.Text;
        var s = new StringLit { Raw = raw };
        if (raw.StartsWith('\''))
        {
            s.Kind = StringKind.Single;
            s.Value = Lexer.Decode(Unescape(raw.AsSpan(1, Math.Max(0, raw.Length - 2)), single: true));
        }
        else if (raw.StartsWith('"'))
        {
            s.Kind = StringKind.Double;
            s.Value = Lexer.Decode(Unescape(raw.AsSpan(1, Math.Max(0, raw.Length - 2)), single: false));
        }
        else if (raw.StartsWith('`'))
        {
            s.Kind = StringKind.Backtick;
        }
        else
        {
            s.Kind = StringKind.Nowdoc;
        }

        return s;
    }

    private static string Unescape(ReadOnlySpan<char> body, bool single)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '\\' && i + 1 < body.Length)
            {
                var n = body[i + 1];
                if (single)
                {
                    if (n is '\\' or '\'')
                    {
                        sb.Append(n);
                        i++;
                        continue;
                    }
                }
                else
                {
                    var rep = n switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '\\' => '\\', '"' => '"', '$' => '$', 'e' => '\x1b', 'v' => '\v', 'f' => '\f', _ => '\0' };
                    if (rep != '\0')
                    {
                        sb.Append(rep);
                        i++;
                        continue;
                    }
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private Expr ParseTemplate(Token t)
    {
        var tpl = new TemplateExpr { Raw = t.Text };
        foreach (var part in t.Parts ?? [])
        {
            switch (part)
            {
                case TplSimple s:
                {
                    Expr v = new Variable { Name = s.Var[1..], Start = s.Start, End = s.VarEnd };
                    if (s.Suffix is TplDim d)
                    {
                        Expr key = d.IsVariable
                            ? new Variable { Name = d.Text[1..], Start = d.Start, End = d.End }
                            : d.IsNumber
                                ? new NumberLit { Text = d.Text, Start = d.Start, End = d.End }
                                : new StringLit { Raw = d.Text, Value = d.Text, Kind = StringKind.Single, Start = d.Start, End = d.End };
                        v = new ArrayDimFetch { Var = v, Dim = key, Start = s.Start, End = s.End };
                    }
                    else if (s.Suffix is TplProp p)
                    {
                        v = new PropertyFetch
                        {
                            Var = v,
                            NameNode = new Ident { Name = p.Name, Start = p.Start, End = p.End },
                            Start = s.Start,
                            End = s.End,
                        };
                    }

                    tpl.Parts.Add(v);
                    break;
                }

                case TplExpr x:
                {
                    var toks = new List<Token>(x.Tokens) { new() { Kind = TK.Eof, Start = x.End, End = x.End } };
                    var sub = new PhpParser(_src, toks, this);
                    tpl.Parts.Add(sub.ParseExpr(0));
                    break;
                }
            }
        }

        return Fin(tpl, t);
    }

    private Expr ParseOpPrimary(Token t)
    {
        switch (t.Text)
        {
            case "(":
            {
                Next();
                var e = ParseExpr(0);
                ExpectOp(")");
                return e;
            }

            case "[":
                return ParseArrayLiteral(t, "]");
            case "$":
            {
                Next();
                if (AcceptOp("{"))
                {
                    var ne = ParseExpr(0);
                    ExpectOp("}");
                    return Fin(new Variable { NameExpr = ne }, t);
                }

                var inner = ParsePrimary();
                return Fin(new Variable { NameExpr = inner }, t);
            }

            case "&":
            {
                Next();
                var e = ParseExpr(20);
                return Fin(new GenericExpr { Kind = "&", Kids = [e] }, t);
            }

            case "#[":
            {
                var attrs = ParseAttributes();
                _attrsForExpr = attrs;
                _attrsStart = t;
                return ParsePrimary();
            }
        }

        throw Error($"unexpected '{t.Text}'");
    }

    private Expr ParseArrayLiteral(Token start, string close)
    {
        Next();
        var arr = new ArrayLit { IsList = close == ")" };
        while (!IsOp(close) && Cur.Kind != TK.Eof)
        {
            if (IsOp(","))
            {
                Next();
                arr.Items.Add(null);
                continue;
            }

            var istart = Cur;
            var item = new ArrayItem();
            if (AcceptOp("..."))
            {
                item.Unpack = true;
                item.Value = ParseExpr(0);
            }
            else if (AcceptOp("&"))
            {
                item.ByRef = true;
                item.Value = ParseExpr(20);
            }
            else
            {
                var v = ParseExpr(0);
                if (AcceptOp("=>"))
                {
                    item.Key = v;
                    if (AcceptOp("&"))
                    {
                        item.ByRef = true;
                    }

                    item.Value = ParseExpr(0);
                }
                else
                {
                    item.Value = v;
                }
            }

            arr.Items.Add(Fin(item, istart));
            if (!AcceptOp(","))
            {
                break;
            }
        }

        ExpectOp(close);
        return Fin(arr, start);
    }

    private Expr ParseNamePrimary(Token t)
    {
        if (t.NameKind != NameKind.Unqualified)
        {
            return ParseNameExpr(t);
        }

        var kw = t.Text.ToLowerInvariant();
        switch (kw)
        {
            case "new":
                return ParseNew();
            case "function":
            case "fn":
                return ParseClosure(t, isStatic: false);
            case "static" when Peek().IsKeyword("function") || Peek().IsKeyword("fn"):
                Next();
                return ParseClosure(t, isStatic: true);
            case "match" when Peek().IsOp("("):
                return ParseMatch();
            case "isset" when Peek().IsOp("("):
            {
                Next();
                ExpectOp("(");
                var kids = new List<Node?>();
                while (!IsOp(")"))
                {
                    kids.Add(ParseExpr(0));
                    if (!AcceptOp(","))
                    {
                        break;
                    }
                }

                ExpectOp(")");
                return Fin(new GenericExpr { Kind = "isset", Kids = kids }, t);
            }

            case "empty" when Peek().IsOp("("):
            {
                Next();
                ExpectOp("(");
                var e = ParseExpr(0);
                ExpectOp(")");
                return Fin(new GenericExpr { Kind = "empty", Kids = [e] }, t);
            }

            case "exit" or "die":
            {
                Next();
                var kids = new List<Node?>();
                if (AcceptOp("("))
                {
                    if (!IsOp(")"))
                    {
                        kids.Add(ParseExpr(0));
                    }

                    ExpectOp(")");
                }

                return Fin(new GenericExpr { Kind = "exit", Kids = kids }, t);
            }

            case "list" when Peek().IsOp("("):
            {
                Next();
                var arr = (ArrayLit)ParseArrayLiteral(t, ")");
                return Fin(new GenericExpr { Kind = "list", Kids = [.. arr.Items.Where(i => i != null)] }, t);
            }

            case "array" when Peek().IsOp("("):
            {
                Next();
                return ParseArrayLiteral(t, ")");
            }

            case "__line__" or "__file__" or "__dir__" or "__function__" or "__class__" or "__trait__" or "__method__" or "__namespace__":
                Next();
                return Fin(new MagicConst { Text = t.Text }, t);
        }

        return ParseNameExpr(t);
    }

    private Expr ParseNameExpr(Token t)
    {
        Next();
        var next = Cur;
        if (next.IsOp("::"))
        {
            var cls = MakeName(t, 1);
            return new ClassRefExpr(cls);
        }

        if (next.IsOp("("))
        {
            var fn = MakeName(t, 2);
            var args = ParseArgs(out _);
            return Fin(new FuncCall { Callee = fn, Args = args }, t);
        }

        var cn = MakeName(t, 3);
        return Fin(new ConstFetch { Name = cn }, t);
    }

    private Expr ParseNew()
    {
        var start = Next();
        var attrs = new List<AttrGroup>();
        if (IsOp("#["))
        {
            attrs = ParseAttributes();
        }

        if (IsKw("class") || ((IsKw("readonly") || IsKw("final") || IsKw("abstract")) && Peek().IsKeyword("class")))
        {
            var flags = Mod.None;
            if (AcceptKw("readonly"))
            {
                flags |= Mod.ReadOnly;
            }

            var ct = Next();
            var c = new ClassStmt { Flags = flags, AttrGroups = attrs };
            if (IsOp("("))
            {
                c.CtorArgs = ParseArgs(out _);
            }

            ParseExtendsImplements(c);
            c.Stmts = ParseClassBody();
            Fin(c, ct);
            return Fin(new NewExpr { Class = c }, start);
        }

        Node cls;
        var clsStart = Cur;
        if (Cur.Kind == TK.Name)
        {
            cls = MakeName(Next(), 1);
        }
        else if (IsOp("("))
        {
            Next();
            cls = ParseExpr(0);
            ExpectOp(")");
        }
        else
        {
            cls = ParseNewVariable();
        }

        _ = clsStart;
        var args = IsOp("(") ? ParseArgs(out _) : [];
        return Fin(new NewExpr { Class = cls, Args = args }, start);
    }

    private Expr ParseNewVariable()
    {
        var start = Cur;
        Expr e = ParsePrimary();
        while (true)
        {
            var t = Cur;
            if (t.IsOp("[") )
            {
                Next();
                Expr? dim = IsOp("]") ? null : ParseExpr(0);
                ExpectOp("]");
                e = Fin(new ArrayDimFetch { Var = e, Dim = dim }, start);
            }
            else if (t.IsOp("->") || t.IsOp("?->"))
            {
                Next();
                Node nameNode = Cur.Kind == TK.Variable ? ParseVariableToken() : MakeIdent(Next());
                e = Fin(new PropertyFetch { Var = e, NameNode = nameNode, Nullsafe = t.Text == "?->" }, start);
            }
            else if (t.IsOp("::") && Peek().Kind == TK.Variable)
            {
                Next();
                var vt = Next();
                e = Fin(new StaticPropertyFetch { Class = e, NameNode = new Ident { Name = vt.Text[1..], Start = vt.Start, End = vt.End } }, start);
            }
            else
            {
                break;
            }
        }

        return e;
    }

    private Expr ParseClosure(Token start, bool isStatic)
    {
        var attrs = _attrsForExpr ?? [];
        var attrStart = _attrsStart;
        _attrsForExpr = null;
        _attrsStart = null;
        var kw = Next(); // function | fn
        var isFn = kw.Text.Equals("fn", StringComparison.OrdinalIgnoreCase);
        var first = attrStart ?? start;
        var byRef = AcceptOp("&");
        if (isFn)
        {
            var f = new ArrowFunction { Static = isStatic, ByRef = byRef, AttrGroups = attrs };
            f.Params = ParseParams();
            if (AcceptOp(":"))
            {
                f.ReturnType = ParseType();
            }

            ExpectOp("=>");
            f.Body = ParseExpr(4);
            return Fin(f, first);
        }

        var c = new Closure { Static = isStatic, ByRef = byRef, AttrGroups = attrs };
        c.Params = ParseParams();
        if (AcceptKw("use"))
        {
            ExpectOp("(");
            while (!IsOp(")"))
            {
                var us = Cur;
                var u = new ClosureUse { ByRef = AcceptOp("&") };
                u.Var = ParseVariableToken();
                c.Uses.Add(Fin(u, us));
                if (!AcceptOp(","))
                {
                    break;
                }
            }

            ExpectOp(")");
        }

        if (AcceptOp(":"))
        {
            c.ReturnType = ParseType();
        }

        c.Stmts = ParseBlockBody();
        return Fin(c, first);
    }

    private Expr ParseMatch()
    {
        var start = Next();
        ExpectOp("(");
        var m = new MatchExpr { Cond = ParseExpr(0) };
        ExpectOp(")");
        ExpectOp("{");
        while (!IsOp("}") && Cur.Kind != TK.Eof)
        {
            var astart = Cur;
            var arm = new MatchArm();
            if (IsKw("default") && (Peek().IsOp("=>") || Peek().IsOp(",")))
            {
                Next();
                AcceptOp(",");
            }
            else
            {
                arm.Conds = [];
                do
                {
                    if (IsOp("=>"))
                    {
                        break;
                    }

                    arm.Conds.Add(ParseExpr(0));
                }
                while (AcceptOp(","));
            }

            ExpectOp("=>");
            arm.Body = ParseExpr(0);
            m.Arms.Add(Fin(arm, astart));
            if (!AcceptOp(","))
            {
                break;
            }
        }

        ExpectOp("}");
        return Fin(m, start);
    }
}

/// <summary>Placeholder produced for <c>Name::</c>; replaced by the static member access in the postfix loop.</summary>
internal sealed class ClassRefExpr : Expr
{
    public ClassRefExpr(Name name)
    {
        Name = name;
        Start = name.Start;
        End = name.End;
        TokPos = name.TokPos;
        Doc = name.Doc;
    }

    public Name Name { get; }

    public override void Children(List<Node> into) => into.Add(Name);
}
