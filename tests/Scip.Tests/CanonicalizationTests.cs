#pragma warning disable CS0612 // deprecated range fields are exercised on purpose

using Google.Protobuf.WellKnownTypes;

namespace Scip.Tests;

public class CanonicalizationTests
{
    private static Occurrence Occ(string symbol, int[] range, int roles = 0, params string[] overrideDocs)
    {
        var o = new Occurrence { Symbol = symbol, SymbolRoles = roles };
        o.Range.AddRange(range);
        o.OverrideDocumentation.AddRange(overrideDocs);
        return o;
    }

    private static SymbolInformation Sym(string symbol, string[]? docs = null, params Relationship[] rels)
    {
        var s = new SymbolInformation { Symbol = symbol };
        s.Documentation.AddRange(docs ?? []);
        s.Relationships.AddRange(rels);
        return s;
    }

    private static string Show(Occurrence o) =>
        $"{o.Symbol}|{string.Join(',', o.Range)}|{o.SymbolRoles}|{string.Join(';', o.OverrideDocumentation)}";

    private static string Show(SymbolInformation s) =>
        $"{s.Symbol}|{string.Join(';', s.Documentation)}|{string.Join(';', s.Relationships.Select(r => $"{r.Symbol}:{r.IsReference}:{r.IsImplementation}"))}";

    [Test]
    public async Task CanonicalizeDocument()
    {
        var document = new Document { RelativePath = "foo.go" };
        document.Symbols.AddRange([
            Sym("foo"), Sym("bar"), Sym("bonk"),
            Sym("baz", null, new Relationship { Symbol = "bazzer", IsReference = true }),
            Sym("baz", null, new Relationship { Symbol = "bazImpl", IsImplementation = true }),
            Sym("quux", ["docs1"]),
            Sym("quux", ["docs2"]),
        ]);
        document.Occurrences.AddRange([
            Occ("foo", []), // illegal range (removed)
            Occ("bar", [2, 1, 2, 4], (int)SymbolRole.Definition),
            Occ("foo", [1, 1, 1, 4]),
            Occ("bonk", [4, 1, 4, 4]),
            Occ("honk", [6, 1, 6, 4]),
            Occ("quux", [5, 1, 5, 4], 0, "primo"),
            Occ("quux", [5, 1, 5, 4], 0, "secondo"),
            Occ("baz", [3, 1, 4, 4]),
            Occ("bazImpl", [3, 1, 4, 4]),
        ]);

        var result = Canonicalizer.CanonicalizeDocument(document);

        await Assert.That(result).IsSameReferenceAs(document);
        await Assert.That(result.RelativePath).IsEqualTo("foo.go");
        await Assert.That(result.Symbols.Select(Show).ToArray()).IsEquivalentTo(new[]
        {
            "bar||",
            "baz||bazImpl:False:True;bazzer:True:False",
            "bonk||",
            "foo||",
            "quux|docs1;docs2|",
        });
        await Assert.That(result.Occurrences.Select(Show).ToArray()).IsEquivalentTo(new[]
        {
            "foo|1,1,4|0|",
            "bar|2,1,4|1|",
            "baz|3,1,4,4|0|",
            "bazImpl|3,1,4,4|0|",
            "bonk|4,1,4|0|",
            "quux|5,1,4|0|primo;secondo",
            "honk|6,1,4|0|",
        });
    }

    [Test]
    public async Task CanonicalizeSortsDiagnosticsAndTags()
    {
        var occ = Occ("a", [0, 0, 1]);
        occ.Diagnostics.Add(new Diagnostic { Severity = Severity.Warning, Message = "b", Tags = { DiagnosticTag.Deprecated, DiagnosticTag.Unnecessary } });
        occ.Diagnostics.Add(new Diagnostic { Severity = Severity.Error, Message = "z" });
        occ.Diagnostics.Add(new Diagnostic { Severity = Severity.Warning, Message = "a" });

        var result = Canonicalizer.CanonicalizeOccurrence(occ);

        await Assert.That(result.Diagnostics.Select(d => $"{d.Severity}:{d.Message}").ToArray())
            .IsEquivalentTo(new[] { "Error:z", "Warning:a", "Warning:b" });
        await Assert.That(result.Diagnostics[2].Tags.ToArray())
            .IsEquivalentTo(new[] { DiagnosticTag.Unnecessary, DiagnosticTag.Deprecated });
    }

    [Test]
    public async Task CanonicalizeLeavesTypedRangesAlone()
    {
        var occ = new Occurrence { Symbol = "a" };
        occ.SetSourceRange(new Range(1, 2, 1, 3));
        var result = Canonicalizer.CanonicalizeOccurrences([occ]);
        await Assert.That(result.Single().SingleLineRange).IsEqualTo(new SingleLineRange { Line = 1, StartCharacter = 2, EndCharacter = 3 });
    }

