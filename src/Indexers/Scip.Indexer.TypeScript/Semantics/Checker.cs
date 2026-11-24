using Scip.Indexer.TypeScript.Syntax;

namespace Scip.Indexer.TypeScript.Semantics;

/// <summary>
/// Name resolution and a deliberately small "type" model: instead of full type inference we compute, for an expression,
/// the declarations that provide its members (<c>containers</c>). That covers declared types, <c>new X()</c>,
/// <c>this</c>, imports and call results with declared return types — enough to resolve member accesses to the
/// declarations SCIP symbols are built from. Anything needing real inference (generics, control flow, lib types) is
/// not resolved and yields no occurrence.
/// </summary>
public sealed class Checker(TsProgram program)
{
    public TsProgram Program { get; } = program;

    private Binder B(Node n) => Program.BinderFor(n.File!);

    private const int MaxDepth = 40;
    private const int MaxNest = 160;
    [ThreadStatic] private static int _nest;
    [ThreadStatic] private static HashSet<(Node, int)>? _activeSet;
    private static HashSet<(Node, int)> Active => _activeSet ??= [];

    private readonly Dictionary<Node, Node> _staticSides = [];

    /// <summary>Synthetic "typeof Class" container: the static side (constructor function) of a class.</summary>
    private Node StaticSide(Node cls)
    {
        if (!_staticSides.TryGetValue(cls, out var w))
        {
            w = new Node(SK.TypeQuery, cls.Start) { Expression = cls, File = cls.File, Parent = cls.Parent };
            _staticSides[cls] = w;
        }

        return w;
    }

    // ------------------------------------------------------------------ symbol at location
    private static bool IsDeclarationName(Node parent, Node node) =>
        ReferenceEquals(parent.Name, node) && parent.Kind is SK.VariableDeclaration or SK.Parameter or SK.FunctionDeclaration or SK.FunctionExpression
            or SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration or SK.TypeAliasDeclaration or SK.EnumDeclaration or SK.EnumMember
            or SK.ModuleDeclaration or SK.TypeParameter or SK.PropertyDeclaration or SK.PropertySignature or SK.MethodDeclaration or SK.MethodSignature
            or SK.GetAccessor or SK.SetAccessor or SK.PropertyAssignment or SK.ShorthandPropertyAssignment or SK.BindingElement or SK.ImportSpecifier
            or SK.ImportClause or SK.NamespaceImport or SK.ImportEqualsDeclaration or SK.NamedTupleMember or SK.NamespaceExport;

    /// <summary>Returns the declarations of the symbol at <paramref name="node"/>, de-aliasing imports like upstream does.</summary>
    public List<Node>? SymbolAtLocation(Node node)
    {
        var parent = node.Parent;
        if (parent is null) { return null; }
        if (node.Kind == SK.StringLiteral) { return StringLiteralSymbol(node, parent); }

        if (IsDeclarationName(parent, node))
        {
            switch (parent.Kind)
            {
                case SK.ImportSpecifier or SK.ImportClause or SK.NamespaceImport or SK.ImportEqualsDeclaration:
                    return ResolveAlias(parent) ?? [parent];
                case SK.NamespaceExport:
                    return [parent];
                default:
                    return [parent];
            }
        }

        switch (parent.Kind)
        {
            case SK.PropertyAccessExpression when ReferenceEquals(parent.Name, node):
                return MemberDecls(ContainersOfExpression(parent.Expression!, 0), Name(node));
            case SK.QualifiedName when ReferenceEquals(parent.Right, node):
                return MemberDecls(ContainersOfEntity(parent.Left!, Meaning.Any, 0), node.Text);
            case SK.ImportSpecifier when ReferenceEquals(parent.PropertyName, node):
                return ResolveAlias(parent) ?? [parent];
            case SK.ExportSpecifier:
                return ExportSpecifierSymbol(parent);
            case SK.BindingElement when ReferenceEquals(parent.PropertyName, node):
                return MemberDecls(ContainersOfBindingParent(parent, 0), node.Text);
            case SK.MetaProperty or SK.LabeledStatement or SK.BreakStatement or SK.ContinueStatement or SK.JsxAttribute:
                return null;
            case SK.JsxElement when char.IsLower(node.Text[0]) && !node.Text.Contains('.'):
                return null; // intrinsic element (div, p, ...): needs JSX.IntrinsicElements, not modelled
            case SK.ImportType when ReferenceEquals(parent.Name, node):
                return null;
        }

        return LookupIdentifier(node);
    }

    private static string Name(Node n) => n.Text;

    private List<Node>? StringLiteralSymbol(Node node, Node parent)
    {
        switch (parent.Kind)
        {
            case SK.ImportDeclaration or SK.ExportDeclaration when ReferenceEquals(parent.ModuleSpecifier, node):
            case SK.ExternalModuleReference:
            case SK.ImportCall when parent.Arguments.Count > 0 && ReferenceEquals(parent.Arguments[0], node):
                return ModuleRoot(node);
            case SK.CallExpression when parent.Expression is { Kind: SK.Identifier, Text: "require" } && node.File!.IsJavaScript &&
                parent.Arguments.Count == 1 && ReferenceEquals(parent.Arguments[0], node):
                return ModuleRoot(node);
            case SK.ElementAccessExpression when ReferenceEquals(parent.Right, node):
                return MemberDecls(ContainersOfExpression(parent.Expression!, 0), node.Text);
            case SK.ComputedPropertyName:
                return null;
        }

        if (IsDeclarationName(parent, node)) { return [parent]; }
        return null;
    }

    private List<Node>? ModuleRoot(Node specifier)
    {
        var target = Program.ResolveModule(specifier.File!, specifier.Text);
        return target is null ? null : [target.Root];
    }

    private List<Node>? ExportSpecifierSymbol(Node spec)
    {
        var exportDecl = spec.Parent!.Parent!;
        var name = (spec.PropertyName ?? spec.Name)!.Text;
        if (exportDecl.ModuleSpecifier is { } ms)
        {
            var target = Program.ResolveModule(spec.File!, ms.Text);
            return target is null ? null : LookupExport(target, name, []);
        }

        var local = B(spec).Lookup(spec, name, Meaning.Any);
        return local is null ? null : DeAlias(local, 0);
    }

    private bool IsTypePosition(Node node)
    {
        var p = node.Parent;
        var child = node;
        while (p is not null && (p.Kind == SK.QualifiedName || (p.Kind == SK.PropertyAccessExpression && p.Parent?.Kind == SK.ExpressionWithTypeArguments)))
        {
            child = p;
            p = p.Parent;
        }

        _ = child;
        if (p is null) { return false; }
        switch (p.Kind)
        {
            case SK.TypeReference: return true;
            case SK.ExpressionWithTypeArguments:
                // class extends X -> value; implements / interface extends -> type
                var clause = p.Parent!;
                return !(clause.IsExtends && clause.Parent!.Kind is SK.ClassDeclaration or SK.ClassExpression);
            case SK.TypePredicate or SK.TypeQuery: return false;
            default: return p.Kind >= SK.TypeReference && p.Kind <= SK.RestType;
        }
    }

    private List<Node>? LookupIdentifier(Node node)
    {
        var meaning = IsTypePosition(node) ? Meaning.Type : (node.Parent!.Kind is SK.ExportAssignment ? Meaning.Any : Meaning.Value);
        var decls = B(node).Lookup(node, node.Text, meaning);
        return decls is null ? null : DeAlias(decls, 0);
    }

    // ------------------------------------------------------------------ aliases and exports
    /// <summary>Follows import aliases (not namespace imports, which upstream keeps as the alias).</summary>
    public List<Node> DeAlias(List<Node> decls, int depth)
    {
        if (depth > MaxDepth) { return decls; }
        var result = new List<Node>();
        foreach (var d in decls)
        {
            if (d.Kind is SK.ImportSpecifier or SK.ImportClause or SK.ImportEqualsDeclaration)
            {
                var target = ResolveAlias(d, depth + 1);
                if (target is not null) { result.AddRange(target); continue; }
            }
            else if (d.Kind == SK.VariableDeclaration && RequireAliasTarget(d) is { } required)
            {
                result.AddRange(required);
                continue;
            }

            result.Add(d);
        }

        return result;
    }

