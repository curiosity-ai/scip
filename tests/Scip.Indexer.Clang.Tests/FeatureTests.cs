using Scip.Indexer.Clang;
using Scip.Indexing;

namespace Scip.Indexer.Clang.Tests;

/// <summary>Behaviour tests on small inline sources (no upstream snapshot needed).</summary>
public class FeatureTests
{
    private static async Task<(Scip.Index Index, Scip.Document Doc)> IndexAsync(string source, string std = "-std=c++17")
    {
        var dir = Directory.CreateTempSubdirectory("scip-clang-feat").FullName;
        var src = Path.Combine(dir, "a.cc");
        File.WriteAllText(src, source);
        File.WriteAllText(Path.Combine(dir, "compile_commands.json"),
            System.Text.Json.JsonSerializer.Serialize(new[] { new { directory = dir, file = src, arguments = new[] { "cc", std, "-c", src } } }));
        var output = Path.Combine(dir, "index.scip");
        var code = await new ClangIndexer().IndexAsync(new IndexerOptions(dir, output, []), CancellationToken.None);
        await Assert.That(code).IsEqualTo(0);
        var index = Scip.Index.Parser.ParseFrom(File.ReadAllBytes(output));
        return (index, index.Documents.Single(d => d.RelativePath == "a.cc"));
    }

    private static bool Has(Scip.Document d, string symbolSuffix, bool definition) =>
        d.Occurrences.Any(o => o.Symbol.EndsWith(symbolSuffix, StringComparison.Ordinal) && ((o.SymbolRoles & 1) != 0) == definition);

    [Test]
    [Arguments("void f() {}", "f(49f6e7a06ebc5aa8).")]
    [Arguments("void f(int) {}", "f(d4f767463ce0a6b3).")]
    [Arguments("void f(const char *) {}", "f(85c52e162fed56f9).")]
    [Arguments("typedef int myint; void f(const myint x) {}", "f(d4f767463ce0a6b3).")]
    [Arguments("void f(int x[3]) {}", "f(11b0e290e57a7e53).")]
    [Arguments("struct D { D() = default; };", "D#D(ced63f7c635d850d).")]
    [Arguments("namespace a { namespace c { enum class E {}; } } void f(a::c::E) {}", "f(5734375c1c12cb14).")]
    public async Task FunctionDisambiguatorsMatchUpstreamHashes(string source, string expectedSuffix)
    {
        var (_, doc) = await IndexAsync(source);
        await Assert.That(Has(doc, expectedSuffix, definition: true)).IsTrue();
    }

    [Test]
    public async Task ClassesAndOverridesCarryImplementationRelationships()
    {
        var (_, doc) = await IndexAsync("""
            struct B { virtual void f() {} virtual void g() {} };
            struct M : B { void f() override {} };
            struct D : M { void g() override {} void f() override {} };
            """);
        var d = doc.Symbols.Single(s => s.Symbol.EndsWith("D#", StringComparison.Ordinal));
        await Assert.That(d.Relationships.Select(r => r.Symbol.Split(' ')[^1]).Order()).IsEquivalentTo(new[] { "B#", "M#" });
        await Assert.That(d.Relationships.All(static r => r.IsImplementation)).IsTrue();
        var df = doc.Symbols.Single(s => s.Symbol.Contains("D#f(", StringComparison.Ordinal));
        var rel = df.Relationships.Single();
        await Assert.That(rel.Symbol).Contains("M#f(");
        await Assert.That(rel.IsImplementation && rel.IsReference).IsTrue();
        // D::g overrides B::g directly (M does not redeclare it)
        var dg = doc.Symbols.Single(s => s.Symbol.Contains("D#g(", StringComparison.Ordinal));
        await Assert.That(dg.Relationships.Single().Symbol).Contains("B#g(");
    }