    [Test]
    public async Task FlattenDocuments()
    {
        var s1 = new[] { Sym("foo"), Sym("bar") };
        var o1 = new[] { Occ("foo", [1, 2, 3]), Occ("bar", [2, 3, 4]) };
        var s2 = new[] { Sym("baz"), Sym("bonk") };
        var o2 = new[] { Occ("baz", [3, 4, 5]), Occ("bonk", [4, 5, 6]) };
        var s3 = new[] { Sym("quux") };
        var o3 = new[] { Occ("quux", [5, 6, 7]) };

        Document Doc(string path, SymbolInformation[] s, Occurrence[] o)
        {
            var d = new Document { RelativePath = path };
            d.Symbols.AddRange(s);
            d.Occurrences.AddRange(o);
            return d;
        }

        var flattened = Sorting.SortDocuments(Flattener.FlattenDocuments([Doc("foo.go", s1, o1), Doc("bar.go", s2, o2), Doc("foo.go", s3, o3)]));

        await Assert.That(flattened.Select(d => d.RelativePath).ToArray()).IsEquivalentTo(new[] { "bar.go", "foo.go" });
        await Assert.That(flattened[0].Symbols.Select(s => s.Symbol).ToArray()).IsEquivalentTo(new[] { "baz", "bonk" });
        await Assert.That(flattened[1].Symbols.Select(s => s.Symbol).ToArray()).IsEquivalentTo(new[] { "foo", "bar", "quux" });
        await Assert.That(flattened[1].Occurrences.Select(Show).ToArray())
            .IsEquivalentTo(new[] { "foo|1,2,3|0|", "bar|2,3,4|0|", "quux|5,6,7|0|" });
    }

