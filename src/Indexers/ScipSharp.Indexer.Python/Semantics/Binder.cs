using ScipSharp.Indexer.Python.Syntax;

namespace ScipSharp.Indexer.Python.Semantics;

public enum DeclKind { Variable, Parameter, Function, Class, Alias, Intrinsic, Builtin, External }

/// <summary>
/// A declaration of a symbol (cf. pyright's <c>Declaration</c>). <see cref="Node"/> is the pyright "declaration node":
/// the <see cref="NameNode"/> for variables, the <see cref="ParameterNode"/>, <see cref="FunctionNode"/>,
/// <see cref="ClassNode"/>, or the import node for aliases.
/// </summary>
public sealed class Declaration
{
    public DeclKind Kind { get; init; }

    public Node? Node { get; init; }

    public SourceModule? Module { get; init; }

    /// <summary>For variables declared as <c>self.x = ...</c>: the class that owns the attribute.</summary>
    public ClassNode? MemberOwner { get; init; }

    public bool IsClassMember { get; init; }

    /// <summary>For <see cref="DeclKind.Alias"/> wildcard imports and <see cref="DeclKind.External"/>/<see cref="DeclKind.Builtin"/>: the bound name.</summary>
    public string Name { get; init; } = "";

    /// <summary>For <see cref="DeclKind.External"/>: the module the symbol lives in.</summary>
    public string ExternalModule { get; init; } = "";

    /// <summary>For <see cref="DeclKind.External"/> members of a builtin class (<c>str.lower</c>): the class.</summary>
    public string ExternalClass { get; init; } = "";
}

public sealed class Symbol(string name)
{
    public string Name { get; } = name;

    public List<Declaration> Declarations { get; } = [];
}

public enum ScopeKind { Module, Class, Function, Lambda, Comprehension }

public sealed class Scope(ScopeKind kind, Node node, Scope? parent)
{
    public ScopeKind Kind { get; } = kind;

    public Node Node { get; } = node;

    public Scope? Parent { get; } = parent;

    public Dictionary<string, Symbol> Symbols { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Globals { get; } = [];

    public HashSet<string> Nonlocals { get; } = [];

    public Symbol Get(string name)
    {
        if (!Symbols.TryGetValue(name, out var s))
        {
            Symbols[name] = s = new Symbol(name);
        }

        return s;
    }
}

/// <summary>A parsed and bound Python source file.</summary>
public sealed class SourceModule
{
    private Dictionary<Node, Scope>? _scopes;

    public SourceModule(string path, string moduleName, string text, ModuleNode tree, bool isProject)
    {
        Path = path;
        ModuleName = moduleName;
        Text = text;
        Tree = tree;
        IsProject = isProject;
        Lines = new LineMap(text);
    }

    public string Path { get; }

    /// <summary>Dotted module name as pyright's <c>fileInfo.moduleName</c> (no <c>__init__</c> suffix).</summary>
    public string ModuleName { get; }

    public string Text { get; }

    public ModuleNode Tree { get; }

    public bool IsProject { get; }

    public LineMap Lines { get; }

    public bool IsPackageInit => System.IO.Path.GetFileNameWithoutExtension(Path) == "__init__";

    public Dictionary<Node, Scope> Scopes => _scopes ??= new Binder(this).Bind();

    public Scope ModuleScope => Scopes[Tree];

    public ModuleSystem? Modules { get; init; }

    public IEnumerable<string> WildcardNames(ImportFromNode node) => Modules?.WildcardNames(this, node) ?? [];
}

public sealed class LineMap
{
    private readonly List<int> _starts = [0];

    public LineMap(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                _starts.Add(i + 1);
            }
            else if (text[i] == '\n')
            {
                _starts.Add(i + 1);
            }
        }
    }

    public int LineCount => _starts.Count;

    public (int Line, int Character) Position(int offset)
    {
        var idx = _starts.BinarySearch(offset);
        if (idx < 0)
        {
            idx = ~idx - 1;
        }

        return (idx, offset - _starts[idx]);
    }
}

/// <summary>
/// Builds scopes and declarations in the order pyright's binder does: module/class bodies first, function and lambda bodies
/// deferred (FIFO).
/// </summary>
public sealed class Binder(SourceModule module)
{
    private readonly Dictionary<Node, Scope> _scopes = [];
    private readonly Queue<Action> _deferred = new();

    public Dictionary<Node, Scope> Bind()
    {
        var scope = new Scope(ScopeKind.Module, module.Tree, null);
        _scopes[module.Tree] = scope;
        foreach (var n in module.Tree.Statements)
        {
            Visit(n, scope);
        }

        while (_deferred.Count > 0)
        {
            _deferred.Dequeue()();
        }

        return _scopes;
    }

    private Scope ModuleScope => _scopes[module.Tree];

