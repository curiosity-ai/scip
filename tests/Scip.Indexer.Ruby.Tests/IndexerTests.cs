using Google.Protobuf;
using Scip.Indexing;

namespace Scip.Indexer.Ruby.Tests;

public class IndexerTests
{
    private static readonly GemMetadata Gem = new("g", "1.0");

    private static List<(string Symbol, bool Def, int Line, int Col, int EndCol)> Occurrences(string source)
    {
        var doc = RubyIndexer.IndexSources([("a.rb", source)], Gem)[0];
        return [.. doc.Occurrences.Select(o => (o.Symbol, (o.SymbolRoles & 1) != 0, o.SingleLineRange.Line, o.SingleLineRange.StartCharacter, o.SingleLineRange.EndCharacter))];
    }

    [Test]
    public async Task SymbolsCarryGemPackageAndSorbetStyleDescriptors()
    {
        var occs = Occurrences("class A\n  def self.s; end\n  def i(x); x; end\n  K = 1\n  attr_accessor :v\nend\n");
        var defs = occs.Where(o => o.Def && !o.Symbol.StartsWith("local", StringComparison.Ordinal)).Select(o => o.Symbol).ToList();
        await Assert.That(defs).IsEquivalentTo(new[]
        {
            "scip-ruby gem g 1.0 A#",
            "scip-ruby gem g 1.0 `<Class:A>`#s().",
            "scip-ruby gem g 1.0 A#i().",
            "scip-ruby gem g 1.0 A#K.",
            "scip-ruby gem g 1.0 A#`v=`().",
            "scip-ruby gem g 1.0 A#v().",
        });
    }

    [Test]
    public async Task LocalsAndParametersShareIdsWithinTheirScope()
    {
        var occs = Occurrences("def f(a)\n  b = a\n  b\nend\n").Where(o => o.Symbol.StartsWith("local", StringComparison.Ordinal)).ToList();
        await Assert.That(occs.Select(o => (o.Symbol, o.Def))).IsEquivalentTo(new[]
        {
            ("local 0", true), ("local 1", true), ("local 0", false), ("local 1", false),
        });
    }

    [Test]
    public async Task ConstantsResolveLexicallyAcrossFiles()
    {
        var docs = RubyIndexer.IndexSources(
        [
            ("use.rb", "module M\n  def self.go; Helper::LIMIT; end\nend\n"),
            ("def.rb", "module M\n  module Helper\n    LIMIT = 3\n  end\nend\n"),
        ], Gem);
        var refs = docs[0].Occurrences.Where(o => (o.SymbolRoles & 1) == 0).Select(o => o.Symbol).ToList();
        await Assert.That(refs).IsEquivalentTo(new[] { "scip-ruby gem g 1.0 M#Helper#", "scip-ruby gem g 1.0 M#Helper#LIMIT." });
    }

    [Test]
    public async Task ImplicitSelfCallsResolveThroughIncludesAndSuperclasses()
    {
        var occs = Occurrences("module Util\n  def helper; end\nend\nclass Base\n  def base_m; end\nend\nclass C < Base\n  include Util\n  def run\n    helper\n    base_m\n  end\nend\n");
        var refs = occs.Where(o => !o.Def).Select(o => o.Symbol).ToList();
        await Assert.That(refs).Contains("scip-ruby gem g 1.0 Util#helper().");
        await Assert.That(refs).Contains("scip-ruby gem g 1.0 Base#base_m().");
        await Assert.That(refs).Contains("scip-ruby gem g 1.0 Util#");
    }

    [Test]
    public async Task EnclosingRangeSpansDefinitionBody()
    {
        var doc = RubyIndexer.IndexSources([("a.rb", "class A\n  def m\n    1\n  end\nend\n")], Gem)[0];
        var m = doc.Occurrences.Single(o => o.Symbol.EndsWith("A#m().", StringComparison.Ordinal));
        await Assert.That(m.MultiLineEnclosingRange.StartLine).IsEqualTo(1);
        await Assert.That(m.MultiLineEnclosingRange.StartCharacter).IsEqualTo(2);
        await Assert.That(m.MultiLineEnclosingRange.EndLine).IsEqualTo(3);
        await Assert.That(m.MultiLineEnclosingRange.EndCharacter).IsEqualTo(5);
    }

    [Test]
    public async Task EndlessDefsAndModifiersDoNotUnbalanceBlocks()
    {
        var occs = Occurrences("class A\n  def a = 1\n  def b\n    return 1 if true\n    x = 2 unless false\n  end\nend\nclass B\nend\n");
        var defs = occs.Where(o => o.Def && !o.Symbol.StartsWith("local", StringComparison.Ordinal)).Select(o => o.Symbol).ToList();
        // `B` is top-level (not nested in A), proving every block was closed.
        await Assert.That(defs).IsEquivalentTo(new[] { "scip-ruby gem g 1.0 A#", "scip-ruby gem g 1.0 A#a().", "scip-ruby gem g 1.0 A#b().", "scip-ruby gem g 1.0 B#" });
    }

    [Test]
    public async Task HeredocsStringsAndCommentsAreSkipped()
    {
        var src = "class A\n  X = <<~EOS\n    class Fake\n    def nope; end\n  EOS\n  # class Commented\n  Y = \"class Str #{X} \\\" def\"\n  def real; end\nend\n";
        var defs = Occurrences(src).Where(o => o.Def).Select(o => o.Symbol).ToList();
        await Assert.That(defs).IsEquivalentTo(new[]
        {
            "scip-ruby gem g 1.0 A#", "scip-ruby gem g 1.0 A#X.", "scip-ruby gem g 1.0 A#Y.", "scip-ruby gem g 1.0 A#real().",
        });
    }

    [Test]
    public async Task PositionsAreUtf8ByteOffsets()
    {
        var occs = Occurrences("# é\nclass Ünï; end\n");
        var cls = occs.Single(o => o.Def);
        await Assert.That(cls.Col).IsEqualTo(6);
        await Assert.That(cls.EndCol).IsEqualTo(6 + 5); // "Ünï" is 3 chars but 5 UTF-8 bytes
    }

    [Test]
    public async Task GlobalVariablesUseThePlaceholderGem()
    {
        var occs = Occurrences("$count = 1\nputs $count\n");
        await Assert.That(occs.Select(o => o.Symbol).Where(s => s.Contains("$count", StringComparison.Ordinal))).IsEquivalentTo(new[]
        {
            "scip-ruby gem _global_ latest `<Class:<root>>`#$count.", "scip-ruby gem _global_ latest `<Class:<root>>`#$count.",
        });
    }

    [Test]
    public async Task EndToEndWritesAParsableIndex()
    {
        var dir = Directory.CreateTempSubdirectory("scip-ruby-test").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "lib.rb"), "class Lib\n  def go; end\nend\n");
            await File.WriteAllTextAsync(Path.Combine(dir, "mygem.gemspec"), "Gem::Specification.new do |s|\n  s.name = \"mygem\"\n  s.version = \"2.0\"\nend\n");
            var output = Path.Combine(dir, "out", "index.scip");
            var code = await new RubyIndexer().IndexAsync(new IndexerOptions(dir, output, []), CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var index = Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output));
            await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-ruby");
            await Assert.That(index.Documents.Select(d => d.RelativePath)).Contains("lib.rb");
            var lib = index.Documents.Single(d => d.RelativePath == "lib.rb");
            await Assert.That(lib.Occurrences.Select(o => o.Symbol)).Contains("scip-ruby gem mygem 2.0 Lib#go().");
            await Assert.That(lib.Language).IsEqualTo("Ruby");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
