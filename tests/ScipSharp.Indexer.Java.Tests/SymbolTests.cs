namespace ScipSharp.Indexer.Java.Tests;

/// <summary>Ports of upstream SymbolDescriptorTest and JdkPackageTest.</summary>
public class SymbolTests
{
    [Test]
    [Arguments("Test#", DescriptorKind.Type, "Test", "_root_/")]
    [Arguments("sample/Test#", DescriptorKind.Type, "Test", "sample/")]
    [Arguments("sample/Test#m1().", DescriptorKind.Method, "m1", "sample/Test#")]
    [Arguments("sample/Test#m1().(t1)", DescriptorKind.Parameter, "t1", "sample/Test#m1().")]
    [Arguments("sample/Test#m1().[T1]", DescriptorKind.TypeParameter, "T1", "sample/Test#m1().")]
    [Arguments("sample/Test#field.", DescriptorKind.Term, "field", "sample/Test#")]
    [Arguments("sample/Test#`<init>`().", DescriptorKind.Method, "<init>", "sample/Test#")]
    [Arguments("sample/Test#Inner#", DescriptorKind.Type, "Inner", "sample/Test#")]
    [Arguments("sample/", DescriptorKind.Package, "sample", "_root_/")]
    public async Task ParsesDescriptor(string symbol, DescriptorKind kind, string name, string owner)
    {
        var parsed = JavaSymbols.Parse(symbol);
        await Assert.That(parsed.Descriptor.Name).IsEqualTo(name);
        await Assert.That(parsed.Owner).IsEqualTo(owner);
        await Assert.That(parsed.Descriptor.Kind).IsEqualTo(kind);
    }

    [Test]
    public async Task TopLevelWalksOwners()
    {
        var top = JavaSymbols.TopLevel("com/ex/Outer#Inner#m().(x)");
        await Assert.That(top!.Descriptor.Name).IsEqualTo("Outer");
        await Assert.That(top.Owner).IsEqualTo("com/ex/");
        await Assert.That(JavaSymbols.TopLevel("com/ex/")).IsNull();
        await Assert.That(JavaSymbols.TopLevel("local 3")).IsNull();
    }

    [Test]
    public async Task EncodesNames()
    {
        await Assert.That(new JavaDescriptor(DescriptorKind.Method, "<init>", "()").Encode()).IsEqualTo("`<init>`().");
        await Assert.That(JavaSymbols.Global("sample/", new JavaDescriptor(DescriptorKind.Type, "Test"))).IsEqualTo("sample/Test#");
        await Assert.That(JavaSymbols.Global(JavaSymbols.RootPackage, new JavaDescriptor(DescriptorKind.Type, "Test"))).IsEqualTo("Test#");
        await Assert.That(JavaSymbols.EncodeName("")).IsEqualTo("``");
    }

    [Test]
    public async Task RewrittenSymbolsParseWithScipSymbolParser()
    {
        var full = $"semanticdb {new MavenPackage("x.jar", "g", "a", "1.0").ScipTypedEncoding} sample/Test#m1().";
        var parsed = SymbolParser.Parse(full);
        await Assert.That(parsed.Scheme).IsEqualTo("semanticdb");
        await Assert.That(parsed.Package.Version).IsEqualTo("1.0");
    }

    [Test]
    [Arguments("11.0.9", "11")]
    [Arguments("17.0.5", "17")]
    [Arguments("21", "21")]
    public async Task JdkKeepsMajorVersion(string input, string expected) =>
        await Assert.That(JdkPackage.Parse(input).Version).IsEqualTo(expected);
}
