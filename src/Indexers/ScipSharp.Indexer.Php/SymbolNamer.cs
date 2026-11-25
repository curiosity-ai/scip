using ScipSharp.Indexer.Php.Composer;
using ScipSharp.Indexer.Php.Syntax;

namespace ScipSharp.Indexer.Php;

/// <summary>Port of <c>ScipPhp\SymbolNamer</c>: builds scip-php symbol strings for syntax nodes.</summary>
internal sealed class SymbolNamer(ComposerProject composer)
{
    private const string Scheme = "scip-php";
    private const string Manager = "composer";

    /// <summary>Returns the fully-qualified class, function or constant name of the given symbol (null if malformed).</summary>
    public string? ExtractIdent(string symbol)
    {
        var parts = symbol.Split(' ');
        if (parts.Length != 5)
        {
            return null;
        }

        var desc = parts[4];
        var i = desc.IndexOf('#', StringComparison.Ordinal);
        if (i >= 0)
        {
            desc = desc[..i];
        }

        desc = desc.Replace('/', '\\').TrimEnd('.').TrimEnd('(', ')');
        return desc == "" ? null : desc;
    }

    private static bool IsSimple(string s)
    {
        foreach (var c in s)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '+' or '$'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Makes the descriptor part of a symbol conform to scip.proto: identifier runs containing characters outside the
    /// simple-identifier set (non-ASCII PHP identifiers) are wrapped in backticks.
    /// </summary>
    public static string Escape(string symbol)
    {
        var d = 0;
        for (var spaces = 0; d < symbol.Length && spaces < 4; d++)
        {
            if (symbol[d] == ' ')
            {
                spaces++;
            }
        }

        var needs = false;
        for (var i = d; i < symbol.Length; i++)
        {
            var c = symbol[i];
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '+' or '$' or '/' or '#' or '.' or '(' or ')'))
            {
                needs = true;
                break;
            }
        }

        if (!needs)
        {
            return symbol;
        }

        var b = new System.Text.StringBuilder(symbol.Length + 8);
        b.Append(symbol, 0, d);
        var run = new System.Text.StringBuilder();

        void Flush()
        {
            if (run.Length == 0)
            {
                return;
            }

            var text = run.ToString();
            run.Clear();
            if (IsSimple(text))
            {
                b.Append(text);
            }
            else
            {
                b.Append('`').Append(text.Replace("`", "``", StringComparison.Ordinal)).Append('`');
            }
        }

        for (var i = d; i < symbol.Length; i++)
        {
            var c = symbol[i];
            if (c is '/' or '#' or '.' or '(' or ')')
            {
                Flush();
                b.Append(c);
            }
            else
            {
                run.Append(c);
            }
        }

