using System.Text;
using ScipSharp.Indexer.Clang;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Clang.Tests;

/// <summary>Malformed or pathological input must neither hang the indexer nor make it quadratic/exponential.</summary>
public class RobustnessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    private static async Task<ScipSharp.Document> IndexWithin(string source)
    {
        var dir = Directory.CreateTempSubdirectory("scip-clang-robust").FullName;
        File.WriteAllText(Path.Combine(dir, "a.cc"), source);
        var output = Path.Combine(dir, "index.scip");
        var run = Task.Run(() => new ClangIndexer().IndexAsync(new IndexerOptions(dir, output, ["--jobs", "1"]), CancellationToken.None));
        await Assert.That(await run.WaitAsync(Limit)).IsEqualTo(0); // TimeoutException = hang
        return ScipSharp.Index.Parser.ParseFrom(File.ReadAllBytes(output)).Documents.Single(d => d.RelativePath == "a.cc");
    }

    [Test]
    public async Task UnclosedAngleInConstructorInitializerTerminates()
    {
        // `F <` never closes: SkipAngles returned -1, the scan jumped back to token 0 and looped forever
        var doc = await IndexWithin("{ } ; operator else ( ) : F < { }\nint after;\n");
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("after.", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task RecursiveIncludesWithoutGuardsAreBounded()
    {
        // each level includes itself twice: 2^100 file entries without the per-TU budget
        var dir = Directory.CreateTempSubdirectory("scip-clang-pp-rec").FullName;
        File.WriteAllText(Path.Combine(dir, "h.h"), "#include \"h.h\"\n#include \"h.h\"\n");
        var main = Path.Combine(dir, "t.cc");
        File.WriteAllText(main, "#include \"h.h\"\nint x;\n");
        var pp = new Preprocessor(new FileTable(dir), [], []);
        var run = Task.Run(() => pp.Run(main, []));
        await run.WaitAsync(Limit); // TimeoutException = hang
        await Assert.That(pp.IncludeBudgetExceeded).IsTrue();
        await Assert.That(string.Concat(pp.Output.Select(static t => t.Text))).IsEqualTo("intx;");
    }

    [Test]
    public async Task NestedMacroArgumentsAreExpandedOncePerParameter()
    {
        // MAX uses each parameter twice; pre-expanding the argument per use made this 4^n instead of 2^n
        var src = "#define MAX(a,b) ((a)>(b)?(a):(b))\nint x = " + string.Concat(Enumerable.Repeat("MAX(", 14)) + "1" + string.Concat(Enumerable.Repeat(",2)", 14)) + ";\n";
        var dir = Directory.CreateTempSubdirectory("scip-clang-pp-max").FullName;
        var main = Path.Combine(dir, "t.cc");
        File.WriteAllText(main, src);
        var pp = new Preprocessor(new FileTable(dir), [], []);
        var run = Task.Run(() => pp.Run(main, []));
        await run.WaitAsync(Limit); // TimeoutException = hang
        await Assert.That(pp.Output.Count(static t => t.Text == "1")).IsEqualTo(1 << 14);
    }

    [Test]
    public async Task LongChainsAndListsStayLinear()
    {
        // else-if chain (one nested scope per branch, walked by every lookup), comma list of class-typed values
        // (backward scan per comma), `x.get()` on an unknown receiver with thousands of `get` members, `A < i`
        // comparisons in an initializer (each scanned for a closing '>'). Before the fixes ~8 s, now well under 1 s.
        const int n = 8000;
        var s = new StringBuilder("struct S { S operator,(S); };\nenum { A = 1 };\n");
        for (var i = 0; i < n; i++)
        {
            s.Append($"struct C{i} {{ int get(); }};\n");
        }

        s.Append("int f(int x, S a) {\n  if (x == 0) return 0;\n");
        for (var i = 1; i < n; i++)
        {
            s.Append($"  else if (x == {i}) return {i};\n");
        }

        s.Append("  g(").AppendJoin(", ", Enumerable.Repeat("a", n)).Append(");\n");
        s.Append("  int t[] = {").AppendJoin(", ", Enumerable.Range(0, n).Select(static i => $"A < {i}")).Append("};\n");
        s.Append("  ").Append(string.Concat(Enumerable.Repeat("y.get(); ", n))).Append("\n  return -1;\n}\n");
        var doc = await IndexWithin(s.ToString());
        await Assert.That(doc.Occurrences.Count).IsGreaterThan(3 * n);
    }
}
