using System.Runtime.CompilerServices;
using ScipSharp.Cli.Linting;

namespace ScipSharp.Indexer.Pascal.Tests;

/// <summary>Malformed input never crashes the indexer and still produces a lint-clean index.</summary>
public class RobustnessTests
{
    private static string CorpusFile(string rel, [CallerFilePath] string path = "") =>
        Path.Combine(Path.GetDirectoryName(path)!, "testdata", "corpus", rel);

    [Test]
    [Arguments("pascal-p5/pascals.pas")]
    [Arguments("threadsafecollections-fp/src/ThreadSafeCollections.List.pas")]
    public async Task TruncatedAndScrambledSourcesIndexCleanly(string rel)
    {
        var text = await File.ReadAllTextAsync(CorpusFile(rel));
        var random = new Random(7185);
        for (var round = 0; round < 12; round++)
        {
            var mutated = round switch
            {
                < 4 => text[..random.Next(text.Length)],
                < 8 => Scramble(text, random),
                _ => text.Remove(random.Next(text.Length / 2), random.Next(1, text.Length / 4)),
            };
            using var p = new TestProject().Add(Path.GetFileName(rel), mutated);
            var warnings = new List<string>();
            var options = PascalIndexer.ParseArguments(p.Root, [])!;
            var (metadata, documents, external) = PascalIndexer.Build(options, [], warnings.Add);
            var index = new Index { Metadata = metadata };
            index.Documents.AddRange(documents);
            index.ExternalSymbols.AddRange(external);
            await Assert.That(warnings).IsEmpty();
            await Assert.That(IndexLinter.Lint(index).Select(static d => d.Message)).IsEmpty();
        }
    }

    [Test]
    [Arguments("")]
    [Arguments("unit")]
    [Arguments("program;")]
    [Arguments("begin end.")]
    [Arguments("unit U; interface type T = class(")]
    [Arguments("program P; procedure A; procedure B; procedure C; begin")]
    [Arguments("{ unterminated comment")]
    [Arguments("program P; const s = 'unterminated\nbegin end.")]
    [Arguments("program P; begin with with with do do do end.")]
    [Arguments("program P; type T = record case of end; begin end.")]
    [Arguments("program P; var x: array[1..] of ; begin x[ := ] end.")]
    [Arguments("unit U; interface implementation procedure T.X.Y.Z; begin inherited end; end.")]
    [Arguments("program P; {$IFDEF X} {$ELSE} {$ELSE} {$ENDIF} {$ENDIF} {$I missing.inc} begin end.")]
    public async Task GarbageDoesNotThrow(string text)
    {
        using var p = new TestProject().Add("g.pas", text);
        var index = p.Index();
        await Assert.That(IndexLinter.Lint(index).Select(static d => d.Message)).IsEmpty();
    }

    [Test]
    public async Task DeepNestingIsHandled()
    {
        var b = new System.Text.StringBuilder("program P;\n");
        const int depth = 300;
        for (var i = 0; i < depth; i++)
        {
            b.Append("procedure N").Append(i).Append(";\n");
        }

        for (var i = depth - 1; i >= 0; i--)
        {
            b.Append("begin N").Append(i).Append(" end;\n");
        }

        b.Append("begin\n  writeln(").Append('(', 2000).Append('1').Append(')', 2000).Append(")\nend.\n");
        using var p = new TestProject().Add("p.pas", b.ToString());
        var index = p.Index();
        var doc = index.Documents.Single();
        await Assert.That(doc.Occurrences.Count(static o => o.HasRole(SymbolRole.Definition))).IsGreaterThanOrEqualTo(depth);
        await Assert.That(doc.Occurrences.Any(static o => o.Symbol.EndsWith("N298().N299().", StringComparison.Ordinal))).IsTrue();
        await Assert.That(IndexLinter.Lint(index)).IsEmpty();
    }

    private static string Scramble(string text, Random random)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length / 10; i++)
        {
            var a = random.Next(lines.Length);
            var b = random.Next(lines.Length);
            (lines[a], lines[b]) = (lines[b], lines[a]);
        }

        return string.Join('\n', lines);
    }
}
