namespace Scip.Indexer.Clang;

public enum EKind { Namespace, Class, Enum, EnumConst, Func, Var, Field, Typedef, Param, Local, TemplateParam }

public enum SKind { Global, Namespace, Class, Enum, Function, Block, Template }

/// <summary>A (possibly unresolved) reference to a type by name, resolved lazily from the scope where it was written.</summary>
public sealed record TypeRef(Scope Scope, List<string> Chain, bool Leading, bool Indirect = false);

public sealed class Entity(string name, EKind kind, string symbol, Scope declScope, bool inProject)
{
    public string Name { get; } = name;

    public EKind Kind { get; } = kind;

    public string Symbol { get; } = symbol;

    public Scope DeclScope { get; } = declScope;

    public bool InProject { get; } = inProject;

    /// <summary>For namespaces, classes and enums: the scope holding the members.</summary>
    public Scope? Own { get; set; }

    public SymbolInformation.Types.Kind SymbolKind { get; set; }

    public string Signature { get; set; } = "";

    public int MinArgs { get; set; }

    public int MaxArgs { get; set; }

    /// <summary>Canonical parameter types of a function (clang spelling), used to pick overloads.</summary>
    public List<string> ParamTypes { get; set; } = [];

    public bool Defined { get; set; }

    /// <summary>Descriptor path of the symbol (null for locals).</summary>
    public List<SymbolDescriptor>? Path { get; set; }

    public string Disambiguator { get; set; } = "";

    public bool IsVirtual { get; set; }

    /// <summary>The function's declared return type is an rvalue reference.</summary>
    public bool ReturnsRvalueRef { get; set; }

    /// <summary>Symbols this entity relates to (implementation of base classes / overridden methods).</summary>
    public List<(string Symbol, bool Reference)> Relationships { get; } = [];

    public bool IsStatic { get; set; }

    /// <summary>Entities that resolve names but never produce occurrences (namespace aliases).</summary>
    public bool Silent { get; set; }

    /// <summary>Declared type (variables, fields, typedefs) or return type (functions).</summary>
    public TypeRef? Type { get; set; }

    public List<TypeRef> Bases { get; } = [];

    /// <summary>For typedefs and aliases: the aliased type as tokens (declarator name removed) and the scope to resolve it in.</summary>
    public Tok[]? AliasToks { get; set; }

    public Scope? AliasScope { get; set; }

    /// <summary>For class templates: the template scope declaring the parameters.</summary>
    public Scope? TemplateScope { get; set; }

    public bool IsType => Kind is EKind.Class or EKind.Enum or EKind.Typedef or EKind.TemplateParam;
}

public sealed class Scope(Scope? parent, SKind kind)
{
    public Scope? Parent { get; } = parent;

    public SKind Kind { get; } = kind;

    public Entity? Owner { get; set; }

    /// <summary>Set on function-body scopes: the enclosing function's descriptor path (used for local classes).</summary>
    public List<SymbolDescriptor>? OwnFnPath { get; set; }

    public List<SymbolDescriptor>? FnPath => OwnFnPath ?? Parent?.FnPath;

    /// <summary>Symbol descriptors leading to this scope; null inside functions (declarations there are local).</summary>
    public List<SymbolDescriptor>? Path { get; set; }

    public Dictionary<string, List<Entity>> Names { get; } = [];

    public List<Scope> Usings { get; } = [];

    /// <summary>Template scopes: names of the template parameters in declaration order ("" when unnamed).</summary>
    public List<string> TemplateParamNames { get; } = [];

    public HashSet<string> TemplateTemplateNames { get; } = [];

    /// <summary>Where declarations made "in" this scope go (template scopes forward to their parent).</summary>
    public Scope Target => Kind == SKind.Template ? Parent!.Target : this;

    public Scope? NearestClass()
    {
        for (var s = this; s is not null; s = s.Parent)
        {
            if (s.Kind == SKind.Class)
            {
                return s;
            }
        }

        return null;
    }

    public Scope NearestNamespace()
    {
        var s = this;
        while (s.Kind is not (SKind.Namespace or SKind.Global))
        {
            s = s.Parent!;
        }

        return s;
    }

    public void Add(Entity e, string? key = null)
    {
        var k = key ?? e.Name;
        if (!Names.TryGetValue(k, out var list))
        {
            Names[k] = list = [];
        }

        if (!list.Contains(e))
        {
            list.Add(e);
        }
    }

    /// <summary>Looks a name up in this scope only, following base classes and using-directives.</summary>
    public List<Entity>? LookupHere(string name, HashSet<Scope>? seen = null)
    {
        seen ??= [];
        if (!seen.Add(this))
        {
            return null;
        }

        if (Names.TryGetValue(name, out var l) && l.Count > 0)
        {
            return l;
        }

        if (Kind == SKind.Class && Owner is { } cls)
        {
            foreach (var b in cls.Bases)
            {
                if (SymbolTable.ResolveType(b)?.Own is { } bs && bs.LookupHere(name, seen) is { } found)
                {
                    return found;
                }
            }
        }

        foreach (var u in Usings)
        {
            if (u.LookupHere(name, seen) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    public List<Entity>? Lookup(string name)
    {
        for (var s = this; s is not null; s = s.Parent)
        {
            if (s.LookupHere(name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}

public static class SymbolTable
{
    public static Entity? ResolveType(TypeRef? t, int depth = 0)
    {
        if (t is null || depth > 8 || t.Chain.Count == 0)
        {
            return null;
        }

        var scope = t.Leading ? GlobalOf(t.Scope) : t.Scope;
        Entity? cur = null;
        for (var i = 0; i < t.Chain.Count; i++)
        {
            var found = i == 0 ? (t.Leading ? scope.LookupHere(t.Chain[0]) : scope.Lookup(t.Chain[0])) : cur?.Own?.LookupHere(t.Chain[i]);
            cur = found?.FirstOrDefault(static e => e.Kind != EKind.Func) ?? found?.FirstOrDefault();
            if (cur is null)
            {
                return null;
            }

            if (cur.Kind == EKind.Typedef)
            {
                cur = ResolveType(cur.Type, depth + 1);
                if (cur is null)
                {
                    return null;
                }
            }
        }

        return cur;
    }

    private static Scope GlobalOf(Scope s)
    {
        while (s.Parent is not null)
        {
            s = s.Parent;
        }

        return s;
    }
}
