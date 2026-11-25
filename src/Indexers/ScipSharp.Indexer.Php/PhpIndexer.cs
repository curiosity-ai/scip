using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Php;

/// <summary>
/// Native C# port of <c>scip-php</c>: indexes a composer project without PHP, composer or any other external tool.
/// See README.md in this project for scope and known gaps.
/// </summary>
public sealed class PhpIndexer : IIndexer
{
    private const string Usage = """
        usage: scip index php [options]

        scip-php is a SCIP indexer for PHP (native C# port).

        Options:
          -h --help                 display this help and exit
             --php-version=<v>      PHP version attributed to built-in symbols (default: 8.4.0)
             --memory-limit=<v>     accepted for compatibility with upstream; ignored
        """;

    public string Name => "php";

    public string Languages => "PHP";

    public string UpstreamProject => "scip-php";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var phpVersion = PhpProjectIndexer.DefaultPhpVersion;
        foreach (var arg in options.Arguments)
        {
            if (arg is "-h" or "--help")
            {
                Console.Out.WriteLine(Usage);
                return 0;
            }

            if (arg.StartsWith("--php-version=", StringComparison.Ordinal))
            {
                phpVersion = arg["--php-version=".Length..];
            }
        }

        var root = Path.GetFullPath(options.ProjectRoot).TrimEnd(Path.DirectorySeparatorChar);
        try
        {
            var indexer = new PhpProjectIndexer(root, "0.0.1", options.Arguments, phpVersion, Console.Error.WriteLine);
            await using var stream = File.Create(options.OutputPath);
            using var writer = new IndexWriter(stream, leaveOpen: true);
            var (_, ext) = indexer.Run(
                doc =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.WriteDocument(doc);
                },
                writer.WriteMetadata);
            foreach (var s in ext)
            {
                writer.WriteExternalSymbol(s);
            }

            return 0;
        }
        catch (InvalidOperationException e)
        {
            await Console.Error.WriteLineAsync(e.Message);
            return 1;
        }
    }
}