        Flush();
        return b.ToString();
    }

    public string NameConst(string symbol, string c) => $"{symbol}{c}.";

    public string NameMeth(string symbol, string m) => $"{symbol}{m}().";

    public string NameParam(string symbol, string p) => $"{symbol}(${p})";

    public string NameProp(string symbol, string p) => $"{symbol}${p}.";

    /// <summary>Symbol of a (built-in) class by its fully-qualified name.</summary>
    public string? ClassSymbol(string fqName) => Desc(fqName.Replace('\\', '/'), "#");

    public string? NameNearestClassLike(Node n)
    {
        var ns = NamespaceName(n);
        var cls = ClassLikeName(n);
        return cls == null ? null : Desc($"{ns}{cls}", "#");
    }

    public string? Name(Node n)
    {
        switch (n)
        {
            case Closure or ArrowFunction:
            {
                var ns = NamespaceName(n);
                return Desc($"{ns}anon-func-{n.TokPos}", "().");
            }

            case ConstItem c:
            {
                var ns = NamespaceName(c);
                var cls = ClassLikeName(c);
                return cls == null ? null : Desc($"{ns}{cls}", $"#{c.Name.Name}.");
            }

            case ClassLike cl:
            {
                var ns = NamespaceName(cl);
                var cls = cl.Name?.Name;
                if (string.IsNullOrEmpty(cls))
                {
                    cls = $"anon-class-{cl.TokPos}";
                }

                return Desc($"{ns}{cls}", "#");
            }

            case ClassMethod m:
            {
                var ns = NamespaceName(m);
                var cls = ClassLikeName(m);
                return cls == null ? null : Desc($"{ns}{cls}", $"#{m.Name.Name}().");
            }

            case EnumCase e:
            {
                var ns = NamespaceName(e);
                var cls = ClassLikeName(e);
                return cls == null ? null : Desc($"{ns}{cls}", $"#{e.Name.Name}.");
            }

            case FunctionStmt f when f.Name.Name != "":
            {
                var name = f.NamespacedName != "" ? f.NamespacedName : f.Name.Name;
                return Desc(name.Replace('\\', '/'), "().");
            }

            case Name nm when nm.Text != "":
                return NameOfName(nm);

            case Param { Var.Name: { Length: > 0 } pv } p:
            {
                var fn = FuncLikeName(p);
                return fn == null ? null : NameParam(fn, pv);
            }

            case PropertyItem pi:
            {
                var ns = NamespaceName(pi);
                var cls = ClassLikeName(pi);
                return cls == null ? null : Desc($"{ns}{cls}", $"#${pi.Name.Name}.");
            }
        }

        return null;
    }

    private string? NameOfName(Name n)
    {
        var text = n.Text;
        if (text is "self" or "static" || text.Equals("self", StringComparison.OrdinalIgnoreCase) || text.Equals("static", StringComparison.OrdinalIgnoreCase))
        {
            if (n.Kind != NameKind.Unqualified)
            {
                return NameOfPlainName(n);
            }

            var ns = NamespaceName(n);
            var cls = ClassLikeName(n);
            return cls == null ? null : Desc($"{ns}{cls}", "#");
        }

        if (text.Equals("parent", StringComparison.OrdinalIgnoreCase) && n.Kind == NameKind.Unqualified)
        {
            var cl = ClassLike(n);
            switch (cl)
            {
                case ClassStmt { Extends: { } ext }:
                    return Name(ext);
                default:
                    // No parent class, a trait, or an unexpected node type.
                    return null;
            }
        }

        return NameOfPlainName(n);
    }

    private string? NameOfPlainName(Name n)
    {
        var name = n.Text;
        var desc = name.Replace('\\', '/');
        if (composer.IsConst(name))
        {
            return Desc(desc, ".");
        }

        if (composer.IsClassLike(name))
        {
            return Desc(desc, "#");
        }

        if (!IsClassContext(n) && composer.IsFunc(name))
        {
            return Desc(desc, "().");
        }

        return null;
    }

    /// <summary>True when the name can only denote a class (e.g. <c>Event::listen()</c>, <c>new Event</c>, <c>use Event;</c>), never a function.</summary>
    private static bool IsClassContext(Name n) => n.Parent switch
    {
        StaticCall sc => sc.Class == n,
        StaticPropertyFetch sp => sp.Class == n,
        ClassConstFetch cf => cf.Class == n,
        NewExpr => true,
        UseItem { Type: not 2 and not 3 } ui => ui.Parent is not UseStmt { Type: 2 or 3 },
        _ => false,
    };

    private static string NamespaceName(Node n)
    {
        for (var p = n.Parent; p != null; p = p.Parent)
        {
            if (p is NamespaceStmt ns)
            {
                var name = (ns.Name?.Text ?? "").Replace('\\', '/');
                return name != "" ? name + "/" : "";
            }
        }

        return "";
    }

    private static string? ClassLikeName(Node n)
    {
        var c = ClassLike(n);
        if (c == null)
        {
            return null;
        }

        var name = c.Name?.Name;
        return string.IsNullOrEmpty(name) ? $"anon-class-{c.TokPos}" : name;
    }

    private static ClassLike? ClassLike(Node n)
    {
        for (var p = n.Parent; p != null; p = p.Parent)
        {
            if (p is ClassLike c)
            {
                return c;
            }
        }

        return null;
    }

    public string? FuncLikeName(Node n)
    {
        for (var p = n.Parent; p != null; p = p.Parent)
        {
            if (p is IFunctionLike)
            {
                return Name(p);
            }
        }

        return null;
    }

    private string? Desc(string name, string suffix)
    {
        var c = name.Replace('/', '\\');
        var pkg = composer.Pkg(c);
        if (pkg == null)
        {
            return null;
        }

        return $"{Scheme} {Manager} {pkg.Value.Name} {pkg.Value.Version} {name}{suffix}";
    }
}
