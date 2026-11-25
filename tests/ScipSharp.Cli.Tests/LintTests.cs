using ScipSharp.Cli.Linting;
using static ScipSharp.Cli.Tests.TestHelpers;

namespace ScipSharp.Cli.Tests;

[NotInParallel]
public class LintTests
{
    private static Dictionary<string, string[]> Map(params (string Key, string[] Values)[] entries) =>
        entries.ToDictionary(static e => e.Key, static e => e.Values);

    private static readonly Range Placeholder = new(0, 0, 0, 0);

    private static IEnumerable<(string Name, Index Index, LintDiagnostic[] Expected)> BuildCases()
    {
        yield return ("duplicateSymbolInfo",
            MakeIndex([], Map(("myfile", ["dupSym", "dupSym"])), null),
            [new DuplicateSymbolInfoWarning("dupSym", "myfile")]);

        yield return ("emptyString",
            MakeIndex(["", "k~#r"], Map(("", []), ("a", ["", "b", "h~#r"])), Map(("c", [""]))),
            [
                new EmptyStringError("symbol", "external symbols"),
                new EmptyStringError("document", "index"),
                new EmptyStringError("symbol", "symbols for document a"),
                new EmptyStringError("symbol", "occurrence at c @ 0:0-0:0"),
                new EmptyStringError("symbol", "relationships for h"),
                new EmptyStringError("symbol", "relationships for external symbol k"),
            ]);

        yield return ("nonCanonicalSymbol",
            MakeIndex([], Map(), Map(("c", [". . . . `simple`#"]))),
            [new NonCanonicalSymbolError(". . . . `simple`#", ". . . . simple#", "")]);

        yield return ("duplicateDocument",
            new Index { Documents = { new Document { RelativePath = "dup" }, new Document { RelativePath = "dup" } } },
            [new DuplicateDocumentWarning("dup")]);

        yield return ("bothLocalAndExternalSymbol",
            MakeIndex(["foo"], Map(("x", ["foo"])), null),
            [new BothLocalAndExternalSymbolError("foo", "x")]);

        yield return ("missingRelationshipFlag",
            MakeIndex(["b", "a~b#"], Map(("f", ["x~a#"])), null),
            [
                new MissingRelationshipFlagError("a", "external symbols"),
                new MissingRelationshipFlagError("x", "symbols for file f"),
            ]);

        yield return ("missingSymbolInRelationship",
            MakeIndex(["a~b#r"], Map(("f", ["x~y#r"])), null),
            [
                new MissingSymbolInRelationshipError("a", "external symbols", "b", "external symbols"),
                new MissingSymbolInRelationshipError("x", "symbols for file f", "y", "external symbols or some other document"),
            ]);

        yield return ("multipleRelationship",
            MakeIndex(["b", "a~b#r", "a~b#d"], Map(("f", ["x~a#r", "x~a#d"])), null),
            [new MultipleRelationshipWarning("a", "b"), new MultipleRelationshipWarning("x", "a")]);

        yield return ("missingSymbolForOccurrence",
            MakeIndex([], null, Map(("f", ["a"]))),
            [new MissingSymbolForOccurrenceError("a", "f", Placeholder)]);

        yield return ("duplicateOccurrence",
            MakeIndex(["a"], Map(("f", ["b"])), Map(("f", ["a", "a", "b", "b"]))),
            [
                new DuplicateOccurrenceWarning("a", "f", Placeholder, 0),
                new DuplicateOccurrenceWarning("b", "f", Placeholder, 0),
            ]);

        yield return ("invalidOccurrenceRange",
            new Index
            {
                Documents =
                {
                    new Document
                    {
                        RelativePath = "f",
                        Occurrences = { new Occurrence { Symbol = "a", Range = { 1, 2 } } }, // wrong length
                    },
                },
            },
            [new InvalidOccurrenceRangeError("a", "f")]);
    }

    public static IEnumerable<string> CaseNames() => BuildCases().Select(static c => c.Name);

