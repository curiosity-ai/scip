using ScipSharp.Indexer.TypeScript.Syntax;

namespace ScipSharp.Indexer.TypeScript.Semantics;

public sealed class Scope(Node owner, Scope? parent)
{
    public Node Owner { get; } = owner;
    public Scope? Parent { get; } = parent;
    public Dictionary<string, List<Node>> Table { get; } = new(StringComparer.Ordinal);

    public void Declare(string name, Node decl)
    {
        if (!Table.TryGetValue(name, out var list)) { Table[name] = list = []; }
        if (!list.Contains(decl)) { list.Add(decl); }
    }
}

[Flags]
public enum Meaning { Value = 1, Type = 2, Any = 3 }

/// <summary>Builds lexical scopes and export tables for one source file (the part of TypeScript's binder we need).</summary>
public sealed class Binder
{
    public TsProgram Program { get; }
    public TsSourceFile File { get; }
    public Scope FileScope { get; }
    private readonly Dictionary<Node, Scope> _scopeOf = [];
    private readonly Dictionary<Node, Dictionary<string, List<Node>>> _exports = [];
    private readonly Dictionary<Node, List<Node>> _exportDeclarations = [];

    public Binder(TsProgram program, TsSourceFile file)
    {
        Program = program;
        File = file;
        FileScope = new Scope(file.Root, null);
        _scopeOf[file.Root] = FileScope;
        var implicitExports = file.FileName.EndsWith(".d.ts", StringComparison.Ordinal) && file.IsExternalModule &&
            !file.Root.Elements.Any(e => e.Kind is SK.ExportAssignment or SK.ExportDeclaration);
        CollectExports(file.Root, file.Root.Elements, implicitExports);
        foreach (var c in file.Root.Children) { Bind(c, FileScope, FileScope); }
    }

    public static string? NameText(Node? name) => name switch
    {
        null => null,
        { Kind: SK.Identifier or SK.StringLiteral or SK.NumericLiteral or SK.PrivateIdentifier } => name.Text,
        { Kind: SK.ComputedPropertyName, Expression: { Kind: SK.StringLiteral or SK.NumericLiteral } e } => e.Text,
        _ => null,
    };

    public Scope? ScopeOf(Node n) => _scopeOf.GetValueOrDefault(n);

    // ------------------------------------------------------------------ binding
    private Scope NewScope(Node owner, Scope parent)
    {
        var s = new Scope(owner, parent);
        _scopeOf[owner] = s;
        return s;
    }

    private static void DeclareNames(Node? name, Scope scope, Node decl)
    {
        if (name is null) { return; }
        if (name.Kind == SK.Identifier) { scope.Declare(name.Text, decl); return; }
        if (name.Kind is SK.ObjectBindingPattern or SK.ArrayBindingPattern)
        {
            foreach (var el in name.Elements)
            {
                if (el.Kind == SK.BindingElement) { DeclareNames(el.Name, scope, el); }
            }
        }
    }