    [Test]
    public async Task FlattenOccurrencesMergesRolesSyntaxAndDocs()
    {
        var a = Occ("s", [1, 0, 3], (int)SymbolRole.Definition, "one");
        var b = Occ("s", [1, 0, 3], (int)SymbolRole.ReadAccess, "two");
        b.SyntaxKind = SyntaxKind.Identifier;
        var result = Flattener.FlattenOccurrences([a, b]);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].SymbolRoles).IsEqualTo((int)SymbolRole.Definition | (int)SymbolRole.ReadAccess);
        await Assert.That(result[0].SyntaxKind).IsEqualTo(SyntaxKind.Identifier);
        await Assert.That(result[0].OverrideDocumentation.ToArray()).IsEquivalentTo(new[] { "one", "two" });
    }

    [Test]
    public async Task FlattenRelationshipsOrsFlags()
    {
        var result = Flattener.FlattenRelationships([
            new Relationship { Symbol = "x", IsReference = true },
            new Relationship { Symbol = "y" },
            new Relationship { Symbol = "x", IsDefinition = true, IsTypeDefinition = true },
        ]);
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].IsReference && result[0].IsDefinition && result[0].IsTypeDefinition && !result[0].IsImplementation).IsTrue();
    }

    [Test]
    public async Task FlattenSymbolsDeduplicatesDocumentation()
    {
        var result = Flattener.FlattenSymbols([Sym("a", ["x", "y"]), Sym("a", ["y", "z"])]);
        await Assert.That(result.Single().Documentation.ToArray()).IsEquivalentTo(new[] { "x", "y", "z" });
    }

    // ---- sort.go ----

    private static readonly int[][] UnsortedRanges =
    [
        [2, 3, 5], [11, 10, 12], [6, 3, 5], [10, 4, 8], [10, 10, 12], [0, 3, 4, 5], [12, 1, 13, 12],
        [11, 1, 3], [5, 3, 5], [10, 1, 3], [12, 10, 13, 3], [11, 4, 8], [12, 4, 13, 8], [1, 3, 3, 5],
    ];

    // Indexes into UnsortedRanges in sorted order (the "rank" comments of the Go test).
    private static readonly int[] SortedOrder = [5, 13, 0, 8, 2, 9, 3, 4, 7, 11, 1, 6, 12, 10];

    [Test]
    public async Task FindOccurrences()
    {
        var occurrences = new[]
        {
            Occ("", [0, 3, 4, 5]), Occ("", [1, 3, 3, 5]), Occ("", [2, 3, 5]), Occ("", [5, 3, 5]), Occ("", [6, 3, 5]),
        };
        var matches = Sorting.FindOccurrences(occurrences, 2, 4);
        await Assert.That(matches.Count).IsEqualTo(3);
        await Assert.That(matches[0]).IsSameReferenceAs(occurrences[2]);
        await Assert.That(matches[1]).IsSameReferenceAs(occurrences[1]);
        await Assert.That(matches[2]).IsSameReferenceAs(occurrences[0]);
    }

    [Test]
    public async Task SortOccurrences()
    {
        var unsorted = UnsortedRanges.Select(r => Occ("", r)).ToList();
        Sorting.SortOccurrences(unsorted);
        await Assert.That(unsorted.Select(o => string.Join(',', o.Range)).ToArray())
            .IsEquivalentTo(SortedOrder.Select(i => string.Join(',', UnsortedRanges[i])).ToArray());
    }

    [Test]
    public async Task SortRanges()
    {
        var unsorted = UnsortedRanges.Select(r => Range.CreateUnchecked(r)).ToList();
        Sorting.SortRanges(unsorted);
        await Assert.That(unsorted.Select(r => string.Join(',', r.ToScipRange())).ToArray())
            .IsEquivalentTo(SortedOrder.Select(i => string.Join(',', Range.CreateUnchecked(UnsortedRanges[i]).ToScipRange())).ToArray());
    }

    [Test]
    public async Task SortOccurrencesAcceptsRepeatedField()
    {
        var doc = new Document();
        doc.Occurrences.AddRange(UnsortedRanges.Select(r => Occ("", r)));
        Sorting.SortOccurrences(doc.Occurrences);
        await Assert.That(string.Join(',', doc.Occurrences[0].Range)).IsEqualTo("0,3,4,5");
    }

    [Test]
    public async Task FindSymbolBinarySearch()
    {
        // Deterministic stand-in for the Go rapid test: strings from a mixed alphabet incl. non-BMP and unsorted input.
        string[] names = ["", "a", "ab", "b", "zeta", "～", "\U0001F680", "local 1", "scip . . . x#", "Z", "a b"];
        var doc = new Document();
        foreach (var n in names.Concat(names.Take(4)))
        {
            doc.Symbols.Add(new SymbolInformation { Symbol = n });
        }

        Canonicalizer.CanonicalizeDocument(doc);
        await Assert.That(doc.Symbols.Count).IsEqualTo(names.Length);
        foreach (var n in names)
        {
            await Assert.That(Sorting.FindSymbolBinarySearch(doc, n)?.Symbol).IsEqualTo(n);
            await Assert.That(Sorting.FindSymbol(doc, n)?.Symbol).IsEqualTo(n);
        }

        await Assert.That(Sorting.FindSymbolBinarySearch(doc, "missing")).IsNull();
        await Assert.That(Sorting.FindSymbolBinarySearch(new Document(), "x")).IsNull();
    }

    [Test]
    public async Task ComparePositionToRange()
    {
        await Assert.That(Sorting.ComparePositionToRange(1, 2, 3, 4, 0, 9)).IsEqualTo(1);
        await Assert.That(Sorting.ComparePositionToRange(1, 2, 3, 4, 4, 0)).IsEqualTo(-1);
        await Assert.That(Sorting.ComparePositionToRange(1, 2, 3, 4, 1, 1)).IsEqualTo(1);
        await Assert.That(Sorting.ComparePositionToRange(1, 2, 3, 4, 3, 4)).IsEqualTo(-1);
        await Assert.That(Sorting.ComparePositionToRange(1, 2, 3, 4, 2, 100)).IsEqualTo(0);
    }

    [Test]
    public async Task SanitizeReplacesLoneSurrogates()
    {
        await Assert.That(Sanitizer.SanitizeString("ok \U0001F680")).IsEqualTo("ok \U0001F680");
        await Assert.That(Sanitizer.SanitizeString("a\uD800b")).IsEqualTo("a�b");
        await Assert.That(Sanitizer.SanitizeString("a\uDC00")).IsEqualTo("a�");
        await Assert.That(Sanitizer.SanitizeString("\uDC00\uD800")).IsEqualTo("��");
    }

    [Test]
    public async Task SanitizeDocumentCoversAllStrings()
    {
        var doc = new Document { Language = "x\uD800", RelativePath = "p\uD800" };
        var occ = new Occurrence { Symbol = "s\uD800" };
        occ.OverrideDocumentation.Add("d\uD800");
        occ.Diagnostics.Add(new Diagnostic { Code = "c\uD800", Message = "m\uD800", Source = "o\uD800" });
        doc.Occurrences.Add(occ);
        var sym = new SymbolInformation { Symbol = "y\uD800" };
        sym.Documentation.Add("e\uD800");
        sym.Relationships.Add(new Relationship { Symbol = "r\uD800" });
        doc.Symbols.Add(sym);

        Sanitizer.SanitizeDocument(doc);

        await Assert.That(doc.ToString()).DoesNotContain("\uD800");
        await Assert.That(doc.Language).IsEqualTo("x�");
        await Assert.That(doc.Symbols[0].Relationships[0].Symbol).IsEqualTo("r�");
        await Assert.That(doc.Occurrences[0].Diagnostics[0].Source).IsEqualTo("o�");
    }
}
