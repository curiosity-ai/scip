using System.Diagnostics;
using ScipSharp.Indexer.Java.Project;
using ScipSharp.Indexer.Java.Syntax;

namespace ScipSharp.Indexer.Java.Tests;

/// <summary>Pathological inputs that used to overflow the stack, hang or take quadratic time.</summary>
public class RobustnessTests
{
    private static string Repeat(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    private static string Method(string expr) =>
        "package p;\npublic class Main {\n  int a; static int f(Object x) { return 0; }\n  Object m() {\n    Object x = " + expr + ";\n    return x;\n  }\n}\n";

    private sealed class Collecting : AggregatorReporter
    {
        public List<string> Messages { get; } = [];

        public override void Warning(string message) => Messages.Add("warning: " + message);

        public override void ReportError(string message)
        {
            Messages.Add("error: " + message);
            base.ReportError(message);
        }
    }

    private static async Task<(int Exit, Index Index, List<string> Log)> IndexAsync(string dir)
    {
        var output = Path.Combine(dir, "index.scip");
        var reporter = new Collecting();
        var project = new ProjectDiscovery(new ArtifactLocator()).Discover(dir);
        var rc = await NativeProjectIndexer.RunAsync(project, output, null, [], reporter, CancellationToken.None);
        var index = rc == 0 ? Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output)) : new Index();
        return (rc, index, reporter.Messages);
    }

    private static async Task<(int Exit, Index Index, List<string> Log)> IndexSourcesAsync(params (string Path, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("scipjava-robust").FullName;
        try
        {
            foreach (var (path, text) in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);
                await File.WriteAllTextAsync(Path.Combine(dir, path), text);
            }
            return await IndexAsync(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    [Arguments("plus")]
    [Arguments("calls")]
    public async Task LongChainsAreIndexed(string kind)
    {
        // Parsed with loops, but the tree is as deep as the chain: the evaluator must not overflow on it.
        var expr = kind == "plus" ? string.Join(" + ", Enumerable.Repeat("a", 50_000)) : "new StringBuilder()" + Repeat(".append(1)", 20_000);
        var (exit, index, log) = await IndexSourcesAsync(("p/Main.java", Method(expr)));
        await Assert.That(exit).IsEqualTo(0).Because(string.Join('\n', log));
        await Assert.That(log).IsEmpty();
        await Assert.That(index.Documents.Single().Occurrences.Any(o => o.Symbol.EndsWith("p/Main#m().", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Arguments("parens")]
    [Arguments("lambdas")]
    [Arguments("elseif")]
    [Arguments("generics")]
    public async Task DeeplyNestedFileIsSkippedInsteadOfOverflowing(string kind)
    {
        const int n = 100_000;
        var deep = kind switch
        {
            "parens" => Method(Repeat("(", n) + "1" + Repeat(")", n)),
            "lambdas" => Method(Repeat("(Runnable) () -> f(", n) + "1" + Repeat(")", n)),
            "elseif" => "package p;\nclass Main { int f(int x) {\n" + Repeat(" if (x == 1) { return 1; } else\n", n) + " { return 0; }\n} }\n",
            _ => "package p;\nclass Main { " + Repeat("java.util.List<", n) + "String" + Repeat(">", n) + " x; }\n",
        };
        var (exit, index, log) = await IndexSourcesAsync(("p/Main.java", deep), ("p/Other.java", "package p;\nclass Other {}\n"));
        await Assert.That(exit).IsEqualTo(0).Because(string.Join('\n', log));
        await Assert.That(log.Any(m => m.Contains("Main.java: cannot parse (nested too deeply)", StringComparison.Ordinal))).IsTrue();
        await Assert.That(index.Documents.Select(d => d.RelativePath)).IsEquivalentTo(["p/Other.java"]);
    }

    [Test]
    public async Task LessThanChainParsesInLinearTime()
    {
        // Every identifier followed by `<` speculatively parses a generic type; failures are now remembered.
        var sw = Stopwatch.StartNew();
        Parser.ParseFile(Method(string.Join(" < ", Enumerable.Repeat("a", 3000))));
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task LargeClassesAreIndexedWithoutQuadraticLookups()
    {
        // Method/field lookups and overload disambiguators used to scan the whole member list per use.
        const int n = 20_000;
        var body = string.Concat(Enumerable.Range(0, n).Select(i => $"  int m{i}(int x) {{ return m{Math.Max(i - 1, 0)}(x) + f{i}; }}\n  int f{i};\n"));
        var sw = Stopwatch.StartNew();
        var (exit, index, log) = await IndexSourcesAsync(("p/Main.java", "package p;\npublic class Main {\n" + body + "}\n"));
        await Assert.That(exit).IsEqualTo(0).Because(string.Join('\n', log));
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
        var refs = index.Documents.Single().Occurrences.Where(o => (o.SymbolRoles & 1) == 0).Select(o => o.Symbol).ToHashSet();
        await Assert.That(refs).Contains("semanticdb maven . . p/Main#m19998().");
        await Assert.That(refs).Contains("semanticdb maven . . p/Main#f19999.");
    }

    [Test]
    public async Task SymlinkCyclesDoNotHangFileDiscovery()
    {
        var dir = Directory.CreateTempSubdirectory("scipjava-symlink").FullName;
        var links = new[] { Path.Combine(dir, "p", "up"), Path.Combine(dir, "p", "self") };
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "p"));
            await File.WriteAllTextAsync(Path.Combine(dir, "p", "A.java"), "package p;\nclass A {}\n");
            try
            {
                Directory.CreateSymbolicLink(links[0], "..");
                Directory.CreateSymbolicLink(links[1], ".");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return; // symbolic links not available here
            }
            await Assert.That(ProjectDiscovery.EnumerateJavaFiles(dir).Select(f => Path.GetFileName(f))).IsEquivalentTo(["A.java"]);
        }
        finally
        {
            foreach (var link in links) if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(dir, recursive: true);
        }
    }
}