    private void Bind(Node n, Scope scope, Scope varScope)
    {
        switch (n.Kind)
        {
            case SK.VariableDeclarationList:
            {
                var target = n.IsConst || n.IsLet ? scope : varScope;
                foreach (var d in n.Elements) { DeclareNames(d.Name, target, d); }
                break;
            }

            case SK.FunctionDeclaration:
                if (n.Name is not null) { scope.Declare(n.Name.Text, n); }
                BindFunctionLike(n, scope);
                return;
            case SK.FunctionExpression or SK.ArrowFunction or SK.MethodDeclaration or SK.Constructor or SK.GetAccessor or SK.SetAccessor
                or SK.MethodSignature or SK.CallSignature or SK.ConstructSignature or SK.FunctionType or SK.ConstructorType or SK.IndexSignature:
            {
                var fs = BindFunctionLike(n, scope, declareParametersOnly: true);
                if (n.Kind == SK.FunctionExpression && n.Name is not null) { fs.Declare(n.Name.Text, n); }
                return;
            }

            case SK.ClassDeclaration or SK.ClassExpression:
            {
                if (n.Kind == SK.ClassDeclaration && n.Name is not null) { scope.Declare(n.Name.Text, n); }
                var cs = NewScope(n, scope);
                if (n.Kind == SK.ClassExpression && n.Name is not null) { cs.Declare(n.Name.Text, n); }
                foreach (var c in n.Children) { Bind(c, cs, cs); }
                return;
            }

            case SK.InterfaceDeclaration or SK.TypeAliasDeclaration:
            {
                if (n.Name is not null) { scope.Declare(n.Name.Text, n); }
                var ts = NewScope(n, scope);
                foreach (var c in n.Children) { Bind(c, ts, ts); }
                return;
            }

            case SK.EnumDeclaration:
                if (n.Name is not null) { scope.Declare(n.Name.Text, n); }
                break;
            case SK.ModuleDeclaration:
                if (n.Name is { Kind: SK.Identifier }) { scope.Declare(n.Name.Text, n); }
                break;
            case SK.ModuleBlock:
            {
                var ms = NewScope(n, scope);
                var implicitExports = n.Parent?.Has(Mods.Declare) == true || File.FileName.EndsWith(".d.ts", StringComparison.Ordinal);
                CollectExports(n.Parent!, n.Elements, implicitExports);
                foreach (var c in n.Children) { Bind(c, ms, ms); }
                return;
            }

            case SK.ImportSpecifier or SK.NamespaceImport or SK.NamespaceExport when n.Kind != SK.NamespaceExport:
                if (n.Name is not null) { scope.Declare(n.Name.Text, n); }
                return;
            case SK.ImportClause:
                if (n.Name is not null) { scope.Declare(n.Name.Text, n); }
                break;
            case SK.ImportEqualsDeclaration:
                if (n.Name is not null) { scope.Declare(n.Name.Text, n); }
                break;
            case SK.Block or SK.CaseBlock or SK.ForStatement or SK.ForInStatement or SK.ForOfStatement:
            {
                var bs = NewScope(n, scope);
                foreach (var c in n.Children) { Bind(c, bs, varScope); }
                return;
            }

            case SK.CatchClause:
            {
                var cs = NewScope(n, scope);
                if (n.Initializer is { } decl) { DeclareNames(decl.Name, cs, decl); }
                foreach (var c in n.Children) { Bind(c, cs, varScope); }
                return;
            }

            case SK.ConditionalType or SK.MappedType:
            {
                var cs = NewScope(n, scope);
                foreach (var c in n.Children) { Bind(c, cs, cs); }
                return;
            }

            case SK.TypeParameter:
                if (n.Name is not null) { scope.Declare(n.Name.Text, n); }
                break;
            case SK.Parameter:
                DeclareNames(n.Name, scope, n);
                break;
        }

        foreach (var c in n.Children) { Bind(c, scope, varScope); }
    }

    private Scope BindFunctionLike(Node n, Scope parent, bool declareParametersOnly = false)
    {
        _ = declareParametersOnly;
        var fs = NewScope(n, parent);
        foreach (var c in n.Children) { Bind(c, fs, fs); }
        return fs;
    }

