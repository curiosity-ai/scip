using Scip.Indexer.Clang;
using Scip.Indexing;

namespace Scip.Indexer.Clang.Tests;

/// <summary>Developer aid: set SCIP_DUMP_DIR to a directory with a compile_commands.json to print the occurrences found.</summary>
public class DumpTests
{
    [Test]
    public async Task Dump()
    {
        var dir = Environment.GetEnvironmentVariable("SCIP_DUMP_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), "scip-dump-" + Guid.NewGuid().ToString("N") + ".scip");
        var code = await new ClangIndexer().IndexAsync(new IndexerOptions(dir, output, []), CancellationToken.None);
        var index = Scip.Index.Parser.ParseFrom(File.ReadAllBytes(output));
        foreach (var d in index.Documents)
        {
            Console.WriteLine($"DOC {d.RelativePath}");
            foreach (var o in d.Occurrences.OrderBy(static o => o.SingleLineRange.Line).ThenBy(static o => o.SingleLineRange.StartCharacter))
            {
                Console.WriteLine($"OCC {o.SingleLineRange.Line}:{o.SingleLineRange.StartCharacter}-{o.SingleLineRange.EndCharacter} {((o.SymbolRoles & 1) != 0 ? "def" : "ref")} {o.Symbol}");
            }

            foreach (var si in d.Symbols)
            {
                Console.WriteLine($"SYM {si.Symbol} kind={si.Kind} rel={string.Join(",", si.Relationships.Select(static r => r.Symbol + (r.IsImplementation ? "(impl)" : "") + (r.IsReference ? "(ref)" : "")))} doc={string.Join("|", si.Documentation)}");
            }
        }

        await Assert.That(code).IsEqualTo(0);
    }
}