    public List<Node>? ResolveAlias(Node alias, int depth = 0)
    {
        if (depth > MaxDepth) { return null; }
        TsSourceFile? target;
        string name;
        switch (alias.Kind)
        {
            case SK.ImportSpecifier:
            {
                var decl = alias.Parent!.Parent!.Parent!;
                target = Program.ResolveModule(alias.File!, decl.ModuleSpecifier!.Text);
                name = (alias.PropertyName ?? alias.Name)!.Text;
                break;
            }

            case SK.ImportClause:
                target = Program.ResolveModule(alias.File!, alias.Parent!.ModuleSpecifier!.Text);
                name = "default";
                break;
            case SK.NamespaceImport:
                target = Program.ResolveModule(alias.File!, alias.Parent!.Parent!.ModuleSpecifier!.Text);
                return target is null ? null : [target.Root];
            case SK.ImportEqualsDeclaration:
                if (alias.Expression is { Kind: SK.ExternalModuleReference } ext)
                {
                    target = Program.ResolveModule(alias.File!, ext.Expression!.Text);
                    return target is null ? null : [target.Root];
                }

                return ResolveEntityExpression(alias.Expression!, depth + 1);
            default:
                return null;
        }

        return target is null ? null : LookupExport(target, name, []);
    }

    private List<Node>? ResolveEntityExpression(Node entity, int depth)
    {
        if (_nest >= MaxNest || !Active.Add((entity, 1))) { return null; }
        _nest++;
        try { return ResolveEntityExpressionCore(entity, depth); }
        finally { _nest--; Active.Remove((entity, 1)); }
    }

    private List<Node>? ResolveEntityExpressionCore(Node entity, int depth)
    {
        if (entity.Kind == SK.Identifier)
        {
            var d = B(entity).Lookup(entity, entity.Text, Meaning.Any);
            return d is null ? null : DeAlias(d, depth);
        }

        var left = entity.Kind == SK.QualifiedName ? entity.Left : entity.Expression;
        var right = entity.Kind == SK.QualifiedName ? entity.Right : entity.Name;
        if (left is null || right is null) { return null; }
        var leftDecls = ResolveEntityExpression(left, depth + 1);
        return leftDecls is null ? null : MemberDecls(ContainersOfDecls(leftDecls, depth + 1), right.Text);
    }

    public List<Node>? LookupExport(TsSourceFile file, string name, HashSet<TsSourceFile> visited, bool keepAlias = false)
    {
        if (!visited.Add(file)) { return null; }
        var b = Program.BinderFor(file);
        var table = b.ExportTable(file.Root);
        if (table is not null)
        {
            if (table.TryGetValue(name, out var local)) { return ExpandExport(file, local); }
            if (name == "default" && table.TryGetValue("export=", out var eq)) { return ExpandExport(file, eq); }
        }

        foreach (var exportDecl in b.ExportDeclarations(file.Root))
        {
            if (exportDecl.Clause is { Kind: SK.NamedExports } named)
            {
                foreach (var spec in named.Elements)
                {
                    if (spec.Name!.Text != name) { continue; }
                    var original = (spec.PropertyName ?? spec.Name).Text;
                    if (keepAlias)
                    {
                        // Member access through a namespace sees the alias symbol (the export specifier) itself.
                        if (exportDecl.ModuleSpecifier is { } alias ? Program.ResolveModule(file, alias.Text) is not null : b.Lookup(spec, original, Meaning.Any) is not null) { return [spec]; }
                    }
                    else if (exportDecl.ModuleSpecifier is { } ms)
                    {
                        var target = Program.ResolveModule(file, ms.Text);
                        var r = target is null ? null : LookupExport(target, original, visited);
                        if (r is not null) { return r; }
                    }
                    else
                    {
                        var local2 = b.Lookup(spec, original, Meaning.Any);
                        if (local2 is not null) { return DeAlias(local2, 0); }
                    }
                }
            }
            else if (exportDecl.Clause is { Kind: SK.NamespaceExport } nsExport && nsExport.Name!.Text == name && exportDecl.ModuleSpecifier is { } nms)
            {
                var target = Program.ResolveModule(file, nms.Text);
                if (target is not null) { return keepAlias ? [nsExport] : [target.Root]; }
            }
        }

        foreach (var exportDecl in b.ExportDeclarations(file.Root))
        {
            if (exportDecl.Clause is null && exportDecl.ModuleSpecifier is { } ms && name != "default")
            {
                var target = Program.ResolveModule(file, ms.Text);
                var r = target is null ? null : LookupExport(target, name, visited, keepAlias);
                if (r is not null) { return r; }
            }
        }

        return null;
    }

    /// <summary>
    /// A JavaScript <c>var x = require("./m")</c> is an alias of the module: references resolve to what the module exports
    /// (its <c>module.exports = ...</c> assignment, or the module itself).
    /// </summary>
    private List<Node>? RequireAliasTarget(Node declaration)
    {
        if (declaration.File is not { IsJavaScript: true } || declaration.Name is not { Kind: SK.Identifier }) { return null; }
        if (declaration.Initializer is not { Kind: SK.CallExpression, Expression: { Kind: SK.Identifier, Text: "require" }, Arguments: [{ Kind: SK.StringLiteral } spec] }) { return null; }
        var target = Program.ResolveModule(declaration.File, spec.Text);
        if (target is null) { return null; }
        var table = Program.BinderFor(target).ExportTable(target.Root);
        return table is not null && table.TryGetValue("export=", out var assignment) ? ExpandExport(target, [assignment[^1]]) : [target.Root];
    }

    private List<Node> ExpandExport(TsSourceFile file, List<Node> decls)
    {
        var result = new List<Node>();
        foreach (var d in decls)
        {
            if (d.Kind == SK.BinaryExpression && d.Right is { Kind: SK.Identifier } rhs)
            {
                // `module.exports = createApplication` aliases the declaration.
                var local = Program.BinderFor(file).Lookup(rhs, rhs.Text, Meaning.Any);
                if (local is not null) { result.AddRange(DeAlias(local, 0)); continue; }
            }

            if (d.Kind == SK.ExportAssignment)
            {
                if (d.Expression is { Kind: SK.Identifier } id)
                {
                    var local = Program.BinderFor(file).Lookup(id, id.Text, Meaning.Any);
                    if (local is not null) { result.AddRange(DeAlias(local, 0)); continue; }
                }

                result.Add(d);
            }
            else
            {
                result.AddRange(DeAlias([d], 0));
            }
        }

        return result;
    }

    // ------------------------------------------------------------------ the type model
    //
    // A "container" (<see cref="Cont"/>) is a declaration that provides members (class, interface, type literal, object
    // literal, namespace, module, mapped type, function) together with the environment that binds the declaration's type
    // parameters (and `this`) to the type arguments it was referenced with. That is just enough of the compiler's type
    // model to resolve member accesses through generics, inheritance, call results, awaited promises, array elements,
    // contextual typing, ... to the declarations SCIP symbols are built from.

    private static readonly Node Opaque = new(SK.KeywordType, 0) { Text = "any" };
    private static readonly Node ThisKey = new(SK.ThisType, 0);

    private int _cuts;
    private int _groups;
    private readonly Dictionary<Node, List<Cont>> _valueCache = [];
    private readonly Dictionary<Node, Dictionary<string, List<Node>>> _memberIndex = [];

    /// <summary>Wraps a guarded computation: recursion guard, nesting limit and cut accounting for result caching.</summary>
    private List<Cont> Guarded(Node key, int tag, int depth, Func<List<Cont>> compute)
    {
        if (depth > MaxDepth || _nest >= MaxNest || !Active.Add((key, tag)))
        {
            _cuts++;
            return [];
        }

        _nest++;
        try { return compute(); }
        finally { _nest--; Active.Remove((key, tag)); }
    }

    private static List<Cont> Real(List<Cont> conts) => conts.Any(c => ReferenceEquals(c.Decl, Opaque)) ? conts.Where(c => !ReferenceEquals(c.Decl, Opaque)).ToList() : conts;

    // ---- lib
    private List<Cont> LibConts(params string[] names)
    {
        var result = new List<Cont>();
        foreach (var n in names)
        {
            foreach (var d in Program.Libs.TypeDecls(n)) { result.Add(new Cont(d, null)); }
        }

        return result;
    }

    private List<Cont> KeywordConts(string keyword) => keyword switch
    {
        "string" => LibConts("String"),
        "number" => LibConts("Number"),
        "boolean" => LibConts("Boolean"),
        "symbol" => LibConts("Symbol"),
        "bigint" => LibConts("BigInt"),
        "object" => LibConts("Object"),
        "any" or "unknown" => [new Cont(Opaque, null)],
        _ => [],
    };

    private List<Cont> FunctionConts() => LibConts("CallableFunction", "Function");

    private List<Cont> ArrayConts(Func<int, List<Cont>> element, bool readOnly = false)
    {
        var result = new List<Cont>();
        foreach (var d in Program.Libs.TypeDecls(readOnly ? "ReadonlyArray" : "Array"))
        {
            result.Add(new Cont(d, d.TypeParameters.Count > 0 ? new Env(d.TypeParameters[0], element, null) : null));
        }

        return result;
    }

