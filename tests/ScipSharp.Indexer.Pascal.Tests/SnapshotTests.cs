using System.Runtime.CompilerServices;
using ScipSharp.Cli.Linting;
using ScipSharp.Indexer.Pascal.Syntax;
using ScipSharp.Testing;

namespace ScipSharp.Indexer.Pascal.Tests;

/// <summary>
/// Golden snapshots of the fixtures in <c>testdata/snapshots/input/&lt;case&gt;</c> (rendered with the core
/// <see cref="SnapshotFormatter"/>). Regenerate with <c>SCIP_UPDATE_SNAPSHOTS=1</c>.
/// </summary>
public class SnapshotTests
{
    private static string ProjectDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    private static string TestData => Path.Combine(ProjectDir(), "testdata");

    public static IEnumerable<string> Cases() =>
        Directory.EnumerateDirectories(Path.Combine(TestData, "snapshots", "input")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!;

    [Test]
    public async Task SnapshotsMatch()
    {
        var mismatches = SnapshotTesting.RunDirectories(
            Path.Combine(TestData, "snapshots", "input"),
            Path.Combine(TestData, "snapshots", "output"),
            static (inputDir, _, _) =>
            {
                var index = PascalIndexer.BuildIndex(inputDir);
                return index.Documents.Select(d =>
                {
                    var text = ScipSharp.SnapshotFormatter.FormatSnapshot(d, "//", SymbolFormatter.Verbose, Path.Combine(inputDir, d.RelativePath)).Text;
                    return new SourceFile(Path.Combine(inputDir, d.RelativePath), d.RelativePath, text);
                }).ToList();
            });
        await Assert.That(string.Join("\n", mismatches.Select(static m => m.Diff))).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task FixtureIndexIsLintClean(string testCase)
    {
        var index = PascalIndexer.BuildIndex(Path.Combine(TestData, "snapshots", "input", testCase));
        await Assert.That(index.Documents.Count).IsGreaterThan(0);
        await Assert.That(IndexLinter.Lint(index).Select(static d => d.Message)).IsEmpty();
    }

    [Test]
    public async Task EveryFixtureFileIsIndexed()
    {
        var iso = PascalIndexer.BuildIndex(Path.Combine(TestData, "snapshots", "input", "iso"));
        await Assert.That(iso.Documents.Select(static d => d.RelativePath)).IsEquivalentTo(["bank.p", "scanner.pas"]);
        var turbo = PascalIndexer.BuildIndex(Path.Combine(TestData, "snapshots", "input", "turbo"));
        await Assert.That(turbo.Documents.Select(static d => d.RelativePath)).IsEquivalentTo(["colors.inc", "draw.pas", "shapes.pas"]);
        var lazarus = PascalIndexer.BuildIndex(Path.Combine(TestData, "snapshots", "input", "lazarus"));
        await Assert.That(lazarus.Documents.Select(static d => d.RelativePath)).IsEquivalentTo(["collections.pas", "demo.lpr", "mainform.lfm", "mainform.pas"]);
        foreach (var doc in lazarus.Documents)
        {
            await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf8CodeUnitOffsetFromLineStart);
        }
    }

    /// <summary>Real-world corpora: everything indexes without warnings, lint-clean, with (almost) every identifier resolved.</summary>
    [Test]
    [Arguments("pascal-p5", 9, 0.97)]
    [Arguments("threadsafecollections-fp", 12, 0.97)]
    public async Task CorpusIndexesCleanly(string corpus, int minDocuments, double minCoverage)
    {
        var root = Path.Combine(TestData, "corpus", corpus);
        var warnings = new List<string>();
        var options = PascalIndexer.ParseArguments(root, [])!;
        var (metadata, documents, external) = PascalIndexer.Build(options, [], warnings.Add);
        var index = new Index { Metadata = metadata };
        index.Documents.AddRange(documents);
        index.ExternalSymbols.AddRange(external);
        await Assert.That(warnings).IsEmpty();
        await Assert.That(index.Documents.Count).IsGreaterThanOrEqualTo(minDocuments);
        await Assert.That(IndexLinter.Lint(index).Select(static d => d.Message)).IsEmpty();

        // Coverage: identifier tokens (from the raw lexer, so inactive {$IFDEF} code counts against us) with an occurrence.
        long identifiers = 0, covered = 0;
        foreach (var doc in index.Documents.Where(static d => !d.RelativePath.EndsWith(".lfm", StringComparison.Ordinal)))
        {
            var src = SourceText.Load(Path.Combine(root, doc.RelativePath), System.Text.Encoding.Latin1);
            var starts = doc.Occurrences.Select(static o => o.GetSourceRange()!.Value.Start).ToHashSet();
            var lexer = new Lexer(src);
            for (var t = lexer.Next(); t.Kind != TokenKind.Eof; t = lexer.Next())
            {
                if (t.Kind != TokenKind.Identifier || Preprocessor.IsReserved(t.Lower, PascalMode.ObjFpc) || IsContextual(t.Lower))
                {
                    continue;
                }

                identifiers++;
                if (starts.Contains(src.PositionOf(t.Start)))
                {
                    covered++;
                }
            }
        }

        var coverage = (double)covered / identifiers;
        await Assert.That(coverage).IsGreaterThanOrEqualTo(minCoverage);
    }

    private static bool IsContextual(string lower) => lower is "private" or "protected" or "public" or "published" or "strict"
        or "virtual" or "override" or "overload" or "abstract" or "forward" or "external" or "read" or "write" or "default"
        or "generic" or "specialize" or "helper" or "otherwise" or "self" or "on" or "out" or "reintroduce" or "inline"
        or "static" or "message" or "final" or "cdecl" or "stdcall" or "nested" or "sealed" or "constref" or "deprecated"
        or "platform" or "experimental" or "stored" or "nodefault" or "index" or "name" or "absolute" or "assembler";
}
