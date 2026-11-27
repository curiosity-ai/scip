using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Sql;

/// <summary>
/// Native C# SCIP indexer for SQL (ANSI, PostgreSQL/PL/pgSQL, T-SQL, MySQL/MariaDB, SQLite, Oracle PL/SQL). No database
/// server or external tool is needed. See README.md in this project for the symbol scheme, scope and known gaps.
/// </summary>
public sealed class SqlIndexer : IIndexer
{
    private const string Usage = """
        usage: scip index sql [options]

        scip-sql indexes the .sql files below the project root as one catalog (native C# indexer).

        Options:
          -h --help                    display this help and exit
             --dialect=<d>             ansi | postgres | tsql | mysql | sqlite | plsql (default: auto-detect per file)
             --default-schema=<s>      schema of unqualified names (default: public / dbo / main / default)
             --package-name=<name>     package name in symbols (default: the project directory name)
             --package-version=<v>     package version in symbols (default: empty)
        """;

    public string Name => "sql";

    public string Languages => "SQL (ANSI, PostgreSQL, T-SQL, MySQL, SQLite, PL/SQL)";

    public string UpstreamProject => "(none: native implementation)";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var opts = new SqlIndexerOptions { Arguments = options.Arguments };
        foreach (var arg in options.Arguments)
        {
            if (arg is "-h" or "--help")
            {
                Console.Out.WriteLine(Usage);
                return 0;
            }

            if (Value(arg, "--dialect") is { } d)
            {
                if (!Dialects.TryParse(d, out var dialect))
                {
                    Console.Error.WriteLine($"error: unknown dialect '{d}'\n{Usage}");
                    return 1;
                }

                opts = opts with { Dialect = dialect };
            }
            else if (Value(arg, "--default-schema") is { } s)
            {
                opts = opts with { DefaultSchema = s };
            }
            else if (Value(arg, "--package-name") is { } n)
            {
                opts = opts with { PackageName = n };
            }
            else if (Value(arg, "--package-version") is { } v)
            {
                opts = opts with { PackageVersion = v };
            }
            else
            {
                Console.Error.WriteLine($"error: unknown option '{arg}'\n{Usage}");
                return 1;
            }
        }

        var root = Path.GetFullPath(options.ProjectRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!Directory.Exists(root) && !File.Exists(root))
        {
            Console.Error.WriteLine($"error: project root not found: {root}");
            return 1;
        }

        var indexer = new SqlProjectIndexer(root, opts, Console.Error.WriteLine);
        await using var stream = File.Create(options.OutputPath);
        using var writer = new IndexWriter(stream, leaveOpen: true);
        var count = 0;
        var external = indexer.Run(
            doc =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteDocument(doc);
                count++;
            },
            writer.WriteMetadata);
        foreach (var s in external)
        {
            writer.WriteExternalSymbol(s);
        }

        Console.Error.WriteLine($"scip-sql: indexed {count} file(s), {external.Count} unresolved symbol(s)");
        return 0;
    }

    private static string? Value(string arg, string name) =>
        arg.StartsWith(name + "=", StringComparison.Ordinal) ? arg[(name.Length + 1)..] : null;
}
