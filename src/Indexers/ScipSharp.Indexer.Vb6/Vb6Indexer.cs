using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Vb6;

/// <summary>
/// SCIP indexer for Visual Basic 6 and VBA (native C#; there is no upstream scip-vb6). See README.md in this project.
/// </summary>
public sealed class Vb6Indexer : IIndexer
{
    public string Name => "vb6";

    public string Languages => "Visual Basic 6 / VBA";

    public string UpstreamProject => "(none: native implementation)";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        Vb6Options parsed;
        try
        {
            parsed = Vb6Options.Parse(options.Arguments);
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine($"error: {e.Message}\n\n{Vb6Options.Usage}");
            return 1;
        }

        if (parsed.ShowHelp)
        {
            Console.Out.WriteLine(Vb6Options.Usage);
            return 0;
        }

        var root = Path.GetFullPath(options.ProjectRoot);
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"error: project root '{root}' does not exist");
            return 1;
        }

        try
        {
            var indexer = new Vb6ProjectIndexer(root, parsed, Console.Error.WriteLine, options.Arguments);
            await using var stream = File.Create(options.OutputPath);
            using var writer = new IndexWriter(stream, leaveOpen: true);
            var external = indexer.Run(
                doc =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.WriteDocument(doc);
                },
                writer.WriteMetadata);
            foreach (var symbol in external)
            {
                writer.WriteExternalSymbol(symbol);
            }

            return 0;
        }
        catch (IOException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }
}
