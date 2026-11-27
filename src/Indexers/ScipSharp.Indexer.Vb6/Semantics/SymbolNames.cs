using Suffix = ScipSharp.SymbolDescriptor.Types.Suffix;

namespace ScipSharp.Indexer.Vb6.Semantics;

/// <summary>
/// Builds SCIP symbol strings: <c>scip-vb6 vbp &lt;project&gt; &lt;version&gt; &lt;descriptors&gt;</c>. Modules are types
/// (<c>Form1#</c>), procedures/events methods (<c>Form1#Command1_Click().</c>), properties/variables/constants/controls terms
/// (<c>Form1#Command1.</c>), UDTs and enums nested types (<c>Module1#POINTAPI#x.</c>). External type-library classes use the
/// library as package with an unknown version: <c>scip-vb6 vbp VB . CommandButton#</c>.
/// </summary>
internal static class SymbolNames
{
    public const string Scheme = "scip-vb6";
    public const string Manager = "vbp";

    public static string Format(string package, string version, params (string Name, Suffix Suffix)[] descriptors)
    {
        var symbol = new Symbol
        {
            Scheme = Scheme,
            Package = new Package { Manager = Manager, Name = package, Version = version == "." ? "" : version },
        };
        foreach (var (name, suffix) in descriptors)
        {
            symbol.Descriptors.Add(new SymbolDescriptor { Name = name, Suffix = suffix });
        }

        return SymbolFormatter.Verbose.Format(symbol);
    }

    public static string Project(ProjectInfo p) => Format(p.PackageName, p.Version, (p.Name, Suffix.Namespace));

    public static string Module(ProjectInfo p, string module) => Format(p.PackageName, p.Version, (module, Suffix.Type));

    public static string Member(ProjectInfo p, string module, string member, Suffix suffix) =>
        Format(p.PackageName, p.Version, (module, Suffix.Type), (member, suffix));

    public static string Nested(ProjectInfo p, string module, string type, string member) =>
        Format(p.PackageName, p.Version, (module, Suffix.Type), (type, Suffix.Type), (member, Suffix.Term));

    public static string ExternalClass(string library, string cls) => Format(library, ".", (cls, Suffix.Type));

    public static string ExternalEvent(string library, string cls, string ev) => Format(library, ".", (cls, Suffix.Type), (ev, Suffix.Method));
}
