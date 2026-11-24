using System.Globalization;
using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Semantics;

/// <summary>Builds SCIP symbol strings for semantic entities (port of <c>GlobalSymbolsCache</c>); one instance per document.</summary>
public sealed class SymbolNamer
{
    private readonly Dictionary<object, string> _locals = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(object, string), string> _tparams = [];
    private int _counter;

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

    private static string Full(JavaPackage origin, string bare) => bare.StartsWith("local ", StringComparison.Ordinal) ? bare : $"scip-java {origin.ScipTypedEncoding} {bare}";

    private static TypeInfo Top(TypeInfo t)
    {
        while (t.Outer is not null) t = t.Outer;
        return t;
    }

    public string BareType(TypeInfo t)
    {
        if (t.IsAnonymous || t.IsLocal) return Local(t);
        var owner = t.Outer is null ? PackageBare(t.Package) : BareType(t.Outer);
        if (owner.StartsWith("local ", StringComparison.Ordinal)) return Local(t);
        return owner + JavaSymbols.EncodeName(t.Simple) + "#";
    }

    public string Type(TypeInfo t) => Full(Top(t).Origin, BareType(t));

    public string ClassLiteral(TypeInfo t) => Full(Top(t).Origin, BareType(t) + "class.");

    public string Package(string pkg) => Full(JavaPackage.Empty, PackageBare(pkg));

    public static string Disambiguator(MethodInfo m)
    {
        var index = 0;
        foreach (var x in m.Owner.Methods)
        {
            if (x == m) break;
            if (x.Name == m.Name) index++;
        }
        return index == 0 ? "()" : $"(+{index.ToString(CultureInfo.InvariantCulture)})";
    }

    public string BareMethod(MethodInfo m)
    {
        var owner = BareType(m.Owner);
        if (owner.StartsWith("local ", StringComparison.Ordinal)) return Local(m);
        return owner + JavaSymbols.EncodeName(m.Name) + Disambiguator(m) + ".";
    }

    public string Method(MethodInfo m) => Full(Top(m.Owner).Origin, BareMethod(m));

    public string BareField(FieldInfo f)
    {
        var owner = BareType(f.Owner);
        if (owner.StartsWith("local ", StringComparison.Ordinal)) return Local(f);
        return owner + JavaSymbols.EncodeName(f.Name) + ".";
    }

    public string Field(FieldInfo f) => Full(Top(f.Owner).Origin, BareField(f));

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

    public string Variable(LocalVar v) => Local(v);
}
