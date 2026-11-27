using System.Text;

namespace ScipSharp.Indexer.Cobol.Analysis;

/// <summary>
/// Builds SCIP symbols: <c>scip-cobol cobol &lt;package&gt; &lt;version&gt; &lt;descriptors&gt;</c>.
/// <list type="bullet">
/// <item>program <c>PROG/</c>, nested program <c>OUTER/INNER/</c>, class <c>CLS#</c>, method <c>CLS#METH().</c>, function <c>FN().</c></item>
/// <item>data item <c>PROG/REC.GROUP.FIELD.</c> (named ancestors), 88-level <c>PROG/REC.FLAG.IS-ON.</c>, index name <c>PROG/TABLE.IDX.</c></item>
/// <item>section <c>PROG/SECT().</c>, paragraph <c>PROG/PARA().</c> or <c>PROG/SECT().PARA().</c>, entry <c>PROG/NAME(entry).</c></item>
/// <item>file <c>PROG/CUSTFILE#</c>, special name <c>PROG/TOP-OF-PAGE.</c></item>
/// <item>copybook <c>`path/BOOK.cpy`/</c>; anything defined in a copybook lives under it instead of the program, so a
/// copybook field is one symbol across every program that copies it.</item>
/// </list>
/// Names are upper-cased (COBOL is case-insensitive).
/// </summary>
internal sealed class SymbolNamer(string packageName, string packageVersion, Func<int, string?> copybookPath)
{
    public const string Scheme = "scip-cobol";
    public const string Manager = "cobol";

    public string Package { get; } = $"{Scheme} {Manager} {EscapePackage(packageName)} {EscapePackage(packageVersion)} ";

    public static string EscapePackage(string s) => s.Length == 0 ? "." : s.Replace(" ", "  ", StringComparison.Ordinal);

    public static string Descriptor(string name, char suffix)
    {
        var b = new StringBuilder();
        AppendName(b, name);
        switch (suffix)
        {
            case '(':
                b.Append("().");
                break;
            default:
                b.Append(suffix);
                break;
        }

        return b.ToString();
    }

    private static void AppendName(StringBuilder b, string name)
    {
        if (name.Length > 0 && IsSimpleIdentifier(name))
        {
            b.Append(name);
        }
        else
        {
            b.Append('`').Append(name.Replace("`", "``", StringComparison.Ordinal)).Append('`');
        }
    }

    /// <summary>Same rule as the SCIP symbol grammar's simple identifiers.</summary>
    public static bool IsSimpleIdentifier(string s) =>
        s.Length > 0 && s.All(static c => c is '_' or '+' or '-' or '$' || char.IsAsciiLetterOrDigit(c));

    public static string UnitPrefix(ProgramUnit unit)
    {
        var parts = new List<string>();
        for (var u = unit; u is not null; u = u.Parent)
        {
            if (u.IsPseudo)
            {
                continue;
            }

            parts.Insert(0, Descriptor(u.Name, u.UnitKind switch
            {
                UnitKind.Class => '#',
                UnitKind.Method or UnitKind.Function => '(',
                _ => '/',
            }));
        }

        return string.Concat(parts);
    }

    public string CopybookDescriptor(int fileId)
    {
        var path = copybookPath(fileId) ?? "";
        if (IsSimpleIdentifier(path))
        {
            path = "./" + path;
        }

        return Descriptor(path, '/');
    }

    public string CopybookSymbol(int fileId) => Package + CopybookDescriptor(fileId);

    /// <summary>Descriptor prefix for a definition owned by <paramref name="owner"/> (a copybook id, or -1) inside <paramref name="unit"/>.</summary>
    public string Prefix(ProgramUnit unit, int owner) =>
        owner >= 0 && copybookPath(owner) is not null ? CopybookDescriptor(owner) : UnitPrefix(unit);

    /// <summary>Normalised owner: copybooks outside the project root have no document, so their definitions belong to the program.</summary>
    public int EffectiveOwner(int owner) => owner >= 0 && copybookPath(owner) is not null ? owner : -1;

    public string ForUnit(ProgramUnit unit) => Package + UnitPrefix(unit);

    public string ForItem(ProgramUnit unit, DataItem item)
    {
        var owner = EffectiveOwner(item.Owner);
        return Package + Prefix(unit, owner) + Chain(item, owner);
    }

    private string Chain(DataItem item, int owner)
    {
        var names = new List<string> { Descriptor(item.Name, '.') };
        for (var p = item.Parent; p is not null && EffectiveOwner(p.Owner) == owner; p = p.Parent)
        {
            if (p.IsNamed)
            {
                names.Insert(0, Descriptor(p.Name, '.'));
            }
        }

        return string.Concat(names);
    }

    public string ForProcedure(ProgramUnit unit, Procedure p)
    {
        var owner = EffectiveOwner(p.Owner);
        var section = p.Section is { } s && EffectiveOwner(s.Owner) == owner ? Descriptor(s.Name, '(') : "";
        return Package + Prefix(unit, owner) + section + Descriptor(p.Name, '(');
    }

    public string ForFile(ProgramUnit unit, FileDefinition f) => Package + Prefix(unit, EffectiveOwner(f.Owner)) + Descriptor(f.Name, '#');

    public string ForSimple(ProgramUnit unit, SimpleDefinition d)
    {
        var owner = EffectiveOwner(d.Owner);
        return d.SimpleKind switch
        {
            SimpleKind.Entry => Package + Prefix(unit, owner) + EntryDescriptor(d.Name),
            SimpleKind.Index when d.Item is { } item && EffectiveOwner(item.Owner) == owner => Package + Prefix(unit, owner) + Chain(item, owner) + Descriptor(d.Name, '.'),
            _ => Package + Prefix(unit, owner) + Descriptor(d.Name, '.'),
        };
    }

    private static string EntryDescriptor(string name)
    {
        var b = new StringBuilder();
        AppendName(b, name);
        b.Append("(entry).");
        return b.ToString();
    }

    /// <summary>Symbol for a CALL target that no indexed program defines.</summary>
    public string ForExternalProgram(string name) => Package + Descriptor(name.ToUpperInvariant(), '/');
}
