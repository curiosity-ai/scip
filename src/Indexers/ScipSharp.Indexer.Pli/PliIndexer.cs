using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Pli;

/// <summary>
/// Native C# SCIP indexer for PL/I: lexes, parses and resolves PL/I sources (with %INCLUDE members) without any PL/I
/// compiler. See README.md in this project for the symbol scheme, options and known gaps.
/// </summary>
public sealed class PliIndexer : IIndexer
{
    public string Name => "pli";

    public string Languages => "PL/I";

    public string UpstreamProject => "(none: native implementation)";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        PliOptions pli;
        try
        {
            pli = PliOptions.Parse(options.Arguments);
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }

        if (pli.Help)
        {
            Console.Out.WriteLine(PliOptions.Usage);
            return 0;
        }

        try
        {
            var indexer = new PliProjectIndexer(options.ProjectRoot, pli, options.Arguments, Console.Error.WriteLine);
            await using var stream = File.Create(options.OutputPath);
            using var writer = new IndexWriter(stream, leaveOpen: true);
            var external = indexer.Run(
                doc =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.WriteDocument(doc);
                },
                writer.WriteMetadata);
            foreach (var s in external)
            {
                writer.WriteExternalSymbol(s);
            }

            return 0;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}