    // ---- environments
    private Env? BindArgs(Node decl, List<Node> args, Env? callerEnv)
    {
        Env? env = null;
        for (var i = 0; i < decl.TypeParameters.Count; i++)
        {
            var tp = decl.TypeParameters[i];
            if (i < args.Count)
            {
                var arg = args[i];
                env = new Env(tp, d => TypeConts(arg, callerEnv, d), env);
            }
            else if (tp.Initializer is { } def)
            {
                var captured = env;
                env = new Env(tp, d => TypeConts(def, captured, d), env);
            }
        }

        return env;
    }

    // ------------------------------------------------------------------ declared types -> containers
    private List<Cont> ContainersOfDecls(List<Node> decls, int depth, bool asValue = false)
    {
        var result = new List<Cont>();
        foreach (var d in decls) { result.AddRange(DeclConts(d, null, depth, asValue)); }
        return result;
    }

    public List<Cont> ContainersOfEntity(Node entity, Meaning meaning, int depth)
    {
        if (depth > MaxDepth) { return []; }
        var decls = EntityDecls(entity, meaning, depth);
        return decls is null ? [] : ContainersOfDecls(decls, depth + 1, meaning == Meaning.Value);
    }

    private List<Node>? EntityDecls(Node entity, Meaning meaning, int depth)
    {
        if (entity.Kind == SK.Identifier)
        {
            var decls = B(entity).Lookup(entity, entity.Text, meaning);
            return decls is null ? null : DeAlias(decls, 0);
        }

        return ResolveEntityExpression(entity, depth + 1);
    }

    private List<Cont> TypeDeclConts(Node d, List<Node> args, Env? env, int depth)
    {
        switch (d.Kind)
        {
            case SK.TypeParameter:
                if (env?.Find(d) is { } bound) { return bound(depth + 1); }
                return d.Type is null ? [new Cont(Opaque, null)] : TypeConts(d.Type, env, depth + 1);
            case SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration:
                return [new Cont(d, BindArgs(d, args, env))];
            case SK.TypeAliasDeclaration:
                return TypeConts(d.Type!, BindArgs(d, args, env), depth + 1);
            case SK.ImportSpecifier or SK.ImportClause or SK.NamespaceImport or SK.ImportEqualsDeclaration:
            {
                var target = ResolveAlias(d);
                var result = new List<Cont>();
                foreach (var t in target ?? []) { result.AddRange(TypeDeclConts(t, args, env, depth + 1)); }
                return result;
            }

            default:
                return DeclConts(d, null, depth + 1, asValue: false);
        }
    }

    public List<Cont> TypeConts(Node t, Env? env, int depth) => Guarded(t, 3, depth, () => TypeContsCore(t, env, depth));

    private List<Cont> TypeContsCore(Node t, Env? env, int depth)
    {
        switch (t.Kind)
        {
            case SK.TypeReference:
            {
                var decls = EntityDecls(t.Name!, Meaning.Type, depth);
                var result = new List<Cont>();
                foreach (var d in decls ?? []) { result.AddRange(TypeDeclConts(d, t.TypeArguments, env, depth)); }
                return result;
            }

            case SK.ExpressionWithTypeArguments:
            {
                var decls = EntityDecls(t.Expression!, Meaning.Any, depth);
                var result = new List<Cont>();
                foreach (var d in decls ?? []) { result.AddRange(TypeDeclConts(d, t.TypeArguments, env, depth)); }
                return result;
            }

            case SK.TypeLiteral or SK.MappedType or SK.FunctionType:
                return [new Cont(t, env)];
            case SK.ConstructorType:
                return [new Cont(t, env)];
            case SK.ParenthesizedType or SK.OptionalType or SK.RestType or SK.NamedTupleMember:
                return t.Type is null ? [] : TypeConts(t.Type, env, depth + 1);
            case SK.UnionType:
                return t.Elements.SelectMany(e => TypeConts(e, env, depth + 1)).Select(c => c with { Alt = true }).ToList();
            case SK.IntersectionType:
            {
                var group = ++_groups;
                return t.Elements.SelectMany(e => TypeConts(e, env, depth + 1)).Select(c => c with { Group = group }).ToList();
            }

            case SK.TypeQuery:
            {
                var decls = EntityDecls(t.Expression!, Meaning.Value, depth);
                return decls is null ? [] : ContainersOfDecls(decls, depth + 1, asValue: true);
            }

            case SK.ThisType:
                return env?.Find(ThisKey) is { } self ? self(depth + 1) : EnclosingThisContainers(t);
            case SK.KeywordType:
                return KeywordConts(t.Text);
            case SK.LiteralType:
                return t.Expression switch
                {
                    { Kind: SK.StringLiteral } => LibConts("String"),
                    { Kind: SK.NumericLiteral } or { Kind: SK.PrefixUnaryExpression } => LibConts("Number"),
                    { Kind: SK.KeywordLiteral, Text: "true" or "false" } => LibConts("Boolean"),
                    _ => [],
                };
            case SK.TemplateLiteralType:
                return LibConts("String");
            case SK.ArrayType:
            {
                var elem = t.Type!;
                return ArrayConts(d => TypeConts(elem, env, d));
            }

            case SK.TupleType:
            {
                var elements = t.Elements;
                return ArrayConts(d => elements.SelectMany(e => TypeConts(e, env, d)).ToList());
            }

            case SK.TypeOperator when t.Text == "readonly":
            {
                if (t.Type is { Kind: SK.ArrayType } arr) { return ArrayConts(d => TypeConts(arr.Type!, env, d), readOnly: true); }
                if (t.Type is { Kind: SK.TupleType } tup) { return ArrayConts(d => tup.Elements.SelectMany(e => TypeConts(e, env, d)).ToList(), readOnly: true); }
                return TypeConts(t.Type!, env, depth + 1);
            }

            case SK.IndexedAccessType:
            {
                var objects = TypeConts(t.Left!, env, depth + 1);
                if (t.Right is { Kind: SK.LiteralType, Expression: { Kind: SK.StringLiteral or SK.NumericLiteral } lit })
                {
                    return HitsToConts(Members(objects, lit.Text, depth + 1), depth + 1);
                }

                return IndexValue(objects, depth + 1);
            }

            case SK.ConditionalType:
                return TypeConts(t.Then!, env, depth + 1).Concat(TypeConts(t.Else!, env, depth + 1)).Select(c => c with { Alt = true }).ToList();
            case SK.ImportType when t.Name is null && t.Extra is { Kind: SK.LiteralType, Expression: { } lit }:
            {
                var target = Program.ResolveModule(t.File!, lit.Text);
                return target is null ? [] : [new Cont(target.Root, null)];
            }

            default:
                return [];
        }
    }

    private List<Cont> EnclosingThisContainers(Node from)
    {
        for (var n = from.Parent; n is not null; n = n.Parent)
        {
            switch (n.Kind)
            {
                case SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration:
                    return [new Cont(n, null)];
                case SK.ObjectLiteralExpression:
                    return [new Cont(n, null)];
                case SK.FunctionDeclaration or SK.FunctionExpression:
                    return [];
            }
        }

        return [];
    }

    /// <summary>The type of declaration <paramref name="d"/> as containers (value side when <paramref name="asValue"/>).</summary>
    public List<Cont> DeclConts(Node d, Env? env, int depth, bool asValue)
    {
        if (env is null && asValue && _declCache.TryGetValue(d, out var cached)) { return cached; }
        var before = _cuts;
        var result = Guarded(d, 2, depth, () => DeclContsCore(d, env, depth, asValue));
        if (env is null && asValue && _cuts == before) { _declCache[d] = result; }
        return result;
    }

    private readonly Dictionary<Node, List<Cont>> _declCache = [];

