namespace ScipSharp.Indexer.Ruby.Tests;

/// <summary>Local type inference, sig parsing, the embedded core payload and the DSL rewriters.</summary>
public class InferenceTests
{
    private static readonly GemMetadata Gem = new("g", "1.0");

    private static List<string> Symbols(string source, bool defs = false)
    {
        var doc = RubyIndexer.IndexSources([("a.rb", source)], Gem)[0];
        return [.. doc.Occurrences.Where(o => ((o.SymbolRoles & 1) != 0) == defs && !o.Symbol.StartsWith("local", StringComparison.Ordinal)).Select(o => o.Symbol)];
    }

    [Test]
    public async Task LocalAssignedFromNewResolvesMethodCalls()
    {
        var refs = Symbols("class Foo\n  def bar; end\nend\ndef f\n  x = Foo.new\n  x.bar\nend\n");
        await Assert.That(refs).Contains("scip-ruby gem g 1.0 Foo#bar().");
    }

    [Test]
    public async Task SigReturnTypeAndParamTypesFlowToReceivers()
    {
        var refs = Symbols("class Foo\n  extend T::Sig\n  def bar; end\n  sig { returns(Foo) }\n  def self.make; new; end\n  sig { params(a: Foo).void }\n  def self.use(a); a.bar; end\nend\nFoo.make.bar\n");
        await Assert.That(refs.Count(r => r == "scip-ruby gem g 1.0 Foo#bar().")).IsEqualTo(2);
    }

    [Test]
    public async Task InstanceVariablesTakeTheirTypeFromAssignmentsAndTLet()
    {
        var refs = Symbols("class B\n  def go; end\nend\nclass A\n  def initialize\n    @b = T.let(B.new, B)\n  end\n  def run\n    @b.go\n  end\nend\n");
        await Assert.That(refs).Contains("scip-ruby gem g 1.0 B#go().");
    }

    [Test]
    public async Task CoreReferencesUseTheRubyStdlibGem()
    {
        var refs = Symbols("puts \"x\".upcase\n[1].each { }\n");
        await Assert.That(refs).Contains("scip-ruby gem ruby latest Kernel#puts().");
        await Assert.That(refs).Contains("scip-ruby gem ruby latest String#upcase().");
        await Assert.That(refs).Contains("scip-ruby gem ruby latest Array#each().");
    }

    [Test]
    public async Task KeywordParametersAreGlobalParameterSymbols()
    {
        var doc = RubyIndexer.IndexSources([("a.rb", "class K\n  def go(a:, b: 1)\n    a\n  end\n  def run\n    go(a: 2)\n  end\nend\n")], Gem)[0];
        var all = doc.Occurrences.Select(o => (o.Symbol, Def: (o.SymbolRoles & 1) != 0)).ToList();
        await Assert.That(all).Contains(("scip-ruby gem g 1.0 K#go().(a)", true));
        await Assert.That(all).Contains(("scip-ruby gem g 1.0 K#go().(b)", true));
        await Assert.That(all.Count(o => o.Symbol == "scip-ruby gem g 1.0 K#go().(a)" && !o.Def)).IsEqualTo(2);
    }

    [Test]
    public async Task TStructPropsDefineAccessorsAndInitialize()
    {
        var defs = Symbols("class S < T::Struct\n  prop :a, Integer\n  const :b, String\nend\n", defs: true);
        await Assert.That(defs).Contains("scip-ruby gem g 1.0 S#initialize().");
        await Assert.That(defs).Contains("scip-ruby gem g 1.0 S#a().");
        await Assert.That(defs).Contains("scip-ruby gem g 1.0 S#`a=`().");
        await Assert.That(defs).Contains("scip-ruby gem g 1.0 S#b().");
        await Assert.That(defs).DoesNotContain("scip-ruby gem g 1.0 S#`b=`().");
    }

    [Test]
    public async Task MattrDelegateAndDefDelegatorDefineMethods()
    {
        var defs = Symbols("class M\n  mattr_reader :a, instance_accessor: false\n  delegate :x, to: :y, prefix: true\n  def_delegator :@l, :size, :len\nend\n", defs: true);
        await Assert.That(defs).Contains("scip-ruby gem g 1.0 `<Class:M>`#a().");
        await Assert.That(defs).DoesNotContain("scip-ruby gem g 1.0 M#a().");
        await Assert.That(defs).Contains("scip-ruby gem g 1.0 M#y_x().");
        await Assert.That(defs).Contains("scip-ruby gem g 1.0 M#len().");
    }

    [Test]
    public async Task AliasedMethodsResolveToTheOriginal()
    {
        var refs = Symbols("class X\n  def a; end\n  alias_method :b, :a\n  def c; b; end\nend\n");
        await Assert.That(refs.Count(r => r == "scip-ruby gem g 1.0 X#a().")).IsEqualTo(1);
    }

    [Test]
    public async Task SelfReferentialFieldAssignmentsDoNotOverflow()
    {
        var refs = Symbols("class A\n  def a; @x = @x; @x.foo; end\nend\n");
        await Assert.That(refs.Count).IsGreaterThanOrEqualTo(0);
    }
}
