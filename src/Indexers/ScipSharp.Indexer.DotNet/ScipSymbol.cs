namespace ScipSharp.Indexer.DotNet;

/// <summary>A formatted SCIP symbol string, built with <see cref="SymbolFormatter"/>.</summary>
internal sealed class ScipSymbol
{
    private const string Scheme = "scip-dotnet";
    private const string Manager = "nuget";

    private static readonly SymbolFormatter Formatter = SymbolFormatter.Verbose;

    private ScipSymbol(string value) => Value = value;

    public string Value { get; }

    public static ScipSymbol Empty { get; } = new("");

    /// <summary>Package placeholder for symbols that are only visible inside this index.</summary>
    public static ScipSymbol IndexLocalPackage { get; } = Package(".", ".");

    /// <summary>False for the empty symbol and for a bare package (no descriptors, e.g. the global namespace).</summary>
    public bool HasDescriptors() => IsLocal() || (Value.Length > 0 && !Value.EndsWith(' '));

    public bool IsLocal() => Value.StartsWith("local ", StringComparison.Ordinal);

    /// <summary>True for a global symbol of a real external NuGet/BCL package (not the index-local placeholder).</summary>
    public bool IsExternalPackageSymbol() =>
        Value.StartsWith("scip-dotnet nuget ", StringComparison.Ordinal)
        && !Value.StartsWith(IndexLocalPackage.Value, StringComparison.Ordinal);

    public static ScipSymbol Global(ScipSymbol owner, SymbolDescriptor descriptor) =>
        new(owner.Value + Formatter.FormatDescriptors([descriptor]));

    public static ScipSymbol Local(int counter) => new("local " + counter);

    public static ScipSymbol Package(string name, string version) =>
        new(Formatter.Format(new Symbol
        {
            Scheme = Scheme,
            Package = new Package { Manager = Manager, Name = name, Version = version },
        }));
}
