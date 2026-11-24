namespace Scip.Indexer.Rust;

using Kind = SymbolInformation.Types.Kind;
using Suffix = SymbolDescriptor.Types.Suffix;

/// <summary>One descriptor of a moniker (port of <c>ide::MonikerDescriptor</c>).</summary>
public readonly record struct MonikerDescriptor(string Name, Suffix Kind);

/// <summary>Cargo package a symbol belongs to (port of <c>ide::PackageInformation</c>).</summary>
public sealed record PackageInformation(string Name, string? Version);

/// <summary>A non-local rust-analyzer moniker.</summary>
public sealed record Moniker(PackageInformation Package, IReadOnlyList<MonikerDescriptor> Descriptors);

/// <summary>
/// Port of the symbol-scheme logic of <c>ide/src/moniker.rs</c> and the moniker-to-symbol conversion in
/// <c>cli/scip.rs</c>.
/// </summary>
public static class RustMonikers
{
    public const string Scheme = "rust-analyzer";
    public const string PackageManager = "cargo";

    /// <summary>Port of <c>impl From&lt;SymbolInformationKind&gt; for MonikerDescriptorKind</c>.</summary>
    public static Suffix DescriptorKindOf(Kind kind) => kind switch
    {
        Kind.AssociatedType or Kind.Enum or Kind.EnumMember or Kind.Struct or Kind.Trait or Kind.Type
            or Kind.TypeAlias or Kind.Union => Suffix.Type,
        Kind.Attribute => Suffix.Meta,
        Kind.Constant or Kind.Field or Kind.StaticVariable or Kind.Variable => Suffix.Term,
        Kind.Function or Kind.Method or Kind.StaticMethod or Kind.TraitMethod => Suffix.Method,
        Kind.Macro => Suffix.Macro,
        Kind.Module => Suffix.Namespace,
        Kind.Parameter or Kind.SelfParameter => Suffix.Parameter,
        Kind.TypeParameter => Suffix.TypeParameter,
        _ => Suffix.Term,
    };

    /// <summary>
    /// Descriptors naming the crate root of a cargo target (the crate-root branch of
    /// <c>def_to_non_local_moniker</c>). <paramref name="targetKind"/> is a value from <see cref="NormalizeTargetKind"/>.
    /// </summary>
    public static IReadOnlyList<MonikerDescriptor> CrateDescriptors(string targetKind, string targetName) =>
        [new("crate", Suffix.Namespace)];

    /// <summary>
    /// Internally every descriptor chain starts with a crate-root descriptor (the crate name for sysroot crates,
    /// <c>crate</c> for cargo targets). The shipped rust-analyzer keeps it only on the root module itself (<c>crate/</c>);
    /// all other symbols start at the first item below the root.
    /// </summary>
    private static Moniker Normalize(Moniker m)
    {
        var d = m.Descriptors;
        if (d.Count == 0 || d[0].Kind != Suffix.Namespace)
        {
            return m;
        }

        var isStd = m.Package.Version is { } v && v.StartsWith("https://github.com/rust-lang/rust/library/", StringComparison.Ordinal);
        if (d[0].Name != "crate" && !(isStd && d[0].Name == m.Package.Name))
        {
            return m;
        }

        return d.Count == 1 ? new Moniker(m.Package, [new("crate", Suffix.Namespace)]) : new Moniker(m.Package, [.. d.Skip(1)]);
    }

    /// <summary>Maps cargo's <c>kind</c> array of a target to the kinds rust-analyzer distinguishes.</summary>
    public static string NormalizeTargetKind(IEnumerable<string> cargoKinds)
    {
        foreach (var k in cargoKinds)
        {
            switch (k)
            {
                case "lib" or "rlib" or "dylib" or "cdylib" or "staticlib": return "lib";
                case "proc-macro" or "bin" or "example" or "test" or "bench" or "custom-build": return k;
            }
        }

        return "other";
    }

    /// <summary>Port of <c>moniker_to_symbol</c>.</summary>
    public static Symbol ToSymbol(Moniker moniker)
    {
        var symbol = new Symbol
        {
            Scheme = Scheme,
            Package = new Package
            {
                Manager = PackageManager,
                Name = moniker.Package.Name,
                Version = moniker.Package.Version ?? ".",
            },
        };
        foreach (var d in moniker.Descriptors)
        {
            symbol.Descriptors.Add(new SymbolDescriptor { Name = d.Name, Suffix = d.Kind });
        }

        return symbol;
    }

    public static string Format(Moniker moniker) => SymbolFormatter.Verbose.Format(ToSymbol(Normalize(moniker)));

    public static string LocalSymbol(int index) => $"local {index}";

    /// <summary>
    /// True for the symbol of an inherent impl block itself (<c>.., impl#, [SelfType]</c>); only the first one is
    /// emitted as <see cref="SymbolInformation"/> because their names are not disambiguated (upstream issue #18772).
    /// </summary>
    public static bool IsInherentImplBlock(IReadOnlyList<MonikerDescriptor> descriptors) =>
        descriptors is [.., { Kind: Suffix.Type, Name: "impl" }, _];

    /// <summary>Same check on a formatted symbol string.</summary>
    public static bool IsInherentImplBlock(string symbol)
    {
        if (!SymbolParser.TryParse(symbol, out var parsed, out _))
        {
            return false;
        }

        var d = parsed.Descriptors;
        return d.Count >= 2 && d[^2].Suffix == Suffix.Type && d[^2].Name == "impl";
    }
}
