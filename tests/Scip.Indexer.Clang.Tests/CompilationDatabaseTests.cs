using Scip.Indexer.Clang;

namespace Scip.Indexer.Clang.Tests;

// Ported from .reference/scip-clang/test/compdb (simple.json / simple-2..4.snapshot.yaml).
public class CompilationDatabaseTests
{
    [Test]
    public async Task ParsesCommandAndArgumentsForms()
    {
        var cmds = CompilationDatabase.Load(Path.Combine(AppContext.BaseDirectory, "Data", "compdb_simple.json"));
        await Assert.That(cmds.Count).IsEqualTo(3);
        await Assert.That(cmds[0].Arguments).IsEquivalentTo(new[] { "arg1", "arg2", "-X=arg3" });
        await Assert.That(cmds[0].File).IsEqualTo("f1");
        await Assert.That(cmds[0].Directory).IsEqualTo("d1");
        await Assert.That(cmds[1].Arguments).IsEquivalentTo(new[] { "arg1", "arg2", "-X=\"arg3\"" });
        await Assert.That(cmds[2].Arguments).IsEquivalentTo(new[] { "-Y=dq\"dq", "-Z=blah", "-W=ww" });
    }

    [Test]
    public async Task CleanerStripsOutputAndDependencyFlags()
    {
        var cleaned = CommandLineCleaner.Clean(
            ["gcc", "-c", "-o", "out.o", "-MD", "-MF", "out.d", "-march=native", "-mfix-cortex-a53-843419", "-Iinc", "-DX=1", "a.cc"],
            "a.cc", "/work");
        await Assert.That(cleaned).IsEquivalentTo(new[] { "-Iinc", "-DX=1" });
        await Assert.That(CommandLineCleaner.IncludeDirs(cleaned, "/work")).IsEquivalentTo(new[] { "/work/inc" });
    }
}
