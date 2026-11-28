using System.Diagnostics;
using System.Text;

namespace ScipSharp.Indexer.Delphi.Tests;

/// <summary>Pathological inputs that used to take quadratic time or overflow the stack (which kills the process).</summary>
public class PerformanceTests
{
    /// <summary>Indexes on a thread with the given stack size; returns the index, the warnings and the elapsed time.</summary>
    private static (Index? Index, List<string> Warnings, TimeSpan Elapsed, Exception? Failure) IndexOnThread(Dictionary<string, string> files, int stackSize)
    {
        var dir = TestUtil.NewProject(files);
        var warnings = new List<string>();
        Index? index = null;
        Exception? failure = null;
        var sw = Stopwatch.StartNew();
        var thread = new Thread(() =>
        {
            try
            {
                index = TestUtil.Index(dir, warnings: warnings);
            }
            catch (Exception e)
            {
                failure = e;
            }
        }, stackSize);
        thread.Start();
        thread.Join();
        return (index, warnings, sw.Elapsed, failure);
    }

    [Test]
    public async Task DeepNestingOnSmallStackDoesNotCrash()
    {
        var b = new StringBuilder("program P;\n{$IF ").Append('(', 20_000).Append('1').Append(')', 20_000).Append("}{$ENDIF}\n");
        b.Append("type T = ").Append(string.Concat(Enumerable.Repeat("record x: ", 5_000))).Append("Integer").Append(string.Concat(Enumerable.Repeat("; end", 5_000))).Append(";\n");
        b.Append("var a: Integer;\nbegin\n  a := ").Append(string.Concat(Enumerable.Repeat("Abs(", 20_000))).Append('a').Append(')', 20_000).Append(";\n");
        b.Append("  a := ").Append('(', 20_000).Append('a').Append(')', 20_000).Append(";\nend.\n");
        var (index, warnings, _, failure) = IndexOnThread(new() { ["P.dpr"] = b.ToString(), ["Q.pas"] = "unit Q; interface var q: Integer; implementation end." }, 512 * 1024);
        await Assert.That(failure).IsNull();
        await Assert.That(warnings).IsNotEmpty();
        await Assert.That(index!.Documents.Any(static d => d.RelativePath == "Q.pas")).IsTrue();
    }

    [Test]
    public async Task DeeplyNestedParenthesesAreLinear()
    {
        // Every nesting level used to re-scan its whole group (SkipParens, the `as` search, CountArgs).
        var src = "program P;\nvar a: Integer;\nbegin\n  a := " + string.Concat(Enumerable.Repeat("Abs((", 10_000)) + "a" + new string(')', 20_000) + ";\nend.\n";
        var (index, _, elapsed, failure) = IndexOnThread(new() { ["P.dpr"] = src }, 256 * 1024 * 1024);
        await Assert.That(failure).IsNull();
        await Assert.That(index!.Documents.Single().Occurrences.Count).IsGreaterThan(10_000);
        await Assert.That(elapsed.TotalSeconds).IsLessThan(5);
    }

    [Test]
    public async Task LongDottedChainIsLinear()
    {
        var src = "program P;\nuses SysUtils;\nvar a: Integer;\nbegin\n  a := a" + string.Concat(Enumerable.Repeat(".c", 60_000)) + ";\nend.\n";
        var (_, _, elapsed, failure) = IndexOnThread(new() { ["P.dpr"] = src }, 16 * 1024 * 1024);
        await Assert.That(failure).IsNull();
        await Assert.That(elapsed.TotalSeconds).IsLessThan(5);
    }

    [Test]
    public async Task UnterminatedBlocksAndWithsAreLinear()
    {
        // Every `with` asked for the end of the following (unterminated) block, re-scanning to the end of the file, and
        // every name then searched all the with-scopes piled up so far.
        var src = "program P;\ntype TR = record x, y: Integer; end;\nvar r: TR;\nbegin\n" +
            string.Concat(Enumerable.Repeat("  with r do begin x := y\n", 10_000));
        var (index, _, elapsed, failure) = IndexOnThread(new() { ["P.dpr"] = src }, 16 * 1024 * 1024);
        await Assert.That(failure).IsNull();
        var occurrences = index!.Documents.Single().Occurrences;
        await Assert.That(occurrences.Count(static o => o.Symbol.EndsWith("TR#x.", StringComparison.Ordinal))).IsGreaterThanOrEqualTo(10_000);
        await Assert.That(elapsed.TotalSeconds).IsLessThan(5);
    }

    [Test]
    public async Task AnonymousMethodsMissingEndAreNotRewalkedExponentially()
    {
        // Each body (`begin` without `end`) ran past its enclosing parentheses to the end of the file and was walked
        // again by every enclosing walk: 22 such lines took ~8 s, each further line ~1.6x more.
        var src = "program P;\nvar a: Integer;\nbegin\n" + string.Concat(Enumerable.Repeat("  a := F(procedure begin a := 1);\n", 200)) + "end.\n";
        var (index, _, elapsed, failure) = IndexOnThread(new() { ["P.dpr"] = src }, 16 * 1024 * 1024);
        await Assert.That(failure).IsNull();
        await Assert.That(index!.Documents.Single().Occurrences.Count(static o => o.Symbol.EndsWith("/a.", StringComparison.Ordinal))).IsGreaterThanOrEqualTo(400);
        await Assert.That(elapsed.TotalSeconds).IsLessThan(5);
    }
}
