using System.Globalization;
using ScipSharp.Indexer.Java.Syntax;

namespace ScipSharp.Indexer.Java.Semantics;

/// <summary>Builds SCIP symbol strings for semantic entities (port of <c>GlobalSymbolsCache</c>); one instance per document.</summary>
public sealed class SymbolNamer
{
    private readonly Dictionary<object, string> _locals = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(object, string), string> _tparams = [];
    private int _counter;

    /// <summary>Every global symbol handed out, with the entity it names (used to synthesize external symbol information).</summary>
    public Dictionary<string, object> Entities { get; } = new(StringComparer.Ordinal);

    private string Note(string symbol, object entity)
    {
        if (!symbol.StartsWith("local ", StringComparison.Ordinal)) Entities.TryAdd(symbol, entity);
        return symbol;
    }

    private string LocalTypeParam(object owner, string name)
    {
        if (_tparams.TryGetValue((owner, name), out var s)) return s;
        s = "local " + (_counter++).ToString(CultureInfo.InvariantCulture);
        _tparams[(owner, name)] = s;
        return s;
    }

    public string Local(object key)
    {
        if (_locals.TryGetValue(key, out var s)) return s;
        s = "local " + (_counter++).ToString(CultureInfo.InvariantCulture);
        _locals[key] = s;
        return s;
    }

    public static string PackageBare(string pkg)
    {
        if (pkg.Length == 0) return "";
        return string.Concat(pkg.Split('/').Select(p => JavaSymbols.EncodeName(p) + "/"));
    }

    private static string Full(JavaPackage origin, string bare) => bare.StartsWith("local ", StringComparison.Ordinal) ? bare : $"{JavaPackage.Scheme} {origin.ScipTypedEncoding} {bare}";

    private static TypeInfo Top(TypeInfo t)
    {
        while (t.Outer is not null) t = t.Outer;
        return t;
    }

    private readonly Dictionary<TypeInfo, string> _bareTypes = new(ReferenceEqualityComparer.Instance);

    /// <summary>Memoised: recomputing the owner chain for every reference was cubic in the nesting depth of member classes.</summary>
    public string BareType(TypeInfo t)
    {
        if (_bareTypes.TryGetValue(t, out var cached)) return cached;
        var bare = BareTypeCore(t);
        _bareTypes[t] = bare;
        return bare;
    }

    private string BareTypeCore(TypeInfo t)
    {
        if (t.IsAnonymous) return Local(t);
        if (t.IsLocal && t.LocalOwner is { } lo)
        {
            var mo = BareMethod(lo);
            return mo.StartsWith("local ", StringComparison.Ordinal) ? Local(t) : mo + JavaSymbols.EncodeName(t.Simple) + "#";
        }
        if (t.IsLocal && t.Outer is null) return Local(t);
        var owner = t.Outer is null ? PackageBare(t.Package) : BareType(t.Outer);
        if (owner.StartsWith("local ", StringComparison.Ordinal)) return Local(t);
        return owner + JavaSymbols.EncodeName(t.Simple) + "#";
    }

    public string Type(TypeInfo t) => Note(Full(Top(t).Origin, BareType(t)), t);

    public string ClassLiteral(TypeInfo t)
    {
        var bare = BareType(t);
        if (bare.StartsWith("local ", StringComparison.Ordinal)) return "";
        return Note(Full(Top(t).Origin, bare + "class."), new ClassLiteralEntity(t));
    }

    /// <summary>Members javac models on its synthetic array class: <c>Array#length.</c> and <c>Array#clone().</c>.</summary>
    public string ArrayMember(string name, bool method) =>
        Note(Full(JavaPackage.Empty, "Array#" + name + (method ? "()." : ".")), new ArrayMemberEntity(name, method));

    /// <summary>javac models <c>this</c> / <c>super</c> as fields of the class; semanticdb-javac references them for <c>super</c> and <c>Outer.this</c>.</summary>
    public string ThisSuper(TypeInfo owner, string name)
    {
        var bare = BareType(owner);
        if (bare.StartsWith("local ", StringComparison.Ordinal)) return "";
        return Note(Full(Top(owner).Origin, bare + name + "."), new ThisSuperEntity(owner, name));
    }

    /// <summary><c>int.class</c> / <c>String[].class</c>: javac owns these by a synthetic class in the root package.</summary>
    public string SpecialClassLiteral(string owner) => Note(Full(JavaPackage.Empty, owner + "#class."), new SpecialClassLiteralEntity(owner));

