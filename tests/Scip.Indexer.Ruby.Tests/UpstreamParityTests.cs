namespace Scip.Indexer.Ruby.Tests;

/// <summary>
/// Behaviour checked against what the real scip-ruby 0.5.1 emits on sinatra and rack: occurrence roles, hover
/// documentation, and field/alias relationships.
/// </summary>
public class UpstreamParityTests
{
    private static readonly GemMetadata Gem = new("g", "1.0");

    private static Document Index(string source) => RubyIndexer.IndexSources([("a.rb", source)], Gem)[0];

    /// <summary>The roles at a position; a compound assignment (`x ||= v`) also has a ReadAccess twin, which is ignored here.</summary>
    private static int RolesAt(Document doc, int line, int col) =>
        doc.Occurrences.Where(o => o.SingleLineRange.Line == line && o.SingleLineRange.StartCharacter == col)
            .Select(o => o.SymbolRoles).OrderBy(r => r == (int)SymbolRole.ReadAccess ? int.MaxValue : r).First();

    private static string[] DocsOf(Document doc, string symbolSuffix) =>
        [.. doc.Symbols.Single(s => s.Symbol.EndsWith(symbolSuffix, StringComparison.Ordinal)).Documentation];

    // ---- roles ---------------------------------------------------------------------------------------------

    [Test]
    public async Task ConstantAndLocalReferencesAreReadAccessAndMethodCallsHaveNoRole()
    {
        var doc = Index("module M\n  X = 1\n  def f(a)\n    b = a\n    puts X\n    b\n  end\nend\n");
        await Assert.That(RolesAt(doc, 3, 4)).IsEqualTo(1); // b = ...  definition
        await Assert.That(RolesAt(doc, 3, 8)).IsEqualTo((int)SymbolRole.ReadAccess); // a
        await Assert.That(RolesAt(doc, 4, 4)).IsEqualTo(0); // puts
        await Assert.That(RolesAt(doc, 4, 9)).IsEqualTo((int)SymbolRole.ReadAccess); // X
        await Assert.That(RolesAt(doc, 5, 4)).IsEqualTo((int)SymbolRole.ReadAccess); // b
    }

    [Test]
    public async Task ScopeQualifiersSuperclassesAndConstantCallsCarryNoRole()
    {
        var doc = Index("class Base; end\nmodule N; class K; end; end\nclass D < Base\n  def f\n    N::K.new\n    Array(1)\n  end\nend\n");
        await Assert.That(RolesAt(doc, 2, 10)).IsEqualTo(0); // superclass
        await Assert.That(RolesAt(doc, 4, 4)).IsEqualTo(0); // N in N::K
        await Assert.That(RolesAt(doc, 4, 7)).IsEqualTo((int)SymbolRole.ReadAccess); // K
        await Assert.That(RolesAt(doc, 5, 4)).IsEqualTo(0); // Array(1)
    }

    [Test]
    public async Task FieldAssignmentsAreDefinitionsUnlessAlreadyReadOrAssignedOnThePath()
    {
        var doc = Index("class A\n  def a\n    @x = 1\n    @x = 2\n    nil\n  end\n  def b\n    @x = @x.to_s\n    @y ||= 1\n    @z += 1\n  end\n  def c\n    puts 1 if @x\n  end\n  def d\n    @p, @q = 1, 2\n  end\nend\n");
        await Assert.That(RolesAt(doc, 2, 4)).IsEqualTo(1);
        await Assert.That(RolesAt(doc, 3, 4)).IsEqualTo((int)SymbolRole.WriteAccess);
        await Assert.That(RolesAt(doc, 7, 4)).IsEqualTo((int)SymbolRole.WriteAccess); // RHS read comes first
        await Assert.That(RolesAt(doc, 8, 4)).IsEqualTo(1); // ||= is a definition
        await Assert.That(RolesAt(doc, 9, 4)).IsEqualTo((int)SymbolRole.WriteAccess); // += reads first
        await Assert.That(RolesAt(doc, 12, 14)).IsEqualTo(0); // bare condition
        await Assert.That(RolesAt(doc, 15, 4)).IsEqualTo(1); // multiple assignment
        await Assert.That(RolesAt(doc, 15, 8)).IsEqualTo(1);
    }

