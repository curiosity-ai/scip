namespace ScipSharp.Indexer.Vb6.Tests;

/// <summary>Inputs that used to crash the indexer or make it quadratic.</summary>
public class PathologicalInputTests
{
    [Test]
    public async Task MalformedConditionalCompilationDoesNotAbortTheRun()
    {
        // Out-of-range literals and an empty &H used to throw OverflowException/ArgumentOutOfRangeException out of the
        // whole run; absurd nesting must not overflow the stack.
        var deep = new string('(', 50_000) + "1" + new string(')', 50_000);
        var bas = "Attribute VB_Name = \"M\"\n#If 99999999999999999999 Then\n#End If\n#If &H Then\n#End If\n#If &HFFFFFFFFFFFFFFFFFFFF Then\n#End If\n"
            + $"#If {deep} Then\n#End If\n#If {string.Concat(Enumerable.Repeat("Not ", 50_000))}0 Then\n#End If\nSub A()\nEnd Sub\n";
        using var dir = TestSupport.Files(("M.bas", bas));
        Index? index = null;
        var thread = new Thread(() => index = TestSupport.Build(dir.Path), 1024 * 1024); // a thread-pool sized stack
        thread.Start();
        thread.Join();
        await Assert.That(index).IsNotNull();
        await Assert.That(index!.Documents.Single(d => d.RelativePath == "M.bas").Symbols.Any(s => s.Symbol.EndsWith("M#A().", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Timeout(30_000)]
    public async Task ManyMiscasedVbpPathsResolveQuickly(CancellationToken ct)
    {
        const int N = 2000;
        var files = new List<(string, string)>();
        var vbp = new List<string> { "Type=Exe", "Name=\"Big\"" };
        for (var i = 0; i < N; i++)
        {
            files.Add(($"src/mod{i}.bas", $"Attribute VB_Name = \"Mod{i}\"\nPublic Sub S{i}()\nEnd Sub\n"));
            vbp.Add($"Module=Mod{i}; SRC\\MOD{i}.BAS");
        }

        files.Add(("big.vbp", string.Join("\r\n", vbp) + "\r\n"));
        using var dir = TestSupport.Files([.. files]);
        var index = TestSupport.Build(dir.Path);
        await Assert.That(index.Documents.Count(d => d.RelativePath.EndsWith(".bas", StringComparison.Ordinal))).IsEqualTo(N);
    }
}