    /// <summary><c>Feature[]::new</c>: javac resolves array constructor references to a synthetic constructor with index -1.</summary>
    public string ArrayConstructor() => Note(Full(JavaPackage.Empty, "Array#`<init>`(+-1)."), new ArrayMemberEntity("<init>", true));

    public string Package(string pkg) => Note(Full(JavaPackage.Empty, PackageBare(pkg)), new PackageEntity(pkg));

    public static string Disambiguator(MethodInfo m)
    {
        var methods = m.Owner.Methods;
        int index;
        if (methods.Count < 16) index = OverloadIndex(methods, m);
        else
        {
            // Indexed per method list: a linear scan per method symbol was quadratic in large (generated) classes.
            if (!OverloadIndexes.TryGetValue(methods, out var cached) || cached.Count != methods.Count)
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                var map = new Dictionary<MethodInfo, int>(ReferenceEqualityComparer.Instance);
                foreach (var x in methods)
                {
                    counts.TryGetValue(x.Name, out var c);
                    map.TryAdd(x, c);
                    counts[x.Name] = c + 1;
                }
                cached = new OverloadIndexCache(methods.Count, map);
                OverloadIndexes.AddOrUpdate(methods, cached);
            }
            index = cached.Index.TryGetValue(m, out var i) ? i : OverloadIndex(methods, m);
        }
        return index == 0 ? "()" : $"(+{index.ToString(CultureInfo.InvariantCulture)})";
    }

    private static int OverloadIndex(List<MethodInfo> methods, MethodInfo m)
    {
        var index = 0;
        foreach (var x in methods)
        {
            if (x == m) break;
            if (x.Name == m.Name) index++;
        }
        return index;
    }

    private sealed record OverloadIndexCache(int Count, Dictionary<MethodInfo, int> Index);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<List<MethodInfo>, OverloadIndexCache> OverloadIndexes = new();

    public string BareMethod(MethodInfo m)
    {
        var owner = BareType(m.Owner);
        if (owner.StartsWith("local ", StringComparison.Ordinal)) return Local(m);
        return owner + JavaSymbols.EncodeName(m.Name) + Disambiguator(m) + ".";
    }

    public string Method(MethodInfo m) => Note(Full(Top(m.Owner).Origin, BareMethod(m)), m);

    public string BareField(FieldInfo f)
    {
        var owner = BareType(f.Owner);
        if (owner.StartsWith("local ", StringComparison.Ordinal)) return Local(f);
        return owner + JavaSymbols.EncodeName(f.Name) + ".";
    }

    public string Field(FieldInfo f) => Note(Full(Top(f.Owner).Origin, BareField(f)), f);

    public string TypeParameter(TypeInfo owner, string name)
    {
        var o = BareType(owner);
        if (o.StartsWith("local ", StringComparison.Ordinal)) return LocalTypeParam(owner, name);
        return Full(Top(owner).Origin, o + "[" + JavaSymbols.EncodeName(name) + "]");
    }

    public string TypeParameter(MethodInfo owner, string name)
    {
        var o = BareMethod(owner);
        if (o.StartsWith("local ", StringComparison.Ordinal)) return LocalTypeParam(owner, name);
        return Full(Top(owner.Owner).Origin, o + "[" + JavaSymbols.EncodeName(name) + "]");
    }

    public string Variable(LocalVar v)
    {
        if (v.TermOwner is { } owner)
        {
            var bare = BareMethod(owner);
            if (!bare.StartsWith("local ", StringComparison.Ordinal)) return Full(Top(owner.Owner).Origin, bare + JavaSymbols.EncodeName(v.Name) + ".");
        }
        return Local(v);
    }
}

/// <summary>Entity behind a <c>Foo#class.</c> class-literal symbol.</summary>
public sealed record ClassLiteralEntity(TypeInfo Type);

/// <summary>Entity behind a package symbol.</summary>
public sealed record PackageEntity(string Name);

/// <summary>Entity behind an <c>Array#length.</c> / <c>Array#clone().</c> symbol.</summary>
public sealed record ArrayMemberEntity(string Name, bool IsMethod);

/// <summary>Entity behind a <c>Foo#this.</c> / <c>Foo#super.</c> symbol.</summary>
public sealed record ThisSuperEntity(TypeInfo Owner, string Name);

/// <summary>Entity behind an <c>int#class.</c> / <c>Array#class.</c> symbol.</summary>
public sealed record SpecialClassLiteralEntity(string Owner);
