using Scip.Indexing;

namespace Scip.Indexer.Dart.Tests;

public class IndexerTests
{
    private static Document Doc(DartIndex index, string path) => index.Documents.Single(d => d.RelativePath == path);

    private static string[] Symbols(Document doc) => [.. doc.Symbols.Select(s => s.Symbol)];

    [Test]
    public async Task EmitsClassMembersWithUpstreamSymbolShapes()
    {
        var root = Fixtures.TempPackage("demo", new()
        {
            ["lib/a.dart"] = """
                /// A thing.
                class Thing<T> {
                  final int size;
                  static int count = 0;
                  Thing(this.size);
                  Thing.named({required int x}) : size = x;
                  String get label => '$size';
                  set label(String v) {}
                  void run<R>(R r, {int times = 1}) {}
                  bool operator ==(Object other) => true;
                  int _hidden = 0;
                }
                """,
        });

        var doc = Doc(DartIndexer.Build(root), "lib/a.dart");
        var symbols = Symbols(doc);
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#[T]");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#size.");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#`<constructor>`().");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#named().");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#named().(x)");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#`<get>label`.");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#`<set>label`.");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#run().(times)");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#run().[R]");
        await Assert.That(symbols).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#`==`().");

        // private names and positional parameters are local symbols
        await Assert.That(symbols.Count(s => s.StartsWith("local ", StringComparison.Ordinal))).IsGreaterThanOrEqualTo(2);

        var thing = doc.Symbols.Single(s => s.Symbol.EndsWith("a.dart`/Thing#", StringComparison.Ordinal));
        await Assert.That(thing.Kind).IsEqualTo(SymbolInformation.Types.Kind.Class);
        await Assert.That(thing.Documentation.Single()).IsEqualTo("A thing.");
    }

    [Test]
    public async Task EmitsOverrideRelationships()
    {
        var root = Fixtures.TempPackage("demo", new()
        {
            ["lib/a.dart"] = """
                abstract class Base { void run() {} String get name => ''; }
                class Mid extends Base {}
                class Leaf extends Mid { @override void run() {} @override String get name => 'x'; }
                """,
        });

        var doc = Doc(DartIndexer.Build(root), "lib/a.dart");
        var leaf = doc.Symbols.Single(s => s.Symbol.EndsWith("/Leaf#", StringComparison.Ordinal));
        await Assert.That(leaf.Relationships.Select(r => r.Symbol).Order().ToArray()).IsEquivalentTo(
            ["scip-dart pub demo 1.0.0 lib/`a.dart`/Base#", "scip-dart pub demo 1.0.0 lib/`a.dart`/Mid#"]);
        await Assert.That(leaf.Relationships.All(r => r.IsImplementation)).IsTrue();

        var run = doc.Symbols.Single(s => s.Symbol.EndsWith("/Leaf#run().", StringComparison.Ordinal));
        var rel = run.Relationships.Single();
        await Assert.That(rel.Symbol).IsEqualTo("scip-dart pub demo 1.0.0 lib/`a.dart`/Base#run().");
        await Assert.That(rel.IsReference).IsTrue();
        await Assert.That(rel.IsImplementation).IsTrue();

        var getter = doc.Symbols.Single(s => s.Symbol.EndsWith("/Leaf#`<get>name`.", StringComparison.Ordinal));
        await Assert.That(getter.Relationships.Single().Symbol).IsEqualTo("scip-dart pub demo 1.0.0 lib/`a.dart`/Base#`<get>name`.");
    }

    [Test]
    public async Task ResolvesReferencesAcrossFilesAndDirectives()
    {
        var root = Fixtures.TempPackage("demo", new()
        {
            ["lib/a.dart"] = "class Foo { Foo(); static void make() {} }\nvoid helper() {}\n",
            ["lib/b.dart"] = "import 'a.dart';\nimport 'package:demo/a.dart' as a;\nvoid main() { Foo(); Foo.make(); helper(); a.helper(); }\n",
        });

        var index = DartIndexer.Build(root);
        var doc = Doc(index, "lib/b.dart");
        var refs = doc.Occurrences.Where(o => (o.SymbolRoles & (int)SymbolRole.Definition) == 0).Select(o => o.Symbol).ToList();
        await Assert.That(refs).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/");
        await Assert.That(refs).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Foo#`<constructor>`().");
        await Assert.That(refs).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Foo#");
        await Assert.That(refs).Contains("scip-dart pub demo 1.0.0 lib/`a.dart`/Foo#make().");
        await Assert.That(refs.Count(r => r.EndsWith("/helper().", StringComparison.Ordinal))).IsEqualTo(2);
        await Assert.That(refs).Contains("scip-dart pub demo 1.0.0 lib/`b.dart`/a.");
    }

