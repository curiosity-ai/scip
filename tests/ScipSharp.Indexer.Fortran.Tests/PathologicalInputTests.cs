using System.Text;

namespace ScipSharp.Indexer.Fortran.Tests;

/// <summary>Inputs that used to hang or crash the indexer (exponential walks, quadratic lookups, stack overflows).</summary>
public class PathologicalInputTests
{
    [Test]
    [Timeout(30_000)]
    public async Task DiamondUseGraphIsWalkedLinearly(CancellationToken ct)
    {
        // 14 layers of 4 modules, each using all 4 of the layer below: 4^14 USE paths. A lookup that misses
        // everywhere (sin, an implicit variable) used to walk every path.
        var sb = new StringBuilder();
        const int Layers = 14, Width = 4;
        for (var s = 0; s < Width; s++)
        {
            sb.Append($"module m0_{s}\n integer :: base{s}\nend module\n");
        }

        for (var k = 1; k <= Layers; k++)
        {
            for (var s = 0; s < Width; s++)
            {
                sb.Append($"module m{k}_{s}\n");
                for (var u = 0; u < Width; u++)
                {
                    sb.Append($" use m{k - 1}_{u}\n");
                }

                sb.Append("end module\n");
            }
        }

        sb.Append($"program p\n use m{Layers}_0\n y = sin(y) + base3\nend program\n");
        var p = TestSupport.Build("m.f90", sb.ToString());
        await Assert.That(p.Sym("m.f90", "y = sin", "base3")).IsNotNull();
    }

    [Test]
    [Timeout(30_000)]
    public async Task DiamondIncludeGraphIsBounded(CancellationToken ct)
    {
        // Every header includes all 3 headers of the level below: 3^15 expansions without a budget.
        var files = new Dictionary<string, string>();
        const int Depth = 15, Width = 3;
        for (var k = 0; k <= Depth; k++)
        {
            for (var s = 0; s < Width; s++)
            {
                files[$"i{k}_{s}.inc"] = k == 0
                    ? $"      integer v{s}\n"
                    : string.Concat(Enumerable.Range(0, Width).Select(u => $"#include \"i{k - 1}_{u}.inc\"\n"));
            }
        }

        files["p.F90"] = $"program p\n#include \"i{Depth}_0.inc\"\n v0 = 1\nend program\n";
        var p = TestSupport.Build(files);
        await Assert.That(p.Index.Documents.Any(static d => d.RelativePath == "i0_0.inc")).IsTrue();
    }

    [Test]
    [Timeout(30_000)]
    public async Task MissingIncludesInManyFilesAreResolvedOnce(CancellationToken ct)
    {
        var files = new Dictionary<string, string>();
        for (var i = 0; i < 1500; i++)
        {
            files[$"f{i}.f90"] = $"subroutine s{i}\n include 'mpif.h'\nend subroutine\n";
        }

        var p = TestSupport.Build(files);
        await Assert.That(p.Index.Documents.Count).IsEqualTo(1500);
    }

    [Test]
    [Timeout(30_000)]
    public async Task DeepLogicalIfChainDoesNotOverflowTheStack(CancellationToken ct)
    {
        // Run on a small stack (like a thread-pool worker) so the guard, not the stack size, is what is tested.
        Indexed? p = null;
        var source = "program p\n x = 1\n " + string.Concat(Enumerable.Repeat("if (x > 0) ", 20_000)) + "x = 2\n"
            + "#if " + new string('(', 50_000) + "1" + new string(')', 50_000) + "\n y = 3\n#endif\nend program\n";
        var thread = new Thread(() => p = TestSupport.Build("a.F90", source), 1024 * 1024);
        thread.Start();
        thread.Join();
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.Index.Documents.Count).IsEqualTo(1);
    }
}
