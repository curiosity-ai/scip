using Scip.Indexer.Clang;
using Scip.Indexing;

namespace Scip.Indexer.Clang.Tests;

public class IncludeTests
{
    [Test]
    public async Task IndexesHeaderOnceAndEmitsIncludeReferenceWithoutCompdb()
    {
        var dir = Directory.CreateTempSubdirectory("scip-clang-inc").FullName;
        File.WriteAllText(Path.Combine(dir, "h.h"), "#pragma once\ninline int twice(int v) { return v * 2; }\n");
        File.WriteAllText(Path.Combine(dir, "a.cc"), "#include \"h.h\"\nint a() { return twice(1); }\n");
        File.WriteAllText(Path.Combine(dir, "b.cc"), "#include \"h.h\"\nint b() { return twice(2); }\n");
        var output = Path.Combine(dir, "out.scip");

        var code = await new ClangIndexer().IndexAsync(new IndexerOptions(dir, output, ["--jobs", "2"]), CancellationToken.None);
        await Assert.That(code).IsEqualTo(0);

        var index = Scip.Index.Parser.ParseFrom(File.ReadAllBytes(output));
        await Assert.That(index.Documents.Select(d => d.RelativePath).Order()).IsEquivalentTo(new[] { "a.cc", "b.cc", "h.h" });

        var header = index.Documents.Single(d => d.RelativePath == "h.h");
        await Assert.That(header.Occurrences.Count(o => o.Symbol.Contains("twice(", StringComparison.Ordinal) && (o.SymbolRoles & 1) != 0)).IsEqualTo(1);

        var a = index.Documents.Single(d => d.RelativePath == "a.cc");
        await Assert.That(a.Occurrences.Any(o => o.Symbol == "cxx . . $ `<file>/h.h`/" && (o.SymbolRoles & 1) == 0)).IsTrue();
        await Assert.That(a.Occurrences.Any(o => o.Symbol.Contains("twice(", StringComparison.Ordinal) && (o.SymbolRoles & 1) == 0)).IsTrue();
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-clang");
        await Assert.That(index.Metadata.TextDocumentEncoding).IsEqualTo(TextEncoding.Utf8);
    }
}