    [Test]
    public async Task LocalsAssignedInSiblingBranchesAreDefinedAgain()
    {
        var doc = Index("def f(c)\n  if c\n    v = 1\n  else\n    v = 2\n  end\n  v = 3\nend\n");
        await Assert.That(RolesAt(doc, 2, 4)).IsEqualTo(1);
        await Assert.That(RolesAt(doc, 4, 4)).IsEqualTo(1);
        await Assert.That(RolesAt(doc, 6, 2)).IsEqualTo((int)SymbolRole.WriteAccess);
    }

    [Test]
    public async Task WhileConditionDoesNotSwallowALaterBlock()
    {
        var doc = Index("class A\n  def f(xs)\n    while xs.empty?\n      xs.each do |x|\n        x\n      end\n    end\n  end\n  def g; end\nend\n");
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("A#g().", StringComparison.Ordinal) && (o.SymbolRoles & 1) != 0)).IsTrue();
    }

    // ---- documentation ---------------------------------------------------------------------------------------

    [Test]
    public async Task MethodsGetSorbetStyleSignatureAndDocComment()
    {
        var doc = Index("class A\n  # Does it.\n  def run(a, b = 1, *r, k:, o: 2, **kr, &blk)\n  end\n\n  sig { params(v: String).returns(Integer) }\n  def typed(v); end\n\n  sig { params(v: String).void }\n  def self.make(v); end\nend\n");
        await Assert.That(DocsOf(doc, "A#run().")).IsEquivalentTo(new[]
        {
            "```ruby\nsig do\n  params(\n    a: T.untyped,\n    b: T.untyped,\n    r: T.untyped,\n    k: T.untyped,\n    o: T.untyped,\n    kr: T.untyped,\n    blk: T.untyped\n  )\n  .returns(T.untyped)\nend\ndef run(a, b=…, *r, k:, o: …, **kr, &blk)\n```",
            "Does it.",
        });
        await Assert.That(DocsOf(doc, "A#typed().")).IsEquivalentTo(new[] { "```ruby\nsig { params(v: String).returns(Integer) }\ndef typed(v)\n```" });
        await Assert.That(DocsOf(doc, "`<Class:A>`#make().")).IsEquivalentTo(new[] { "```ruby\nsig { params(v: String).void }\ndef self.make(v)\n```" });
    }

    [Test]
    public async Task ClassesModulesAndVariablesGetHovers()
    {
        var doc = Index("# The base.\nclass Base; end\nmodule Mixin; end\nclass Kid < Base\n  LIMIT = 3\n  NAME = 'n'\n  def f(x)\n    y = x\n    @z = y\n  end\nend\nclass Orphan < Elsewhere::Missing; end\n");
        await Assert.That(DocsOf(doc, "Base#")).IsEquivalentTo(new[] { "```ruby\nclass Base\n```", "The base." });
        await Assert.That(DocsOf(doc, "Mixin#")).IsEquivalentTo(new[] { "```ruby\nmodule Mixin\n```" });
        await Assert.That(DocsOf(doc, "Kid#")).IsEquivalentTo(new[] { "```ruby\nclass Kid < Base\n```" });
        await Assert.That(DocsOf(doc, "Kid#LIMIT.")).IsEquivalentTo(new[] { "```ruby\nLIMIT (Integer)\n```" });
        await Assert.That(DocsOf(doc, "Kid#NAME.")).IsEquivalentTo(new[] { "```ruby\nNAME (String)\n```" });
        await Assert.That(DocsOf(doc, "Kid#`@z`.")).IsEquivalentTo(new[] { "```ruby\n@z (T.untyped)\n```" });
        await Assert.That(DocsOf(doc, "Orphan#")).IsEquivalentTo(new[] { "```ruby\nclass Orphan < Sorbet::Private::Static::<StubModule>\n```" });
        await Assert.That(doc.Symbols.Where(s => s.Symbol.StartsWith("local", StringComparison.Ordinal)).SelectMany(s => s.Documentation))
            .IsEquivalentTo(new[] { "```ruby\nx (T.untyped)\n```", "```ruby\ny (T.untyped)\n```" });
    }

    [Test]
    public async Task LiteralTypesAreShownOnlyInTypedFiles()
    {
        var typed = Index("# typed: true\ndef f\n  a = 'x'\n  b = 3\nend\n");
        await Assert.That(typed.Symbols.SelectMany(s => s.Documentation).Where(d => d.StartsWith("```ruby\na ", StringComparison.Ordinal)))
            .IsEquivalentTo(new[] { "```ruby\na (String(\"x\"))\n```" });
        var untyped = Index("def f\n  a = 'x'\nend\n");
        await Assert.That(untyped.Symbols.SelectMany(s => s.Documentation).Single(d => d.StartsWith("```ruby\na ", StringComparison.Ordinal)))
            .IsEqualTo("```ruby\na (T.untyped)\n```");
    }

    [Test]
    public async Task EveryDefinitionOfSinatraStyleSourceHasDocumentation()
    {
        var doc = Index("module Sinatra\n  class Base\n    attr_accessor :app\n    def call!(env); end\n  end\nend\n");
        var missing = doc.Symbols.Where(s => s.Documentation.Count == 0).Select(s => s.Symbol).ToList();
        await Assert.That(missing).IsEmpty();
        await Assert.That(DocsOf(doc, "Base#`app=`().")[0]).IsEqualTo("```ruby\nsig { params(app: T.untyped).returns(T.untyped) }\ndef app=(app)\n```");
    }

    // ---- relationships ---------------------------------------------------------------------------------------

    [Test]
    public async Task InheritedAndMixedInFieldsGetRelationships()
    {
        var doc = Index(
            "module Helpers\n  def h; @shared = 1; end\nend\nclass Base\n  def b; @shared = 2; @own = 1; end\nend\nclass Kid < Base\n  include Helpers\n  def k; @shared = 3; end\n  def r; @own; end\nend\n");
        var kid = doc.Symbols.Single(s => s.Symbol.EndsWith("Kid#`@shared`.", StringComparison.Ordinal));
        await Assert.That(kid.Relationships.Select(r => (r.Symbol, r.IsDefinition, r.IsReference))).IsEquivalentTo(new[]
        {
            ("scip-ruby gem g 1.0 Base#`@shared`.", true, false),
            ("scip-ruby gem g 1.0 Helpers#`@shared`.", false, true),
        });
        // Used but not defined in this class: a reference-only SymbolInformation still carries the relationship.
        var own = doc.Symbols.Single(s => s.Symbol.EndsWith("Kid#`@own`.", StringComparison.Ordinal));
        await Assert.That(own.Relationships.Single().Symbol).IsEqualTo("scip-ruby gem g 1.0 Base#`@own`.");
        await Assert.That(doc.Symbols.Single(s => s.Symbol.EndsWith("Base#`@shared`.", StringComparison.Ordinal)).Relationships).IsEmpty();
    }

    [Test]
    public async Task ConstantAliasesReferenceTheirTarget()
    {
        var doc = Index("module M\n  ALLOWED = 1\n  PRIV = ALLOWED\nend\n");
        var priv = doc.Symbols.Single(s => s.Symbol.EndsWith("M#PRIV.", StringComparison.Ordinal));
        await Assert.That(priv.Relationships.Select(r => (r.Symbol, r.IsReference))).IsEquivalentTo(new[] { ("scip-ruby gem g 1.0 M#ALLOWED.", true) });
    }

    [Test]
    public async Task NestedDescribeGroupsInheritTheirParentsFields()
    {
        var doc = Index("class SomeTest < Minitest::Test\n  setup do\n    @app = 1\n  end\n\n  describe 'x' do\n    it 'y' do\n      @app\n    end\n  end\nend\n");
        var info = doc.Symbols.Single(s => s.Symbol.EndsWith("`<describe 'x'>`#`@app`.", StringComparison.Ordinal));
        await Assert.That(info.Relationships.Single().Symbol).IsEqualTo("scip-ruby gem g 1.0 SomeTest#`@app`.");
    }

    // ---- structure ------------------------------------------------------------------------------------------

    [Test]
    public async Task TopLevelDescribeByConstantDefinesAGroupAtTheLastSegment()
    {
        var doc = Index("describe Rack::MediaType do\n  before { @h = {} }\n  describe 'inner' do\n    it 'x' do\n      @h\n    end\n  end\nend\n");
        var defs = doc.Occurrences.Where(o => (o.SymbolRoles & 1) != 0 && !o.Symbol.StartsWith("local", StringComparison.Ordinal)).Select(o => o.Symbol[(o.Symbol.IndexOf("1.0 ", StringComparison.Ordinal) + 4)..]).ToList();
        await Assert.That(defs).Contains("`<describe 'Rack::MediaType'>`#");
        await Assert.That(defs).Contains("`<describe 'Rack::MediaType'>`#`<describe 'inner'>`#`<it 'x'>`().");
        var group = doc.Occurrences.Single(o => o.Symbol.EndsWith("`<describe 'Rack::MediaType'>`#", StringComparison.Ordinal) && (o.SymbolRoles & 1) != 0);
        await Assert.That(group.SingleLineRange.StartCharacter).IsEqualTo(15);
    }

    [Test]
    public async Task CharacterLiteralsOfPunctuationDoNotStartRegexes()
    {
        var doc = Index("def f(p)\n  p == ?/\n  x = 1\nend\ndef g; end\n");
        await Assert.That(doc.Occurrences.Count(o => (o.SymbolRoles & 1) != 0 && o.Symbol.EndsWith("g().", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(doc.Occurrences.Any(o => o.SingleLineRange.Line == 2 && o.SingleLineRange.StartCharacter == 2 && (o.SymbolRoles & 1) != 0)).IsTrue();
    }

    [Test]
    public async Task ParametersOfEmptyMethodsAreNotDefinedButAnonymousSplatsAre()
    {
        var doc = Index("def empty(a, b)\nend\ndef used(a, *)\n  a\nend\n");
        var locals = doc.Occurrences.Where(o => o.Symbol.StartsWith("local", StringComparison.Ordinal) && (o.SymbolRoles & 1) != 0).Select(o => (o.SingleLineRange.Line, o.SingleLineRange.StartCharacter)).ToList();
        await Assert.That(locals).IsEquivalentTo(new[] { (2, 9), (2, 12) });
    }

    [Test]
    public async Task RegexLiteralsAndBareRescueReferenceCoreClasses()
    {
        var doc = Index("def f(s)\n  s =~ /a+/\n  begin\n    1\n  rescue\n    2\n  end\nend\n");
        var refs = doc.Occurrences.Where(o => (o.SymbolRoles & 1) == 0).Select(o => (o.Symbol, o.SingleLineRange.Line, o.SingleLineRange.StartCharacter, o.SymbolRoles)).ToList();
        await Assert.That(refs.Any(r => r.Symbol.EndsWith("Regexp#", StringComparison.Ordinal) && r.Line == 1 && r.StartCharacter == 7 && r.SymbolRoles == 8)).IsTrue();
        await Assert.That(refs.Any(r => r.Symbol.EndsWith("StandardError#", StringComparison.Ordinal) && r.Line == 4 && r.StartCharacter == 2)).IsTrue();
    }

    // ---- fixtures ------------------------------------------------------------------------------------------

    /// <summary>The number of upstream snapshot fixtures that match exactly must not regress (74 of 113 at the time of writing).</summary>
    [Test]
    public async Task ExactFixtureCountDoesNotRegress()
    {
        var exact = SnapshotRunner.FixtureNames().Count(n => SnapshotRunner.Run(n).Problems.Count == 0);
        await Assert.That(exact).IsGreaterThanOrEqualTo(74);
    }
}