    private List<Cont> DeclContsCore(Node d, Env? env, int depth, bool asValue)
    {
        switch (d.Kind)
        {
            case SK.ClassDeclaration or SK.ClassExpression:
                return asValue ? [new Cont(StaticSide(d), null)] : [new Cont(d, env)];
            case SK.InterfaceDeclaration:
                return [new Cont(d, env)];
            case SK.EnumDeclaration or SK.ModuleDeclaration or SK.TsSourceFile or SK.TypeLiteral or SK.ObjectLiteralExpression:
                return [new Cont(d, env)];
            case SK.TypeAliasDeclaration or SK.TypeParameter:
                return TypeDeclConts(d, [], env, depth + 1);
            case SK.VariableDeclaration or SK.Parameter or SK.PropertyDeclaration or SK.PropertySignature:
            {
                if (d.Type is not null) { return TypeConts(d.Type, env, depth + 1); }
                if (d.File is { IsJavaScript: true } && JsDocType(d) is { } jsType) { return TypeConts(jsType, null, depth + 1); }
                if (d.Initializer is not null) { return ValueConts(d.Initializer, depth + 1); }
                if (d.Kind == SK.Parameter && d.Parent is { } fn && fn.Kind != SK.TsSourceFile) { return ContextualParameterConts(d, depth + 1); }
                if (d.Kind == SK.VariableDeclaration && d.Parent is { Kind: SK.VariableDeclarationList, Parent: { Kind: SK.ForOfStatement or SK.ForInStatement } loop })
                {
                    return loop.Kind == SK.ForInStatement ? LibConts("String") : IndexValue(ValueConts(loop.Expression!, depth + 1), depth + 1);
                }

                return [];
            }

            case SK.GetAccessor:
                if (d.Type is not null) { return TypeConts(d.Type, env, depth + 1); }
                return d.Body is not null ? ReturnedConts(d, depth + 1) : [];
            case SK.SetAccessor:
                return d.Parameters.Count > 0 && d.Parameters[0].Type is { } st ? TypeConts(st, env, depth + 1) : [];
            case SK.PropertyAssignment:
                return d.Initializer is null ? [] : ValueConts(d.Initializer, depth + 1);
            case SK.ShorthandPropertyAssignment:
            {
                var decls = B(d).Lookup(d.Name!, d.Name!.Text, Meaning.Value);
                return decls is null ? [] : ContainersOfDecls(DeAlias(decls, 0), depth + 1, asValue: true);
            }

            case SK.MethodDeclaration or SK.MethodSignature or SK.FunctionDeclaration or SK.FunctionExpression or SK.ArrowFunction
                or SK.CallSignature or SK.ConstructSignature or SK.Constructor:
                return [new Cont(d, env)];
            case SK.BindingElement:
                return BindingElementConts(d, depth + 1);
            case SK.ImportSpecifier or SK.ImportClause or SK.NamespaceImport or SK.ImportEqualsDeclaration:
            {
                var target = ResolveAlias(d);
                return target is null ? [] : ContainersOfDecls(target, depth + 1, asValue);
            }

            case SK.ExportSpecifier:
            {
                var target = ExportSpecifierSymbol(d);
                return target is null ? [] : ContainersOfDecls(target, depth + 1, asValue);
            }

            case SK.NamespaceExport:
            {
                var target = Program.ResolveModule(d.File!, d.Parent!.ModuleSpecifier!.Text);
                return target is null ? [] : [new Cont(target.Root, null)];
            }

            case SK.ExportAssignment:
                return d.Expression is null ? [] : ValueConts(d.Expression, depth + 1);
            case SK.BinaryExpression when d.Text == "=" && d.Right is not null:
                return ValueConts(d.Right, depth + 1);
            default:
                return [];
        }
    }

    private List<Cont> BindingElementConts(Node element, int depth)
    {
        var pattern = element.Parent!;
        var owner = ContainersOfBindingOwner(pattern, depth);
        if (pattern.Kind == SK.ArrayBindingPattern) { return IndexValue(owner, depth + 1); }
        var key = element.PropertyName ?? element.Name;
        var name = Binder.NameText(key);
        return name is null ? [] : HitsToConts(Members(owner, name, depth + 1), depth + 1);
    }

    private List<Cont> ContainersOfBindingOwner(Node pattern, int depth)
    {
        var owner = pattern.Parent!;
        if (owner.Kind == SK.BindingElement) { return BindingElementConts(owner, depth + 1); }
        if (owner.Type is not null) { return TypeConts(owner.Type, null, depth + 1); }
        if (owner.Initializer is not null) { return ValueConts(owner.Initializer, depth + 1); }
        if (owner.Kind == SK.Parameter) { return ContextualParameterConts(owner, depth + 1); }
        if (owner.Kind == SK.VariableDeclaration && owner.Parent is { Kind: SK.VariableDeclarationList, Parent: { Kind: SK.ForOfStatement } loop }) { return IndexValue(ValueConts(loop.Expression!, depth + 1), depth + 1); }
        return [];
    }

    public List<Cont> ContainersOfBindingParent(Node bindingElement, int depth) => ContainersOfBindingOwner(bindingElement.Parent!, depth);

    // ------------------------------------------------------------------ members
    private static bool MemberNameMatches(Node member, string name) => Binder.NameText(member.Name) == name;

    private static bool IsParameterProperty(Node p) => (p.Modifiers & (Mods.Public | Mods.Private | Mods.Protected | Mods.Readonly | Mods.Override)) != 0;

    /// <summary>A member declaration found on a container, with the environment of that container.</summary>
    public readonly record struct Hit(Node Decl, Env? Env, Cont Receiver);

    private Dictionary<string, List<Node>> IndexOf(Node decl)
    {
        if (_memberIndex.TryGetValue(decl, out var index)) { return index; }
        index = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
        void Add(string? name, Node member)
        {
            if (name is null) { return; }
            if (!index.TryGetValue(name, out var l)) { index[name] = l = []; }
            l.Add(member);
        }

        foreach (var m in decl.Kind == SK.ObjectLiteralExpression ? decl.Elements : decl.Members)
        {
            if (m.Kind is SK.IndexSignature or SK.CallSignature or SK.ConstructSignature or SK.ClassStaticBlock or SK.Constructor) { continue; }
            if (decl.Kind == SK.ObjectLiteralExpression && m.Kind is not (SK.PropertyAssignment or SK.ShorthandPropertyAssignment or SK.MethodDeclaration or SK.GetAccessor or SK.SetAccessor)) { continue; }
            Add(Binder.NameText(m.Name), m);
        }

        if (decl.Kind is SK.ClassDeclaration or SK.ClassExpression)
        {
            foreach (var ctor in decl.Members.Where(m => m.Kind == SK.Constructor))
            {
                foreach (var p in ctor.Parameters)
                {
                    if (IsParameterProperty(p) && p.Name?.Kind == SK.Identifier) { Add(p.Name.Text, p); }
                }
            }
        }

        _memberIndex[decl] = index;
        return index;
    }

    public List<Node>? MemberDecls(List<Cont> containers, string name)
    {
        var hits = Members(containers, name, 0);
        return hits.Count == 0 ? null : hits.Select(h => h.Decl).Distinct().ToList();
    }

    public List<Hit> Members(List<Cont> containers, string name, int depth) => Members(containers, name, [], depth);

    private List<Hit> Members(List<Cont> containers, string name, HashSet<Cont> visited, int depth)
    {
        if (depth > MaxDepth || ++_nest > MaxNest) { _nest--; _cuts++; return []; }
        try { return MembersCore(containers, name, visited, depth); }
        finally { _nest--; }
    }

