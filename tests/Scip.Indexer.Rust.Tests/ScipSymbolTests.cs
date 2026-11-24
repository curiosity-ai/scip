using System.Text;

namespace Scip.Indexer.Rust.Tests;

/// <summary>
/// Ports of the <c>check_symbol</c> tests of <c>cli/scip.rs</c> that can be expressed against the native scanner.
/// Tests that need cross-crate resolution (<c>deps:foo</c> fixtures with the cursor on a use site) or type
/// display (<c>AddAssign&lt;Self&gt;</c>) are listed as skipped in README.md.
/// </summary>
public class ScipSymbolTests
{
    private static async Task Check(string fixture, string expected, string kind = "lib", string name = "main", string? version = null)
    {
        var offset = fixture.IndexOf("$0", StringComparison.Ordinal);
        var source = fixture.Remove(offset, 2);
        var dir = Directory.CreateTempSubdirectory("scip-rust-").FullName;
        try
        {
            var file = Path.Combine(dir, "lib.rs");
            await File.WriteAllTextAsync(file, source);
            var package = new CargoPackage(name, version, Path.Combine(dir, "Cargo.toml"), []);
            var target = new CargoTarget(name, kind, file);
            var scanner = new RustCrateScanner(package, target, new HashSet<string>());
            scanner.Scan();
            RustAnalysis.Analyze([new ScanUnit(package, target, scanner)]);
            var result = scanner.Files.Single();
            var byteOffset = Encoding.UTF8.GetByteCount(source.AsSpan(0, offset));
            var occ = result.Occurrences.First(o => o.Start != 0 && o.Start <= byteOffset - 1 && byteOffset - 1 < o.End);
            var actual = occ.Symbol;
            if (actual.StartsWith("local ", StringComparison.Ordinal))
            {
                actual = "local enclosed by " + result.Symbols.Single(s => s.Symbol == actual).EnclosingSymbol;
            }

            await Assert.That(actual).IsEqualTo(expected);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public Task SymbolForTraitFunction() => Check(
        "pub mod module {\n    pub trait MyTrait {\n        pub fn func$0() {}\n    }\n}\n",
        "rust-analyzer cargo foo 0.1.0 lib/module/MyTrait#func().", name: "foo", version: "0.1.0");

    [Test]
    public Task SymbolForTraitAlias() => Check(
        "pub mod module {\n    pub trait MyTrait {}\n    pub trait MyTraitAlias$0 = MyTrait;\n}\n",
        "rust-analyzer cargo foo 0.1.0 lib/module/MyTraitAlias#", name: "foo", version: "0.1.0");

    [Test]
    public Task SymbolForTraitConstant() => Check(
        "pub mod module {\n    pub trait MyTrait {\n        const MY_CONST$0: u8;\n    }\n}\n",
        "rust-analyzer cargo foo 0.1.0 lib/module/MyTrait#MY_CONST.", name: "foo", version: "0.1.0");

    [Test]
    public Task SymbolForTraitType() => Check(
        "pub mod module {\n    pub trait MyTrait {\n        type MyType$0;\n    }\n}\n",
        "rust-analyzer cargo foo 0.1.0 lib/module/MyTrait#MyType#", name: "foo", version: "0.1.0");

    [Test]
    public Task SymbolForTraitImplFunction() => Check(
        "pub mod module {\n    pub trait MyTrait {\n        pub fn func() {}\n    }\n\n    struct MyStruct {}\n\n    impl MyTrait for MyStruct {\n        pub fn func$0() {}\n    }\n}\n",
        "rust-analyzer cargo foo 0.1.0 lib/module/impl#[MyStruct][MyTrait]func().", name: "foo", version: "0.1.0");

    [Test]
    public Task SymbolForField() => Check(
        "pub struct St {\n    pub a$0: i32,\n}\n",
        "rust-analyzer cargo foo 0.1.0 lib/St#a.", name: "foo", version: "0.1.0");

    [Test]
    public Task SymbolForParam() => Check(
        "pub mod example_mod {\n    pub fn func(x$0: usize) {}\n}\n",
        "local enclosed by rust-analyzer cargo foo 0.1.0 lib/example_mod/func().", name: "foo", version: "0.1.0");

    [Test]
    public Task LocalSymbolForLocal() => Check(
        "pub mod module {\n    pub fn func() {\n        let x$0 = 2;\n    }\n}\n",
        "local enclosed by rust-analyzer cargo foo 0.1.0 lib/module/func().", name: "foo", version: "0.1.0");

    [Test]
    public Task SymbolForTypeAlias() => Check(
        "pub type MyTypeAlias$0 = u8;\n", "rust-analyzer cargo main . lib/MyTypeAlias#");

    [Test]
    public Task ProcMacroTarget() => Check("struct S$0;\n", "rust-analyzer cargo name . proc-macro/S#", "proc-macro", "name");

    [Test]
    public Task BinTarget() => Check("struct S$0;\n", "rust-analyzer cargo main . bin/main/S#", "bin");

    [Test]
    public Task ExampleTarget() => Check("struct S$0;\n", "rust-analyzer cargo name . example/name/S#", "example", "name");

    [Test]
    public Task TestTarget() => Check("struct S$0;\n", "rust-analyzer cargo name . test/name/S#", "test", "name");

    [Test]
    public Task BenchTarget() => Check("struct S$0;\n", "rust-analyzer cargo name . bench/name/S#", "bench", "name");

    [Test]
    public Task CustomBuildTarget() => Check("struct S$0;\n", "rust-analyzer cargo build . custom-build/S#", "custom-build", "build");

    [Test]
    public Task OtherTarget() => Check("struct S$0;\n", "rust-analyzer cargo other . other/S#", "other", "other");

    // These four tests record rust-analyzer's current misbehaviour (FIXME in scip.rs): items nested in a
    // function body do not mention the function.
    [Test]
    public Task SymbolForNestedFunction() => Check(
        "pub fn func() {\n   pub fn inner_func$0() {}\n}\n", "rust-analyzer cargo main . lib/inner_func().");

    [Test]
    public Task SymbolForStructInFunction() => Check(
        "pub fn func() {\n   struct SomeStruct$0 {}\n}\n", "rust-analyzer cargo main . lib/SomeStruct#");

    [Test]
    public Task SymbolForConstInFunction() => Check(
        "pub fn func() {\n   const SOME_CONST$0: u32 = 1;\n}\n", "rust-analyzer cargo main . lib/SOME_CONST.");

    [Test]
    public Task SymbolForStaticInFunction() => Check(
        "pub fn func() {\n   static SOME_STATIC$0: u32 = 1;\n}\n", "rust-analyzer cargo main . lib/SOME_STATIC.");
}
