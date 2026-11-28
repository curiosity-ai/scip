using System.Text;

namespace ScipSharp.Indexer.Dart.Tests;

/// <summary>Inputs that used to crash the indexer, overflow the stack or make it quadratic.</summary>
public class PathologicalInputTests
{
    private static Document Index(string source)
    {
        var root = Fixtures.TempPackage("demo", new() { ["lib/a.dart"] = source });
        return DartIndexer.Build(root).Documents.Single(d => d.RelativePath == "lib/a.dart");
    }

    [Test]
    public async Task LeadingMemberAccessDoesNotCrash()
    {
        // `.x` as the first tokens: the missing receiver used to be looked up as a null name.
        var doc = Index(".x;\nclass A {}\n");
        await Assert.That(doc.Symbols.Any(s => s.Symbol.EndsWith("A#", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Timeout(20_000)]
    public async Task ClassWithThousandsOfMembersIsLinear(CancellationToken ct)
    {
        // Generated bindings: one class, thousands of members referencing each other (member lookups were linear).
        const int N = 3000;
        var sb = new StringBuilder("class Big {\n");
        for (var i = 0; i < N; i++)
        {
            sb.Append($"  int f{i} = {i};\n  int g{i}(int a) {{ final y = f{i} + a; return g{(i + 1) % N}(y) + f{(i + 3) % N}; }}\n");
        }

        sb.Append("}\n");
        var doc = Index(sb.ToString());
        await Assert.That(doc.Symbols.Count(s => s.Symbol.Contains("Big#g", StringComparison.Ordinal))).IsEqualTo(N);
    }

    [Test]
    [Timeout(20_000)]
    public async Task LongMemberChainsAndClosureListsAreLinear(CancellationToken ct)
    {
        // Every member of `.toString().trim()...` used to re-walk the rest of the chain; every closure of the list
        // re-scanned the block up to itself.
        var source = "void main() {\n  final sb = StringBuffer();\n  final y = sb" + string.Concat(Enumerable.Repeat(".toString().trim()", 3000)) + ";\n"
            + "  final fs = [" + string.Join(", ", Enumerable.Range(0, 10_000).Select(i => $"(a) => a + {i}")) + "];\n}\n";
        var doc = Index(source);
        await Assert.That(doc.Occurrences.Count(o => o.Symbol.Contains("String#trim", StringComparison.Ordinal))).IsEqualTo(3000);
    }

    [Test]
    public async Task DeeplyNestedInterpolationDoesNotOverflowTheStack()
    {
        var source = "void main() {\n  var a = 1;\n  print('" + string.Concat(Enumerable.Repeat("${'", 20_000)) + "a"
            + string.Concat(Enumerable.Repeat("'}", 20_000)) + "');\n}\nclass After {}\n";
        Document? doc = null;
        var thread = new Thread(() => doc = Index(source), 1024 * 1024); // a thread-pool sized stack
        thread.Start();
        thread.Join();
        await Assert.That(doc).IsNotNull();
    }
}