    private List<Hit> MembersCore(List<Cont> containers, string name, HashSet<Cont> visited, int depth)
    {
        var alternatives = new List<Hit>();
        foreach (var c in containers)
        {
            if (ReferenceEquals(c.Decl, Opaque) || !visited.Add(c)) { continue; }
            var found = new List<Hit>();
            var decl = c.Decl;
            switch (decl.Kind)
            {
                case SK.TsSourceFile:
                {
                    var export = LookupExport(decl.File!, name, [], keepAlias: true);
                    foreach (var e in export ?? []) { found.Add(new Hit(e, null, c)); }
                    if (found.Count == 0 && B(decl).ExportTable(decl)?.GetValueOrDefault("export=") is { } equals)
                    {
                        // `export = x` / `module.exports = x`: the module's members are the members of x.
                        found.AddRange(Members(ContainersOfDecls(ExpandExport(decl.File!, equals), depth + 1, asValue: true), name, visited, depth + 1));
                    }

                    break;
                }

                case SK.ModuleDeclaration:
                {
                    if (decl.Body is { Kind: SK.ModuleDeclaration } inner && inner.Name!.Text == name) { found.Add(new Hit(inner, null, c)); }
                    var table = B(decl).ExportTable(decl);
                    if (table is not null && table.TryGetValue(name, out var l)) { foreach (var e in DeAlias(l, 0)) { found.Add(new Hit(e, null, c)); } }
                    break;
                }

                case SK.TypeQuery when decl.Expression is { Kind: SK.ClassDeclaration or SK.ClassExpression } cls:
                {
                    foreach (var m in cls.Members)
                    {
                        if (m.Kind is SK.IndexSignature or SK.CallSignature or SK.ConstructSignature or SK.ClassStaticBlock or SK.Constructor || !m.Has(Mods.Static)) { continue; }
                        if (MemberNameMatches(m, name)) { found.Add(new Hit(m, null, c)); }
                    }

                    if (found.Count == 0) { found.AddRange(Members(HeritageConts(cls, null, depth + 1, staticSide: true), name, visited, depth + 1)); }
                    if (found.Count == 0 && cls.HeritageClauses.Count == 0) { found.AddRange(Members(LibConts("NewableFunction", "CallableFunction", "Function"), name, visited, depth + 1)); }
                    break;
                }

                case SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration or SK.TypeLiteral or SK.EnumDeclaration or SK.ObjectLiteralExpression:
                {
                    if (IndexOf(decl).TryGetValue(name, out var members))
                    {
                        foreach (var m in members)
                        {
                            if (decl.Kind is SK.ClassDeclaration or SK.ClassExpression && m.Has(Mods.Static)) { continue; }
                            found.Add(new Hit(m, c.Env, c));
                        }
                    }

                    if (found.Count == 0 && decl.Kind is not (SK.EnumDeclaration or SK.ObjectLiteralExpression))
                    {
                        found.AddRange(Members(HeritageConts(decl, c.Env, depth + 1), name, visited, depth + 1));
                    }

                    if (found.Count == 0 && decl.Kind == SK.TypeLiteral && decl.Members.Any(m => m.Kind == SK.CallSignature))
                    {
                        found.AddRange(Members(FunctionConts(), name, visited, depth + 1));
                    }

                    // Declarations of one merged interface/class (e.g. `interface Array<T>` in several lib files) all belong
                    // to the same symbol: collect the member from each of them.
                    if (found.Count > 0 && decl.Kind is SK.InterfaceDeclaration or SK.ClassDeclaration && decl.Name is not null)
                    {
                        foreach (var other in containers)
                        {
                            if (other.Decl.Kind != decl.Kind || other.Decl.Name?.Text != decl.Name.Text || ReferenceEquals(other.Decl, decl) || !visited.Add(other)) { continue; }
                            if (IndexOf(other.Decl).TryGetValue(name, out var more))
                            {
                                foreach (var m in more)
                                {
                                    if (decl.Kind == SK.ClassDeclaration && m.Has(Mods.Static)) { continue; }
                                    found.Add(new Hit(m, other.Env, other));
                                }
                            }
                        }
                    }

                    break;
                }

                case SK.MappedType:
                    // Homomorphic mapped types (`{ [P in keyof T]: ... }`, Partial, Required, Pick, Omit) keep T's declarations.
                    found.AddRange(Members(MappedSourceConts(decl, c.Env, depth + 1), name, visited, depth + 1));
                    break;
                case SK.FunctionType or SK.ConstructorType or SK.FunctionDeclaration or SK.FunctionExpression or SK.ArrowFunction or SK.MethodDeclaration or SK.MethodSignature:
                    found.AddRange(Members(FunctionConts(), name, visited, depth + 1));
                    break;
            }

            if (found.Count == 0) { continue; }
            if (!c.Alt && c.Group == 0) { return alternatives.Count == 0 ? found : [.. alternatives, .. found]; }
            alternatives.AddRange(found);
        }

        return alternatives;
    }

    private List<Cont> MappedSourceConts(Node mapped, Env? env, int depth)
    {
        var constraint = mapped.Clause?.Type;
        for (var i = 0; i < 4 && constraint is not null; i++)
        {
            if (constraint is { Kind: SK.TypeOperator, Text: "keyof" } keyof) { return TypeConts(keyof.Type!, env, depth + 1).Select(c => c with { Derived = true }).ToList(); }
            if (constraint is { Kind: SK.TypeReference } reference && reference.Name is { Kind: SK.Identifier } nm)
            {
                var decl = B(reference).Lookup(reference, nm.Text, Meaning.Type)?.FirstOrDefault(x => x.Kind == SK.TypeParameter);
                constraint = decl?.Type;
                continue;
            }

            break;
        }

        return [];
    }

    public List<Cont> HeritageConts(Node classOrInterface, Env? env, int depth, bool staticSide = false)
    {
        var result = new List<Cont>();
        foreach (var clause in classOrInterface.HeritageClauses)
        {
            var isClassExtends = clause.IsExtends && classOrInterface.Kind is SK.ClassDeclaration or SK.ClassExpression;
            foreach (var e in clause.Elements)
            {
                var decls = EntityDecls(e.Expression!, isClassExtends ? Meaning.Value : Meaning.Type, depth + 1);
                foreach (var d in decls ?? [])
                {
                    if (isClassExtends && staticSide)
                    {
                        result.AddRange(DeclConts(d, null, depth + 1, asValue: true));
                    }
                    else if (d.Kind is SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration)
                    {
                        result.Add(new Cont(d, BindArgs(d, e.TypeArguments, env)));
                    }
                    else if (d.Kind == SK.TypeAliasDeclaration)
                    {
                        result.AddRange(TypeDeclConts(d, e.TypeArguments, env, depth + 1));
                    }
                    else if (isClassExtends)
                    {
                        // `class X extends mixin(Base)` / `extends core.$constructor<T>`: instance type from construct signatures.
                        foreach (var c in DeclConts(d, null, depth + 1, asValue: true)) { result.AddRange(ConstructResult(c, e.TypeArguments, env, depth + 1)); }
                    }
                }
            }
        }

        return result;
    }

    private List<Cont> ConstructResult(Cont c, List<Node> typeArguments, Env? callerEnv, int depth)
    {
        var result = new List<Cont>();
        var decl = c.Decl;
        if (decl.Kind == SK.TypeQuery && decl.Expression is { Kind: SK.ClassDeclaration or SK.ClassExpression } cls)
        {
            result.Add(new Cont(cls, BindArgs(cls, typeArguments, callerEnv)));
            return result;
        }

        if (decl.Kind is SK.InterfaceDeclaration or SK.TypeLiteral or SK.ClassDeclaration)
        {
            foreach (var sig in decl.Members.Where(m => m.Kind == SK.ConstructSignature && m.Type is not null))
            {
                var env = sig.TypeParameters.Count > 0 ? BindArgs(sig, typeArguments, callerEnv) : null;
                var merged = env is null ? c.Env : env;
                result.AddRange(TypeConts(sig.Type!, MergeEnv(merged, c.Env), depth + 1));
            }

            if (result.Count == 0 && decl.Kind == SK.InterfaceDeclaration && decl.HeritageClauses.Count > 0)
            {
                foreach (var b in HeritageConts(decl, c.Env, depth + 1)) { result.AddRange(ConstructResult(b, typeArguments, callerEnv, depth + 1)); }
            }
        }

        return result;
    }

    private static Env? MergeEnv(Env? primary, Env? secondary)
    {
        if (primary is null) { return secondary; }
        if (secondary is null || ReferenceEquals(primary, secondary)) { return primary; }
        return new Env(primary.Param, primary.Value, MergeEnv(primary.Next, secondary));
    }

    private List<Cont> HitsToConts(List<Hit> hits, int depth)
    {
        var result = new List<Cont>();
        foreach (var h in hits)
        {
            var receiver = h.Receiver;
            var env = new Env(ThisKey, _ => [receiver], h.Env);
            result.AddRange(DeclConts(h.Decl, env, depth + 1, asValue: true));
        }

        return result;
    }

    /// <summary>Element/index value type of array-likes, index signatures and mapped types.</summary>
    private List<Cont> IndexValue(List<Cont> containers, int depth)
    {
        var result = new List<Cont>();
        foreach (var c in containers)
        {
            var d = c.Decl;
            if (d.Kind is SK.InterfaceDeclaration or SK.ClassDeclaration && d.Name?.Text is "Array" or "ReadonlyArray" or "Set" or "ReadonlySet" or "Iterable" or "IterableIterator" or "Iterator" or "AsyncIterable" or "Generator" or "NodeListOf" or "HTMLCollectionOf")
            {
                if (d.TypeParameters.Count > 0 && c.Env?.Find(d.TypeParameters[0]) is { } v) { result.AddRange(v(depth + 1)); }
                continue;
            }

            if (d.Kind == SK.MappedType) { if (d.Type is not null) { result.AddRange(TypeConts(d.Type, c.Env, depth + 1)); } continue; }
            if (d.Kind is SK.InterfaceDeclaration or SK.TypeLiteral or SK.ClassDeclaration)
            {
                foreach (var sig in d.Members.Where(m => m.Kind == SK.IndexSignature && m.Type is not null)) { result.AddRange(TypeConts(sig.Type!, c.Env, depth + 1)); }
                if (result.Count == 0 && d.Kind != SK.TypeLiteral)
                {
                    result.AddRange(IndexValue(HeritageConts(d, c.Env, depth + 1), depth + 1));
                }
            }
        }

        return result;
    }

    private List<Cont> UnwrapPromise(List<Cont> containers, int depth)
    {
        var result = new List<Cont>();
        foreach (var c in containers)
        {
            var d = c.Decl;
            if (d.Kind == SK.InterfaceDeclaration && d.Name?.Text is "Promise" or "PromiseLike")
            {
                if (d.TypeParameters.Count > 0 && c.Env?.Find(d.TypeParameters[0]) is { } v) { result.AddRange(v(depth + 1)); }
                continue;
            }

            result.Add(c);
        }

        return result;
    }

