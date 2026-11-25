#pragma warning disable CS0618 // Occurrence.Range / EnclosingRange are deprecated in scip.proto but are what scip-python emits.
using System.Text;
using ScipSharp.Indexer.Python.Semantics;
using ScipSharp.Indexer.Python.Syntax;
using SuffixKind = ScipSharp.SymbolDescriptor.Types.Suffix;

namespace ScipSharp.Indexer.Python.Emit;

/// <summary>State shared by all <see cref="TreeVisitor"/>s of one indexing run.</summary>
public sealed class IndexState(ModuleSystem modules, Evaluator evaluator, SymbolNames names, PythonPackage projectPackage, PythonPackage stdlibPackage)
{
    public ModuleSystem Modules { get; } = modules;

    public Evaluator Evaluator { get; } = evaluator;

    public SymbolNames Names { get; } = names;

    public PythonPackage ProjectPackage { get; } = projectPackage;

    public PythonPackage StdlibPackage { get; } = stdlibPackage;

    /// <summary>symbol node -> symbol, for symbols that are valid across documents.</summary>
    public Dictionary<Node, string> GlobalSymbols { get; } = [];

    public Dictionary<string, SymbolInformation> ExternalSymbols { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Port of <c>treeVisitor.ts</c>: walks one document and emits occurrences and symbol information. Where upstream asks
/// pyright's evaluator for types, this uses <see cref="Evaluator"/>.
/// </summary>
public sealed class TreeVisitor
{
    private readonly IndexState _state;
    private readonly SourceModule _module;
    private readonly Evaluator _eval;
    private readonly SymbolNames _names;
    private readonly Document _doc;
    private readonly Dictionary<Node, string> _documentSymbols = [];
    private readonly HashSet<string> _symbolInfoEmitted = [];
    private int _counter;

    public TreeVisitor(IndexState state, SourceModule module, Document doc)
    {
        _state = state;
        _module = module;
        _doc = doc;
        _eval = state.Evaluator;
        _names = state.Names;
    }

    private PythonPackage Project => _state.ProjectPackage;

    private PythonPackage Stdlib => _state.StdlibPackage;

    public void Walk(Node node)
    {
        if (!Visit(node))
        {
            return;
        }

        foreach (var child in node.Children())
        {
            if (child is not null)
            {
                Walk(child);
            }
        }
    }

    private bool Visit(Node node) => node switch
    {
        ModuleNode m => VisitModule(m),
        TypeAnnotationNode ta => VisitTypeAnnotation(ta),
        AssignmentNode a => VisitAssignment(a),
        FunctionNode f => VisitFunction(f),
        ImportNode i => VisitImport(i),
        ImportFromNode f => VisitImportFrom(f),
        ImportFromAsNode f => VisitImportFromAs(f),
        ImportAsNode i => VisitImportAs(i),
        NameNode n => VisitName(n),
        _ => true,
    };

    private string NextLocal() => SymbolNames.Local(_counter++);

    // ------------------------------------------------------------------ positions

    private int[] RangeOf(Node node)
    {
        var (sl, sc) = _module.Lines.Position(node.Start);
        var (el, ec) = _module.Lines.Position(node.End);
        return sl == el ? [sl, sc, ec] : [sl, sc, el, ec];
    }

    private void Push(Node node, string symbol, int role = (int)SymbolRole.ReadAccess, Node? enclosing = null)
    {
        var occ = new Occurrence { SymbolRoles = role, Symbol = symbol };
        occ.Range.AddRange(RangeOf(node));
        if (enclosing is not null)
        {
            occ.EnclosingRange.AddRange(RangeOf(enclosing));
        }

        _doc.Occurrences.Add(occ);
        if (symbol.StartsWith(StdlibPrefix, StringComparison.Ordinal))
        {
            EmitExternalSymbolInformation(symbol, []);
        }
    }

    private string StdlibPrefix => field ??= SymbolNames.Package(Stdlib);

    // ------------------------------------------------------------------ tree utils (port of parseTreeUtils)

    private static ClassNode? EnclosingClass(Node node, bool stopAtFunction)
    {
        for (var cur = node.Parent; cur is not null; cur = cur.Parent)
        {
            switch (cur)
            {
                case ClassNode c:
                    return c;
                case ModuleNode:
                    return null;
                case FunctionNode when stopAtFunction:
                    return null;
            }
        }

        return null;
    }

    private static FunctionNode? EnclosingFunction(Node node)
    {
        Node? prev = node;
        for (var cur = node.Parent; cur is not null; prev = cur, cur = cur.Parent)
        {
            if (cur is FunctionNode f && !f.Decorators.Contains(prev))
            {
                return f;
            }

            if (cur is ClassNode)
            {
                return null;
            }
        }

        return null;
    }

    private static LambdaNode? EnclosingLambda(Node node)
    {
        for (var cur = node.Parent; cur is not null; cur = cur.Parent)
        {
            if (cur is LambdaNode l)
            {
                return l;
            }

            if (cur is SuiteNode)
            {
                return null;
            }
        }

        return null;
    }

    private static SuiteNode? EnclosingSuite(Node node)
    {
        for (var cur = node.Parent; cur is not null; cur = cur.Parent)
        {
            if (cur is SuiteNode s)
            {
                return s;
            }
        }

        return null;
    }

    private static Node? EnclosingClassOrFunction(Node node)
    {
        for (var cur = node.Parent; cur is not null; cur = cur.Parent)
        {
            if (cur is FunctionNode or ClassNode)
            {
                return cur;
            }
        }

        return null;
    }

    private static bool HasAncestor(Node node, params NodeKind[] kinds)
    {
        for (var cur = (Node?)node; cur is not null; cur = cur.Parent)
        {
            if (kinds.Contains(cur.Kind))
            {
                return true;
            }
        }

        return false;
    }

    private static string? DocString(IReadOnlyList<Node> statements)
    {
        if (statements.Count > 0 && statements[0] is StringListNode { } list && list.Strings.All(s => !s.IsFormat))
        {
            return string.Concat(list.Strings.Select(s => s.Value));
        }

        return null;
    }

    // ------------------------------------------------------------------ module

    private bool VisitModule(ModuleNode node)
    {
        var package = GetPackageInfo(node, _module.ModuleName);
        if (package is null)
        {
            return true;
        }

        var symbol = _names.ModuleInit(package, _module.ModuleName);
        var occ = new Occurrence { SymbolRoles = (int)SymbolRole.Definition, Symbol = symbol };
        occ.Range.AddRange([0, 0, 0]);
        _doc.Occurrences.Add(occ);

        var documentation = new List<string> { $"(module) {_module.ModuleName}" };
        var doc = DocString(node.Statements);
        if (doc is not null)
        {
            documentation.Add(doc.Trim());
        }

        var info = new SymbolInformation { Symbol = symbol };
        info.Documentation.AddRange(documentation);
        _doc.Symbols.Add(info);
        return true;
    }

    // ------------------------------------------------------------------ annotations and assignments

    private bool VisitTypeAnnotation(TypeAnnotationNode node)
    {
        if (node.ValueExpression is NameNode name)
        {
            var decl = _eval.DeclarationsForName(_module, name).FirstOrDefault();
            if (decl?.Node is not null)
            {
                EmitSymbolInformationOnce(name, GetScipSymbol(decl.Node));
            }
        }

        return true;
    }

    private bool VisitAssignment(AssignmentNode node)
    {
        if (node.Targets.Count > 0 && node.Targets[0] is NameNode name)
        {
            var decls = _eval.DeclarationsForName(_module, name);
            if (decls.Count > 0 && decls[0].Node is { } declNode && declNode.Parent?.Id == node.Id)
            {
                EmitSymbolInformationOnce(declNode, GetScipSymbol(declNode), AssignmentDocumentation(node));
            }
        }

        return true;
    }

    /// <summary>Port of <c>TypeStubExtendedWriter.visitAssignment</c>: the full name of the assigned value's type.</summary>
    private List<string> AssignmentDocumentation(AssignmentNode node)
    {
        var line = "";
        var isTypeAlias = false;
        if (IsTypeVarFactoryCall(node.Value))
        {
            // getFullNameOfType(TypeVar) is undefined and the TypeVar call is treated like a type alias.
            line = "undefined";
            isTypeAlias = true;
        }
        else if (ValueTypeFullName(node.Value) is { } full)
        {
            line = full;
        }

        if (line.Length == 0)
        {
            return [];
        }

        if (isTypeAlias)
        {
            line += " = " + ExpressionPrinter.Print(node.Value, _module.Text);
        }

        return ["```python\n" + line + "\n```"];
    }

    private bool IsTypeVarFactoryCall(Node value) =>
        value is CallNode call &&
        _eval.EvalExpression(_module, call.Left) is ExternalEntity { IsClass: true, Module: "typing" or "typing_extensions", Name: "TypeVar" or "TypeVarTuple" or "ParamSpec" or "NewType" };

    private string SourceOf(Node n) =>
        _module.Text[Math.Clamp(n.Start, 0, _module.Text.Length)..Math.Clamp(n.End, 0, _module.Text.Length)];

    private string? ValueTypeFullName(Node value)
    {
        switch (value)
        {
            case StringListNode sl:
            {
                var prefix = sl.Strings.Count > 0 ? SourceOf(sl.Strings[0]).TakeWhile(char.IsLetter) : [];
                return prefix.Any(c => c is 'b' or 'B') ? "builtins.bytes" : "builtins.str";
            }

            case NumberNode n:
            {
                var src = SourceOf(n);
                if (src.EndsWith('j') || src.EndsWith('J'))
                {
                    return "builtins.complex";
                }

                var hex = src.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                return !hex && (src.Contains('.') || src.Contains('e') || src.Contains('E')) ? "builtins.float" : "builtins.int";
            }

            case ConstantNode { Text: "True" or "False" }:
                return "builtins.bool";
            case SequenceNode { Kind: NodeKind.List }:
                return "builtins.list";
            case SequenceNode { Kind: NodeKind.Tuple }:
                return "builtins.tuple";
            case SequenceNode { Kind: NodeKind.Set }:
                return "builtins.set";
            case DictionaryNode:
                return "builtins.dict";
            case CallNode call:
                switch (_eval.EvalExpression(_module, call.Left))
                {
                    case ClassEntity c:
                        return c.Module.ModuleName + "." + QualifiedClassName(c.Node);
                    case ExternalEntity { IsClass: true, Module: "builtins" } b:
                        return "builtins." + b.Name;
                    case null when IsUnknownExpression(call.Left):
                        return "typing.Any";
                }

                return null;
            case NameNode or MemberAccessNode or IndexNode when IsUnknownExpression(value):
                return "typing.Any";
        }

        return null;
    }

    private static string QualifiedClassName(ClassNode cls)
    {
        var name = cls.Name.Value;
        for (var cur = cls.Parent; cur is not null; cur = cur.Parent)
        {
            if (cur is ClassNode outer)
            {
                name = outer.Name.Value + "." + name;
            }
            else if (cur is FunctionNode)
            {
                return name;
            }
        }

        return name;
    }

    /// <summary>True when the expression is rooted in an import that cannot be resolved (pyright evaluates it to Unknown).</summary>
    private bool IsUnknownExpression(Node node, int depth = 0)
    {
        if (depth > 6)
        {
            return false;
        }

        switch (node)
        {
            case CallNode c:
                return IsUnknownExpression(c.Left, depth + 1);
            case MemberAccessNode m:
                return IsUnknownExpression(m.Left, depth + 1);
            case IndexNode i:
                return IsUnknownExpression(i.Left, depth + 1);
            case WrapperNode { Kind: NodeKind.Await, Expression: { } inner }:
                return IsUnknownExpression(inner, depth + 1);
            case NameNode n:
            {
                var decl = _eval.DeclarationsForName(_module, n).FirstOrDefault();
                if (decl is { Kind: DeclKind.Variable, Node: NameNode { Parent: AssignmentNode a } } && a.Targets.Count == 1 && a.Targets[0] == decl.Node)
                {
                    // a local bound once to an Unknown value
                    return IsUnknownExpression(a.Value, depth + 1);
                }

                if (decl is { Kind: DeclKind.Parameter, Node: ParameterNode { Annotation: null, Default: null, Category: ParamCategory.Simple, Parent: FunctionNode fn } prm })
                {
                    // an unannotated parameter without default is Unknown (except the implicit self/cls of a method)
                    return !(fn.Parameters.Count > 0 && fn.Parameters[0] == prm && EnclosingClass(fn, false) is not null);
                }

                return decl?.Kind == DeclKind.Alias && decl.Node switch
                {
                    ImportAsNode ia => _state.Modules.Resolve(ia.Module, _module) is null,
                    ImportFromAsNode { Parent: ImportFromNode f } => _state.Modules.Resolve(f.Module, _module) is null,
                    _ => false,
                };
            }
        }

        return false;
    }

    // ------------------------------------------------------------------ functions

    private List<Relationship>? FunctionRelationships(FunctionNode node)
    {
        if (node.Name.Value.StartsWith("__", StringComparison.Ordinal))
        {
            return null;
        }

        var cls = EnclosingClass(node, true);
        if (cls is null)
        {
            return null;
        }

        var entity = new ClassEntity(cls, _module);
        var found = _eval.ClassMembers(entity, node.Name.Value, [], skipOwn: true);
        var map = new Dictionary<string, Relationship>();
        foreach (var d in found.Where(d => d.Kind == DeclKind.Function))
        {
            var symbol = GetFunctionSymbol(d);
            if (!SymbolNames.IsLocal(symbol))
            {
                map[symbol] = new Relationship { Symbol = symbol, IsImplementation = true };
            }
        }

        return map.Count > 0 ? [.. map.Values] : null;
    }

    private bool VisitFunction(FunctionNode node)
    {
        var documentation = new List<string> { "```python\n" + FunctionStub(node) + "\n```" };
        var functionDoc = DocString(node.Suite.Statements);
        if (functionDoc is not null)
        {
            documentation.Add(functionDoc);
        }

        var info = new SymbolInformation { Symbol = GetScipSymbol(node) };
        info.Documentation.AddRange(documentation);
        if (FunctionRelationships(node) is { } rels)
        {
            info.Relationships.AddRange(rels);
        }

        _doc.Symbols.Add(info);

        foreach (var d in node.Decorators)
        {
            Walk(d);
        }

        VisitName(node.Name);
        if (node.ReturnAnnotation is not null)
        {
            Walk(node.ReturnAnnotation);
        }

        foreach (var p in node.Parameters)
        {
            var paramInfo = new SymbolInformation { Symbol = GetScipSymbol(p) };
            if (p.Name is not null && DocStrings.ExtractParameterDocumentation(functionDoc ?? "", p.Name.Value) is { Length: > 0 } paramDoc)
            {
                paramInfo.Documentation.Add(paramDoc);
            }

            _doc.Symbols.Add(paramInfo);
            Walk(p);
        }

        Walk(node.Suite);
        return false;
    }

    private string Text(Node n)
    {
        // Nodes parsed out of string (forward-reference) annotations can carry offsets outside the module text.
        var text = _module.Text;
        var start = Math.Clamp(n.Start, 0, text.Length);
        var end = Math.Clamp(n.End, start, text.Length);
        return text[start..end];
    }

    private string FunctionStub(FunctionNode node) => HoverStubs.Function(node, _module.Text);

    // ------------------------------------------------------------------ imports

    private bool VisitImport(ImportNode node)
    {
        foreach (var child in node.List)
        {
            Walk(child);
        }

        return false;
    }

    private bool VisitImportFrom(ImportFromNode node)
    {
        var symbol = GetScipSymbol(node);
        Push(node.Module, symbol, (int)SymbolRole.ReadAccess);
        var symbolPackage = ModuleNameToPackage(node.Module, null);
        if (symbolPackage == Stdlib)
        {
            EmitExternalSymbolInformation(symbol, []);
        }

        foreach (var imp in node.Imports)
        {
            Walk(imp);
        }

        return false;
    }

    private bool VisitImportFromAs(ImportFromAsNode node)
    {
        Push(node, GetScipSymbol(node), (int)SymbolRole.ReadAccess);
        return false;
    }

    private bool VisitImportAs(ImportAsNode node)
    {
        var moduleName = node.Module.Formatted;
        var resolved = _state.Modules.Resolve(node.Module, _module);
        var roles = (int)SymbolRole.ReadAccess;

        if (resolved?.FilePath is { } file && _state.Modules.IsUnderProject(file))
        {
            Push(node.Module, _names.ModuleInit(Project, moduleName), roles);
        }
        else
        {
            var package = ModuleNameToPackage(node.Module, resolved?.FilePath);
            if (package is not null)
            {
                var symbol = _names.ModuleInit(package, moduleName);
                Push(node.Module, symbol, roles);
                EmitExternalSymbolInformation(symbol, []);
            }
            else
            {
                var symbol = GetLocalForDeclaration(node, [$"(module): {moduleName} [unable to resolve module]"]);
                Push(node.Module, symbol, roles);
            }
        }

        if (node.Alias is not null)
        {
            Push(node.Alias, GetLocalForDeclaration(node.Alias));
        }

        return false;
    }

    // ------------------------------------------------------------------ names

    private bool VisitName(NameNode node)
    {
        if (node.Value == "_")
        {
            return true;
        }

        var decls = _eval.DeclarationsForName(_module, node);
        if (decls.Count == 0)
        {
            // Without a declaration, member accesses (and keyword arguments we could not resolve) get no occurrence.
            if (node.Parent is MemberAccessNode)
            {
                return true;
            }

            if (node.Parent is ArgumentNode { Parent: CallNode call } && !IsUnknownCallee(call))
            {
                return true;
            }

            // pyright's hover for a name without declaration: the Unknown type printed with the "function" label.
            Push(node, GetLocalForDeclaration(node, ["```python\n(function) " + node.Value + ": Unknown\n```"]));
            return true;
        }

        EmitDeclaration(node, decls[0]);
        return true;
    }

    /// <summary>True when pyright would evaluate the callee to Unknown (e.g. an unresolved import), so keyword arguments get locals.</summary>
    private bool IsUnknownCallee(CallNode call) => _eval.EvalExpression(_module, call.Left) is null;

    private void EmitDeclaration(NameNode node, Declaration decl)
    {
        var parent = node.Parent!;
        switch (decl.Kind)
        {
            case DeclKind.Builtin:
                EmitBuiltin(node, decl.Name);
                return;
            case DeclKind.External:
                EmitExternal(node, decl);
                return;
            case DeclKind.Intrinsic:
            {
                // pyright's intrinsic declarations belong to the module node: scip-python ends up using its namespace symbol.
                var module = _state.Modules.ModuleOf(node);
                var symbol = GetScipSymbol(module.Tree);
                Push(node, symbol);
                EmitExternalSymbolInformation(symbol, []);
                return;
            }
        }

        var declNode = decl.Node!;
        var isDefinition = declNode.Id == parent.Id;
        var existing = RawGet(declNode);
        if (existing is not null && !(isDefinition && declNode is ClassNode))
        {
            if (declNode.Id == parent.Id || declNode.Id == node.Id)
            {
                Push(node, existing, (int)SymbolRole.Definition, declNode is FunctionNode or ClassNode ? declNode : null);
            }
            else
            {
                Push(node, existing);
            }

            return;
        }

        switch (declNode)
        {
            case ImportAsNode ia:
            {
                if (ia.Alias is not null)
                {
                    Push(node, GetScipSymbol(ia.Alias));
                    return;
                }

                var moduleName = ia.Module.Formatted;
                var symbol = GetSymbolOnce(declNode, () =>
                {
                    var resolved = _state.Modules.Resolve(ia.Module, decl.Module!);
                    var package = ModuleNameToPackage(ia.Module, resolved?.FilePath);
                    return package is null ? GetLocalForDeclaration(node) : _names.ModuleInit(package, moduleName);
                });

                // `import a.b.c` ... `a.b.c.X`: one occurrence covers the whole dotted prefix that names a module.
                Node rangeNode = node;
                while (rangeNode.Parent is MemberAccessNode ma && ma.Left == rangeNode && IsSubmoduleAccess(ma))
                {
                    rangeNode = ma;
                }

                Push(rangeNode, symbol);
                return;
            }

            case FunctionNode when !isDefinition:
                Push(node, GetScipSymbol(declNode));
                return;
            case NameNode when HasAncestor(declNode, NodeKind.ComprehensionFor):
                GetLocalForDeclaration(declNode);
                break;
        }

        if (decl.Kind == DeclKind.Alias)
        {
            Push(node, AliasSymbol(decl, declNode));
            return;
        }

        if (isDefinition)
        {
            if (declNode is ClassNode cls)
            {
                EmitClassDefinition(node, cls);
            }
            else
            {
                Push(node, GetScipSymbol(declNode), (int)SymbolRole.Definition);
            }

            return;
        }

        if (declNode.Id == node.Id)
        {
            Push(node, GetScipSymbol(declNode), (int)SymbolRole.Definition);
            return;
        }

        var already = RawGet(declNode);
        Push(node, already ?? GetScipSymbol(declNode));
    }

    private bool IsSubmoduleAccess(MemberAccessNode ma)
    {
        var left = _eval.EvalExpression(_module, ma.Left);
        if (left is ModuleEntity { Dotted: "os", Remaining.Count: 0 } && ma.Member.Value == "path" && !_eval.ImportsSubmodule(_module, "os", "path"))
        {
            return false; // os/__init__.pyi declares `path`
        }

        return left is ModuleEntity m
            && (m.Remaining.Count > 0 && m.Remaining[0] == ma.Member.Value
                || m.Source is null && Hardcoded.IsStdlibSubmodule(m.Dotted + "." + ma.Member.Value));
    }

    private string AliasSymbol(Declaration decl, Node declNode)
    {
        if (declNode is ImportFromNode)
        {
            // wildcard import: the symbol is that of the imported declaration
            var (entity, resolved) = _eval.ResolveAlias(decl);
            if (resolved?.Node is { } n)
            {
                return GetScipSymbol(n);
            }

            if (entity is ModuleEntity m)
            {
                return _names.ModuleInit(ModuleNameToPackageByName(m.Dotted, m.Resolved?.FilePath) ?? Project, m.Dotted);
            }

            return GetLocalForDeclaration(declNode);
        }

        return GetScipSymbol(declNode);
    }

    private void EmitClassDefinition(NameNode node, ClassNode cls)
    {
        var symbol = GetScipSymbol(cls);
        var documentation = new List<string> { "```python\n" + ClassStub(cls) + "\n```" };
        var doc = DocString(cls.Suite.Statements)?.Trim();
        if (!string.IsNullOrEmpty(doc))
        {
            documentation.Add(doc);
        }

        var info = new SymbolInformation { Symbol = symbol };
        info.Documentation.AddRange(documentation);
        var entity = new ClassEntity(cls, _module);
        foreach (var arg in cls.Arguments.Where(a => a.Name is null && a.Category == 0))
        {
            var baseExpr = arg.Value is IndexNode ix ? ix.Left : arg.Value;
            var baseSymbol = _eval.EvalExpression(_module, baseExpr) switch
            {
                ClassEntity b => ClassSymbolOf(b),
                ExternalEntity { IsClass: true, Module: "builtins", Name: "object" } => null,
                ExternalEntity { IsClass: true } e => GuessPackage(e.Module, null) is { } p ? _names.Class(p, e.Module, e.Name) : null,
                _ => null,
            };
            if (baseSymbol is not null && info.Relationships.All(r => r.Symbol != baseSymbol))
            {
                info.Relationships.Add(new Relationship { Symbol = baseSymbol, IsImplementation = true });
            }
        }

        _ = entity;
        _doc.Symbols.Add(info);
        Push(node, symbol, (int)SymbolRole.Definition, cls);
    }

    private string? ClassSymbolOf(ClassEntity b)
    {
        var package = GuessPackage(b.Module.ModuleName, b.Module.Path);
        return package is null ? null : _names.Class(package, b.Module.ModuleName, b.Node.Name.Value);
    }

    private string ClassStub(ClassNode cls) => HoverStubs.Class(cls, _module.Text);

    private void EmitNameWithoutDeclaration(NameNode node) => Push(node, GetLocalForDeclaration(node));

    // ------------------------------------------------------------------ builtins and external symbols

    private void EmitBuiltin(NameNode node, string name)
    {
        if (Hardcoded.IsBuiltinClass(name))
        {
            var symbol = _names.Class(Stdlib, "builtins", name);
            Push(node, symbol);
            // A call node shows the constructor signature instead of the plain `(class)` header.
            var isCall = node.Parent is CallNode c && c.Left == node;
            EmitExternalSymbolInformation(symbol, isCall || name.StartsWith("__", StringComparison.Ordinal) ? [] : ["```python\n(class) " + name + "\n```"]);
        }
        else if (Hardcoded.OverloadedBuiltinFunctions.Contains(name))
        {
            var symbol = SymbolNames.Method(_names.Module(Stdlib, "builtins"), name);
            Push(node, symbol);
            EmitExternalSymbolInformation(symbol, []);
        }
        else
        {
            // typeshed declares these as plain functions; scip-python emits a fresh local symbol for each use.
            var local = NextLocal();
            Push(node, local);
            if (BuiltinHovers.Functions.TryGetValue(name, out var hover))
            {
                EmitSymbolInformationOnce(node, local, ["```python\n" + hover + "\n```"]);
            }
        }
    }

    private void EmitExternal(NameNode node, Declaration decl)
    {
        var package = GuessPackage(decl.ExternalModule, null);
        if (package is null)
        {
            Push(node, GetLocalForDeclaration(node));
            return;
        }

        if (decl.ExternalModule is "typing" or "typing_extensions" && decl.Name is "overload" or "final" or "type_check_only")
        {
            // Functions that typeshed's builtins.pyi imports resolve through the builtins scope: a fresh local per use.
            var local = NextLocal();
            Push(node, local);
            EmitSymbolInformationOnce(node, local, decl.Name == "overload" ? [.. BuiltinHovers.Overload] : []);
            return;
        }

        if (decl.ExternalClass.Length > 0)
        {
            var methodSymbol = SymbolNames.Method(_names.Class(Stdlib, "builtins", decl.ExternalClass), decl.Name);
            Push(node, methodSymbol);
            EmitExternalSymbolInformation(methodSymbol, []);
            return;
        }

        var symbol = ExternalSymbol(package, decl.ExternalModule, decl.Name, node.Parent is MemberAccessNode m && m.Member == node);
        Push(node, symbol);
        EmitExternalSymbolInformation(symbol, []);
    }

    /// <summary>
    /// Symbol for a stdlib/untyped-module member. Without typeshed we cannot know the declaration kind, so
    /// CamelCase names are classes, typing special forms and CONSTANTS are terms, everything else a function.
    /// </summary>
    private readonly Dictionary<string, string> _stdlibLocals = new(StringComparer.Ordinal);

    private string ExternalSymbol(PythonPackage package, string module, string name, bool memberAccess = false)
    {
        // scip-python 0.6.8 resolves every member of `os.path` to the first declaration of ntpath's star import: `ntpath/join()`.
        if (module == "os.path")
        {
            return SymbolNames.Method(_names.Module(Stdlib, "ntpath"), "join");
        }

        if (module == "os" && name == "path")
        {
            return SymbolNames.Term(_names.Module(package, module), name);
        }

        if (Hardcoded.StdlibDefiningModule(module, name) is { } definingModule)
        {
            module = definingModule;
        }

        // Only for `typing.Callable`-style member access: names imported with `from typing import X` keep their typing symbol.
        if (memberAccess && Hardcoded.IsBuiltinsImportedClass(module, name))
        {
            // typeshed's builtins.pyi imports these, and pyright resolves them through the builtins scope.
            return _names.Class(Stdlib, "builtins", name);
        }

        // Re-exports: collections.abc and typing_extensions forward to the declarations in typing.
        if (module is "collections.abc" or "_collections_abc" && Hardcoded.TypingNames.Contains(name) ||
            module == "typing_extensions" && Hardcoded.TypingExtensionsReexports.Contains(name))
        {
            module = "typing";
            package = Stdlib;
        }
        else if (package == Stdlib && (module.Contains('.') && !Hardcoded.StdlibModuleNames.Contains(module) || Hardcoded.IsTypeshedOnlyStdlibModule(module)))
        {
            // pyright names the declaring module `urllib.parse`, which is not in scip-python's (top level) stdlib list, so the
            // declaration gets no package: one local per declaration and document.
            var key = module + "/" + name;
            if (!_stdlibLocals.TryGetValue(key, out var local))
            {
                _stdlibLocals[key] = local = NextLocal();
            }

            return local;
        }

        if (Hardcoded.IsStdlibLowercaseClass(module, name) ||
            (Hardcoded.LooksLikeClass(name) && !Hardcoded.IsTypingSpecialForm(module, name) && module != "__future__"))
        {
            return _names.Class(package, module, name);
        }

        if (module == "__future__" || Hardcoded.IsStdlibAnnotatedVariable(module, name))
        {
            // Variables declared with an annotation at the top level of a stub: the annotated name is a term in the term of the name.
            var owner = SymbolNames.Term(_names.Module(package, module), name);
            return SymbolNames.Term(owner, name);
        }

        if (Hardcoded.IsTypingSpecialForm(module, name) || name.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_'))
        {
            return SymbolNames.Term(_names.Module(package, module), name);
        }

        return SymbolNames.Method(_names.Module(package, module), name);
    }

    private void EmitExternalSymbolInformation(string symbol, List<string> documentation)
    {
        if (SymbolNames.IsLocal(symbol))
        {
            return;
        }

        if (_state.ExternalSymbols.TryGetValue(symbol, out var existing) && existing.Documentation.Count > 0)
        {
            return;
        }

        var info = new SymbolInformation { Symbol = symbol };
        info.Documentation.AddRange(documentation);
        _state.ExternalSymbols[symbol] = info;
    }

    // ------------------------------------------------------------------ symbol caches

    private string? RawGet(Node node) =>
        _state.GlobalSymbols.TryGetValue(node, out var g) ? g : _documentSymbols.GetValueOrDefault(node);

    private void RawSet(Node node, string symbol, bool isLocal)
    {
        if (isLocal)
        {
            _documentSymbols[node] = symbol;
        }
        else
        {
            _state.GlobalSymbols[node] = symbol;
        }
    }

    private string GetSymbolOnce(Node node, Func<string> finder)
    {
        var existing = RawGet(node);
        if (existing is not null)
        {
            return existing;
        }

        var symbol = finder();
        RawSet(node, symbol, SymbolNames.IsLocal(symbol));
        return symbol;
    }

    private string GetLocalForDeclaration(Node node, List<string>? documentation = null)
    {
        var existing = RawGet(node);
        if (existing is not null)
        {
            return existing;
        }

        var symbol = NextLocal();
        RawSet(node, symbol, true);
        EmitSymbolInformationOnce(node, symbol, documentation);
        return symbol;
    }

    private void EmitSymbolInformationOnce(Node node, string symbol, List<string>? documentation = null)
    {
        if (!_symbolInfoEmitted.Add(symbol))
        {
            return;
        }

        if (documentation is null && node is NameNode { Parent: TypeAnnotationNode ta } n && ta.ValueExpression == n)
        {
            documentation = ["```python\n(variable) " + n.Value + ": " + Text(ta.Annotation) + "\n```"];
        }

        if (documentation is null)
        {
            return;
        }

        var info = new SymbolInformation { Symbol = symbol };
        info.Documentation.AddRange(documentation);
        _doc.Symbols.Add(info);
    }

    // ------------------------------------------------------------------ packages

    private PythonPackage? GetPackageInfo(Node node, string moduleName)
    {
        var path = _state.Modules.ModuleOf(node).Path;
        if (_state.Modules.IsUnderProject(path))
        {
            return Project;
        }

        return _state.Modules.Environment.GetPackageForModule(moduleName);
    }

    private PythonPackage? ModuleNameToPackage(ModuleNameNode node, string? declPath)
    {
        if (node.LeadingDots > 0)
        {
            return Project;
        }

        return GuessPackage(node.Formatted, declPath);
    }

    private PythonPackage? ModuleNameToPackageByName(string moduleName, string? path) => GuessPackage(moduleName, path);

    public PythonPackage? GuessPackage(string moduleName, string? declPath)
    {
        if (moduleName == "builtins")
        {
            return Stdlib;
        }

        if (moduleName.StartsWith('.'))
        {
            return Project;
        }

        if (!string.IsNullOrEmpty(declPath) && _state.Modules.IsUnderProject(declPath))
        {
            return Project;
        }

        var env = _state.Modules.Environment;
        var package = env.GetPackageForModule(moduleName) ?? env.GuessPackage(moduleName);
        if (package is not null)
        {
            return package;
        }

        return Hardcoded.StdlibModuleNames.Contains(moduleName.Split('.')[0]) || Hardcoded.IsTypeshedOnlyStdlibModule(moduleName) ? Stdlib : null;
    }

    // ------------------------------------------------------------------ symbol construction (makeScipSymbol)

    private string GetScipSymbol(Node node)
    {
        var existing = RawGet(node);
        if (existing is not null)
        {
            return existing;
        }

        var moduleName = _state.Modules.ModuleOf(node).ModuleName;
        PythonPackage? package;
        if (Hardcoded.StdlibModuleNames.Contains(moduleName))
        {
            var stdSymbol = MakeScipSymbol(Stdlib, moduleName, node);
            EmitExternalSymbolInformation(stdSymbol, []);
            return stdSymbol;
        }

        package = GetPackageInfo(node, moduleName);
        if (package is null)
        {
            var again = RawGet(node);
            if (again is not null)
            {
                return again;
            }

            var local = NextLocal();
            RawSet(node, local, true);
            EmitSymbolInformationOnce(node, local);
            return local;
        }

        var symbol = MakeScipSymbol(package, moduleName, node);
        RawSet(node, symbol, true);
        return symbol;
    }

    private string MakeScipSymbol(PythonPackage package, string moduleName, Node node)
    {
        switch (node)
        {
            case ModuleNode:
                return _names.Module(package, _state.Modules.ModuleOf(node).ModuleName);
            case ModuleNameNode mn:
            {
                if (mn.NameParts.Count == 0)
                {
                    return NextLocal();
                }

                if (Hardcoded.StdlibModuleNames.Contains(mn.NameParts[0].Value))
                {
                    package = Stdlib;
                }

                return _names.ModuleInit(package, string.Join('.', mn.NameParts.Select(p => p.Value)));
            }

            case MemberAccessNode:
                return "";
            case ParameterNode p:
                return p.Name is null ? NextLocal() : SymbolNames.Parameter(GetScipSymbol(p.Parent!), p.Name.Value);
            case ClassNode c:
                return SymbolNames.Type(GetScipSymbol(c.Parent!), c.Name.Value);
            case FunctionNode f:
            {
                var cls = EnclosingClass(f, false);
                return SymbolNames.Method(GetScipSymbol((Node?)cls ?? f.Parent!), f.Name.Value);
            }

            case SuiteNode s:
                return s.Parent is not null ? GetScipSymbol(s.Parent) : "";
            case NameNode n:
                return MakeNameSymbol(n);
            case TypeAnnotationNode ta:
                // scip-python 0.6.8: a (module level) annotated name is a term nested in the term of the same name (`x.x.`).
                return ta.ValueExpression is NameNode vn
                    ? SymbolNames.Term(GetScipSymbol((Node?)EnclosingSuite(ta) ?? ta.Parent!), vn.Value)
                    : NextLocal();
            case DecoratorNode:
                return NextLocal();
            case AssignmentNode a:
            {
                if (EnclosingClassOrFunction(a) is not null)
                {
                    return package.Version.Length > 0 ? GetScipSymbol(a.Parent!) : NextLocal();
                }

                return GetScipSymbol(a.Parent!);
            }

            case ImportAsNode:
                return _names.ModuleInit(package, moduleName);
            case ImportFromNode ifn:
            {
                var importPackage = ModuleNameToPackage(ifn.Module, null);
                return importPackage is null ? GetScipSymbol(ifn.Module) : MakeScipSymbol(importPackage, ifn.Module.Formatted, ifn.Module);
            }

            case ImportFromAsNode ifa:
                return ImportFromAsSymbol(ifa);
            case LambdaNode or ComprehensionForNode:
                return NextLocal();
            case ComprehensionNode:
                return "";
            default:
                return node.Parent is null ? NextLocal() : GetScipSymbol(node.Parent);
        }
    }

    private string ImportFromAsSymbol(ImportFromAsNode node)
    {
        var module = _state.Modules.ModuleOf(node);
        var parent = (ImportFromNode)node.Parent!;
        var decl = new Declaration { Kind = DeclKind.Alias, Node = node, Module = module };
        var (entity, resolved) = _eval.ResolveAlias(decl);
        if (resolved?.Node is { } rn && rn.Id != node.Id)
        {
            return GetScipSymbol(rn);
        }

        switch (entity)
        {
            case ModuleEntity m:
            {
                if (node.Alias is not null)
                {
                    // `from pkg import module as alias`: scip-python 0.6.8 gives the alias a local symbol.
                    return NextLocal();
                }

                var package = ModuleNameToPackage(parent.Module, null) ?? Project;
                var parts = parent.Module.NameParts.Select(p => p.Value).Append(node.Name.Value);
                _ = m;
                return _names.ModuleInit(package, string.Join('.', parts));
            }

            case ExternalEntity e:
            {
                var package = GuessPackage(e.Module, null);
                return package is null ? NextLocal() : ExternalSymbol(package, e.Module, e.Name);
            }

            case ClassEntity or FunctionEntity:
                return NextLocal();
        }

        return NextLocal();
    }

    private string MakeNameSymbol(NameNode node)
    {
        var parent = node.Parent ?? throw new InvalidOperationException("must have parent");
        if (parent is MemberAccessNode ma && ma.Member == node)
        {
            var module = _state.Modules.ModuleOf(node);
            if (_eval.EvalExpression(module, ma.Left) is SelfEntity self)
            {
                return GetSymbolOnce(node, () =>
                {
                    var boundModule = self.Class.Module.ModuleName;
                    var package = GetPackageInfo(self.Class.Node, boundModule);
                    if (package is null)
                    {
                        return NextLocal();
                    }

                    var symbol = SymbolNames.Term(SymbolNames.Type(_names.Module(package, boundModule), self.Class.Node.Name.Value), node.Value);
                    EmitSymbolInformationOnce(node, symbol);
                    return symbol;
                });
            }
        }

        var suite = EnclosingSuite(node);
        if (suite is not null && (EnclosingFunction(node) is not null || EnclosingLambda(node) is not null))
        {
            return NextLocal();
        }

        var scope = (Node?)suite ?? parent;
        var owner = GetScipSymbol(scope);
        // Names bound inside a comprehension (e.g. walrus targets) have no enclosing global symbol: use a local.
        return owner.Length == 0 ? NextLocal() : SymbolNames.Term(owner, node.Value);
    }

    private string GetFunctionSymbol(Declaration decl)
    {
        var fn = (FunctionNode)decl.Node!;
        var module = decl.Module!;
        var package = GuessPackage(module.ModuleName, module.Path);
        if (package is null)
        {
            return NextLocal();
        }

        var cls = EnclosingClass(fn, false);
        if (cls is not null)
        {
            var classPackage = GuessPackage(module.ModuleName, module.Path)!;
            return SymbolNames.Method(_names.Class(classPackage, module.ModuleName, cls.Name.Value), fn.Name.Value);
        }

        return SymbolNames.Method(_names.Module(package, module.ModuleName), fn.Name.Value);
    }
}
