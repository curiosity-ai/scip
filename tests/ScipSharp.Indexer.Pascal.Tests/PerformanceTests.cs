using System.Diagnostics;
using System.Text;
using ScipSharp.Indexer.Pascal.Semantics;

namespace ScipSharp.Indexer.Pascal.Tests;

/// <summary>Pathological inputs that used to take quadratic/exponential time or overflow the stack.</summary>
public class PerformanceTests
{
    private static async Task<Index> IndexWithin(TestProject p, double seconds)
    {
        var sw = Stopwatch.StartNew();
        var index = p.Index();
        await Assert.That(sw.Elapsed.TotalSeconds).IsLessThan(seconds);
        return index;
    }

    [Test]
    public async Task LongSingleLineIsLinear()
    {
        // Every occurrence on one 400k-char line: positions used to be measured from the line start each time.
        var src = "program P;\nvar b: Integer;\nbegin\n b := " + string.Join(" + ", Enumerable.Repeat("b", 100_000)) + ";\nend.\n";
        using var p = new TestProject().Add("p.pas", src);
        var index = await IndexWithin(p, 5);
        await Assert.That(index.Documents.Single().Occurrences.Count).IsGreaterThan(1000);
    }

    [Test]
    public async Task NonAsciiLinePositionsAreUtf8Offsets()
    {
        var src = "program P;\nvar ä1, b: Integer;\nbegin b := 1; ä1 := b + b; { é } b := ä1; end.\n";
        var (_, doc) = TestProject.Single("p.pas", src);
        var line = src.Split('\n')[2];
        var expected = new List<int>();
        for (var i = line.IndexOf('b', StringComparison.Ordinal); i >= 0; i = line.IndexOf('b', i + 1))
        {
            if (i > 0 && line[i - 1] is ' ' or '+' && (i + 1 >= line.Length || !char.IsLetterOrDigit(line[i + 1])))
            {
                expected.Add(Encoding.UTF8.GetByteCount(line.AsSpan(0, i)));
            }
        }

        var actual = doc.Occurrences.Where(static o => o.Line() == 2 && o.Symbol.EndsWith("b.", StringComparison.Ordinal))
            .Select(static o => o.Column()).Order().ToList();
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    public async Task LongDottedChainIsLinear()
    {
        var src = "program P;\nvar a: Integer;\nbegin\n a := a" + string.Concat(Enumerable.Repeat(".c", 60_000)) + ";\nend.\n";
        using var p = new TestProject().Add("p.pas", src);
        await IndexWithin(p, 5);
    }

    [Test]
    public async Task SelfIncludingFileIsNotExpandedExponentially()
    {
        // a.inc includes itself twice: with only a depth limit this was 2^16 expansions.
        var inc = new StringBuilder();
        for (var i = 0; i < 50; i++)
        {
            inc.Append("a := a + ").Append(i).Append(";\n");
        }

        inc.Append("{$I a.inc}\n{$I a.inc}\n");
        using var p = new TestProject().Add("p.pas", "program P;\nvar a: Integer;\nbegin\n{$I a.inc}\nend.\n").Add("a.inc", inc.ToString());
        var index = await IndexWithin(p, 5);
        await Assert.That(index.Documents.Any(static d => d.RelativePath == "a.inc")).IsTrue();
    }

    [Test]
    public async Task UnbalancedNestedCommentsAreLinear()
    {
        // In objfpc mode every '{ a { b }' comment used to re-scan to the end of the file before falling back.
        var b = new StringBuilder("{$mode objfpc}\nprogram P;\n");
        for (var i = 0; i < 30_000; i++)
        {
            b.Append("{ a { b }\nvar x").Append(i).Append(": Integer;\n");
        }

        b.Append("begin end.\n");
        using var p = new TestProject().Add("p.pas", b.ToString());
        var index = await IndexWithin(p, 5);
        await Assert.That(index.Documents.Single().Occurrences.Count(static o => o.HasRole(SymbolRole.Definition))).IsGreaterThan(30_000);
    }

    [Test]
    public async Task BalancedNestedCommentAfterStrayBraceStillNests()
    {
        var src = "{$mode objfpc}\nprogram P;\n{ stray { }\nvar a: Integer;\n{ outer { inner } still } \nvar b: Integer;\nbegin a := b; end.\n";
        var (_, doc) = TestProject.Single("p.pas", src);
        await Assert.That(doc.SymbolsAt(src, "a", definitions: true)).IsNotEmpty();
        await Assert.That(doc.SymbolsAt(src, "b", definitions: true)).IsNotEmpty();
        await Assert.That(doc.SymbolsAt(src, "still")).IsEmpty();
    }

    [Test]
    public async Task DeepNestingOnSmallStackSkipsTheFileInsteadOfCrashing()
    {
        // Build() runs on a 512 MB stack; on a small one the depth guard must stop the unit, not the process.
        var src = "program P;\nvar a: Integer;\nbegin\n a := " + new string('(', 20_000) + "a" + new string(')', 20_000) + ";\n if a = 1 then a := 2;\nend.\n";
        using var p = new TestProject().Add("p.pas", src);
        var warnings = new List<string>();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                new Workspace(PascalIndexer.ParseArguments(p.Root, [])!, warnings.Add).Run();
            }
            catch (Exception e)
            {
                failure = e;
            }
        }, 256 * 1024);
        thread.Start();
        thread.Join();
        await Assert.That(failure).IsNull();
        await Assert.That(warnings.Any(static w => w.Contains("InsufficientExecutionStackException", StringComparison.Ordinal))).IsTrue();
    }
}