    private void Add(Scope scope, string name, Declaration decl)
    {
        var target = scope;
        if (scope.Globals.Contains(name))
        {
            target = ModuleScope;
        }
        else if (scope.Nonlocals.Contains(name))
        {
            for (var p = scope.Parent; p is not null; p = p.Parent)
            {
                if (p.Kind is ScopeKind.Function or ScopeKind.Lambda && p.Symbols.ContainsKey(name))
                {
                    target = p;
                    break;
                }
            }
        }

        target.Get(name).Declarations.Add(decl);
    }

    private void Visit(Node? node, Scope scope)
    {
        switch (node)
        {
            case null:
                return;
            case FunctionNode f:
                foreach (var d in f.Decorators)
                {
                    Visit(d, scope);
                }

                Add(scope, f.Name.Value, new Declaration { Kind = DeclKind.Function, Node = f, Module = module });
                foreach (var p in f.Parameters)
                {
                    Visit(p.Default, scope);
                    Visit(p.Annotation, scope);
                }

                Visit(f.ReturnAnnotation, scope);
                var fs = new Scope(ScopeKind.Function, f, scope);
                _scopes[f] = fs;
                _deferred.Enqueue(() =>
                {
                    foreach (var p in f.Parameters)
                    {
                        if (p.Name is not null)
                        {
                            Add(fs, p.Name.Value, new Declaration { Kind = DeclKind.Parameter, Node = p, Module = module });
                        }
                    }

                    foreach (var s in f.Suite.Statements)
                    {
                        Visit(s, fs);
                    }
                });
                return;
            case LambdaNode l:
                foreach (var p in l.Parameters)
                {
                    Visit(p.Default, scope);
                }

                var ls = new Scope(ScopeKind.Lambda, l, scope);
                _scopes[l] = ls;
                foreach (var p in l.Parameters)
                {
                    if (p.Name is not null)
                    {
                        Add(ls, p.Name.Value, new Declaration { Kind = DeclKind.Parameter, Node = p, Module = module });
                    }
                }

                _deferred.Enqueue(() => Visit(l.Body, ls));
                return;
            case ClassNode c:
                foreach (var d in c.Decorators)
                {
                    Visit(d, scope);
                }

                foreach (var a in c.Arguments)
                {
                    Visit(a, scope);
                }

                Add(scope, c.Name.Value, new Declaration { Kind = DeclKind.Class, Node = c, Module = module });
                var cs = new Scope(ScopeKind.Class, c, scope);
                _scopes[c] = cs;
                foreach (var s in c.Suite.Statements)
                {
                    Visit(s, cs);
                }

                return;
            case ComprehensionNode comp:
                var first = comp.Clauses.OfType<ComprehensionForNode>().FirstOrDefault();
                if (first is not null)
                {
                    Visit(first.Iterable, scope);
                }

                var cps = new Scope(ScopeKind.Comprehension, comp, scope);
                _scopes[comp] = cps;
                foreach (var clause in comp.Clauses)
                {
                    if (clause is ComprehensionForNode cf)
                    {
                        BindTarget(cf.Target, cps, cf.Target);
                        if (cf != first)
                        {
                            Visit(cf.Iterable, cps);
                        }
                    }
                    else
                    {
                        Visit(clause, cps);
                    }
                }

                Visit(comp.Expression, cps);
                return;
            case AssignmentNode a:
                Visit(a.Value, scope);
                foreach (var t in a.Targets)
                {
                    BindTarget(t, scope, a);
                }

                return;
            case AugAssignmentNode aug:
                Visit(aug.Value, scope);
                BindTarget(aug.Target, scope, aug);
                return;
            case TypeAnnotationNode ta:
                if (ta.Parent is not AssignmentNode)
                {
                    BindTarget(ta, scope, ta);
                }

                Visit(ta.Annotation, scope);
                return;
            case ForNode fo:
                Visit(fo.Iterable, scope);
                BindTarget(fo.Target, scope, fo);
                Visit(fo.Suite, scope);
                Visit(fo.ElseSuite, scope);
                return;
            case WithItemNode wi:
                Visit(wi.Expression, scope);
                if (wi.Target is not null)
                {
                    BindTarget(wi.Target, scope, wi);
                }

                return;
            case ExceptNode ex:
                Visit(ex.TypeExpression, scope);
                if (ex.Name is not null)
                {
                    Add(scope, ex.Name.Value, new Declaration { Kind = DeclKind.Variable, Node = ex.Name, Module = module });
                }

                Visit(ex.Suite, scope);
                return;
            case AssignmentExpressionNode we:
                Visit(we.Right, scope);
                var ts = scope;
                while (ts.Kind == ScopeKind.Comprehension && ts.Parent is not null)
                {
                    ts = ts.Parent;
                }

                Add(ts, we.Name.Value, new Declaration { Kind = DeclKind.Variable, Node = we.Name, Module = module });
                return;
            case GlobalNode g:
                foreach (var n in g.Names)
                {
                    (g.IsNonlocal ? scope.Nonlocals : scope.Globals).Add(n.Value);
                }

                return;
            case ImportAsNode ia:
                if (ia.Alias is not null)
                {
                    Add(scope, ia.Alias.Value, new Declaration { Kind = DeclKind.Alias, Node = ia, Module = module });
                }
                else if (ia.Module.NameParts.Count > 0)
                {
                    var name = ia.Module.NameParts[0].Value;
                    var syms = scope.Get(name);
                    // `import a.b` and `import a.c` share one declaration in pyright; keep the first.
                    if (!syms.Declarations.Any(d => d.Kind == DeclKind.Alias && d.Node is ImportAsNode o && o.Alias is null))
                    {
                        Add(scope, name, new Declaration { Kind = DeclKind.Alias, Node = ia, Module = module });
                    }
                }

                return;
            case ImportFromNode ifn:
                if (ifn.IsWildcard)
                {
                    foreach (var name in module.WildcardNames(ifn))
                    {
                        Add(scope, name, new Declaration { Kind = DeclKind.Alias, Node = ifn, Module = module, Name = name });
                    }
                }

                foreach (var imp in ifn.Imports)
                {
                    Add(scope, (imp.Alias ?? imp.Name).Value, new Declaration { Kind = DeclKind.Alias, Node = imp, Module = module });
                }

                return;
            case CaseNode cn:
                BindPattern(cn.Pattern, scope);
                if (cn.AsName is not null)
                {
                    Add(scope, cn.AsName.Value, new Declaration { Kind = DeclKind.Variable, Node = cn.AsName, Module = module });
                }

                Visit(cn.Guard, scope);
                Visit(cn.Suite, scope);
                return;
            case NameNode or NumberNode or ConstantNode:
                return;
            default:
                foreach (var child in node.Children())
                {
                    Visit(child, scope);
                }

                return;
        }
    }