    // ------------------------------------------------------------------ expressions -> containers
    /// <summary>The containers (members providers) of expression <paramref name="e"/>.</summary>
    public List<Cont> ContainersOfExpression(Node e, int depth) => ValueConts(e, depth);

    public List<Cont> ValueConts(Node e, int depth)
    {
        if (_valueCache.TryGetValue(e, out var cached)) { return cached; }
        var before = _cuts;
        var result = Guarded(e, 4, depth, () => ValueContsCore(e, depth));
        if (_cuts == before) { _valueCache[e] = result; }
        return result;
    }

    private List<Cont> ValueContsCore(Node e, int depth)
    {
        switch (e.Kind)
        {
            case SK.ParenthesizedExpression or SK.NonNullExpression or SK.SatisfiesExpression:
                return ValueConts(e.Expression!, depth + 1);
            case SK.AsExpression or SK.TypeAssertion:
                return e.Type is { Kind: SK.KeywordType, Text: "const" } ? ValueConts(e.Expression!, depth + 1) : TypeConts(e.Type!, null, depth + 1);
            case SK.Identifier:
            {
                var decls = SymbolAtLocation(e);
                return decls is null ? [] : ContainersOfDecls(decls, depth + 1, asValue: true);
            }

            case SK.This:
                return EnclosingThisContainers(e);
            case SK.Super:
            {
                var cls = EnclosingClass(e);
                return cls is null ? [] : HeritageConts(cls, null, depth + 1).Where(c => c.Decl.Kind is SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration).ToList();
            }

            case SK.PropertyAccessExpression:
            {
                var receivers = ValueConts(e.Expression!, depth + 1);
                return HitsToConts(Members(receivers, e.Name!.Text, depth + 1), depth + 1);
            }

            case SK.ElementAccessExpression:
            {
                var receivers = ValueConts(e.Expression!, depth + 1);
                if (e.Right is { Kind: SK.StringLiteral } lit) { return HitsToConts(Members(receivers, lit.Text, depth + 1), depth + 1); }
                return IndexValue(receivers, depth + 1);
            }

            case SK.NewExpression:
            {
                var result = new List<Cont>();
                foreach (var c in ValueConts(e.Expression!, depth + 1))
                {
                    var found = ConstructResult(c, e.TypeArguments, null, depth + 1);
                    if (found.Count == 0 && c.Decl.Kind == SK.InterfaceDeclaration) { found = [c]; }
                    result.AddRange(found);
                }

                return result;
            }

            case SK.CallExpression:
                return CallResultConts(e, depth + 1);
            case SK.ObjectLiteralExpression:
                return [new Cont(e, null)];
            case SK.ClassExpression:
                return [new Cont(StaticSide(e), null)];
            case SK.ConditionalExpression:
                return [.. ValueConts(e.Then!, depth + 1), .. ValueConts(e.Else!, depth + 1)];
            case SK.AwaitExpression:
                return UnwrapPromise(ValueConts(e.Expression!, depth + 1), depth + 1);
            case SK.StringLiteral or SK.TemplateExpression:
                return LibConts("String");
            case SK.NumericLiteral:
                return LibConts("Number");
            case SK.RegexLiteral:
                return LibConts("RegExp");
            case SK.KeywordLiteral when e.Text is "true" or "false":
                return LibConts("Boolean");
            case SK.ArrayLiteralExpression:
            {
                var elements = e.Elements;
                return ArrayConts(d => elements.Where(x => x.Kind is not (SK.SpreadElement or SK.OmittedExpression)).Take(3).SelectMany(x => ValueConts(x, d)).ToList());
            }

            case SK.FunctionExpression or SK.ArrowFunction:
                return [new Cont(e, null)];
            case SK.TypeOfExpression:
                return LibConts("String");
            case SK.PrefixUnaryExpression:
                return e.Text switch { "!" => LibConts("Boolean"), "++" or "--" or "-" or "+" or "~" => LibConts("Number"), _ => [] };
            case SK.PostfixUnaryExpression:
                return LibConts("Number");
            case SK.BinaryExpression:
                switch (e.Text)
                {
                    case "||" or "??" or "&&":
                        return [.. ValueConts(e.Left!, depth + 1), .. ValueConts(e.Right!, depth + 1)];
                    case "==" or "===" or "!=" or "!==" or "<" or ">" or "<=" or ">=" or "instanceof" or "in":
                        return LibConts("Boolean");
                    case "-" or "*" or "/" or "%" or "**" or "&" or "|" or "^" or "<<" or ">>" or ">>>":
                        return LibConts("Number");
                    case "+":
                    {
                        var l = ValueConts(e.Left!, depth + 1);
                        var r = ValueConts(e.Right!, depth + 1);
                        if (IsLib(l, "String") || IsLib(r, "String")) { return LibConts("String"); }
                        return IsLib(l, "Number") && IsLib(r, "Number") ? LibConts("Number") : [];
                    }

                    case "=" or ",":
                        return ValueConts(e.Right!, depth + 1);
                    default:
                        return [];
                }

            default:
                return [];
        }
    }

    private static bool IsLib(List<Cont> containers, string name) => containers.Count > 0 && containers.All(c => c.Decl.File!.IsLib && c.Decl.Name?.Text == name);

    private static Node? EnclosingClass(Node n)
    {
        for (var p = n.Parent; p is not null; p = p.Parent)
        {
            if (p.Kind is SK.ClassDeclaration or SK.ClassExpression) { return p; }
        }

        return null;
    }

    // ---- calls
    private List<Cont> CallResultConts(Node call, int depth)
    {
        var callee = call.Expression!;
        if (callee is { Kind: SK.Identifier, Text: "require" } && call.Arguments is [{ Kind: SK.StringLiteral } spec] && call.File!.IsJavaScript)
        {
            var target = Program.ResolveModule(call.File, spec.Text);
            return target is null ? [] : [new Cont(target.Root, null)];
        }

        var result = new List<Cont>();
        foreach (var c in ValueConts(callee, depth + 1)) { result.AddRange(CallResult(c, call, depth + 1)); }
        return result;
    }

    private List<Cont> CallResult(Cont c, Node? call, int depth)
    {
        var d = c.Decl;
        switch (d.Kind)
        {
            case SK.FunctionDeclaration or SK.MethodDeclaration or SK.MethodSignature or SK.ArrowFunction or SK.FunctionExpression or SK.FunctionType
                or SK.CallSignature or SK.GetAccessor:
                return ReturnConts(d, c.Env, call, depth + 1);
            case SK.InterfaceDeclaration or SK.TypeLiteral or SK.ClassDeclaration:
            {
                var result = new List<Cont>();
                foreach (var sig in d.Members.Where(m => m.Kind == SK.CallSignature)) { result.AddRange(ReturnConts(sig, c.Env, call, depth + 1)); }
                if (result.Count == 0 && d.Kind == SK.InterfaceDeclaration)
                {
                    foreach (var b in HeritageConts(d, c.Env, depth + 1)) { result.AddRange(CallResult(b, call, depth + 1)); }
                }

                return result;
            }

            default:
                return [];
        }
    }

    private List<Cont> ReturnConts(Node fn, Env? env, Node? call, int depth)
    {
        var bound = env;
        if (fn.TypeParameters.Count > 0)
        {
            if (call is not null && call.TypeArguments.Count > 0) { bound = MergeEnv(BindArgs(fn, call.TypeArguments, null), env); }
            else if (call is not null) { bound = MergeEnv(InferTypeArguments(fn, call, depth), env); }
        }

        if (fn.Type is not null)
        {
            if (fn.Type.Kind == SK.TypePredicate) { return LibConts("Boolean"); }
            return TypeConts(fn.Type, bound, depth + 1);
        }

        if (fn.Body is null) { return []; }
        var returned = ReturnedConts(fn, depth + 1);
        if (fn.Has(Mods.Async))
        {
            var captured = returned;
            var promise = new List<Cont>();
            foreach (var d in Program.Libs.TypeDecls("Promise"))
            {
                promise.Add(new Cont(d, d.TypeParameters.Count > 0 ? new Env(d.TypeParameters[0], _ => captured, null) : null));
            }

            return promise;
        }

        return returned;
    }

    /// <summary>Binds type parameters that appear directly as a parameter type to the containers of the matching argument.</summary>
    private Env? InferTypeArguments(Node fn, Node call, int depth)
    {
        Env? env = InferFromReturnContext(fn, call, depth);
        foreach (var tp in fn.TypeParameters)
        {
            for (var i = 0; i < fn.Parameters.Count && i < call.Arguments.Count; i++)
            {
                var pt = fn.Parameters[i].Type;
                var isArray = false;
                if (pt is { Kind: SK.ArrayType }) { pt = pt.Type; isArray = true; }
                if (pt is { Kind: SK.TypeReference } && pt.Name is { Kind: SK.Identifier } nm && nm.Text == tp.Name!.Text && pt.TypeArguments.Count == 0)
                {
                    var arg = call.Arguments[i];
                    var array = isArray;
                    env = new Env(tp, d => array ? IndexValue(ValueConts(arg, d), d) : ValueConts(arg, d), env);
                    break;
                }
            }
        }

        return env;
    }