    // ------------------------------------------------------------------ exports
    private void CollectExports(Node container, List<Node> statements, bool implicitExport)
    {
        if (!_exports.TryGetValue(container, out var table)) { _exports[container] = table = new Dictionary<string, List<Node>>(StringComparer.Ordinal); }
        if (!_exportDeclarations.TryGetValue(container, out var exportDecls)) { _exportDeclarations[container] = exportDecls = []; }
        void Add(string name, Node decl)
        {
            if (!table.TryGetValue(name, out var l)) { table[name] = l = []; }
            if (!l.Contains(decl)) { l.Add(decl); }
        }

        foreach (var st in statements)
        {
            var exported = st.Has(Mods.Export) || (implicitExport && st.Kind is not (SK.ImportDeclaration or SK.ExportDeclaration or SK.ExportAssignment or SK.ImportEqualsDeclaration or SK.ExpressionStatement));
            switch (st.Kind)
            {
                case SK.VariableStatement when exported:
                    foreach (var d in st.Initializer!.Elements) { AddPattern(d.Name, d, Add); }
                    break;
                case SK.FunctionDeclaration or SK.ClassDeclaration or SK.InterfaceDeclaration or SK.TypeAliasDeclaration or SK.EnumDeclaration
                    or SK.ModuleDeclaration or SK.ImportEqualsDeclaration when exported:
                    if (st.Has(Mods.Default)) { Add("default", st); }
                    else if (st.Name is { Kind: SK.Identifier }) { Add(st.Name.Text, st); }
                    break;
                case SK.ExportAssignment:
                    Add(st.Has(Mods.Default) ? "default" : "export=", st);
                    break;
                case SK.ExpressionStatement when File.IsJavaScript && st.Expression is { Kind: SK.BinaryExpression, Text: "=" } assignment:
                    // CommonJS: `module.exports = x` and `exports.name = x` / `module.exports.name = x`.
                    if (IsModuleExports(assignment.Left!)) { Add("export=", assignment); }
                    else if (assignment.Left is { Kind: SK.PropertyAccessExpression } target && (IsExportsObject(target.Expression!) || IsModuleExports(target.Expression!))) { Add(target.Name!.Text, assignment); }
                    break;
                case SK.ExportDeclaration:
                    exportDecls.Add(st);
                    break;
            }
        }
    }

    private static bool IsExportsObject(Node n) => n is { Kind: SK.Identifier, Text: "exports" };

    private static bool IsModuleExports(Node n) =>
        n is { Kind: SK.PropertyAccessExpression, Name.Text: "exports", Expression: { Kind: SK.Identifier, Text: "module" } };

    private static void AddPattern(Node? name, Node decl, Action<string, Node> add)
    {
        if (name is null) { return; }
        if (name.Kind == SK.Identifier) { add(name.Text, decl); return; }
        foreach (var el in name.Elements)
        {
            if (el.Kind == SK.BindingElement) { AddPattern(el.Name, el, add); }
        }
    }

    public Dictionary<string, List<Node>>? ExportTable(Node container) => _exports.GetValueOrDefault(container);

    public List<Node> ExportDeclarations(Node container) => _exportDeclarations.GetValueOrDefault(container) ?? [];

    // ------------------------------------------------------------------ lexical lookup
    public static bool HasMeaning(List<Node> decls, Meaning m) => decls.Any(d => DeclMeaning(d) switch { var dm => (dm & m) != 0 });

    public static Meaning DeclMeaning(Node d) => d.Kind switch
    {
        SK.VariableDeclaration or SK.Parameter or SK.BindingElement or SK.FunctionDeclaration or SK.FunctionExpression => Meaning.Value,
        SK.ClassDeclaration or SK.ClassExpression or SK.EnumDeclaration or SK.ModuleDeclaration or SK.ExportAssignment => Meaning.Any,
        SK.InterfaceDeclaration or SK.TypeAliasDeclaration or SK.TypeParameter => Meaning.Type,
        SK.ImportSpecifier or SK.ImportClause or SK.NamespaceImport or SK.ImportEqualsDeclaration or SK.ExportSpecifier => Meaning.Any,
        SK.EnumMember or SK.PropertyDeclaration or SK.PropertySignature or SK.MethodDeclaration or SK.MethodSignature or SK.PropertyAssignment
            or SK.ShorthandPropertyAssignment or SK.GetAccessor or SK.SetAccessor => Meaning.Value,
        SK.TsSourceFile => Meaning.Any,
        _ => Meaning.Any,
    };

    public List<Node>? Lookup(Node from, string name, Meaning meaning)
    {
        for (var n = from; n is not null; n = n.Parent)
        {
            if (_scopeOf.TryGetValue(n, out var scope) && scope.Table.TryGetValue(name, out var decls) && HasMeaning(decls, meaning))
            {
                // Top-level lib declarations merge across the lib files (`interface ObjectConstructor` in es5 and es2015.core).
                if (File.IsLib && ReferenceEquals(scope, FileScope) && File.Libs?.Lookup(name, meaning) is { } merged) { return merged; }
                return decls;
            }
        }

        // Script files and the lib declare globals; module files only see the lib.
        return (File.IsLib ? File.Libs : Program.Libs)?.Lookup(name, meaning);
    }
}
