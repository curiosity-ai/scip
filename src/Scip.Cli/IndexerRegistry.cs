using Scip.Indexer.Clang;
using Scip.Indexer.Dart;
using Scip.Indexer.DotNet;
using Scip.Indexer.Java;
using Scip.Indexer.Php;
using Scip.Indexer.Python;
using Scip.Indexer.Ruby;
using Scip.Indexer.Rust;
using Scip.Indexer.TypeScript;
using Scip.Indexing;

namespace Scip.Cli;

/// <summary>All per-language indexers known to the CLI.</summary>
internal static class IndexerRegistry
{
    public static IReadOnlyList<IIndexer> All { get; } =
    [
        new JavaIndexer(),
        new TypeScriptIndexer(),
        new RustIndexer(),
        new ClangIndexer(),
        new RubyIndexer(),
        new PythonIndexer(),
        new DotNetIndexer(),
        new DartIndexer(),
        new PhpIndexer(),
    ];
}