    /// <summary>
    /// `const x: Box&lt;Foo&gt; = make("a", (inst) => ...)` with `make&lt;T&gt;(...): Box&lt;T&gt;`: T is inferred from the contextual
    /// type of the call when the declared return type is a reference to the same generic type.
    /// </summary>
    private Env? InferFromReturnContext(Node fn, Node call, int depth)
    {
        if (fn.Type is not { Kind: SK.TypeReference } rt || rt.TypeArguments.Count == 0) { return null; }
        var returnDecls = EntityDecls(rt.Name!, Meaning.Type, depth + 1);
        if (returnDecls is null) { return null; }
        Env? env = null;
        foreach (var c in ContextualConts(call, depth + 1))
        {
            if (!returnDecls.Contains(c.Decl)) { continue; }
            for (var i = 0; i < rt.TypeArguments.Count && i < c.Decl.TypeParameters.Count; i++)
            {
                if (rt.TypeArguments[i] is not { Kind: SK.TypeReference, Name: { Kind: SK.Identifier } nm } || c.Env?.Find(c.Decl.TypeParameters[i]) is not { } bound) { continue; }
                var tp = fn.TypeParameters.FirstOrDefault(x => x.Name!.Text == nm.Text);
                if (tp is not null) { env = new Env(tp, bound, env); }
            }

            if (env is not null) { break; }
        }

        return env;
    }

    private List<Cont> ReturnedConts(Node fn, int depth)
    {
        if (fn.Body is null) { return []; }
        if (fn.Body.Kind != SK.Block) { return ValueConts(fn.Body, depth + 1); }
        var result = new List<Cont>();
        void Walk(Node n)
        {
            if (n.Kind is SK.FunctionDeclaration or SK.FunctionExpression or SK.ArrowFunction or SK.MethodDeclaration or SK.ClassDeclaration or SK.ClassExpression) { return; }
            if (n.Kind == SK.ReturnStatement && n.Expression is not null) { result.AddRange(ValueConts(n.Expression, depth + 1)); return; }
            foreach (var c in n.Children) { Walk(c); }
        }

        foreach (var c in fn.Body.Children) { Walk(c); }
        return result;
    }

    // ------------------------------------------------------------------ JSDoc types (JavaScript files)
    private List<Cont> JsDocConts(Node declaration, int depth)
    {
        var type = JsDocType(declaration);
        return type is null ? [] : TypeConts(type, null, depth + 1);
    }

    private readonly Dictionary<Node, Node?> _jsDocTypes = [];

    /// <summary>The type written in a JSDoc <c>@type</c>/<c>@param</c> tag for a declaration in a JavaScript file.</summary>
    public Node? JsDocType(Node declaration)
    {
        if (declaration.File is not { IsJavaScript: true }) { return null; }
        if (_jsDocTypes.TryGetValue(declaration, out var cached)) { return cached; }
        Node? result = null;
        if (declaration.Kind == SK.Parameter && declaration.Name?.Kind == SK.Identifier && declaration.Parent?.JsDoc is { } fnDoc)
        {
            foreach (var (tag, text) in JsDoc.Tags(fnDoc))
            {
                if (tag is not ("param" or "arg" or "argument")) { continue; }
                var (typeText, rest) = JsDoc.SplitTypeExpression(text);
                if (typeText is null) { continue; }
                var name = rest.TrimStart().TrimStart('[');
                var end = name.IndexOfAny([' ', '\t', '\r', '\n', ']', '=']);
                if ((end < 0 ? name : name[..end]) == declaration.Name.Text) { result = JsDocTypeParser.Parse(typeText, declaration); break; }
            }
        }
        else if (declaration.Kind is SK.VariableDeclaration or SK.PropertyDeclaration or SK.Parameter && JsDocOf(declaration) is { } doc)
        {
            foreach (var (tag, text) in JsDoc.Tags(doc))
            {
                if (tag is not ("type" or "member" or "var")) { continue; }
                var (typeText, _) = JsDoc.SplitTypeExpression(text);
                if (typeText is not null) { result = JsDocTypeParser.Parse(typeText, declaration); break; }
            }
        }

        _jsDocTypes[declaration] = result;
        return result;
    }

    private static string? JsDocOf(Node declaration)
    {
        if (declaration.Kind == SK.VariableDeclaration && declaration.Parent?.Parent is { Kind: SK.VariableStatement } st) { return st.JsDoc ?? declaration.JsDoc; }
        return declaration.JsDoc;
    }

    // ------------------------------------------------------------------ contextual typing
    /// <summary>Containers that give <paramref name="e"/> its contextual type (TypeScript's <c>getContextualType</c>).</summary>
    public List<Cont> ContextualConts(Node e, int depth)
    {
        if (depth > MaxDepth) { _cuts++; return []; }
        var p = e.Parent;
        if (p is null) { return []; }
        switch (p.Kind)
        {
            case SK.VariableDeclaration or SK.PropertyDeclaration or SK.Parameter or SK.BindingElement when ReferenceEquals(p.Initializer, e):
                return p.Type is not null ? TypeConts(p.Type, null, depth + 1) : JsDocConts(p, depth + 1);
            case SK.AsExpression or SK.TypeAssertion or SK.SatisfiesExpression when p.Type is not null && p.Type.Kind != SK.KeywordType:
                return TypeConts(p.Type, null, depth + 1);
            case SK.ParenthesizedExpression or SK.NonNullExpression:
                return ContextualConts(p, depth + 1);
            case SK.ConditionalExpression when !ReferenceEquals(p.Condition, e):
                return ContextualConts(p, depth + 1);
            case SK.BinaryExpression when p.Text is "||" or "??" or "&&" && ReferenceEquals(p.Right, e):
                return ContextualConts(p, depth + 1);
            case SK.BinaryExpression when p.Text == "=" && ReferenceEquals(p.Right, e):
                return ValueConts(p.Left!, depth + 1);
            case SK.ArrayLiteralExpression:
                return IndexValue(ContextualConts(p, depth + 1), depth + 1);
            case SK.SpreadElement when p.Parent?.Kind == SK.ArrayLiteralExpression:
                return ContextualConts(p.Parent, depth + 1);
            case SK.ExportAssignment:
                return [];
            case SK.ReturnStatement:
            {
                for (var f = p.Parent; f is not null; f = f.Parent)
                {
                    if (f.Kind is SK.FunctionDeclaration or SK.FunctionExpression or SK.ArrowFunction or SK.MethodDeclaration or SK.GetAccessor)
                    {
                        return ReturnContextual(f, depth + 1);
                    }
                }

                return [];
            }

            case SK.ArrowFunction when ReferenceEquals(p.Body, e):
                return ReturnContextual(p, depth + 1);
            case SK.PropertyAssignment when ReferenceEquals(p.Initializer, e) && p.Parent is { Kind: SK.ObjectLiteralExpression } outer:
            {
                var name = Binder.NameText(p.Name);
                var outerConts = ContextualConts(outer, depth + 1);
                if (outerConts.Count == 0) { return []; }
                if (name is not null)
                {
                    var hits = Members(outerConts, name, depth + 1);
                    if (hits.Count > 0) { return HitsToConts(hits, depth + 1); }
                }

                return IndexValue(outerConts, depth + 1);
            }

            case SK.CallExpression or SK.NewExpression when p.Arguments.Contains(e):
                return ArgumentConts(p, p.Arguments.IndexOf(e), depth + 1);
            default:
                return [];
        }
    }

    private List<Cont> ReturnContextual(Node fn, int depth)
    {
        if (fn.Type is not null)
        {
            var declared = TypeConts(fn.Type, null, depth + 1);
            return fn.Has(Mods.Async) ? UnwrapPromise(declared, depth + 1) : declared;
        }

        var result = new List<Cont>();
        if (fn.Kind is SK.ArrowFunction or SK.FunctionExpression)
        {
            foreach (var c in ContextualConts(fn, depth + 1))
            {
                foreach (var sig in Signatures(c))
                {
                    if (sig.Decl.Type is not null) { result.AddRange(TypeConts(sig.Decl.Type, sig.Env, depth + 1)); }
                }
            }
        }

        return result;
    }

    /// <summary>The call/function signatures a container offers.</summary>
    private List<Cont> Signatures(Cont c)
    {
        var d = c.Decl;
        switch (d.Kind)
        {
            case SK.FunctionType or SK.FunctionDeclaration or SK.MethodDeclaration or SK.MethodSignature or SK.ArrowFunction or SK.FunctionExpression or SK.CallSignature:
                return [c];
            case SK.InterfaceDeclaration or SK.TypeLiteral:
                return d.Members.Where(m => m.Kind == SK.CallSignature).Select(m => new Cont(m, c.Env)).ToList();
            default:
                return [];
        }
    }