    [Test]
    [MethodDataSource(nameof(CaseNames))]
    public async Task ReportsExpectedDiagnostics(string name)
    {
        var (_, index, expected) = BuildCases().Single(c => c.Name == name);
        IndexLinter.SkipSymbolParseCheck = true;
        try
        {
            var messages = IndexLinter.Lint(index).Select(static d => d.Message).ToHashSet();
            foreach (var diagnostic in expected)
            {
                await Assert.That(messages).Contains(diagnostic.Message).Because($"test {name}: missing '{diagnostic.Message}'");
            }
        }
        finally
        {
            IndexLinter.SkipSymbolParseCheck = false;
        }
    }

    [Test]
    public async Task ValidIndexHasNoDiagnostics()
    {
        var index = MakeIndex([], Map(("f", ["s"])), null);
        index.Documents[0].Symbols[0].Symbol = "local 1";
        index.Documents[0].Occurrences.Add(Occ("local 1", new Range(0, 0, 0, 1), SymbolRole.Definition));
        await Assert.That(IndexLinter.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task UnparseableSymbolIsReportedUnlessSkipped()
    {
        var index = MakeIndex(["a"], Map(("f", ["a2"])), Map(("f", ["a2"])));
        await Assert.That(IndexLinter.Lint(index).OfType<InvalidSymbolError>()).IsNotEmpty();
    }

    [Test]
    public async Task LintCommandExitCodes()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var good = new Index();
            var goodPath = WriteIndex(dir, good, "good.scip");
            var (okCode, _, _) = await RunCli("lint", goodPath);
            await Assert.That(okCode).IsEqualTo(0);

            var bad = new Index { Documents = { new Document { RelativePath = "dup" }, new Document { RelativePath = "dup" } } };
            var badPath = WriteIndex(dir, bad, "bad.scip");
            var (code, _, error) = await RunCli("lint", badPath);
            await Assert.That(code).IsEqualTo(1);
            await Assert.That(error).Contains("found multiple documents with path 'dup'");

            var (missingCode, _, missingError) = await RunCli("lint");
            await Assert.That(missingCode).IsEqualTo(1);
            await Assert.That(missingError).Contains("missing argument");
        }
        finally
        {
            dir.Delete(true);
        }
    }
}

[NotInParallel]
public class LintOrderIndependenceTests
{
    [Test]
    public async Task RelationshipTargetMayLiveInALaterDocument()
    {
        SymbolInformation Info(string symbol, params Relationship[] rels)
        {
            var si = new SymbolInformation { Symbol = symbol };
            si.Relationships.AddRange(rels);
            return si;
        }

        var first = new Document { RelativePath = "a.x" };
        first.Symbols.Add(Info("s a b 1 a/A#", new Relationship { Symbol = "s a b 1 b/B#", IsImplementation = true }));
        var second = new Document { RelativePath = "b.x" };
        second.Symbols.Add(Info("s a b 1 b/B#"));
        var index = new Index { Documents = { first, second } };

        var diagnostics = IndexLinter.Lint(index);

        await Assert.That(diagnostics.Count).IsEqualTo(0);
    }
}

[NotInParallel]
public class LintLocalScopeTests
{
    [Test]
    public async Task SameLocalNameInTwoDocumentsIsNotADuplicateRelationship()
    {
        Document Doc(string path)
        {
            var d = new Document { RelativePath = path };
            var si = new SymbolInformation { Symbol = "local 1" };
            si.Relationships.Add(new Relationship { Symbol = "s a b 1 t/T#", IsImplementation = true });
            d.Symbols.Add(si);
            return d;
        }

        var target = new Document { RelativePath = "t.x" };
        target.Symbols.Add(new SymbolInformation { Symbol = "s a b 1 t/T#" });
        var index = new Index { Documents = { Doc("a.x"), Doc("b.x"), target } };

        await Assert.That(IndexLinter.Lint(index).Count).IsEqualTo(0);
    }
}
