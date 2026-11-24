using Scip.Reprolang;

namespace Scip.Reprolang.Tests;

public class ParserTests
{
    private static SourceFile Source(string text, string path = "a.repro") => new(path, path, text);

    private static Index IndexOf(string text) => ReproIndexer.Index("file:///root", "pkg", [Source(text)]);

    [Test]
    public async Task DefinitionReferenceAndRanges()
    {
        var index = IndexOf("definition hello().\nreference hello().\n");
        var occ = index.Documents[0].Occurrences;
        await Assert.That(occ.Count).IsEqualTo(2);
        await Assert.That(occ[0].Symbol).IsEqualTo("reprolang repro_manager pkg 1.0.0 a.repro/hello().");
        await Assert.That(occ[0].SymbolRoles).IsEqualTo((int)SymbolRole.Definition);
        occ[0].TryGetSourceRange(out var r0);
        await Assert.That(r0).IsEqualTo(new Range(0, 11, 0, 19));
        occ[1].TryGetSourceRange(out var r1);
        await Assert.That(r1).IsEqualTo(new Range(1, 10, 1, 18));
        await Assert.That(occ[1].SymbolRoles).IsEqualTo(0);
    }

    [Test]
    public async Task ForwardDefinitionRole()
    {
        var index = IndexOf("reference forward_definition abc#\ndefinition abc#\n");
        await Assert.That(index.Documents[0].Occurrences.Select(o => o.SymbolRoles))
            .IsEquivalentTo(new[] { (int)SymbolRole.Definition, (int)SymbolRole.ForwardDefinition });
    }

    [Test]
    public async Task LocalsAreFileScoped()
    {
        var index = IndexOf("# docstring: a local\ndefinition local x\nreference local x\n");
        var doc = index.Documents[0];
        await Assert.That(doc.Occurrences.All(o => o.Symbol == "local x")).IsTrue();
        await Assert.That(doc.Symbols[0].Documentation).IsEquivalentTo(new[] { "signature of x", "a local" });
    }

    [Test]
    public async Task RelationshipFlags()
    {
        var index = IndexOf("definition a\ndefinition b\ndefinition c implements a references b type_defines a\n");
        var info = index.Documents[0].Symbols.Single(s => s.Symbol.EndsWith("/c", StringComparison.Ordinal));
        await Assert.That(info.Relationships.Count).IsEqualTo(2);
        var a = info.Relationships.Single(r => r.Symbol.EndsWith("/a", StringComparison.Ordinal));
        await Assert.That(a.IsImplementation).IsTrue();
        await Assert.That(a.IsTypeDefinition).IsTrue();
        await Assert.That(info.Relationships.Single(r => r.Symbol.EndsWith("/b", StringComparison.Ordinal)).IsReference).IsTrue();
    }

    [Test]
    public async Task DefinedByRelationshipsStatement()
    {
        var index = IndexOf("definition C1_f.\nreference C2_f.\nrelationships C2_f. defined_by C1_f.\n");
        var symbols = index.Documents[0].Symbols;
        // C2_f is defined by C1_f, so it shares C1_f's symbol.
        await Assert.That(symbols.Count).IsEqualTo(2);
        await Assert.That(symbols[0].Symbol).IsEqualTo(symbols[1].Symbol);
    }

    [Test]
    public async Task KeywordsAreContextSensitive()
    {
        // "implements" is not a relation of a reference statement, so it is the descriptors of a global identifier.
        var ex = Assert.Throws<ReproIndexException>(() => IndexOf("reference other implements\n"));
        await Assert.That(ex.Message).Contains("\"global other implements\"");
    }

    [Test]
    public async Task UnresolvedReferenceThrows()
    {
        var ex = Assert.Throws<ReproIndexException>(() => IndexOf("reference nope\n"));
        await Assert.That(ex.Errors.Count).IsEqualTo(1);
        await Assert.That(ex.Message).Contains("could not resolve local symbol \"nope\"");
    }

    [Test]
    public async Task DeprecatedIdentifiersGetDiagnostics()
    {
        var index = IndexOf("definition deprecatedX\nreference deprecatedX\ndefinition ok\n");
        var occ = index.Documents[0].Occurrences;
        // Occurrences: definitions first (deprecatedX, ok), then references.
        await Assert.That(occ[0].Diagnostics.Single().Severity).IsEqualTo(Severity.Warning);
        await Assert.That(occ[2].Diagnostics.Single().Message).IsEqualTo("deprecated identifier");
        await Assert.That(occ[1].Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ColumnsAreUtf8Bytes()
    {
        var index = IndexOf("definition é\nreference é\n");
        index.Documents[0].Occurrences[0].TryGetSourceRange(out var r);
        await Assert.That(r).IsEqualTo(new Range(0, 11, 0, 13));
    }

    [Test]
    public async Task CommentsAndNoSymbolInformation()
    {
        var index = IndexOf("# a comment\ndefinition NoSymbolInformation# # trailing\ndefinition d\n");
        var doc = index.Documents[0];
        await Assert.That(doc.Occurrences.Count).IsEqualTo(2);
        await Assert.That(doc.Symbols.Count).IsEqualTo(1);
    }
}
