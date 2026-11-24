using Scip.Indexer.Python.Semantics;

namespace Scip.Indexer.Python.Emit;

/// <summary>
/// Symbol construction, a port of <c>symbols.ts</c>, <c>ScipSymbol.ts</c> and the descriptor printer of
/// <c>lsif-typescript</c>. Strings are produced with <see cref="SymbolFormatter"/>.
/// </summary>
public sealed class SymbolNames(IReadOnlyDictionary<string, string> namespaces)
{
    public static bool IsLocal(string symbol) => symbol.StartsWith("local ", StringComparison.Ordinal);

    public static string Local(int counter) => "local " + counter.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Package(PythonPackage p) => Package(p.Name, p.Version);

    public static string Package(string name, string version) =>
        SymbolFormatter.Verbose.Format(new Symbol
        {
            Scheme = "scip-python",
            Package = new Package { Manager = "python", Name = name, Version = version },
        });

    private static string Descriptor(string name, SymbolDescriptor.Types.Suffix suffix) =>
        SymbolFormatter.Verbose.FormatDescriptors([new SymbolDescriptor { Name = name, Suffix = suffix }]);

    public static string Global(string owner, string name, SymbolDescriptor.Types.Suffix suffix) => owner + Descriptor(name, suffix);

    private string Namespaced(PythonPackage p, string moduleName) =>
        namespaces.TryGetValue(p.Name, out var ns) && ns.Length > 0 ? ns + "." + moduleName : moduleName;

    public string Module(PythonPackage p, string moduleName) =>
        Global(Package(p), Namespaced(p, moduleName), SymbolDescriptor.Types.Suffix.Namespace);

    public string ModuleInit(PythonPackage p, string moduleName) =>
        Global(Module(p, moduleName), "__init__", SymbolDescriptor.Types.Suffix.Meta);

    public string Class(PythonPackage p, string moduleName, string name) =>
        Global(Module(p, moduleName), name, SymbolDescriptor.Types.Suffix.Type);

    public static string Parameter(string owner, string name) => Global(owner, name, SymbolDescriptor.Types.Suffix.Parameter);

    public static string Method(string owner, string name) => Global(owner, name, SymbolDescriptor.Types.Suffix.Method);

    public static string Type(string owner, string name) => Global(owner, name, SymbolDescriptor.Types.Suffix.Type);

    public static string Term(string owner, string name) => Global(owner, name, SymbolDescriptor.Types.Suffix.Term);
}