    [Test]
    public async Task UsingDeclarationReferencesBaseAndDefinesShadow()
    {
        var (_, doc) = await IndexAsync("""
            struct A { void f(int) {} void f(int, int) {} };
            struct B : A { using A::f; };
            void use(B b) { b.f(1); }
            """);
        await Assert.That(Has(doc, "B#f(d4f767463ce0a6b3).", definition: true)).IsTrue();
        await Assert.That(Has(doc, "B#f(3e454b8ccf442868).", definition: true)).IsTrue();
        // uses resolve to the underlying declaration
        await Assert.That(doc.Occurrences.Count(o => o.Symbol.EndsWith("A#f(d4f767463ce0a6b3).", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task OverloadedOperatorUsesAreResolvedFromDeclaredTypes()
    {
        var (_, doc) = await IndexAsync("""
            struct S { int operator[](int i) const { return i; } S *operator->() { return this; } int v; };
            struct T { T operator+(const T &) const { return *this; } };
            int f(S s, T a, T b) { T c = a + b; return s[0] + s->v; }
            """);
        await Assert.That(Has(doc, "S#`operator[]`(b00b2ee11a8c9dd5).", definition: false) || doc.Occurrences.Any(o => o.Symbol.Contains("operator[]", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsTrue();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.Contains("operator+", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsTrue();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.Contains("operator->", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsTrue();
    }

    [Test]
    public async Task ConversionOperatorsAndNewDeleteAreNamedLikeClang()
    {
        var (_, doc) = await IndexAsync("""
            struct C { operator int() const { return 0; } explicit operator const char *() const { return ""; } };
            void *operator new(unsigned long) { return 0; }
            void operator delete(void *) noexcept {}
            """);
        await Assert.That(Has(doc, "C#`operator int`(455f465bc33b4cdf).", definition: true)).IsTrue();
        await Assert.That(Has(doc, "C#`operator const char *`(a9f41ea0e82d88cf).", definition: true)).IsTrue();
        await Assert.That(Has(doc, "`operator new`(bfcfa4d6b7f7ef64).", definition: true)).IsTrue();
        await Assert.That(Has(doc, "`operator delete`(bd21765a0afc8e3c).", definition: true)).IsTrue();
    }

    [Test]
    public async Task ConstructorReferencesFollowTheSelectedOverload()
    {
        var (_, doc) = await IndexAsync("""
            struct D { D() = default; D(const D &) = default; D(D &&) = default; };
            D make();
            void f(D &a) { D x; D y{a}; D z = D(); }
            """);
        await Assert.That(doc.Occurrences.Count(o => o.Symbol.EndsWith("D#D(ced63f7c635d850d).", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsGreaterThanOrEqualTo(2);
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("D#D(cf67c1cd7b9892d0).", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsTrue();
    }

    [Test]
    public async Task LocalClassesAreNamedAfterTheirFunction()
    {
        var (_, doc) = await IndexAsync("void f() { struct L { int field; }; L l; l.field = 1; }");
        await Assert.That(Has(doc, "f(49f6e7a06ebc5aa8).L#", definition: true)).IsTrue();
        await Assert.That(Has(doc, "f(49f6e7a06ebc5aa8).L#field.", definition: true)).IsTrue();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.StartsWith("local ", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task ExplicitSpecialisationsAndTemplateTemplateParametersHashLikeUpstream()
    {
        var (_, doc) = await IndexAsync("""
            template <typename T, typename S = T> void specialized(T) {}
            template <> void specialized<int, int>(int) {}
            template <typename A, typename B, template <typename> typename F> F<B> fmap(A f(B), F<A> fa) { return fa.fmap(f); }
            template <typename... Bs, template <typename...> typename... As> void g(As<Bs...> ...) {}
            """);
        await Assert.That(Has(doc, "specialized(9b289cee16747614).", definition: true)).IsTrue();
        await Assert.That(Has(doc, "specialized(d4f767463ce0a6b3).", definition: true)).IsTrue();
        await Assert.That(Has(doc, "fmap(22a739e3a02d9724).", definition: true)).IsTrue();
        await Assert.That(Has(doc, "g(da52d0cc43a6b199).", definition: true)).IsTrue();
    }

    [Test]
    public async Task SystemHeadersAreModelledAndReportedAsExternalSymbols()
    {
        var (index, doc) = await IndexAsync("""
            #include <vector>
            #include <string>
            int f(std::vector<int> &v, const std::string &s) { v.push_back(1); return v.size() + s.size(); }
            """);
        await Assert.That(Has(doc, "std/vector#", definition: false)).IsTrue();
        await Assert.That(Has(doc, "std/string#", definition: false)).IsTrue();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.Contains("std/vector#push_back(", StringComparison.Ordinal))).IsTrue();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.Contains("std/vector#size(", StringComparison.Ordinal))).IsTrue();
        await Assert.That(index.ExternalSymbols.Any(s => s.Symbol.EndsWith("std/vector#", StringComparison.Ordinal))).IsTrue();
        // no document is emitted for the virtual headers
        await Assert.That(index.Documents.Select(d => d.RelativePath)).IsEquivalentTo(new[] { "a.cc" });
    }

    [Test]
    public async Task NamespaceAliasesResolveSilently()
    {
        var (_, doc) = await IndexAsync("namespace a { namespace c { enum E { E0 }; } namespace c_alias = c; } void f(a::c_alias::E) {}");
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("a/c/E#", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsTrue();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.Contains("c_alias", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task UserDefinedLiteralOperatorsAreReferenced()
    {
        var (_, doc) = await IndexAsync("""
            void operator ""_km(unsigned long long) {}
            void f() { 5_km; }
            """);
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.Contains("operator\"\"_km", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsTrue();
    }
}
