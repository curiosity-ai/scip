namespace ScipSharp.Indexer.Abap;

/// <summary>
/// Builds the SCIP symbols of ABAP declarations. ABAP names are case-insensitive, so every name is upper-cased.
/// </summary>
/// <remarks>
/// Layout (descriptors after <c>scip-abap abap &lt;package&gt; &lt;version&gt;</c>):
/// <list type="bullet">
/// <item>global class or interface <c>ZCL_FOO#</c>, members <c>ZCL_FOO#BAR().</c>, <c>ZCL_FOO#ATTR.</c>, <c>ZCL_FOO#TY_X#</c>, parameters <c>ZCL_FOO#BAR().(P)</c></item>
/// <item>local class in an object <c>ZPROG/LCL_FOO#</c> (members as above)</item>
/// <item>program, include, function group, type pool <c>ZPROG/</c>; its global data <c>ZPROG/GV_X.</c>, types <c>ZPROG/TY_X#</c>, forms <c>ZPROG/FORM().</c></item>
/// <item>function module <c>Z_FM().</c> (names are unique in a system, so not nested in the function group), parameters <c>Z_FM().(P)</c></item>
/// <item>dictionary objects (tables, data elements, …) <c>ZTABLE#</c></item>
/// </list>
/// Names of SAP standard objects that are not in the indexed sources (void references) get the same descriptors in
/// the package <c>. .</c> (unknown package), so references to them can still be grouped.
/// </remarks>
internal sealed class AbapSymbols(string packageName, string packageVersion)
{
    public const string Scheme = "scip-abap";
    public const string Manager = "abap";

    private readonly Package _package = new() { Manager = Manager, Name = packageName == "." ? "" : packageName, Version = packageVersion == "." ? "" : packageVersion };
    private readonly Package _external = new() { Manager = Manager, Name = "", Version = "" };

    public static string Upper(string name) => name.TrimStart('!').ToUpperInvariant();

    public static SymbolDescriptor Namespace(string name) => new() { Name = Upper(name), Suffix = SymbolDescriptor.Types.Suffix.Namespace };

    public static SymbolDescriptor Type(string name) => new() { Name = Upper(name), Suffix = SymbolDescriptor.Types.Suffix.Type };

    public static SymbolDescriptor Term(string name) => new() { Name = Upper(name), Suffix = SymbolDescriptor.Types.Suffix.Term };

    public static SymbolDescriptor Method(string name) => new() { Name = Upper(name), Suffix = SymbolDescriptor.Types.Suffix.Method };

    public static SymbolDescriptor Parameter(string name) => new() { Name = Upper(name), Suffix = SymbolDescriptor.Types.Suffix.Parameter };

    public string Global(params IEnumerable<SymbolDescriptor> descriptors) => Format(_package, descriptors);

    public string External(params IEnumerable<SymbolDescriptor> descriptors) => Format(_external, descriptors);

    /// <summary>The symbol with one more descriptor appended.</summary>
    public static string Child(string parent, SymbolDescriptor descriptor)
    {
        var parsed = SymbolParser.Parse(parent);
        parsed.Descriptors.Add(descriptor);
        return SymbolFormatter.Verbose.Format(parsed);
    }

    public static bool IsExternal(string symbol) => symbol.StartsWith($"{Scheme} {Manager} . . ", StringComparison.Ordinal);

    private static string Format(Package package, IEnumerable<SymbolDescriptor> descriptors)
    {
        var symbol = new Symbol { Scheme = Scheme, Package = package.Clone() };
        symbol.Descriptors.AddRange(descriptors);
        return SymbolFormatter.Verbose.Format(symbol);
    }
}
