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

    private const int MaxDepth = 10;

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

    public List<Node>? LookupExport(TsSourceFile file, string name, HashSet<TsSourceFile> visited)
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
                    if (exportDecl.ModuleSpecifier is { } ms)
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
                if (target is not null) { return [target.Root]; }
            }
        }

        foreach (var exportDecl in b.ExportDeclarations(file.Root))
        {
            if (exportDecl.Clause is null && exportDecl.ModuleSpecifier is { } ms && name != "default")
            {
                var target = Program.ResolveModule(file, ms.Text);
                var r = target is null ? null : LookupExport(target, name, visited);
                if (r is not null) { return r; }
            }
        }

        return null;
    }

    private List<Node> ExpandExport(TsSourceFile file, List<Node> decls)
    {
        var result = new List<Node>();
        foreach (var d in decls)
        {
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

    // ------------------------------------------------------------------ members
    private static bool MemberNameMatches(Node member, string name) => Binder.NameText(member.Name) == name;

    private static bool IsParameterProperty(Node p) => (p.Modifiers & (Mods.Public | Mods.Private | Mods.Protected | Mods.Readonly | Mods.Override)) != 0;

    public List<Node>? MemberDecls(List<Node> containers, string name) => MemberDecls(containers, name, [], 0);

    private List<Node>? MemberDecls(List<Node> containers, string name, HashSet<Node> visited, int depth)
    {
        if (depth > MaxDepth) { return null; }
        foreach (var c in containers)
        {
            if (!visited.Add(c)) { continue; }
            List<Node> found = [];
            switch (c.Kind)
            {
                case SK.TsSourceFile:
                    var export = LookupExport(c.File!, name, []);
                    if (export is not null) { found.AddRange(export); }
                    break;
                case SK.ModuleDeclaration:
                {
                    if (c.Body is { Kind: SK.ModuleDeclaration } inner && inner.Name!.Text == name) { found.Add(inner); }
                    var table = B(c).ExportTable(c);
                    if (table is not null && table.TryGetValue(name, out var l)) { found.AddRange(DeAliasAll(l)); }
                    break;
                }

                case SK.TypeQuery when c.Expression is { Kind: SK.ClassDeclaration or SK.ClassExpression } cls:
                {
                    foreach (var m in cls.Members)
                    {
                        if (m.Kind is SK.IndexSignature or SK.CallSignature or SK.ConstructSignature or SK.ClassStaticBlock or SK.Constructor || !m.Has(Mods.Static)) { continue; }
                        if (MemberNameMatches(m, name)) { found.Add(m); }
                    }

                    if (found.Count == 0)
                    {
                        var inherited = MemberDecls(HeritageContainers(cls, depth + 1, staticSide: true), name, visited, depth + 1);
                        if (inherited is not null) { found.AddRange(inherited); }
                    }

                    break;
                }

                case SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration or SK.TypeLiteral or SK.EnumDeclaration:
                    foreach (var m in c.Members)
                    {
                        if (m.Kind is SK.IndexSignature or SK.CallSignature or SK.ConstructSignature or SK.ClassStaticBlock or SK.Constructor) { continue; }
                        if (c.Kind is SK.ClassDeclaration or SK.ClassExpression && m.Has(Mods.Static)) { continue; }
                        if (MemberNameMatches(m, name)) { found.Add(m); }
                    }

                    if (c.Kind is SK.ClassDeclaration or SK.ClassExpression)
                    {
                        foreach (var ctor in c.Members.Where(m => m.Kind == SK.Constructor))
                        {
                            foreach (var p in ctor.Parameters)
                            {
                                if (IsParameterProperty(p) && p.Name?.Kind == SK.Identifier && p.Name.Text == name) { found.Add(p); }
                            }
                        }
                    }

                    if (found.Count == 0 && c.Kind != SK.EnumDeclaration)
                    {
                        var inherited = MemberDecls(HeritageContainers(c, depth + 1), name, visited, depth + 1);
                        if (inherited is not null) { found.AddRange(inherited); }
                    }

                    break;
                case SK.ObjectLiteralExpression:
                    foreach (var m in c.Elements)
                    {
                        if (m.Kind is SK.PropertyAssignment or SK.ShorthandPropertyAssignment or SK.MethodDeclaration or SK.GetAccessor or SK.SetAccessor && MemberNameMatches(m, name))
                        {
                            found.Add(m);
                        }
                    }

                    break;
            }

            if (found.Count > 0) { return found; }
        }

        return null;
    }

    private List<Node> DeAliasAll(List<Node> l) => DeAlias(l, 0);

    public List<Node> HeritageContainers(Node classOrInterface, int depth, bool staticSide = false)
    {
        var result = new List<Node>();
        foreach (var clause in classOrInterface.HeritageClauses)
        {
            var isClassExtends = clause.IsExtends && classOrInterface.Kind is SK.ClassDeclaration or SK.ClassExpression;
            foreach (var e in clause.Elements)
            {
                if (isClassExtends && !staticSide)
                {
                    // instance side of the base class
                    result.AddRange(ContainersOfEntity(e.Expression!, Meaning.Value, depth + 1).Select(c => c.Kind == SK.TypeQuery ? c.Expression! : c));
                }
                else
                {
                    result.AddRange(ContainersOfEntity(e.Expression!, isClassExtends ? Meaning.Value : Meaning.Type, depth + 1));
                }
            }
        }

        return result;
    }

    // ------------------------------------------------------------------ containers ("types")
    private List<Node> ContainersOfDecls(List<Node> decls, int depth, bool asValue = false)
    {
        var result = new List<Node>();
        foreach (var d in decls) { result.AddRange(ContainersOfDecl(d, depth, asValue)); }
        return result;
    }

    public List<Node> ContainersOfEntity(Node entity, Meaning meaning, int depth)
    {
        if (depth > MaxDepth) { return []; }
        List<Node>? decls;
        if (entity.Kind == SK.Identifier)
        {
            decls = B(entity).Lookup(entity, entity.Text, meaning);
            if (decls is not null) { decls = DeAlias(decls, 0); }
        }
        else
        {
            decls = ResolveEntityExpression(entity, depth + 1);
        }

        return decls is null ? [] : ContainersOfDecls(decls, depth + 1, meaning == Meaning.Value);
    }

    private List<Node> ContainersOfDecl(Node d, int depth, bool asValue = false)
    {
        if (depth > MaxDepth) { return []; }
        switch (d.Kind)
        {
            case SK.ClassDeclaration or SK.ClassExpression:
                return asValue ? [StaticSide(d)] : [d];
            case SK.InterfaceDeclaration or SK.EnumDeclaration or SK.ModuleDeclaration or SK.TsSourceFile
                or SK.TypeLiteral or SK.ObjectLiteralExpression:
                return [d];
            case SK.TypeAliasDeclaration:
                return ContainersOfType(d.Type!, depth + 1);
            case SK.TypeParameter:
                return d.Type is null ? [] : ContainersOfType(d.Type, depth + 1);
            case SK.VariableDeclaration or SK.Parameter or SK.PropertyDeclaration or SK.PropertySignature or SK.GetAccessor:
                if (d.Type is not null) { return ContainersOfType(d.Type, depth + 1); }
                if (d.Initializer is not null) { return ContainersOfExpression(d.Initializer, depth + 1); }
                if (d.Kind == SK.Parameter && d.Parent is { } fn && fn.Kind != SK.TsSourceFile) { return ContextualParameterContainers(d, depth + 1); }
                if (d.Kind == SK.GetAccessor && d.Body is not null) { return ReturnedContainers(d, depth + 1); }
                return [];
            case SK.PropertyAssignment:
                return ContainersOfExpression(d.Initializer!, depth + 1);
            case SK.ShorthandPropertyAssignment:
            {
                var decls = B(d).Lookup(d.Name!, d.Name!.Text, Meaning.Value);
                return decls is null ? [] : ContainersOfDecls(DeAlias(decls, 0), depth + 1);
            }

            case SK.BindingElement:
                return MemberContainers(ContainersOfBindingParent(d, depth + 1), (d.PropertyName ?? d.Name)!, depth + 1);
            case SK.ImportSpecifier or SK.ImportClause or SK.NamespaceImport or SK.ImportEqualsDeclaration:
                var target = ResolveAlias(d);
                return target is null ? [] : ContainersOfDecls(target, depth + 1, asValue);
            case SK.ExportAssignment:
                return d.Expression is null ? [] : ContainersOfExpression(d.Expression, depth + 1);
            default:
                return [];
        }
    }

    private List<Node> MemberContainers(List<Node> containers, Node nameNode, int depth)
    {
        var name = Binder.NameText(nameNode);
        if (name is null) { return []; }
        var members = MemberDecls(containers, name);
        return members is null ? [] : ContainersOfDecls(members, depth + 1);
    }

    private List<Node> ContainersOfBindingParent(Node bindingElement, int depth)
    {
        // BindingElement -> pattern -> (BindingElement | VariableDeclaration | Parameter)
        var pattern = bindingElement.Parent!;
        var owner = pattern.Parent!;
        if (owner.Kind == SK.BindingElement)
        {
            return MemberContainers(ContainersOfBindingParent(owner, depth + 1), (owner.PropertyName ?? owner.Name)!, depth + 1);
        }

        if (owner.Type is not null) { return ContainersOfType(owner.Type, depth + 1); }
        if (owner.Initializer is not null) { return ContainersOfExpression(owner.Initializer, depth + 1); }
        return [];
    }

    private List<Node> ContainersOfType(Node t, int depth)
    {
        if (depth > MaxDepth) { return []; }
        switch (t.Kind)
        {
            case SK.TypeReference:
                return ContainersOfEntity(t.Name!, Meaning.Type, depth + 1);
            case SK.TypeLiteral:
                return [t];
            case SK.ParenthesizedType:
                return ContainersOfType(t.Type!, depth + 1);
            case SK.UnionType or SK.IntersectionType:
                return t.Elements.SelectMany(e => ContainersOfType(e, depth + 1)).ToList();
            case SK.TypeQuery:
                return ContainersOfEntity(t.Expression!, Meaning.Value, depth + 1);
            case SK.ThisType:
                return EnclosingThisContainers(t);
            case SK.ImportType when t.Name is null && t.Extra is { Kind: SK.LiteralType, Expression: { } lit }:
            {
                var target = Program.ResolveModule(t.File!, lit.Text);
                return target is null ? [] : [target.Root];
            }

            default:
                return [];
        }
    }

    private List<Node> EnclosingThisContainers(Node from)
    {
        for (var n = from.Parent; n is not null; n = n.Parent)
        {
            switch (n.Kind)
            {
                case SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration:
                    return [n];
                case SK.ObjectLiteralExpression:
                    return [n];
                case SK.FunctionDeclaration or SK.FunctionExpression:
                    return [];
            }
        }

        return [];
    }

    public List<Node> ContainersOfExpression(Node e, int depth)
    {
        if (depth > MaxDepth) { return []; }
        switch (e.Kind)
        {
            case SK.ParenthesizedExpression or SK.NonNullExpression or SK.SatisfiesExpression:
                return ContainersOfExpression(e.Expression!, depth + 1);
            case SK.AsExpression or SK.TypeAssertion:
                return e.Type is { Kind: SK.KeywordType, Text: "const" } ? ContainersOfExpression(e.Expression!, depth + 1) : ContainersOfType(e.Type!, depth + 1);
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
                return cls is null ? [] : HeritageContainers(cls, depth + 1).Where(c => c.Kind is SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration).ToList();
            }

            case SK.PropertyAccessExpression:
            {
                var members = MemberDecls(ContainersOfExpression(e.Expression!, depth + 1), e.Name!.Text);
                return members is null ? [] : ContainersOfDecls(members, depth + 1, asValue: true);
            }

            case SK.ElementAccessExpression when e.Right is { Kind: SK.StringLiteral } lit:
            {
                var members = MemberDecls(ContainersOfExpression(e.Expression!, depth + 1), lit.Text);
                return members is null ? [] : ContainersOfDecls(members, depth + 1, asValue: true);
            }

            case SK.NewExpression:
                return ContainersOfExpression(e.Expression!, depth + 1)
                    .Select(c => c.Kind == SK.TypeQuery ? c.Expression! : c)
                    .Where(c => c.Kind is SK.ClassDeclaration or SK.ClassExpression or SK.InterfaceDeclaration).ToList();
            case SK.CallExpression:
                return CallResultContainers(e, depth + 1);
            case SK.ObjectLiteralExpression:
                return [e];
            case SK.ConditionalExpression:
                return [.. ContainersOfExpression(e.Then!, depth + 1), .. ContainersOfExpression(e.Else!, depth + 1)];
            case SK.BinaryExpression when e.Text is "||" or "??" or "&&":
                return [.. ContainersOfExpression(e.Left!, depth + 1), .. ContainersOfExpression(e.Right!, depth + 1)];
            case SK.AwaitExpression:
                return [];
            default:
                return [];
        }
    }

    private static Node? EnclosingClass(Node n)
    {
        for (var p = n.Parent; p is not null; p = p.Parent)
        {
            if (p.Kind is SK.ClassDeclaration or SK.ClassExpression) { return p; }
        }

        return null;
    }

    private List<Node> CallResultContainers(Node call, int depth)
    {
        var callee = call.Expression!;
        List<Node>? decls = callee.Kind switch
        {
            SK.Identifier => SymbolAtLocation(callee),
            SK.PropertyAccessExpression => MemberDecls(ContainersOfExpression(callee.Expression!, depth + 1), callee.Name!.Text),
            SK.ParenthesizedExpression or SK.NonNullExpression => null,
            _ => null,
        };
        if (decls is null) { return []; }
        var result = new List<Node>();
        foreach (var d in decls)
        {
            result.AddRange(ReturnContainersOfCallable(d, depth + 1));
        }

        return result;
    }

    private List<Node> ReturnContainersOfCallable(Node d, int depth)
    {
        if (depth > MaxDepth) { return []; }
        switch (d.Kind)
        {
            case SK.FunctionDeclaration or SK.FunctionExpression or SK.MethodDeclaration or SK.MethodSignature or SK.ArrowFunction or SK.GetAccessor:
                if (d.Type is not null) { return ContainersOfType(d.Type, depth + 1); }
                if (d.Body is not null) { return ReturnedContainers(d, depth + 1); }
                return [];
            case SK.VariableDeclaration or SK.PropertyDeclaration or SK.PropertySignature or SK.Parameter:
                if (d.Type is { Kind: SK.FunctionType } ft) { return ContainersOfType(ft.Type!, depth + 1); }
                if (d.Initializer is { Kind: SK.ArrowFunction or SK.FunctionExpression } fn) { return ReturnContainersOfCallable(fn, depth + 1); }
                return [];
            case SK.PropertyAssignment when d.Initializer is { Kind: SK.ArrowFunction or SK.FunctionExpression } fn2:
                return ReturnContainersOfCallable(fn2, depth + 1);
            default:
                return [];
        }
    }

    private List<Node> ReturnedContainers(Node fn, int depth)
    {
        if (fn.Body is null) { return []; }
        if (fn.Body.Kind != SK.Block) { return ContainersOfExpression(fn.Body, depth + 1); }
        var result = new List<Node>();
        void Walk(Node n)
        {
            if (n.Kind is SK.FunctionDeclaration or SK.FunctionExpression or SK.ArrowFunction or SK.MethodDeclaration or SK.ClassDeclaration or SK.ClassExpression) { return; }
            if (n.Kind == SK.ReturnStatement && n.Expression is not null) { result.AddRange(ContainersOfExpression(n.Expression, depth + 1)); return; }
            foreach (var c in n.Children) { Walk(c); }
        }

        foreach (var c in fn.Body.Children) { Walk(c); }
        return result;
    }

    private List<Node> ContextualParameterContainers(Node parameter, int depth)
    {
        // `const f: (x: T) => void = (x) => ...` or callbacks: not inferred.
        _ = parameter;
        _ = depth;
        return [];
    }

    // ------------------------------------------------------------------ contextual typing of object literals
    /// <summary>Containers that give object literal <paramref name="literal"/> its contextual type.</summary>
    public List<Node> ContextualContainers(Node literal, int depth = 0)
    {
        var type = ContextualTypeNode(literal, depth);
        return type is null ? [] : ContainersOfType(type, depth + 1);
    }

    private static Node? ElementTypeOf(Node? type)
    {
        while (type is { Kind: SK.ParenthesizedType }) { type = type.Type; }
        if (type is null) { return null; }
        if (type.Kind == SK.ArrayType) { return type.Type; }
        if (type is { Kind: SK.TypeReference } && type.Name?.Text is "Array" or "ReadonlyArray" && type.TypeArguments.Count == 1) { return type.TypeArguments[0]; }
        return null;
    }

    private Node? ContextualTypeNode(Node e, int depth)
    {
        if (depth > MaxDepth) { return null; }
        var p = e.Parent;
        if (p is null) { return null; }
        switch (p.Kind)
        {
            case SK.VariableDeclaration or SK.PropertyDeclaration or SK.Parameter or SK.BindingElement when ReferenceEquals(p.Initializer, e):
                return p.Type;
            case SK.AsExpression or SK.TypeAssertion or SK.SatisfiesExpression when p.Type is not null && p.Type.Kind != SK.KeywordType:
                return p.Type;
            case SK.ParenthesizedExpression or SK.NonNullExpression:
                return ContextualTypeNode(p, depth + 1);
            case SK.ConditionalExpression when !ReferenceEquals(p.Condition, e):
                return ContextualTypeNode(p, depth + 1);
            case SK.BinaryExpression when p.Text is "||" or "??" or "&&" or "=" && ReferenceEquals(p.Right, e):
                return ContextualTypeNode(p, depth + 1);
            case SK.ArrayLiteralExpression:
                return ElementTypeOf(ContextualTypeNode(p, depth + 1));
            case SK.SpreadElement when p.Parent?.Kind == SK.ArrayLiteralExpression:
                return ContextualTypeNode(p.Parent, depth + 1);
            case SK.ReturnStatement:
            {
                for (var f = p.Parent; f is not null; f = f.Parent)
                {
                    if (f.Kind is SK.FunctionDeclaration or SK.FunctionExpression or SK.ArrowFunction or SK.MethodDeclaration)
                    {
                        return UnwrapPromise(f.Type, f.Has(Mods.Async));
                    }
                }

                return null;
            }

            case SK.ArrowFunction when ReferenceEquals(p.Body, e):
                return UnwrapPromise(p.Type, p.Has(Mods.Async));
            case SK.PropertyAssignment when ReferenceEquals(p.Initializer, e) && p.Parent is { Kind: SK.ObjectLiteralExpression } outer:
            {
                var outerContainers = ContextualContainers(outer, depth + 1);
                var members = outerContainers.Count == 0 ? null : MemberDecls(outerContainers, Binder.NameText(p.Name) ?? "");
                return members?.FirstOrDefault(m => m.Type is not null)?.Type;
            }

            case SK.CallExpression or SK.NewExpression when p.Arguments.Contains(e):
            {
                var index = p.Arguments.IndexOf(e);
                var callee = p.Expression!;
                List<Node>? decls = callee.Kind switch
                {
                    SK.Identifier => SymbolAtLocation(callee),
                    SK.PropertyAccessExpression => MemberDecls(ContainersOfExpression(callee.Expression!, depth + 1), callee.Name!.Text),
                    _ => null,
                };
                foreach (var d in decls ?? [])
                {
                    var fn = d.Kind == SK.ClassDeclaration ? d.Members.FirstOrDefault(m => m.Kind == SK.Constructor) : d;
                    if (fn is not null && index < fn.Parameters.Count && fn.Parameters[index].Type is { } pt) { return pt; }
                }

                return null;
            }

            default:
                return null;
        }
    }

    private static Node? UnwrapPromise(Node? type, bool isAsync)
    {
        if (isAsync && type is { Kind: SK.TypeReference } && type.Name?.Text == "Promise" && type.TypeArguments.Count == 1) { return type.TypeArguments[0]; }
        return type;
    }
}
