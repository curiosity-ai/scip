using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Clang;
using ScipSharp.Indexer.Cobol;
using ScipSharp.Indexer.Dart;
using ScipSharp.Indexer.Delphi;
using ScipSharp.Indexer.DotNet;
using ScipSharp.Indexer.Fortran;
using ScipSharp.Indexer.Java;
using ScipSharp.Indexer.Pascal;
using ScipSharp.Indexer.Php;
using ScipSharp.Indexer.Pli;
using ScipSharp.Indexer.Python;
using ScipSharp.Indexer.Ruby;
using ScipSharp.Indexer.Rust;
using ScipSharp.Indexer.Sql;
using ScipSharp.Indexer.TypeScript;
using ScipSharp.Indexer.Vb6;
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
        new CobolIndexer(),
        new Vb6Indexer(),
        new PliIndexer(),
        new FortranIndexer(),
        new DelphiIndexer(),
        new PascalIndexer(),
        new SqlIndexer(),
        new AbapIndexer(),
    ];
}
