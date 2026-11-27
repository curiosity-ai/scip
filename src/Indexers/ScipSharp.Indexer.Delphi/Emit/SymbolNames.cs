using System.Text;
using ScipSharp.Indexer.Delphi.Semantics;

namespace ScipSharp.Indexer.Delphi.Emit;

/// <summary>
/// Builds <c>scip-delphi</c> symbols: <c>scip-delphi delphi &lt;package&gt; &lt;version&gt; &lt;descriptors&gt;</c> where a unit
/// <c>System.SysUtils</c> contributes the namespaces <c>System/SysUtils/</c>, types end in <c>#</c>, routines in
/// <c>(disambiguator).</c>, other members in <c>.</c>, type parameters are <c>[T]</c> and parameters <c>(Name)</c>.
/// </summary>
internal static class SymbolNames
{
    public const string Scheme = "scip-delphi";

    public static bool IsSimple(string s)
    {
        if (s.Length == 0)
        {
            return false;
        }

        foreach (var c in s)
        {
            if (!(c is '_' or '+' or '-' or '$' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }

    public static string Escape(string name) => IsSimple(name) ? name : "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`";

    public static string PackagePart(string s)
    {
        if (s.Length == 0)
        {
            return ".";
        }

        return s.Replace(' ', '-');
    }

    public static string Global(Entity e)
    {
        if (e.SymbolCache is { } cached)
        {
            return cached;
        }

        var unit = e.Unit ?? e;
        var pkg = unit.Package ?? e.Package ?? new PackageInfo("delphi", "unknown", ".");
        var sym = $"{Scheme} {PackagePart(pkg.Manager)} {PackagePart(pkg.Name)} {PackagePart(pkg.Version)} {Descriptor(e)}";
        e.SymbolCache = sym;
        return sym;
    }

    public static string Descriptor(Entity e)
    {
        var b = new StringBuilder();
        Append(b, e, 0);
        return b.ToString();
    }

    private static void Append(StringBuilder b, Entity e, int depth)
    {
        if (depth > 64)
        {
            return;
        }

        if (e.AnonymousOwner is { } owner)
        {
            Append(b, owner, depth + 1);
            return;
        }

        if (e.IsUnitLike)
        {
            foreach (var seg in e.UnitSegments)
            {
                b.Append(Escape(seg)).Append('/');
            }

            return;
        }

        if (e.Parent is { } parent)
        {
            Append(b, parent, depth + 1);
        }

        var name = e.Name.Length == 0 ? "_" : e.Name;
        switch (e.Kind)
        {
            case EntityKind.TypeParam:
                b.Append('[').Append(Escape(name)).Append(']');
                break;
            case EntityKind.Param:
                b.Append('(').Append(Escape(name)).Append(')');
                break;
            case EntityKind.Method or EntityKind.Routine:
                b.Append(Escape(name)).Append('(').Append(e.Disambiguator).Append(").");
                break;
            default:
                if (e.IsType)
                {
                    b.Append(Escape(name + e.NameSuffix)).Append('#');
                }
                else
                {
                    b.Append(Escape(name)).Append('.');
                }

                break;
        }
    }
}
