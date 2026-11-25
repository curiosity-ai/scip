using System.Text;
using System.Text.RegularExpressions;
using ScipSharp.Indexer.TypeScript.Syntax;
using Suffix = ScipSharp.SymbolDescriptor.Types.Suffix;

namespace ScipSharp.Indexer.TypeScript.Semantics;

/// <summary>
/// Port of upstream <c>FileIndexer.ts</c>. Upstream asks the TypeScript checker for the symbol at every identifier; here
/// <see cref="Checker"/> provides the declarations instead. Symbol naming, definition/reference roles, enclosing ranges,
/// relationships and symbol kinds follow upstream exactly; type text in signature documentation is approximated.
/// </summary>
public sealed class FileIndexer(
    Checker checker,
    string cwd,
    long maxFileByteSize,
    Document document,
    Dictionary<Node, ScipSymbol> globalSymbolTable,
    Dictionary<Node, bool> globalConstructorTable,
    Packages packages,
    TsSourceFile sourceFile,
    Action<string> log)
{
    private int _localCounter = -1;
    private readonly Dictionary<string, int> _propertyCounters = [];
    private readonly Dictionary<Node, ScipSymbol> _localSymbolTable = [];
    private readonly Dictionary<string, SymbolInformation> _symbolInformation = [];
    private readonly HashSet<string> _occurrenceKeys = [];
    private readonly Regex _workingDirectory = new(Regex.Escape(cwd), RegexOptions.Compiled);

    private int NextLocal() => ++_localCounter;

    public void Index()
    {
        var byteSize = Encoding.UTF8.GetByteCount(sourceFile.Text);
        if (maxFileByteSize > 0 && byteSize > maxFileByteSize)
        {
            log($"info: skipping file '{sourceFile.FileName}' because it has byte size {byteSize} that exceeds the maximum threshold {maxFileByteSize}. " +
                "If you intended to index this file, use the flag --max-file-byte-size to configure the maximum file size threshold.");
            return;
        }

        EmitSourceFileOccurrence();
        Visit(sourceFile.Root);
        EnsureLocalSymbolInformation();
    }

    /// <summary>
    /// Every local symbol occurring in a document (e.g. import aliases, which are references without a definition
    /// node) needs a <c>SymbolInformation</c> in the same document, as <c>scip lint</c> requires.
    /// </summary>
    private void EnsureLocalSymbolInformation()
    {
        foreach (var o in document.Occurrences.ToArray())
        {
            if (o.Symbol.StartsWith("local ", StringComparison.Ordinal) && !_symbolInformation.ContainsKey(o.Symbol))
            {
                PushSymbolInformation(new SymbolInformation { Symbol = o.Symbol });
            }
        }
    }

    // ------------------------------------------------------------------ ranges
    private (int, int, int, int) RangeOf(Node n)
    {
        var s = sourceFile.LineAndCharacter(n.Start);
        var e = sourceFile.LineAndCharacter(n.End);
        return (s.Line, s.Character, e.Line, e.Character);
    }

    private (int, int, int, int) FirstTokenRange(Node n)
    {
        var i = n.Start;
        var text = sourceFile.Text;
        var j = i;
        if (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] is '_' or '$'))
        {
            while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] is '_' or '$')) { j++; }
        }
        else
        {
            j = Math.Min(text.Length, i + 1);
        }

        var s = sourceFile.LineAndCharacter(i);
        var e = sourceFile.LineAndCharacter(j);
        return (s.Line, s.Character, e.Line, e.Character);
    }

    private void EmitSourceFileOccurrence()
    {
        var symbol = ScipSymbolOf(sourceFile.Root);
        if (symbol.IsEmpty) { return; }
        PushOccurrence((0, 0, 0, 0), symbol.Value, 1, RangeOf(sourceFile.Root), null);
        var moduleName = Path.GetFileName(sourceFile.FileName);
        PushSymbolInformation(new SymbolInformation
        {
            Symbol = symbol.Value,
            SignatureDocumentation = SignatureDocumentation($"module \"{moduleName}\""),
            Kind = SymbolInformation.Types.Kind.File,
            Documentation = { FencedSignature($"module \"{moduleName}\"") },
        });
    }

    // ------------------------------------------------------------------ traversal
    private void Visit(Node node)
    {
        if (node.Kind is SK.Constructor or SK.Identifier or SK.PrivateIdentifier || (node.Kind == SK.StringLiteral))
        {
            var decls = node.Kind == SK.Constructor ? [node] : checker.SymbolAtLocation(node);
            if (decls is { Count: > 0 }) { VisitSymbolOccurrence(node, decls); }
        }

        foreach (var child in node.Children) { Visit(child); }
    }

    private static Node? DeclarationName(Node node) => node.Kind is SK.BindingElement or SK.EnumDeclaration or SK.EnumMember or SK.VariableDeclaration
        or SK.PropertyDeclaration or SK.GetAccessor or SK.SetAccessor or SK.MethodSignature or SK.MethodDeclaration or SK.PropertySignature
        or SK.FunctionDeclaration or SK.ModuleDeclaration or SK.PropertyAssignment or SK.ShorthandPropertyAssignment or SK.Parameter
        or SK.TypeParameter or SK.TypeAliasDeclaration or SK.InterfaceDeclaration or SK.ClassDeclaration
        ? node.Name
        : null;

    private static bool IsDefinition(Node node) => (node.Parent is not null && ReferenceEquals(DeclarationName(node.Parent), node)) || node.Kind == SK.Constructor;

    private void VisitSymbolOccurrence(Node node, List<Node> symbolDecls)
    {
        var isConstructor = node.Kind == SK.Constructor;
        var range = isConstructor ? FirstTokenRange(node) : RangeOf(node);
        var role = 0;
        var declarations = DeclarationsForPropertyAssignment(node) ?? [];
        var isDefinitionNode = declarations.Count == 0 && IsDefinition(node);
        if (isDefinitionNode) { role |= 1; }
        if (declarations.Count == 0)
        {
            declarations = isConstructor ? [node] : isDefinitionNode ? [node.Parent!] : symbolDecls;
        }

        foreach (var declaration in declarations)
        {
            var scipSymbol = ScipSymbolOf(declaration);
            (int, int, int, int)? enclosing = null;
            if (!isDefinitionNode || scipSymbol.IsEmpty || scipSymbol.IsLocal)
            {
                // skip enclosing ranges
            }
            else if (declaration.Kind == SK.VariableDeclaration && declaration.Initializer is { Kind: SK.FunctionExpression or SK.ArrowFunction })
            {
                var statement = declaration.Parent!.Parent!;
                enclosing = RangeOf(statement.Kind == SK.VariableStatement && statement.Initializer!.Elements.Count == 1 ? statement : declaration);
            }
            else if (declaration.Kind is SK.FunctionDeclaration or SK.EnumDeclaration or SK.TypeAliasDeclaration or SK.ClassDeclaration
                or SK.MethodDeclaration or SK.InterfaceDeclaration or SK.Constructor)
            {
                enclosing = RangeOf(declaration);
            }

            if (((node.Kind == SK.Identifier && node.Parent!.Kind == SK.NewExpression) ||
                 (node.Parent!.Kind == SK.PropertyAccessExpression && node.Parent.Parent!.Kind == SK.NewExpression)) &&
                declaration.Kind == SK.ClassDeclaration && !scipSymbol.IsLocal && !scipSymbol.IsEmpty && HasConstructor(declaration))
            {
                scipSymbol = ScipSymbol.Global(scipSymbol, Suffix.Method, "<constructor>");
            }

            if (scipSymbol.IsEmpty) { continue; }
            PushOccurrence(range, scipSymbol.Value, role, enclosing, DiagnosticsFor(symbolDecls, isDefinitionNode));
            if (isDefinitionNode)
            {
                AddSymbolInformation(node, symbolDecls, declaration, scipSymbol);
                HandleObjectBindingPattern(node, range);
                break;
            }
        }

        HandleShorthandPropertyValue(node.Parent!, range);
    }

    private bool HasConstructor(Node classDeclaration)
    {
        if (globalConstructorTable.TryGetValue(classDeclaration, out var cached)) { return cached; }
        var has = classDeclaration.Members.Any(m => m.Kind == SK.Constructor);
        globalConstructorTable[classDeclaration] = has;
        return has;
    }

    private List<Node>? DeclarationsForPropertyAssignment(Node node)
    {
        var element = node.Parent;
        if (element is null || !ReferenceEquals(element.Name, node) ||
            element.Kind is not (SK.PropertyAssignment or SK.ShorthandPropertyAssignment or SK.MethodDeclaration or SK.GetAccessor or SK.SetAccessor) ||
            element.Parent?.Kind != SK.ObjectLiteralExpression)
        {
            return null;
        }

        var name = Binder.NameText(node);
        var found = name is null ? null : checker.ContextualPropertyDecls(element.Parent, name);
        return found is null || found.Contains(element) ? null : found;
    }

    private void HandleObjectBindingPattern(Node node, (int, int, int, int) range)
    {
        if (!(node.Kind == SK.Identifier && node.Parent!.Kind == SK.BindingElement && node.Parent.Parent!.Kind == SK.ObjectBindingPattern)) { return; }
        var containers = checker.ContainersOfBindingParent(node.Parent, 0);
        var members = containers.Count == 0 ? null : checker.MemberDecls(containers, node.Text);
        foreach (var declaration in members ?? [])
        {
            var scipSymbol = ScipSymbolOf(declaration);
            if (scipSymbol.IsEmpty) { continue; }
            PushOccurrence(range, scipSymbol.Value, 0, null, null);
        }
    }

    private void HandleShorthandPropertyValue(Node declaration, (int, int, int, int) range)
    {
        if (declaration.Kind != SK.ShorthandPropertyAssignment) { return; }
        var decls = checker.SymbolAtLocation(MakeValueProbe(declaration));
        foreach (var symbol in decls ?? [])
        {
            var scipSymbol = ScipSymbolOf(symbol);
            if (scipSymbol.IsEmpty) { continue; }
            PushOccurrence(range, scipSymbol.Value, 0, null, null);
        }
    }

    private static Node MakeValueProbe(Node shorthand)
    {
        // A synthetic identifier with the shorthand's name, parented outside the object literal so that the
        // lookup resolves the *value* (not the property).
        var id = new Node(SK.Identifier, shorthand.Name!.Start) { Text = shorthand.Name.Text, End = shorthand.Name.End, File = shorthand.File, Parent = shorthand.Parent };
        return id;
    }

    // ------------------------------------------------------------------ symbol information
    private string SanitizeDocumentation(string value) => _workingDirectory.Replace(value, "");

    private void AddSymbolInformation(Node node, List<Node> symbolDecls, Node declaration, ScipSymbol symbol)
    {
        var signatureText = SanitizeDocumentation(SignatureForDocumentation(node, symbolDecls, declaration));
        var info = new SymbolInformation
        {
            Symbol = symbol.Value,
            SignatureDocumentation = SignatureDocumentation(signatureText),
            Kind = SymbolKindOf(declaration, symbolDecls),
        };
        // Hover markdown as written by the published scip-typescript releases: a ```ts block, then the JSDoc text.
        if (signatureText.Length > 0) { info.Documentation.Add(FencedSignature(signatureText)); }
        var doc = DocumentationOf(declaration);
        if (doc.Length > 0) { info.Documentation.Add(doc); }
        info.Relationships.AddRange(Relationships(declaration, symbol));
        PushSymbolInformation(info);
    }

    /// <summary>The markdown code fence that hover consumers read from <c>documentation[0]</c>.</summary>
    public static string FencedSignature(string text) => "```ts\n" + text + "\n```";

    private static Signature? SignatureDocumentation(string text) =>
        text.Length == 0 ? null : new Signature { Language = "typescript", Text = text };

    private void PushSymbolInformation(SymbolInformation info)
    {
        if (!_symbolInformation.TryGetValue(info.Symbol, out var existing))
        {
            _symbolInformation[info.Symbol] = info;
            document.Symbols.Add(info);
            return;
        }

        if (existing.Documentation.Count == 0) { existing.Documentation.AddRange(info.Documentation); }
        existing.SignatureDocumentation ??= info.SignatureDocumentation;
        foreach (var r in info.Relationships)
        {
            var previous = existing.Relationships.FirstOrDefault(c => c.Symbol == r.Symbol);
            if (previous is null) { existing.Relationships.Add(r); }
            else
            {
                previous.IsImplementation |= r.IsImplementation;
                previous.IsReference |= r.IsReference;
            }
        }
    }

    private void PushOccurrence((int, int, int, int) range, string symbol, int roles, (int, int, int, int)? enclosing, List<Diagnostic>? diagnostics)
    {
        var key = $"{range.Item1}:{range.Item2}:{range.Item3}:{range.Item4} {roles} {symbol}";
        if (!_occurrenceKeys.Add(key)) { return; }
        var o = new Occurrence { Symbol = symbol, SymbolRoles = roles };
        if (range.Item1 == range.Item3) { o.SingleLineRange = new SingleLineRange { Line = range.Item1, StartCharacter = range.Item2, EndCharacter = range.Item4 }; }
        else { o.MultiLineRange = new MultiLineRange { StartLine = range.Item1, StartCharacter = range.Item2, EndLine = range.Item3, EndCharacter = range.Item4 }; }
        if (enclosing is { } e)
        {
            if (e.Item1 == e.Item3) { o.SingleLineEnclosingRange = new SingleLineRange { Line = e.Item1, StartCharacter = e.Item2, EndCharacter = e.Item4 }; }
            else { o.MultiLineEnclosingRange = new MultiLineRange { StartLine = e.Item1, StartCharacter = e.Item2, EndLine = e.Item3, EndCharacter = e.Item4 }; }
        }

        if (diagnostics is not null) { o.Diagnostics.AddRange(diagnostics); }
        document.Occurrences.Add(o);
    }

    // ------------------------------------------------------------------ documentation
    private static string? RawJsDoc(Node declaration)
    {
        var n = declaration;
        if (n.Kind == SK.VariableDeclaration && n.Parent?.Parent is { Kind: SK.VariableStatement } st) { return st.JsDoc ?? n.JsDoc; }
        return n.JsDoc;
    }

    private static string DocumentationOf(Node declaration)
    {
        if (declaration.Kind == SK.Parameter && declaration.Name?.Kind == SK.Identifier && declaration.Parent?.JsDoc is { } fnDoc)
        {
            foreach (var (tag, text) in JsDoc.Tags(fnDoc))
            {
                if (tag == "param")
                {
                    var t = text.TrimStart();
                    if (t.StartsWith('{')) { var close = t.IndexOf('}'); if (close > 0) { t = t[(close + 1)..].TrimStart(); } }
                    if (t.StartsWith(declaration.Name.Text + " ", StringComparison.Ordinal) || t == declaration.Name.Text)
                    {
                        return t[declaration.Name.Text.Length..].TrimStart(' ', '-').Trim();
                    }
                }
            }

            return "";
        }

        var raw = RawJsDoc(declaration);
        return raw is null ? "" : JsDoc.Description(raw);
    }

    private static List<Diagnostic>? DiagnosticsFor(List<Node> decls, bool isDefinition)
    {
        if (isDefinition) { return null; }
        foreach (var d in decls)
        {
            var raw = RawJsDoc(d);
            if (raw is null) { continue; }
            foreach (var (tag, text) in JsDoc.Tags(raw))
            {
                if (tag == "deprecated")
                {
                    return [new Diagnostic { Severity = Severity.Information, Code = "DEPRECATED", Message = text, Tags = { DiagnosticTag.Deprecated } }];
                }
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ relationships
    private List<Relationship> Relationships(Node declaration, ScipSymbol declarationSymbol)
    {
        var relationships = new List<Relationship>();
        var added = new HashSet<string>();
        void PushImplementation(Node node, bool isReference)
        {
            var symbol = ScipSymbolOf(node);
            if (symbol.IsEmpty || symbol.Value == declarationSymbol.Value || !added.Add(symbol.Value)) { return; }
            relationships.Add(new Relationship { Symbol = symbol.Value, IsImplementation = true, IsReference = isReference });
        }

        if (declaration.Kind == SK.ClassDeclaration)
        {
            ForEachAncestor(declaration, a => PushImplementation(a, false));
        }
        else if (declaration.Kind is SK.MethodDeclaration or SK.MethodSignature or SK.PropertyAssignment or SK.PropertyDeclaration)
        {
            var name = declaration.Name!.GetText();
            ForEachAncestor(declaration.Parent!, ancestor =>
            {
                foreach (var member in ancestor.Members)
                {
                    if (member.Name is not null && name == member.Name.GetText()) { PushImplementation(member, true); }
                }
            });
        }

        return relationships;
    }

    private void ForEachAncestor(Node node, Action<Node> onAncestor)
    {
        var visited = new HashSet<Node>();
        void Loop(Node declaration)
        {
            if (!visited.Add(declaration)) { return; }
            var isClassLike = declaration.Kind is SK.ClassDeclaration or SK.ClassExpression;
            if (isClassLike || declaration.Kind == SK.InterfaceDeclaration) { onAncestor(declaration); }
            if (declaration.Kind == SK.ObjectLiteralExpression)
            {
                foreach (var c in checker.ContextualContainers(declaration)) { Loop(c); }
            }
            else if (isClassLike || declaration.Kind == SK.InterfaceDeclaration)
            {
                foreach (var heritageClause in declaration.HeritageClauses)
                {
                    foreach (var t in heritageClause.Elements)
                    {
                        var decls = checker.SymbolAtLocation(LastIdentifier(t.Expression!));
                        foreach (var d in decls ?? []) { Loop(d); }
                    }
                }
            }
        }

        Loop(node);
    }

    private static Node LastIdentifier(Node entity) => entity.Kind == SK.PropertyAccessExpression ? entity.Name! : entity;

    // ------------------------------------------------------------------ symbol names (port of scipSymbol)
    public ScipSymbol ScipSymbolOf(Node node)
    {
        if (globalSymbolTable.TryGetValue(node, out var fromCache) || _localSymbolTable.TryGetValue(node, out fromCache)) { return fromCache; }
        if (node.Kind == SK.Block) { return ScipSymbol.Empty; }
        if (node.Kind == SK.TsSourceFile)
        {
            if (node.File!.IsLib) { return Cached(node, LibStore.SymbolOf(node.File)); }
            var package = packages.Symbol(node.File!.FileName);
            return Cached(node, package.IsEmpty ? ScipSymbol.AnonymousPackage() : package);
        }

        if (node.Kind is SK.PropertyAssignment or SK.ShorthandPropertyAssignment)
        {
            var name = node.Name!.GetText();
            _propertyCounters.TryGetValue(name, out var c);
            _propertyCounters[name] = c + 1;
            // Counter.next(): first value is 0.
            return Cached(node, ScipSymbol.Global(ScipSymbolOf(node.File!.Root), Suffix.Meta, $"{name}{c}"));
        }

        if (node.Synthetic) { return NewLocalSymbol(node); }
        var owner = ScipSymbolOf(node.Parent!);
        if (owner.IsEmpty || owner.IsLocal) { return NewLocalSymbol(node); }
        if (IsAnonymousContainerOfSymbols(node)) { return Cached(node, ScipSymbolOf(node.Parent!)); }

        if (node.Kind is SK.ImportSpecifier or SK.ImportClause or SK.NamespaceImport)
        {
            // Upstream resolves the checker's type of the import binding; we resolve the alias target instead.
            var target = checker.ResolveAlias(node);
            var first = target?.FirstOrDefault(IsTypeSymbolDeclaration);
            if (first is not null) { return ScipSymbolOf(first); }
        }

        var desc = Descriptor(node);
        if (desc is { } d) { return Cached(node, ScipSymbol.Global(owner, d.Suffix, d.Name)); }
        return NewLocalSymbol(node);
    }

    private static bool IsTypeSymbolDeclaration(Node d) =>
        d.Kind is SK.ClassDeclaration or SK.FunctionDeclaration or SK.InterfaceDeclaration or SK.EnumDeclaration or SK.ModuleDeclaration or SK.TsSourceFile;

    private ScipSymbol NewLocalSymbol(Node node)
    {
        var symbol = ScipSymbol.Local(NextLocal());
        _localSymbolTable[node] = symbol;
        return symbol;
    }

    private ScipSymbol Cached(Node node, ScipSymbol symbol)
    {
        globalSymbolTable[node] = symbol;
        return symbol;
    }

    private static bool IsAnonymousContainerOfSymbols(Node node) =>
        node.Kind is SK.ModuleBlock or SK.ImportDeclaration or SK.NamedImports or SK.VariableStatement or SK.VariableDeclarationList ||
        (node.Kind == SK.ImportClause && node.Name is null);

    private (Suffix Suffix, string Name)? Descriptor(Node node)
    {
        switch (node.Kind)
        {
            case SK.InterfaceDeclaration or SK.EnumDeclaration or SK.TypeAliasDeclaration:
                return (Suffix.Type, node.Name!.GetText());
            case SK.ClassDeclaration or SK.ClassExpression when node.Name is not null:
                return (Suffix.Type, node.Name.GetText());
            case SK.FunctionDeclaration or SK.MethodSignature or SK.MethodDeclaration when node.Name is not null:
                return (Suffix.Method, node.Name.GetText());
            case SK.Constructor:
                return (Suffix.Method, "<constructor>");
            case SK.PropertyDeclaration or SK.PropertySignature or SK.EnumMember or SK.VariableDeclaration:
                return (Suffix.Term, node.Name!.GetText());
            case SK.GetAccessor or SK.SetAccessor:
                return (Suffix.Method, (node.Kind == SK.GetAccessor ? "<get>" : "<set>") + node.Name!.GetText());
            case SK.ModuleDeclaration:
                return (Suffix.Namespace, node.Name!.GetText());
            case SK.Parameter:
                return (Suffix.Parameter, node.Name!.GetText());
            case SK.TypeParameter:
                return (Suffix.TypeParameter, node.Name!.GetText());
            case SK.TypeReference:
                return (Suffix.Meta, node.Name!.GetText());
            case SK.TypeLiteral:
                return (Suffix.Meta, "typeLiteral" + NextLocal());
        }

        return null;
    }

    // ------------------------------------------------------------------ signature documentation (approximate types)
    private string SignatureForDocumentation(Node node, List<Node> symbolDecls, Node declaration)
    {
        // The script element kind comes from the merged symbol's flags: type alias, class, interface, enum, ... before variable.
        var merged = declaration.Parent is not null && node.Kind != SK.Constructor ? checker.MergedDeclarations(declaration) : symbolDecls;
        var first = merged.Count > 0 ? merged.MinBy(KindPriority)! : declaration;
        var text = node.Kind == SK.Constructor ? "constructor" : node.GetText();
        string Type() => TypeText(declaration);
        string? Signature()
        {
            var sd = node.Kind == SK.Constructor ? node
                : declaration.Kind is SK.FunctionDeclaration or SK.MethodDeclaration or SK.MethodSignature ? declaration : null;
            return sd is null ? null : SignatureText(sd);
        }

        switch (first.Kind)
        {
            case SK.TypeAliasDeclaration: return "type " + text;
            case SK.ClassDeclaration or SK.ClassExpression:
                return node.Kind == SK.Constructor ? "constructor" + (Signature() ?? "") : "class " + text;
            case SK.InterfaceDeclaration: return "interface " + text;
            case SK.EnumDeclaration: return "enum " + text;
            case SK.EnumMember:
                return "(enum member) " + text + EnumValueSuffix(first);
            case SK.MethodDeclaration or SK.MethodSignature: return "(method) " + text + (Signature() ?? Type());
            case SK.GetAccessor: return "get " + text + ": " + Type();
            case SK.SetAccessor: return "set " + text + Type();
            case SK.Constructor: return "";
            case SK.FunctionDeclaration: return "function " + text + (Signature() ?? Type());
            case SK.Parameter: return "(parameter) " + text + ": " + Type();
            case SK.VariableDeclaration or SK.BindingElement: return "var " + text + ": " + Type();
            case SK.PropertyDeclaration or SK.PropertySignature or SK.PropertyAssignment or SK.ShorthandPropertyAssignment:
                return "(property) " + text + ": " + Type();
        }

        return text + ": " + Type();
    }

    private static int KindPriority(Node d) => d.Kind switch
    {
        SK.TypeAliasDeclaration => 0,
        SK.ClassDeclaration or SK.ClassExpression => 1,
        SK.InterfaceDeclaration => 2,
        SK.EnumDeclaration => 3,
        SK.EnumMember => 4,
        SK.MethodDeclaration or SK.MethodSignature => 5,
        SK.GetAccessor => 6,
        SK.SetAccessor => 7,
        SK.Constructor => 8,
        SK.FunctionDeclaration => 9,
        _ => 10,
    };

    private static string EnumValueSuffix(Node member)
    {
        var enumDecl = member.Parent!;
        double next = 0;
        string? lastString = null;
        foreach (var m in enumDecl.Members)
        {
            double? value = null;
            string? str = null;
            if (m.Initializer is { Kind: SK.NumericLiteral } n && double.TryParse(n.Text.Replace("_", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) { value = v; }
            else if (m.Initializer is { Kind: SK.StringLiteral } s) { str = s.Text; }
            else if (m.Initializer is null) { value = next; }
            if (value is { } val) { next = val + 1; } else { next = double.NaN; }
            lastString = str;
            if (ReferenceEquals(m, member))
            {
                if (value is { } vv && vv != 0) { return " = " + vv.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                if (!string.IsNullOrEmpty(lastString)) { return " = " + lastString; }
                return "";
            }
        }

        return "";
    }

    private static string NormalizeType(string s) => Regex.Replace(Regex.Replace(s.Trim(), @"\s+", " "), @"'([^'\\\""]*)'", "\"$1\"");

    private string SignatureText(Node fn)
    {
        var sb = new StringBuilder("(");
        for (var i = 0; i < fn.Parameters.Count; i++)
        {
            var p = fn.Parameters[i];
            if (i > 0) { sb.Append(", "); }
            if (p.IsExtends) { sb.Append("..."); }
            sb.Append(p.Name!.GetText());
            if (p.Initializer is not null) { sb.Append('?'); }
            sb.Append(": ").Append(p.Type is null ? (p.Initializer is null ? "any" : InferType(p.Initializer, false)) : NormalizeType(p.Type.GetText()));
        }

        sb.Append("): ");
        sb.Append(fn.Kind == SK.Constructor ? NormalizeType(fn.Parent!.Name?.GetText() ?? "any") : fn.Type is not null ? NormalizeType(fn.Type.GetText()) : fn.Body is null ? "any" : "void");
        return sb.ToString();
    }

    private string TypeText(Node d)
    {
        switch (d.Kind)
        {
            case SK.FunctionDeclaration or SK.MethodDeclaration or SK.MethodSignature:
                return SignatureText(d).Replace("): ", ") => ", StringComparison.Ordinal);
            case SK.VariableDeclaration or SK.Parameter or SK.PropertyDeclaration or SK.PropertySignature or SK.GetAccessor or SK.SetAccessor or SK.BindingElement:
            {
                if (d.Type is not null) { return NormalizeType(d.Type.GetText()); }
                if (d.Initializer is not null)
                {
                    var isConst = d.Kind == SK.VariableDeclaration && d.Parent?.Kind == SK.VariableDeclarationList && d.Parent.IsConst;
                    return InferType(d.Initializer, isConst);
                }

                return checker.PrintConts(checker.DeclConts(d, null, 0, asValue: true)) ?? "any";
            }

            case SK.PropertyAssignment: return d.Initializer is null ? "any" : InferType(d.Initializer, false);
            case SK.EnumMember: return d.Parent!.Name!.GetText();
            default: return "any";
        }
    }

    private string InferType(Node e, bool isConst) => e.Kind switch
    {
        SK.NumericLiteral => isConst ? e.Text : "number",
        SK.StringLiteral => isConst ? "\"" + e.Text + "\"" : "string",
        SK.TemplateExpression => "string",
        SK.KeywordLiteral when e.Text is "true" or "false" => isConst ? e.Text : "boolean",
        SK.NewExpression => e.Expression!.GetText(),
        SK.ArrayLiteralExpression => e.Elements.Count > 0 && e.Elements.All(x => x.Kind == SK.NumericLiteral) ? "number[]"
            : e.Elements.Count > 0 && e.Elements.All(x => x.Kind == SK.StringLiteral) ? "string[]" : "any[]",
        SK.ArrowFunction or SK.FunctionExpression => SignatureText(e).Replace("): ", ") => ", StringComparison.Ordinal),
        SK.ParenthesizedExpression => InferType(e.Expression!, isConst),
        SK.AsExpression when e.Type is { Kind: not SK.KeywordType } => NormalizeType(e.Type.GetText()),
        _ => checker.PrintConts(checker.ValueConts(e, 0)) ?? "any",
    };

    // ------------------------------------------------------------------ kinds
    private static bool HasStatic(Node n) => n.Has(Mods.Static);

    private static SymbolInformation.Types.Kind VariableKind(Node declaration) =>
        declaration.Parent is { Kind: SK.VariableDeclarationList, IsConst: true } ? SymbolInformation.Types.Kind.Constant : SymbolInformation.Types.Kind.Variable;

    private static SymbolInformation.Types.Kind SymbolKindOf(Node declaration, List<Node> symbolDecls)
    {
        var K = SymbolInformation.Types.Kind.UnspecifiedKind;
        _ = K;
        switch (declaration.Kind)
        {
            case SK.ClassDeclaration or SK.ClassExpression: return SymbolInformation.Types.Kind.Class;
            case SK.InterfaceDeclaration: return SymbolInformation.Types.Kind.Interface;
            case SK.TypeAliasDeclaration: return SymbolInformation.Types.Kind.TypeAlias;
            case SK.EnumDeclaration: return SymbolInformation.Types.Kind.Enum;
            case SK.EnumMember: return SymbolInformation.Types.Kind.EnumMember;
            case SK.Constructor: return SymbolInformation.Types.Kind.Constructor;
            case SK.GetAccessor: return SymbolInformation.Types.Kind.Getter;
            case SK.SetAccessor: return SymbolInformation.Types.Kind.Setter;
            case SK.MethodDeclaration or SK.MethodSignature:
                return HasStatic(declaration) ? SymbolInformation.Types.Kind.StaticMethod : SymbolInformation.Types.Kind.Method;
            case SK.FunctionDeclaration: return SymbolInformation.Types.Kind.Function;
            case SK.PropertyDeclaration or SK.PropertySignature or SK.PropertyAssignment or SK.ShorthandPropertyAssignment:
                return HasStatic(declaration) ? SymbolInformation.Types.Kind.StaticProperty : SymbolInformation.Types.Kind.Property;
            case SK.Parameter:
                return declaration.Name!.GetText() == "this" ? SymbolInformation.Types.Kind.ThisParameter : SymbolInformation.Types.Kind.Parameter;
            case SK.TypeParameter: return SymbolInformation.Types.Kind.TypeParameter;
            case SK.ModuleDeclaration:
                return declaration.Name!.Kind == SK.StringLiteral ? SymbolInformation.Types.Kind.Module : SymbolInformation.Types.Kind.Namespace;
            case SK.VariableDeclaration: return VariableKind(declaration);
            case SK.BindingElement:
            {
                var owner = declaration.Parent!;
                while (owner.Kind is SK.BindingElement or SK.ArrayBindingPattern or SK.ObjectBindingPattern) { owner = owner.Parent!; }
                if (owner.Kind == SK.Parameter) { return SymbolInformation.Types.Kind.Parameter; }
                return owner.Kind == SK.VariableDeclaration ? VariableKind(owner) : SymbolInformation.Types.Kind.Variable;
            }
        }

        _ = symbolDecls;
        return SymbolInformation.Types.Kind.UnspecifiedKind;
    }
}
