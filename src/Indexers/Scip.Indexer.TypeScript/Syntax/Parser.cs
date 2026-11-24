namespace Scip.Indexer.TypeScript.Syntax;

/// <summary>
/// Hand written recursive-descent parser for TypeScript and JavaScript (no JSX). Produces the same node shapes the
/// TypeScript compiler does for the constructs SCIP indexing cares about. Syntax errors throw <see cref="SyntaxException"/>.
/// </summary>
public sealed partial class Parser
{
    private readonly Scanner _s;
    private readonly TsSourceFile _file;

    private Parser(TsSourceFile file)
    {
        _file = file;
        _s = new Scanner(file.Text);
        _s.Next();
    }

    public static TsSourceFile Parse(string fileName, string text)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var file = new TsSourceFile { FileName = fileName, Text = text, IsJavaScript = ext is ".js" or ".jsx" or ".mjs" or ".cjs" };
        var parser = new Parser(file);
        var root = new Node(SK.TsSourceFile, 0) { File = file };
        while (parser._s.Kind != T.Eof)
        {
            root.Elements.Add(parser.ParseStatement());
        }

        root.Start = root.Elements.Count > 0 ? root.Elements[0].Start : text.Length;
        root.End = text.Length;
        file.Root = root;
        file.IsExternalModule = root.Elements.Any(IsModuleIndicator);
        SetParents(root, file);
        return file;
    }

    private static bool IsModuleIndicator(Node n) =>
        n.Kind is SK.ImportDeclaration or SK.ExportDeclaration or SK.ExportAssignment ||
        (n.Kind == SK.ImportEqualsDeclaration && n.Expression?.Kind == SK.ExternalModuleReference) ||
        n.Has(Mods.Export);

    private static void SetParents(Node node, TsSourceFile file)
    {
        node.File = file;
        foreach (var child in node.Children)
        {
            child.Parent = node;
            SetParents(child, file);
        }
    }

    // ---------------------------------------------------------------- token helpers
    private bool IsP(string p) => _s.Kind == T.Punct && _s.TokenText == p;

    private bool IsK(string k) => _s.Kind == T.Ident && _s.TokenText == k;

    private bool EatP(string p)
    {
        if (!IsP(p)) { return false; }
        _s.Next();
        return true;
    }

    private bool EatK(string k)
    {
        if (!IsK(k)) { return false; }
        _s.Next();
        return true;
    }

    private void ExpectP(string p)
    {
        if (!EatP(p)) { Fail($"'{p}' expected"); }
    }

    private void ExpectK(string k)
    {
        if (!EatK(k)) { Fail($"'{k}' expected"); }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Fail(string message) => throw new SyntaxException($"{message} (found '{_s.TokenText}')", _s.TokenStart);

    private Node Begin(SK kind) => new(kind, _s.TokenStart) { JsDoc = _s.JsDoc };

    private Node Fin(Node n)
    {
        n.End = _s.PrevEnd;
        return n;
    }

    private static readonly HashSet<string> Reserved =
    [
        "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else", "enum", "export",
        "extends", "false", "finally", "for", "function", "if", "import", "in", "instanceof", "new", "null", "return", "super",
        "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while", "with",
    ];

    private Parser.Snapshot Save() => new(_s.Save());

    private void Restore(Snapshot snap) => _s.Restore(snap.State);

    private readonly record struct Snapshot(Scanner.State State);

    private TNode? TryParse<TNode>(Func<TNode?> f) where TNode : class
    {
        var snap = Save();
        try
        {
            var result = f();
            if (result is null) { Restore(snap); }
            return result;
        }
        catch (SyntaxException)
        {
            Restore(snap);
            return null;
        }
    }

    private Node ParseIdentifier(bool allowReserved = false)
    {
        if (_s.Kind != T.Ident || (!allowReserved && Reserved.Contains(_s.TokenText))) { Fail("identifier expected"); }
        var n = Begin(SK.Identifier);
        n.Text = _s.TokenText;
        _s.Next();
        return Fin(n);
    }

    private Node ParsePropertyName()
    {
        if (_s.Kind == T.String) { var n = Begin(SK.StringLiteral); n.Text = _s.TokenText; _s.Next(); return Fin(n); }
        if (_s.Kind == T.Number) { var n = Begin(SK.NumericLiteral); n.Text = _s.TokenText; _s.Next(); return Fin(n); }
        if (_s.Kind == T.Private) { var n = Begin(SK.PrivateIdentifier); n.Text = _s.TokenText; _s.Next(); return Fin(n); }
        if (IsP("["))
        {
            var n = Begin(SK.ComputedPropertyName);
            _s.Next();
            n.Expression = ParseAssignment();
            ExpectP("]");
            return Fin(n);
        }

        return ParseIdentifier(allowReserved: true);
    }

    private void ConsumeSemicolon()
    {
        if (EatP(";")) { return; }
        // Lenient ASI: accept a missing semicolon anywhere a statement ends.
    }

    // ---------------------------------------------------------------- modifiers / decorators
    private static readonly Dictionary<string, Mods> ModifierWords = new()
    {
        ["export"] = Mods.Export, ["default"] = Mods.Default, ["declare"] = Mods.Declare, ["public"] = Mods.Public,
        ["private"] = Mods.Private, ["protected"] = Mods.Protected, ["static"] = Mods.Static, ["readonly"] = Mods.Readonly,
        ["abstract"] = Mods.Abstract, ["override"] = Mods.Override, ["async"] = Mods.Async, ["const"] = Mods.Const,
        ["accessor"] = Mods.Accessor,
    };

    private bool NextTokenIsModifierFollower()
    {
        // The current token is a modifier word; it is a modifier iff what follows can continue a declaration/member.
        var snap = Save();
        _s.Next();
        var ok = !_s.NewlineBefore || true;
        var follows = (_s.Kind is T.Ident or T.String or T.Number or T.Private)
            || IsP("[") || IsP("{") || IsP("*") || IsP("@");
        Restore(snap);
        return ok && follows;
    }

    private Mods ParseModifiers(bool allowAll)
    {
        var mods = Mods.None;
        while (_s.Kind == T.Ident && ModifierWords.TryGetValue(_s.TokenText, out var m) && NextTokenIsModifierFollower())
        {
            if (!allowAll && m is Mods.Export or Mods.Default or Mods.Declare) { break; }
            if (m == Mods.Const)
            {
                // `const` is a modifier only for `const enum`.
                var snap = Save();
                _s.Next();
                var isEnum = IsK("enum");
                Restore(snap);
                if (!isEnum) { break; }
            }

            if (m == Mods.Async)
            {
                var snap = Save();
                _s.Next();
                var okAsync = !_s.NewlineBefore;
                Restore(snap);
                if (!okAsync) { break; }
            }

            mods |= m;
            _s.Next();
        }

        return mods;
    }

    private void ParseDecorators(List<Node> into)
    {
        while (IsP("@"))
        {
            var d = Begin(SK.Decorator);
            _s.Next();
            d.Expression = ParseLeftHandSide();
            into.Add(Fin(d));
        }
    }
}
