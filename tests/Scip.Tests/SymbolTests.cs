using Scip;

namespace Scip.Tests;

public class SymbolTests
{
    private static SymbolDescriptor D(string name, SymbolDescriptor.Types.Suffix suffix, string disambiguator = "") =>
        new() { Name = name, Suffix = suffix, Disambiguator = disambiguator };

    [Test]
    public async Task ParsesLocalSymbol()
    {
        var s = SymbolParser.Parse("local a");
        await Assert.That(s.Scheme).IsEqualTo("local");
        await Assert.That(s.Package).IsNull();
        await Assert.That(s.Descriptors.Single()).IsEqualTo(D("a", SymbolDescriptor.Types.Suffix.Local));
    }

    [Test]
    public async Task ParsesSpaceEscapedPackageManager()
    {
        var s = SymbolParser.Parse("a b  c d e f.");
        await Assert.That(s.Package.Manager).IsEqualTo("b c");
        await Assert.That(s.Package.Name).IsEqualTo("d");
        await Assert.That(s.Package.Version).IsEqualTo("e");
    }

    [Test]
    public async Task ParsesBacktickEscapedDescriptorAndEmptyPackageFields()
    {
        var s = SymbolParser.Parse("cxx . . $ `<external>/4398592474888995393/assert.h:92:11`!");
        await Assert.That(s.Package.Manager).IsEqualTo("");
        await Assert.That(s.Package.Name).IsEqualTo("");
        await Assert.That(s.Package.Version).IsEqualTo("$");
        await Assert.That(s.Descriptors.Single())
            .IsEqualTo(D("<external>/4398592474888995393/assert.h:92:11", SymbolDescriptor.Types.Suffix.Macro));
    }

    [Test]
    public async Task ParsesAllDescriptorKinds()
    {
        var s = SymbolParser.Parse("lsif-java maven package 1.0.0 java/io/File#Entry.method(+1).(param)[TypeParam]");
        await Assert.That(s.Descriptors).IsEquivalentTo(new[]
        {
            D("java", SymbolDescriptor.Types.Suffix.Namespace),
            D("io", SymbolDescriptor.Types.Suffix.Namespace),
            D("File", SymbolDescriptor.Types.Suffix.Type),
            D("Entry", SymbolDescriptor.Types.Suffix.Term),
            D("method", SymbolDescriptor.Types.Suffix.Method, "+1"),
            D("param", SymbolDescriptor.Types.Suffix.Parameter),
            D("TypeParam", SymbolDescriptor.Types.Suffix.TypeParameter),
        });
    }

    [Test]
    public async Task ParsesNonAsciiEscapedIdentifier()
    {
        var s = SymbolParser.Parse("a b c d `F⃗`.");
        await Assert.That(s.Descriptors.Single().Name).IsEqualTo("F⃗");
    }

    [Test]
    [Arguments("")]
    [Arguments("lsif-java maven package 1.0.0")]
    [Arguments("lsif-java maven package 1.0.0 java/io/File#Entry.trailingstring")]
    [Arguments("lsif-java maven package 1.0.0 java/io/File#Entry.unrecognizedSuffix@")]
    [Arguments("lsif-java maven package 1.0.0 java/io/File#Entry.nonSimpλeIdentifier.")]
    [Arguments("lsif-java maven package 1.0.0 java/io/File#Entry.`unterminatedEscapedIdentifier")]
    [Arguments("lsif-java maven package 1.0.0 java/io/File#Entry.[UnterminatedDescriptorSuffix")]
    [Arguments("local 🧠")]
    [Arguments("local ")]
    [Arguments("local &&&")]
    public async Task RejectsMalformedSymbols(string symbol)
    {
        await Assert.That(SymbolParser.TryParse(symbol, out _, out var error)).IsFalse();
        await Assert.That(error).IsNotNull();
        await Assert.That(() => SymbolParser.Parse(symbol)).Throws<SymbolParseException>();
    }

    [Test]
    public async Task FormatsWithEscaping()
    {
        const string expected =
            "zeroth  escape first  escape second  escape third  escape `github.com/foo/``bar/n2/n3/n4`/T#f().";
        var symbol = new Symbol
        {
            Scheme = "zeroth escape",
            Package = new Package { Manager = "first escape", Name = "second escape", Version = "third escape" },
            Descriptors =
            {
                D("github.com/foo/`bar/n2/n3/n4", SymbolDescriptor.Types.Suffix.Namespace),
                D("T", SymbolDescriptor.Types.Suffix.Type),
                D("f", SymbolDescriptor.Types.Suffix.Method),
            },
        };
        await Assert.That(SymbolFormatter.Verbose.Format(symbol)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("lsif-java maven package 1.0.0 java/io/File#Entry.method(+1).(param)[TypeParam]")]
    [Arguments("rust-analyzer cargo std 1.0.0 macros/println!")]
    [Arguments("zeroth  escape first  escape second  escape third  escape `github.com/foo/``bar/n2/n3/n4`/T#f().")]
    [Arguments("local 42")]
    public async Task RoundTrips(string symbol) =>
        await Assert.That(SymbolFormatter.Verbose.Format(symbol)).IsEqualTo(symbol);

    [Test]
    public async Task DescriptorOnlyFormatterDropsPackage()
    {
        var formatted = SymbolFormatter.DescriptorOnly.Format("a b c d x/Y#");
        await Assert.That(formatted).IsEqualTo("x/Y#");
    }
}
