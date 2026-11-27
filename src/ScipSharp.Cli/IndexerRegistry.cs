using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Clang;
using ScipSharp.Indexer.Dart;
using ScipSharp.Indexer.DotNet;
using ScipSharp.Indexer.Java;
using ScipSharp.Indexer.Php;
using ScipSharp.Indexer.Python;
using ScipSharp.Indexer.Ruby;
using ScipSharp.Indexer.Rust;
using ScipSharp.Indexer.TypeScript;
using ScipSharp.Indexing;

namespace ScipSharp.Cli;

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
        new AbapIndexer(),
    ];
}