    private List<Cont> ArgumentConts(Node call, int index, int depth)
    {
        var result = new List<Cont>();
        foreach (var callee in ValueConts(call.Expression!, depth + 1))
        {
            List<Cont> sigs;
            if (call.Kind == SK.NewExpression)
            {
                sigs = ConstructorSignatures(callee, depth + 1);
            }
            else
            {
                sigs = Signatures(callee);
            }

            foreach (var sig in sigs)
            {
                var ps = sig.Decl.Parameters;
                Node? p = index < ps.Count ? ps[index] : ps.Count > 0 && ps[^1].IsExtends ? ps[^1] : null;
                if (p is null) { continue; }
                if (p.Type is null) { result.AddRange(JsDocConts(p, depth + 1)); continue; }
                var inferred = sig.Decl.TypeParameters.Count > 0 ? MergeEnv(InferTypeArguments(sig.Decl, call, depth), sig.Env) : sig.Env;
                var conts = TypeConts(p.Type, inferred, depth + 1);
                if (p.IsExtends && p.Type.Kind == SK.ArrayType && index >= ps.Count - 1) { conts = TypeConts(p.Type.Type!, inferred, depth + 1); }
                result.AddRange(conts);
            }
        }

        return result;
    }

    private List<Cont> ConstructorSignatures(Cont callee, int depth)
    {
        var d = callee.Decl;
        var result = new List<Cont>();
        if (d.Kind == SK.TypeQuery && d.Expression is { Kind: SK.ClassDeclaration or SK.ClassExpression } cls)
        {
            // Own constructor, otherwise the nearest base class constructor with the heritage type arguments bound.
            var current = new Cont(cls, null);
            for (var i = 0; i < 8; i++)
            {
                var ctor = current.Decl.Members.FirstOrDefault(m => m.Kind == SK.Constructor);
                if (ctor is not null) { result.Add(new Cont(ctor, current.Env)); break; }
                var next = HeritageConts(current.Decl, current.Env, depth + 1).FirstOrDefault(b => b.Decl.Kind is SK.ClassDeclaration or SK.ClassExpression);
                if (next.Decl is null) { break; }
                current = next;
            }

            return result;
        }

        if (d.Kind is SK.InterfaceDeclaration or SK.TypeLiteral or SK.ClassDeclaration)
        {
            result.AddRange(d.Members.Where(m => m.Kind == SK.ConstructSignature).Select(m => new Cont(m, callee.Env)));
        }

        return result;
    }

    private List<Cont> ContextualParameterConts(Node parameter, int depth)
    {
        var fn = parameter.Parent!;
        if (fn.Kind is not (SK.ArrowFunction or SK.FunctionExpression or SK.MethodDeclaration)) { return JsDocConts(parameter, depth + 1); }
        var index = fn.Parameters.IndexOf(parameter);
        var result = new List<Cont>();
        var contextual = fn.Kind == SK.MethodDeclaration && fn.Parent?.Kind == SK.ObjectLiteralExpression
            ? ContextualMemberConts(fn, depth + 1)
            : ContextualConts(fn, depth + 1);
        foreach (var c in contextual)
        {
            foreach (var sig in Signatures(c))
            {
                var ps = sig.Decl.Parameters;
                if (index < ps.Count && ps[index].Type is { } t) { result.AddRange(TypeConts(t, sig.Env, depth + 1)); }
            }
        }

        return result.Count > 0 ? result : JsDocConts(parameter, depth + 1);
    }

    /// <summary>Contextual type of an object literal method: the type of the same-named member of the contextual literal type.</summary>
    private List<Cont> ContextualMemberConts(Node method, int depth)
    {
        var outer = ContextualConts(method.Parent!, depth + 1);
        var name = Binder.NameText(method.Name);
        return name is null ? [] : HitsToConts(Members(outer, name, depth + 1), depth + 1);
    }

    // ---- API used by FileIndexer
    /// <summary>Declarations of the property <paramref name="name"/> in the contextual type of <paramref name="literal"/>,
    /// or null when the contextual type does not declare it (TypeScript's <c>getPropertySymbolFromContextualType</c>).</summary>
    public List<Node>? ContextualPropertyDecls(Node literal, string name)
    {
        var contextual = ContextualConts(literal, 0);
        if (contextual.Count == 0) { return null; }
        contextual = Discriminate(literal, contextual);
        // `getProperty` on a union needs the property in every constituent; intersections need it in one.
        var found = new List<Node>();
        var groups = new Dictionary<int, bool>();
        foreach (var c in contextual)
        {
            var hits = Members([c], name, 0);
            var has = hits.Count > 0;
            if (has) { found.AddRange(hits.Select(h => h.Decl)); }
            if (c.Group == 0) { if (!has) { return null; } }
            else { groups[c.Group] = groups.GetValueOrDefault(c.Group) || has; }
        }

        if (groups.Values.Any(v => !v)) { return null; }
        return found.Count == 0 ? null : found.Distinct().ToList();
    }

    /// <summary>
    /// TypeScript narrows a contextual union type by the literal-valued properties already present in the object
    /// literal (`{ code: "too_big", ... }` selects the member whose `code` is `"too_big"`).
    /// </summary>
    private List<Cont> Discriminate(Node literal, List<Cont> contextual)
    {
        var expanded = new List<Cont>();
        foreach (var c in contextual) { Expand(c, expanded, 0); }
        if (expanded.Count < 2) { return contextual; }
        var narrowed = expanded;
        foreach (var element in literal.Elements)
        {
            if (element.Kind != SK.PropertyAssignment || element.Initializer is not { Kind: SK.StringLiteral or SK.NumericLiteral or SK.KeywordLiteral } value || Binder.NameText(element.Name) is not { } prop) { continue; }
            var kept = new List<Cont>();
            foreach (var c in narrowed)
            {
                var hits = Members([c], prop, 0);
                if (hits.Count == 0 || hits.Any(h => LiteralAllows(h.Decl.Type, value))) { kept.Add(c); }
            }

            if (kept.Count > 0) { narrowed = kept; }
        }

        return narrowed.Count == expanded.Count ? contextual : narrowed;
    }

    private void Expand(Cont c, List<Cont> into, int depth)
    {
        if (c.Decl.Kind == SK.MappedType && depth < 4)
        {
            var source = MappedSourceConts(c.Decl, c.Env, depth + 1);
            if (source.Count > 0) { foreach (var s in source) { Expand(c.Group != 0 ? s with { Group = c.Group } : s, into, depth + 1); } return; }
        }

        into.Add(c);
    }

    /// <summary>False only when <paramref name="type"/> is a literal type (or union of them) that excludes <paramref name="value"/>.</summary>
    private static bool LiteralAllows(Node? type, Node value)
    {
        if (type is null) { return true; }
        switch (type.Kind)
        {
            case SK.LiteralType:
                return type.Expression is { } lit && (lit.Kind != value.Kind || lit.Text == value.Text || lit.Kind is not (SK.StringLiteral or SK.NumericLiteral or SK.KeywordLiteral));
            case SK.UnionType:
                return type.Elements.Any(e => LiteralAllows(e, value));
            case SK.ParenthesizedType:
                return LiteralAllows(type.Type, value);
            default:
                return true;
        }
    }

    /// <summary>The declaration nodes of the containers giving <paramref name="literal"/> its contextual type.</summary>
    public List<Node> ContextualContainers(Node literal, int depth = 0)
    {
        // Upstream reads `getContextualType(literal).symbol.declarations`: only a type that *is* a class or interface has
        // one (unions, intersections, mapped and conditional results do not).
        var conts = Real(ContextualConts(literal, depth));
        if (conts.Any(c => c.Alt || c.Group != 0 || c.Derived)) { return []; }
        return conts.Select(c => c.Decl).Where(d => d.Kind is SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration).Distinct().ToList();
    }

}

/// <summary>Binds the type parameters of a declaration to the type arguments it was used with.</summary>
public sealed class Env(Node param, Func<int, List<Cont>> value, Env? next)
{
    public Node Param { get; } = param;

    public Func<int, List<Cont>> Value { get; } = value;

    public Env? Next { get; } = next;

    public Func<int, List<Cont>>? Find(Node parameter)
    {
        for (var e = this; e is not null; e = e.Next)
        {
            if (ReferenceEquals(e.Param, parameter)) { return e.Value; }
        }

        return null;
    }
}

/// <summary>A declaration providing members plus the environment its type parameters were instantiated in.</summary>
public readonly record struct Cont(Node Decl, Env? Env, int Group = 0, bool Alt = false, bool Derived = false);
