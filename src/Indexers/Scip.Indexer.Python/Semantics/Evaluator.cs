using Scip.Indexer.Python.Syntax;

namespace Scip.Indexer.Python.Semantics;

/// <summary>An entity an expression can evaluate to. This is the (deliberately small) stand-in for pyright's <c>Type</c>.</summary>
public abstract record Entity;

/// <summary>A module. <paramref name="Remaining"/> are the not-yet-accessed parts of an <c>import a.b.c</c> statement.</summary>
public sealed record ModuleEntity(string Dotted, SourceModule? Source, ResolvedModule? Resolved, IReadOnlyList<string> Remaining) : Entity;

public sealed record ClassEntity(ClassNode Node, SourceModule Module) : Entity;

public sealed record InstanceEntity(ClassEntity Class) : Entity;

/// <summary>The synthesized type of <c>self</c>/<c>cls</c>; a TypeVar bound to the class in pyright.</summary>
public sealed record SelfEntity(ClassEntity Class, bool IsClassObject) : Entity;

public sealed record FunctionEntity(FunctionNode Node, SourceModule Module) : Entity;

/// <summary>A symbol of a module we have no source for (stdlib), or a builtin.</summary>
public sealed record ExternalEntity(string Module, string Name, bool IsClass) : Entity;

/// <summary>
/// A light-weight stand-in for pyright's type evaluator: scope lookup, alias (import) resolution, and just enough
/// expression typing (classes, instances, <c>self</c>, modules) to resolve member accesses to declarations.
/// </summary>
public sealed class Evaluator(ModuleSystem modules)
{
    private readonly HashSet<Node> _inProgress = [];
    private readonly Dictionary<ModuleKey, Entity?> _moduleCache = [];

    private readonly record struct ModuleKey(string Dotted, int Dots, string From);

    public ModuleSystem Modules { get; } = modules;

    // ------------------------------------------------------------------ scopes

    public Scope ScopeOf(SourceModule module, Node node)
    {
        var scopes = module.Scopes;
        var below = node;
        for (var cur = node.Parent; cur is not null; below = cur, cur = cur.Parent)
        {
            switch (cur)
            {
                case FunctionNode f when below is SuiteNode:
                    return scopes[f];
                case FunctionNode f when below is ParameterNode p && IsWithin(node, p.Name):
                    return scopes[f];
                case ClassNode c when below is SuiteNode:
                    return scopes[c];
                case LambdaNode l when below is not ParameterNode || IsWithin(node, ((ParameterNode)below).Name):
                    return scopes[l];
                case ComprehensionNode comp when !(below is ComprehensionForNode f
                    && comp.Clauses.OfType<ComprehensionForNode>().First() == f && IsWithin(node, f.Iterable)):
                    return scopes[comp];
                case ModuleNode:
                    return scopes[cur];
            }
        }

        return module.ModuleScope;
    }

