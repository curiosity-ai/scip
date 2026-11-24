using Scip.Indexing;

namespace Scip.Indexer.Rust.Tests;

public class IndexerTests
{
    private static readonly int[] ThreeNumbers = [1, 0, 2];
    private static readonly int[] FourNumbers = [0, 1, 1, 1];

    private static (SourceFileResult File, string Source) ScanSingle(string source)
    {
        var dir = Directory.CreateTempSubdirectory("scip-rust-").FullName;
        var path = Path.Combine(dir, "lib.rs");
        File.WriteAllText(path, source);
        var scanner = new RustCrateScanner(
            new CargoPackage("main", null, Path.Combine(dir, "Cargo.toml"), []),
            new CargoTarget("main", "lib", path),
            new HashSet<string>());
        scanner.Scan();
        return (scanner.Files.Single(), source);
    }

    private static SymbolInformation Info(SourceFileResult f, string name) => f.Symbols.Single(s => s.DisplayName == name);

    private static (int Start, int End) Enclosing(SourceFileResult f, string name)
    {
        var info = Info(f, name);
        return f.Occurrences.Single(o => o.Symbol == info.Symbol && o.IsDefinition).Enclosing!.Value;
    }

    [Test]
    public async Task DocumentationMatchesDocComment()
    {
        var (f, _) = ScanSingle("/// foo\nfn bar() {}");
        await Assert.That(Info(f, "bar").Documentation).IsEquivalentTo(["foo"]);
    }

    [Test]
    public async Task FunctionHasEnclosingRange()
    {
        var (f, _) = ScanSingle("fn foo() {}");
        await Assert.That(Enclosing(f, "foo")).IsEqualTo((0, 11));
    }

    [Test]
    public async Task FunctionEnclosingRangeTrivia()
    {
        const string s = "fn first() {}\n// belongs to first\n/// second docs\nfn second() {}";
        var (f, _) = ScanSingle(s);
        await Assert.That(Enclosing(f, "second")).IsEqualTo((s.IndexOf("/// second docs", StringComparison.Ordinal), s.Length));
    }

    [Test]
    public async Task ConstEnclosingRangeTrivia()
    {
        const string s = "const FOO_ONE: i32 = 123; // one\nconst FOO_TWO: i32 = 123; // two";
        var (f, _) = ScanSingle(s);
        await Assert.That(Enclosing(f, "FOO_TWO")).IsEqualTo((s.IndexOf("const FOO_TWO", StringComparison.Ordinal), s.IndexOf(" // two", StringComparison.Ordinal)));
    }

    [Test]
    public async Task LocalReferencesUseTheLocalSymbol()
    {
        var (f, _) = ScanSingle("fn f(a: i32) -> i32 {\n    let b = a;\n    b + a\n}\n");
        var a = Info(f, "a").Symbol;
        var b = Info(f, "b").Symbol;
        await Assert.That(f.Occurrences.Count(o => o.Symbol == a)).IsEqualTo(3);
        await Assert.That(f.Occurrences.Count(o => o.Symbol == b)).IsEqualTo(2);
    }

    [Test]
    public async Task UniqueItemNamesAreReferenced()
    {
        var (f, _) = ScanSingle("struct Foo;\nimpl Foo {\n    fn new() -> Foo { Foo }\n}\n");
        var foo = f.Symbols.Single(s => s.DisplayName == "Foo" && s.Kind == SymbolInformation.Types.Kind.Struct).Symbol;
        await Assert.That(f.Occurrences.Count(o => o.Symbol == foo && !o.IsDefinition)).IsEqualTo(3);
    }

    [Test]
    public async Task InherentImplDuplicatesOmitSymbolInformation()
    {
        var (f, _) = ScanSingle("struct S;\nimpl S { fn a() {} }\nimpl S { fn b() {} }\n");
        var doc = RustIndexAssembler.ToDocument(f, "lib.rs");
        using var ms = new MemoryStream();
        var log = new StringWriter();
        RustIndexAssembler.Write(ms, new Metadata(), [doc], [], log);
        var index = Index.Parser.ParseFrom(ms.ToArray());
        await Assert.That(index.Documents.Single().Symbols.Count(s => s.Symbol.EndsWith("impl#[S]", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(log.ToString()).IsEmpty();
    }

    [Test]
    public async Task ScipRangesUseThreeNumbersOnOneLine()
    {
        var lines = RustIndexAssembler.LineStarts("ab\ncd\n"u8.ToArray());
        await Assert.That(RustIndexAssembler.ToScipRange(lines, 3, 5)).IsEquivalentTo(ThreeNumbers);
        await Assert.That(RustIndexAssembler.ToScipRange(lines, 1, 4)).IsEquivalentTo(FourNumbers);
    }

    [Test]
    public async Task NativeIndexOfCargoProject()
    {
        var dir = Directory.CreateTempSubdirectory("scip-rust-proj-").FullName;
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "Cargo.toml"), "[package]\nname = \"demo\"\nversion = \"0.2.0\"\n");
        File.WriteAllText(Path.Combine(dir, "src", "lib.rs"), "pub mod util;\npub fn run() { util::help(); }\n");
        File.WriteAllText(Path.Combine(dir, "src", "util.rs"), "pub fn help() {}\n");
        var output = Path.Combine(dir, "index.scip");

        var code = await new RustIndexer().IndexAsync(
            new IndexerOptions(dir, output, []), CancellationToken.None);

        await Assert.That(code).IsEqualTo(0);
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        await Assert.That(index.Documents.Select(d => d.RelativePath).Order()).IsEquivalentTo(["src/lib.rs", "src/util.rs"]);
        var lib = index.Documents.Single(d => d.RelativePath == "src/lib.rs");
        await Assert.That(lib.Symbols.Select(s => s.Symbol)).Contains("rust-analyzer cargo demo 0.2.0 lib/run().");
        await Assert.That(lib.Occurrences.Any(o => o.Symbol == "rust-analyzer cargo demo 0.2.0 lib/util/help()." && o.SymbolRoles == 0)).IsTrue();
        await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
    }

    [Test]
    public async Task SmokeIndexesUpstreamCrates()
    {
        // Robustness check on real-world code; skipped when the reference checkout is absent.
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".reference", "rust-analyzer", "crates")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        if (dir is null)
        {
            return;
        }

        var crate = Path.Combine(dir, ".reference", "rust-analyzer", "crates", "syntax");
        var output = Path.Combine(Path.GetTempPath(), $"scip-rust-smoke-{Guid.NewGuid():N}.scip");
        var code = await new RustIndexer().IndexAsync(new IndexerOptions(crate, output, []), CancellationToken.None);
        await Assert.That(code).IsEqualTo(0);
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        File.Delete(output);
        Console.WriteLine($"smoke: {index.Documents.Count} documents, {index.Documents.Sum(d => d.Occurrences.Count)} occurrences");
        await Assert.That(index.Documents.Count).IsGreaterThan(5);
        foreach (var d in index.Documents)
        {
            foreach (var sym in d.Symbols)
            {
                await Assert.That(SymbolParser.TryParse(sym.Symbol, out _, out _)).IsTrue();
            }
        }
    }
}
