using System.IO.Compression;
using Google.Protobuf;

namespace Scip.Indexer.Java.Tests;

public class AggregatorTests
{
    private sealed class Quiet : AggregatorReporter
    {
        public override void ReportError(string message) { Messages.Add(message); base.ReportError(message); }

        public override void Warning(string message) { }

        public override void Info(string message) { }

        public List<string> Messages { get; } = [];
    }

    private static Occurrence Occ(string symbol, int line, int start, int end, int roles = 0) => new()
    {
        Symbol = symbol,
        SymbolRoles = roles,
        SingleLineRange = new SingleLineRange { Line = line, StartCharacter = start, EndCharacter = end },
    };

    private static void WriteShard(string targetRoot, string relative, Document doc)
    {
        var path = ShardPaths.ShardPath(targetRoot, "/src", "/src/" + relative)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new Index { Documents = { doc } }.ToByteArray());
    }

    private static AggregatorOptions Options(string[] roots, string output, IEnumerable<MavenPackage> packages, bool inverse, AggregatorReporter reporter) =>
        new(roots, output, "/src", new ToolInfo { Name = "scip-java", Version = "1" }, packages.ToList(),
            new JdkPackage("17"), reporter, EmitInverseRelationships: inverse);

    private static Index Aggregate(string dir, IEnumerable<MavenPackage> packages, bool inverse = true)
    {
        var output = Path.Combine(dir, "index.scip");
        ShardAggregator.Run(Options([Path.Combine(dir, "target")], output, packages, inverse, new Quiet()));
        return Index.Parser.ParseFrom(File.ReadAllBytes(output));
    }

    private static string NewDir() => Directory.CreateTempSubdirectory("scipjava").FullName;

    [Test]
    public async Task RewritesSymbolsUsingJarsAndJdk()
    {
        var dir = NewDir();
        var jar = Path.Combine(dir, "lib-1.0.jar");
        using (var zip = ZipFile.Open(jar, ZipArchiveMode.Create))
        {
            zip.CreateEntry("com/lib/Lib.class");
            zip.CreateEntry("com/lib/Lib$Inner.class");
        }

        var doc = new Document { Language = "java", RelativePath = "a/Foo.java", Text = "class Foo {}" };
        doc.Occurrences.Add(Occ("a/Foo#", 0, 6, 9, 1));
        doc.Occurrences.Add(Occ("com/lib/Lib#run().", 1, 0, 3));
        doc.Occurrences.Add(Occ("java/lang/String#", 2, 0, 6));
        doc.Occurrences.Add(Occ("local 0", 3, 0, 1));
        doc.Occurrences.Add(Occ("weird/Thing#", 4, 0, 1));
        doc.Symbols.Add(new SymbolInformation { Symbol = "a/Foo#", DisplayName = "Foo", EnclosingSymbol = "a/", Kind = SymbolInformation.Types.Kind.Class });
        WriteShard(Path.Combine(dir, "target"), "a/Foo.java", doc);

        var index = Aggregate(dir, [new MavenPackage(jar, "com.lib", "lib", "1.0")]);

        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-java");
        await Assert.That(index.Metadata.ProjectRoot).IsEqualTo("file:///src/");
        var result = index.Documents.Single();
        await Assert.That(result.Text).IsEqualTo("class Foo {}");
        await Assert.That(string.Join("\n", result.Occurrences.Select(o => o.Symbol))).IsEqualTo(string.Join("\n",
        [
            "scip-java maven . . a/Foo#",
            "scip-java maven maven/com.lib/lib 1.0 com/lib/Lib#run().",
            "scip-java maven jdk 17 java/lang/String#",
            "local 0",
            "scip-java maven . . weird/Thing#",
        ]));
        await Assert.That(result.Symbols.Single().EnclosingSymbol).IsEqualTo("scip-java maven . . a/");
        await Assert.That(result.Occurrences[0].TypedRangeCase).IsEqualTo(Occurrence.TypedRangeOneofCase.SingleLineRange);
    }

    [Test]
    public async Task EmitsInverseImplementationRelationships()
    {
        var dir = NewDir();
        var target = Path.Combine(dir, "target");
        var iface = new Document { Language = "java", RelativePath = "p/I.java" };
        iface.Symbols.Add(new SymbolInformation { Symbol = "p/I#m().", Kind = SymbolInformation.Types.Kind.Method });
        var impl = new Document { Language = "java", RelativePath = "p/C.java" };
        var m = new SymbolInformation { Symbol = "p/C#m().", Kind = SymbolInformation.Types.Kind.Method };
        m.Relationships.Add(new Relationship { Symbol = "p/I#m().", IsImplementation = true });
        m.Relationships.Add(new Relationship { Symbol = "java/lang/Object#toString().", IsImplementation = true });
        impl.Symbols.Add(m);
        var cls = new SymbolInformation { Symbol = "p/C#", Kind = SymbolInformation.Types.Kind.Class };
        cls.Relationships.Add(new Relationship { Symbol = "p/I#", IsImplementation = true });
        impl.Symbols.Add(cls);
        WriteShard(target, "p/I.java", iface);
        WriteShard(target, "p/C.java", impl);

        var index = Aggregate(dir, []);
        var rel = index.Documents.Single(d => d.RelativePath == "p/I.java").Symbols.Single().Relationships.Single();
        await Assert.That(rel.Symbol).IsEqualTo("scip-java maven . . p/C#m().");
        await Assert.That(rel.IsImplementation).IsTrue();
        await Assert.That(rel.IsReference).IsTrue();

        var without = Aggregate(dir, [], inverse: false);
        await Assert.That(without.Documents.Single(d => d.RelativePath == "p/I.java").Symbols.Single().Relationships.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadsShardsFromJars()
    {
        var dir = NewDir();
        var jar = Path.Combine(dir, "shards.jar");
        using (var zip = ZipFile.Open(jar, ZipArchiveMode.Create))
        using (var s = zip.CreateEntry("META-INF/scip/X.java.scip").Open())
        {
            new Index { Documents = { new Document { RelativePath = "X.java" } } }.WriteTo(s);
        }
        var output = Path.Combine(dir, "index.scip");
        ShardAggregator.Run(Options([jar], output, [], true, new Quiet()));
        await Assert.That(Index.Parser.ParseFrom(File.ReadAllBytes(output)).Documents.Single().RelativePath).IsEqualTo("X.java");
    }

    [Test]
    public async Task NoShardsIsAnError()
    {
        var dir = NewDir();
        Directory.CreateDirectory(Path.Combine(dir, "target"));
        var reporter = new Quiet();
        ShardAggregator.Run(Options([Path.Combine(dir, "target")], Path.Combine(dir, "index.scip"), [], true, reporter));
        await Assert.That(reporter.HasErrors).IsTrue();
        await Assert.That(File.Exists(Path.Combine(dir, "index.scip"))).IsFalse();
    }
}
