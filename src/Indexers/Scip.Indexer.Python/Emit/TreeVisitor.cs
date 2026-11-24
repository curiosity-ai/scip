#pragma warning disable CS0618 // Occurrence.Range / EnclosingRange are deprecated in scip.proto but are what scip-python emits.
using System.Text;
using Scip.Indexer.Python.Semantics;
using Scip.Indexer.Python.Syntax;
using SuffixKind = Scip.SymbolDescriptor.Types.Suffix;

namespace Scip.Indexer.Python.Emit;

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
                EmitSymbolInformationOnce(declNode, GetScipSymbol(declNode), []);
            }
        }

        return true;
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
            _doc.Symbols.Add(new SymbolInformation { Symbol = GetScipSymbol(p) });
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

    private string FunctionStub(FunctionNode node)
    {
        var sb = new StringBuilder();
        sb.Append(node.IsAsync ? "async def " : "def ").Append(node.Name.Value).Append('(');
        var parms = node.Parameters.Select(p =>
        {
            var s = p.Category switch { ParamCategory.ArgsList => "*", ParamCategory.KwargsDict => "**", _ => "" };
            if (p.Name is null)
            {
                return p.Category == ParamCategory.Simple ? "/" : s;
            }

            s += p.Name.Value;
            if (p.Annotation is not null)
            {
                s += ": " + Text(p.Annotation);
            }

            if (p.Default is not null)
            {
                s += " = " + Text(p.Default);
            }

            return s;
        }).ToList();
        if (parms.Count > 0)
        {
            sb.Append("\n  ").Append(string.Join(",\n  ", parms)).Append('\n');
        }

        sb.Append(')');
        if (node.ReturnAnnotation is not null)
        {
            sb.Append(" -> ").Append(Text(node.ReturnAnnotation));
        }

        return sb.Append(':').ToString();
    }

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
        Push(node.Module, symbol, (int)SymbolRole.Import | (int)SymbolRole.ReadAccess);
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
        Push(node, GetScipSymbol(node), (int)SymbolRole.Import | (int)SymbolRole.ReadAccess);
        return false;
    }

    private bool VisitImportAs(ImportAsNode node)
    {
        var moduleName = node.Module.Formatted;
        var resolved = _state.Modules.Resolve(node.Module, _module);
        var roles = (int)SymbolRole.Import | (int)SymbolRole.ReadAccess;

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
            if (node.Parent is MemberAccessNode or ArgumentNode)
            {
                return true;
            }

            Push(node, GetLocalForDeclaration(node));
            return true;
        }

        EmitDeclaration(node, decls[0]);
        return true;
    }

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

    private string ClassStub(ClassNode cls)
    {
        var args = cls.Arguments.Count > 0 ? "(" + string.Join(", ", cls.Arguments.Select(Text)) + ")" : "";
        return $"class {cls.Name.Value}{args}:";
    }

    private void EmitNameWithoutDeclaration(NameNode node) => Push(node, GetLocalForDeclaration(node));

    // ------------------------------------------------------------------ builtins and external symbols

    private void EmitBuiltin(NameNode node, string name)
    {
        if (Hardcoded.IsBuiltinClass(name))
        {
            var symbol = _names.Class(Stdlib, "builtins", name);
            Push(node, symbol);
            EmitExternalSymbolInformation(symbol, []);
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
            Push(node, NextLocal());
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

        var symbol = ExternalSymbol(package, decl.ExternalModule, decl.Name);
        Push(node, symbol);
        EmitExternalSymbolInformation(symbol, []);
    }

    /// <summary>
    /// Symbol for a stdlib/untyped-module member. Without typeshed we cannot know the declaration kind, so
    /// CamelCase names are classes, typing special forms and CONSTANTS are terms, everything else a function.
    /// </summary>
    private string ExternalSymbol(PythonPackage package, string module, string name)
    {
        if (Hardcoded.LooksLikeClass(name) && !Hardcoded.IsTypingSpecialForm(module, name) && module != "__future__")
        {
            return _names.Class(package, module, name);
        }

        if (module == "__future__" || Hardcoded.IsTypingSpecialForm(module, name) || name.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_'))
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

        return Hardcoded.StdlibModuleNames.Contains(moduleName.Split('.')[0]) ? Stdlib : null;
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
                return ta.ValueExpression is NameNode ? GetScipSymbol(ta.ValueExpression) : NextLocal();
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

        var scope = suite ?? (parent is TypeAnnotationNode ? parent.Parent! : parent);
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