    [Test]
    public async Task LocalVariablesShadowTopLevelDeclarations()
    {
        var root = Fixtures.TempPackage("demo", new()
        {
            ["lib/a.dart"] = "int origin() => 0;\nvoid f() { final origin = 1; print(origin); }\n",
        });

        var doc = Doc(DartIndexer.Build(root), "lib/a.dart");
        await Assert.That(doc.Occurrences.Count(o => o.Symbol.EndsWith("/origin().", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task SkipsNestedPackagesAndHiddenDirectories()
    {
        var root = Fixtures.TempPackage("demo", new()
        {
            ["lib/a.dart"] = "class A {}\n",
            ["nested/pubspec.yaml"] = "name: nested\n",
            ["nested/lib/n.dart"] = "class N {}\n",
            [".dart_tool/gen.dart"] = "class G {}\n",
        });

        var index = DartIndexer.Build(root);
        await Assert.That(index.Documents.Select(d => d.RelativePath).ToArray()).IsEquivalentTo(["lib/a.dart"]);
    }

    [Test]
    public async Task UsesUtf16PositionsAndStreamsAValidIndex()
    {
        var root = Fixtures.TempPackage("demo", new()
        {
            ["lib/a.dart"] = "// \U0001F600 emoji\nclass Foo {}\n",
        });

        var output = Path.Combine(root, "index.scip");
        var exit = await new DartIndexer().IndexAsync(new IndexerOptions(root, output, []), CancellationToken.None);
        await Assert.That(exit).IsEqualTo(0);

        var index = Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output));
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-dart");
        var doc = index.Documents.Single();
        await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
        var def = doc.Occurrences.Single(o => o.Symbol.EndsWith("/Foo#", StringComparison.Ordinal));
        await Assert.That(def.SingleLineRange.Line).IsEqualTo(1);
        await Assert.That(def.SingleLineRange.StartCharacter).IsEqualTo(6);
    }

    [Test]
    public async Task MissingPubspecExitsWithOne()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-dart-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var exit = await new DartIndexer().IndexAsync(new IndexerOptions(dir, Path.Combine(dir, "i.scip"), []), CancellationToken.None);
        await Assert.That(exit).IsEqualTo(1);
    }

    [Test]
    public async Task IndexesPubspecLikeUpstream()
    {
        var index = DartIndexer.Build(Fixtures.Input("basic-project"), indexPubspec: true);
        var doc = Doc(index, "pubspec.yaml");
        await Assert.That(doc.Language).IsEqualTo("YAML");

        // `publish_to: none`: no symbol for the package itself, only its dependencies
        var occ = doc.Occurrences.Single();
        await Assert.That(occ.Symbol).IsEqualTo("scip-dart pub test 1.24.3 `pubspec.yaml`/");
        await Assert.That(occ.SingleLineRange.Line).IsEqualTo(8);
        await Assert.That(occ.SingleLineRange.StartCharacter).IsEqualTo(2);
        await Assert.That(occ.SingleLineRange.EndCharacter).IsEqualTo(6);
    }

    [Test]
    public async Task ParsesPubspecAndLock()
    {
        var p = Pubspec.Parse("name: foo # c\nversion: 1.2.3\ndependencies:\n  a: ^1.0.0\n  b:\n    sdk: flutter\ndev_dependencies:\n  c: any\n");
        await Assert.That(p.Name).IsEqualTo("foo");
        await Assert.That(p.Version).IsEqualTo("1.2.3");
        await Assert.That(p.Dependencies["dependencies"].ToArray()).IsEquivalentTo(["a", "b"]);
        await Assert.That(p.Dependencies["dev_dependencies"].ToArray()).IsEquivalentTo(["c"]);

        var lockVersions = Pubspec.ParseLock("packages:\n  a:\n    dependency: \"direct main\"\n    version: \"1.5.0\"\nsdks:\n  dart: \">=3.0.0\"\n");
        await Assert.That(lockVersions["a"]).IsEqualTo("1.5.0");
    }

    [Test]
    public async Task LexerHandlesInterpolationAndComments()
    {
        var (tokens, docs) = DartLexer.Lex("/// doc\nvar s = 'a ${b + c} $d'; // x\n/* y */ r'$e';");
        await Assert.That(tokens.Where(t => t.Kind == TokenKind.Identifier).Select(t => t.Text).ToArray())
            .IsEquivalentTo(["var", "s", "b", "c", "d"]);
        await Assert.That(docs.Single()).IsEqualTo("doc");
    }
}