    private void BindPattern(Node pattern, Scope scope)
    {
        switch (pattern)
        {
            case NameNode n:
                if (n.Value != "_")
                {
                    Add(scope, n.Value, new Declaration { Kind = DeclKind.Variable, Node = n, Module = module });
                }

                break;
            case CallNode c:
                foreach (var a in c.Arguments)
                {
                    BindPattern(a.Value, scope);
                }

                break;
            case BinaryNode b:
                BindPattern(b.Left, scope);
                BindPattern(b.Right, scope);
                break;
            case SequenceNode s:
                foreach (var i in s.Items)
                {
                    BindPattern(i, scope);
                }

                break;
            case WrapperNode { Expression: { } e }:
                BindPattern(e, scope);
                break;
            case DictionaryNode d:
                foreach (var e in d.Entries)
                {
                    switch (e)
                    {
                        case DictKeyEntryNode k:
                            BindPattern(k.Value, scope);
                            break;
                        case DictExpandEntryNode x:
                            BindPattern(x.Expression, scope);
                            break;
                    }
                }

                break;
        }
    }

    /// <summary>Adds declarations for an assignment-like target.</summary>
    private void BindTarget(Node target, Scope scope, Node source)
    {
        switch (target)
        {
            case NameNode n:
                Add(scope, n.Value, new Declaration { Kind = DeclKind.Variable, Node = n, Module = module });
                break;
            case SequenceNode seq:
                foreach (var i in seq.Items)
                {
                    BindTarget(i, scope, source);
                }

                break;
            case WrapperNode { Kind: NodeKind.Unpack, Expression: { } e }:
                BindTarget(e, scope, source);
                break;
            case TypeAnnotationNode ta:
                BindTarget(ta.ValueExpression, scope, source);
                Visit(ta.Annotation, scope);
                break;
            case MemberAccessNode ma:
                BindMemberAccess(ma, scope);
                Visit(ma.Left, scope);
                break;
            default:
                Visit(target, scope);
                break;
        }
    }

    private void BindMemberAccess(MemberAccessNode ma, Scope scope)
    {
        if (ma.Left is not NameNode left)
        {
            return;
        }

        // Find the enclosing method.
        var fnScope = scope;
        while (fnScope is not null && fnScope.Kind == ScopeKind.Comprehension)
        {
            fnScope = fnScope.Parent;
        }

        if (fnScope?.Node is not FunctionNode fn || fnScope.Parent?.Kind != ScopeKind.Class)
        {
            return;
        }

        var first = fn.Parameters.FirstOrDefault();
        if (first?.Name is null || first.Name.Value != left.Value)
        {
            return;
        }

        var isStatic = fn.Decorators.Any(d => d.Expression is NameNode { Value: "staticmethod" });
        if (isStatic)
        {
            return;
        }

        var isClassMethod = fn.Decorators.Any(d => d.Expression is NameNode { Value: "classmethod" }) || fn.Name.Value is "__new__" or "__init_subclass__" or "__class_getitem__";
        var cls = (ClassNode)fnScope.Parent.Node;
        var classScope = fnScope.Parent;
        classScope.Get(ma.Member.Value).Declarations.Add(new Declaration
        {
            Kind = DeclKind.Variable,
            Node = ma.Member,
            Module = module,
            MemberOwner = cls,
            IsClassMember = isClassMethod,
        });
    }
}
