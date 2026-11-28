using System.Text;

namespace ScipSharp.Indexer.Php.Tests;

/// <summary>Inputs that used to overflow the stack, hang, or take exponential/quadratic time.</summary>
public class PathologicalInputTests
{
    private static (Index Index, List<string> Warnings) Build(params (string Path, string Content)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-php-patho-" + Guid.NewGuid().ToString("N"));
        foreach (var (name, content) in files)
        {
            var p = Path.Combine(dir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, content);
        }

        var warnings = new List<string>();
        var index = new PhpProjectIndexer(dir, "t", [], "8.4.0", warnings.Add).Build();
        return (index, warnings);
    }

    private static string Repeat(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    private const string Ok = "<?php\nclass Ok { public function f(): void { $this->f(); } }\n";

    [Test]
    public async Task DeeplyNestedFilesAreSkippedNotCrashing()
    {
        var (index, warnings) = Build(
            ("parens.php", "<?php\n$x = " + Repeat("(", 50_000) + "1" + Repeat(")", 50_000) + ";\n"),
            ("arrays.php", "<?php\n$x = " + Repeat("[", 50_000) + Repeat("]", 50_000) + ";\n"),
            ("ifs.php", "<?php\n" + Repeat("if ($a) {", 50_000) + Repeat("}", 50_000) + "\n"),
            ("unary.php", "<?php\n$x = " + Repeat("!", 50_000) + "$a;\n"),
            ("interp.php", "<?php\n" + Repeat("$a = \"{$b;\n", 20_000)),
            ("ok.php", Ok));
        await Assert.That(warnings.Count(w => w.Contains("too deep", StringComparison.Ordinal))).IsEqualTo(5);
        await Assert.That(index.Documents.Any(d => d.RelativePath == "ok.php")).IsTrue();
    }

    [Test]
    public async Task LongChainsAreLinear()
    {
        const int n = 10_000;
        var (index, _) = Build(
            ("chain.php", "<?php\nclass A {\n    public A $p;\n    public function m(): A { return $this; }\n    public function f() {\n"
                + "        $this" + Repeat("->m()", n) + ";\n        $this" + Repeat("->p", n) + ";\n        $s = 'x'" + Repeat(" . 'x'", n) + ";\n    }\n}\n"));
        var doc = index.Documents.Single();
        // every link of the chains resolves to the method / property
        await Assert.That(doc.Occurrences.Count(o => o.Symbol.EndsWith("A#m().", StringComparison.Ordinal))).IsEqualTo(n + 1);
        await Assert.That(doc.Occurrences.Count(o => o.Symbol.EndsWith("A#$p.", StringComparison.Ordinal))).IsEqualTo(n + 1);
    }

    [Test]
    public async Task DiamondAndCyclicHierarchiesAreNotExponential()
    {
        var src = new StringBuilder("<?php\ninterface I0 {}\ntrait T0 {}\n");
        for (var i = 1; i < 200; i++)
        {
            src.Append($"interface I{i} extends I{i - 1}, I{Math.Max(i - 2, 0)} {{}}\n");
            src.Append($"trait T{i} {{ use T{i - 1}, T{Math.Max(i - 2, 0)}; }}\n");
        }

        src.Append("class X implements I199 { use T199; public function f() { $this->g(); X::h(); } }\n");
        src.Append("class P extends Q {} class Q extends P { public function f() { $this->g(); } }\n");
        var (index, _) = Build(("h.php", src.ToString()));
        await Assert.That(index.Documents.Count).IsEqualTo(1);
    }

    [Test]
    public async Task LongWhitespaceInDocCommentDoesNotBacktrack()
    {
        // `(\s+)?\*/$` used to be retried at every offset of the run: ~30 s for 200k spaces.
        var (index, _) = Build(("d.php", "<?php\nclass A {\n    /** doc" + new string(' ', 200_000) + "x\n     */\n    public function f() {}\n}\n"));
        var info = index.Documents.Single().Symbols.Single(s => s.Symbol.EndsWith("A#f().", StringComparison.Ordinal));
        await Assert.That(info.Documentation.Any(d => d.Contains("doc", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task UnterminatedHeredocsDoNotRescanTheFile()
    {
        var (index, _) = Build(("h.php", "<?php\n" + Repeat("$a = <<<EOT\nabc\n", 30_000)), ("ok.php", Ok));
        await Assert.That(index.Documents.Any(d => d.RelativePath == "ok.php")).IsTrue();
    }
}