    private static bool IsWithin(Node node, Node? ancestor)
    {
        for (var n = node; n is not null; n = n.Parent)
        {
            if (n == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static Declaration[] LookupInScope(Scope start, string name)
    {
        for (var s = start; s is not null; s = s.Parent)
        {
            if (s.Kind == ScopeKind.Class && s != start)
            {
                continue;
            }

            var current = s;
            if (s.Globals.Contains(name))
            {
                while (current.Parent is not null)
                {
                    current = current.Parent;
                }
            }
            else if (s.Nonlocals.Contains(name))
            {
                continue;
            }

            if (current.Symbols.TryGetValue(name, out var sym))
            {
                var decls = sym.Declarations.Where(d => d.MemberOwner is null).ToArray();
                if (decls.Length > 0)
                {
                    return decls;
                }
            }
        }

        return [];
    }

    // ------------------------------------------------------------------ declarations for names

    /// <summary>Declarations of the symbol a name node refers to; empty if unknown (cf. <c>getDeclarationsForNameNode</c>).</summary>
    public IReadOnlyList<Declaration> DeclarationsForName(SourceModule module, NameNode name)
    {
        switch (name.Parent)
        {
            case MemberAccessNode ma when ma.Member == name:
            {
                var left = EvalExpression(module, ma.Left);
                if (left is ModuleEntity m && (m.Remaining.Count > 0 && m.Remaining[0] == name.Value
                    || m.Source is null && Hardcoded.IsStdlibSubmodule(m.Dotted + "." + name.Value)))
                {
                    return []; // a submodule: modules have no declaration
                }

                return MembersOf(module, left, name.Value);
            }
            case ArgumentNode { Name: { } n } arg when n == name:
                return KeywordParameter(module, arg, name.Value);
            case ModuleNameNode:
                return [];
        }

        var decls = LookupInScope(ScopeOf(module, name), name.Value);
        if (decls.Length > 0)
        {
            return decls;
        }

        if (Hardcoded.IsBuiltin(name.Value))
        {
            return [new Declaration { Kind = DeclKind.Builtin, Name = name.Value }];
        }

        return [];
    }

    private IReadOnlyList<Declaration> KeywordParameter(SourceModule module, ArgumentNode arg, string name)
    {
        FunctionNode? fn = null;
        SourceModule? fnModule = null;
        if (arg.Parent is CallNode call)
        {
            switch (EvalExpression(module, call.Left))
            {
                case FunctionEntity f:
                    fn = f.Node;
                    fnModule = f.Module;
                    break;
                case ClassEntity c:
                    var init = MembersOf(module, c, "__init__").FirstOrDefault(d => d.Node is FunctionNode);
                    fn = init?.Node as FunctionNode;
                    fnModule = init?.Module;
                    break;
            }
        }

        var param = fn?.Parameters.FirstOrDefault(p => p.Name?.Value == name);
        return param is null ? [] : [new Declaration { Kind = DeclKind.Parameter, Node = param, Module = fnModule }];
    }

    // ------------------------------------------------------------------ imports

    private Entity? ModuleEntity(SourceModule from, IReadOnlyList<string> parts, int dots, IReadOnlyList<string> remaining, string dottedOverride)
    {
        var resolved = Modules.Resolve(parts, dots, from);
        if (resolved is null)
        {
            return null;
        }

        SourceModule? source = null;
        if (resolved.FilePath is not null)
        {
            source = Modules.Load(resolved.FilePath);
        }

        return new ModuleEntity(dottedOverride, source, resolved, remaining);
    }

    /// <summary>Resolves an alias declaration to the entity it ultimately refers to, and to the final declaration if any.</summary>
    public (Entity? Entity, Declaration? Decl) ResolveAlias(Declaration alias, int depth = 0)
    {
        if (depth > 8 || alias.Module is null)
        {
            return (null, null);
        }

        var from = alias.Module;
        switch (alias.Node)
        {
            case ImportAsNode ia:
            {
                var parts = ia.Module.NameParts.Select(p => p.Value).ToList();
                if (ia.Alias is not null)
                {
                    return (ModuleEntity(from, parts, 0, [], string.Join('.', parts)), null);
                }

                return (ModuleEntity(from, parts.Take(1).ToList(), 0, parts.Skip(1).ToList(), parts[0]), null);
            }

            case ImportFromAsNode ifa when ifa.Parent is ImportFromNode parent:
                return ResolveFromImport(from, parent, ifa.Name.Value, depth);
            case ImportFromNode wild:
                return ResolveFromImport(from, wild, alias.Name, depth);
        }

        return (null, null);
    }

    private (Entity?, Declaration?) ResolveFromImport(SourceModule from, ImportFromNode node, string name, int depth)
    {
        var modParts = node.Module.NameParts.Select(p => p.Value).ToList();
        var resolved = Modules.Resolve(modParts, node.Module.LeadingDots, from);
        if (resolved is null)
        {
            return (null, null);
        }

        if (resolved.FilePath is not null && Modules.Load(resolved.FilePath) is { } target)
        {
            var decls = target.ModuleScope.Symbols.TryGetValue(name, out var sym)
                ? sym.Declarations.Where(d => d.MemberOwner is null).ToList()
                : [];
            if (decls.Count > 0)
            {
                var first = decls[0];
                if (first.Kind == DeclKind.Alias)
                {
                    var (e, d) = ResolveAlias(first, depth + 1);
                    if (e is not null || d is not null)
                    {
                        return (e, d);
                    }

                    return (null, null);
                }

                return (EntityOfDeclaration(first), first);
            }
        }

        // `from pkg import submodule`
        var subDotted = string.Join('.', [.. modParts, name]);
        var sub = ModuleEntity(from, [.. modParts, name], node.Module.LeadingDots, [], subDotted);
        if (sub is ModuleEntity { Resolved: { } r } && (!r.IsStdlib || Hardcoded.IsStdlibSubmodule(subDotted)))
        {
            return (sub, null);
        }

        if (resolved.IsStdlib)
        {
            var module = string.Join('.', modParts);
            return (new ExternalEntity(module, name, Hardcoded.LooksLikeClass(name)),
                new Declaration { Kind = DeclKind.External, Name = name, ExternalModule = module });
        }

        return (null, null);
    }

    // ------------------------------------------------------------------ entities

    public Entity? EntityOfDeclaration(Declaration decl, int depth = 0)
    {
        if (depth > 6)
        {
            return null;
        }

        switch (decl.Kind)
        {
            case DeclKind.Class when decl.Module is not null:
                return new ClassEntity((ClassNode)decl.Node!, decl.Module);
            case DeclKind.Function when decl.Module is not null:
                return new FunctionEntity((FunctionNode)decl.Node!, decl.Module);
            case DeclKind.Builtin:
                return new ExternalEntity("builtins", decl.Name, Hardcoded.IsBuiltinClass(decl.Name));
            case DeclKind.External:
                return new ExternalEntity(decl.ExternalModule, decl.Name, Hardcoded.LooksLikeClass(decl.Name));
            case DeclKind.Alias:
                return ResolveAlias(decl).Entity;
            case DeclKind.Parameter when decl.Module is not null:
                return ParameterEntity((ParameterNode)decl.Node!, decl.Module, depth);
            case DeclKind.Variable when decl.Module is not null && decl.Node is NameNode n:
                return VariableEntity(n, decl.Module, depth);
        }

        return null;
    }

    private Entity? ParameterEntity(ParameterNode p, SourceModule module, int depth)
    {
        if (p.Annotation is not null)
        {
            return InstanceOfAnnotation(module, p.Annotation, depth + 1);
        }

        if (p.Parent is FunctionNode f && f.Parameters.FirstOrDefault() == p && p.Category == ParamCategory.Simple)
        {
            var scope = module.Scopes[f];
            if (scope.Parent?.Kind == ScopeKind.Class && scope.Parent.Node is ClassNode cls
                && !f.Decorators.Any(d => d.Expression is NameNode { Value: "staticmethod" }))
            {
                var isCls = f.Decorators.Any(d => d.Expression is NameNode { Value: "classmethod" }) || f.Name.Value is "__new__" or "__init_subclass__";
                return new SelfEntity(new ClassEntity(cls, module), isCls);
            }
        }

        return null;
    }

    private Entity? VariableEntity(NameNode n, SourceModule module, int depth)
    {
        if (!_inProgress.Add(n))
        {
            return null;
        }

        try
        {
            var parent = n.Parent;
            if (parent is TypeAnnotationNode ta && ta.ValueExpression == n)
            {
                return InstanceOfAnnotation(module, ta.Annotation, depth + 1);
            }

            Node? valueOwner = parent is MemberAccessNode ma ? ma : n;
            var holder = valueOwner.Parent;
            if (holder is TypeAnnotationNode ta2 && ta2.ValueExpression == valueOwner)
            {
                var ann = InstanceOfAnnotation(module, ta2.Annotation, depth + 1);
                if (ann is not null)
                {
                    return ann;
                }

                valueOwner = ta2;
                holder = ta2.Parent;
            }

            if (holder is AssignmentNode asg && asg.Targets.Contains(valueOwner))
            {
                return EvalExpression(module, asg.Value, depth + 1);
            }

            return null;
        }
        finally
        {
            _inProgress.Remove(n);
        }
    }

    public Entity? InstanceOfAnnotation(SourceModule module, Node annotation, int depth = 0)
    {
        if (depth > 8)
        {
            return null;
        }

        switch (annotation)
        {
            case NameNode or MemberAccessNode:
                return EvalExpression(module, annotation, depth + 1) switch
                {
                    ClassEntity c => new InstanceEntity(c),
                    _ => null,
                };
            case IndexNode { Left: NameNode { Value: "Optional" or "Final" or "ClassVar" or "Annotated" } } idx when idx.Items.Count > 0:
                return InstanceOfAnnotation(module, idx.Items[0].Value, depth + 1);
            case BinaryNode { Operator: "|" } b:
                return InstanceOfAnnotation(module, b.Left, depth + 1) ?? InstanceOfAnnotation(module, b.Right, depth + 1);
        }

        return null;
    }

    public Entity? EvalExpression(SourceModule module, Node node, int depth = 0)
    {
        if (depth > 12)
        {
            return null;
        }

        switch (node)
        {
            case NameNode n:
            {
                var decls = DeclarationsForName(module, n);
                foreach (var d in decls.Take(3))
                {
                    var e = EntityOfDeclaration(d, depth + 1);
                    if (e is not null)
                    {
                        return e;
                    }
                }

                return null;
            }

            case MemberAccessNode ma:
            {
                var left = EvalExpression(module, ma.Left, depth + 1);
                if (left is ModuleEntity m)
                {
                    if (m.Remaining.Count > 0 && m.Remaining[0] == ma.Member.Value)
                    {
                        var dotted = m.Dotted + "." + ma.Member.Value;
                        var resolved = Modules.Resolve(dotted.Split('.'), 0, module);
                        SourceModule? src = null;
                        if (resolved?.FilePath is not null)
                        {
                            src = Modules.Load(resolved.FilePath);
                        }

                        return new ModuleEntity(dotted, src, resolved, m.Remaining.Skip(1).ToList());
                    }

                    if (m.Source is null && Hardcoded.IsStdlibSubmodule(m.Dotted + "." + ma.Member.Value))
                    {
                        return new ModuleEntity(m.Dotted + "." + ma.Member.Value, null, new ResolvedModule(null, false, true, null), []);
                    }
                }

                foreach (var d in MembersOf(module, left, ma.Member.Value).Take(3))
                {
                    var e = EntityOfDeclaration(d, depth + 1);
                    if (e is not null)
                    {
                        return e;
                    }
                }

                return null;
            }

            case CallNode call:
                return EvalExpression(module, call.Left, depth + 1) switch
                {
                    ClassEntity c => new InstanceEntity(c),
                    SelfEntity { IsClassObject: true } s => new InstanceEntity(s.Class),
                    FunctionEntity f when f.Node.ReturnAnnotation is { } ret => InstanceOfAnnotation(f.Module, ret, depth + 1),
                    FunctionEntity f => InferReturn(f, depth + 1),
                    _ => null,
                };
            case TernaryNode t:
                return EvalExpression(module, t.IfExpression, depth + 1) ?? EvalExpression(module, t.ElseExpression, depth + 1);
            case AssignmentExpressionNode we:
                return EvalExpression(module, we.Right, depth + 1);
            default:
                return null;
        }
    }

    /// <summary>Return type inference by looking at the first <c>return &lt;expr&gt;</c> that evaluates to something.</summary>
    private Entity? InferReturn(FunctionEntity f, int depth)
    {
        if (!_inProgress.Add(f.Node))
        {
            return null;
        }

        try
        {
            var stack = new Stack<Node>();
            stack.Push(f.Node.Suite);
            var returns = new List<ReturnNode>();
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                foreach (var c in n.Children().Where(c => c is not null).Reverse())
                {
                    if (c is ReturnNode r)
                    {
                        returns.Add(r);
                    }
                    else if (c is not (FunctionNode or ClassNode or LambdaNode))
                    {
                        stack.Push(c!);
                    }
                }
            }

            foreach (var r in returns.OrderBy(r => r.Start))
            {
                if (r.Value is not null && EvalExpression(f.Module, r.Value, depth + 1) is { } e)
                {
                    return e;
                }
            }

            return null;
        }
        finally
        {
            _inProgress.Remove(f.Node);
        }
    }

    // ------------------------------------------------------------------ members

    public IReadOnlyList<Declaration> MembersOf(SourceModule module, Entity? entity, string name)
    {
        switch (entity)
        {
            case ModuleEntity { Source: { } src }:
                return src.ModuleScope.Symbols.TryGetValue(name, out var sym)
                    ? sym.Declarations.Where(d => d.MemberOwner is null).Take(1).ToList()
                    : [];
            case ModuleEntity { Resolved: { IsStdlib: true } } m:
                return [new Declaration { Kind = DeclKind.External, Name = name, ExternalModule = m.Dotted }];
            case ClassEntity c:
                return ClassMembers(c, name, []);
            case InstanceEntity i:
                return ClassMembers(i.Class, name, []);
            case SelfEntity s:
                return ClassMembers(s.Class, name, []);
        }

        _ = module;
        return [];
    }

    /// <summary>Member lookup following the class's base classes (depth-first, left-to-right).</summary>
    public IReadOnlyList<Declaration> ClassMembers(ClassEntity cls, string name, HashSet<ClassNode> seen, bool skipOwn = false)
    {
        if (!seen.Add(cls.Node))
        {
            return [];
        }

        if (!skipOwn && cls.Module.Scopes[cls.Node].Symbols.TryGetValue(name, out var sym) && sym.Declarations.Count > 0)
        {
            return sym.Declarations;
        }

        foreach (var b in BaseClasses(cls))
        {
            var found = ClassMembers(b, name, seen);
            if (found.Count > 0)
            {
                return found;
            }
        }

        return [];
    }

    public IEnumerable<ClassEntity> BaseClasses(ClassEntity cls)
    {
        foreach (var arg in cls.Node.Arguments)
        {
            if (arg.Name is null && arg.Category == 0 && EvalExpression(cls.Module, arg.Value is IndexNode ix ? ix.Left : arg.Value) is ClassEntity b)
            {
                yield return b;
            }
        }
    }
}
