using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Dart;

/// <summary>
/// SCIP indexer for Dart, ported from <c>scip-dart</c>. See the project README for the strategy: declarations,
/// symbols, relationships and a conservative set of references are produced natively (no Dart SDK needed);
/// <c>--use-upstream</c> delegates to the real analyzer-based <c>scip_dart</c> executable instead.
/// </summary>
public sealed class DartIndexer : IIndexer
{
    /// <summary>Version reported in the index metadata (upstream <c>pubspec.yaml</c> version).</summary>
    public const string ToolVersion = "1.8.1";

    public string Name => "dart";

    public string Languages => "Dart";

    public string UpstreamProject => "scip-dart";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var indexPubspec = false;
        var verbose = false;
        foreach (var arg in options.Arguments)
        {
            switch (arg)
            {
                case "--index-pubspec": indexPubspec = true; break;
                case "--verbose" or "-v": verbose = true; break;
                case "--version":
                    Console.WriteLine(ToolVersion);
                    return 0;
                case "--index-relationships" or "--performance" or "--perf":
                    break; // accepted for compatibility with upstream; no effect
                default:
                    Console.Error.WriteLine($"WARN: ignoring unknown argument '{arg}'");
                    break;
            }
        }

        var root = Path.GetFullPath(options.ProjectRoot);
        if (!File.Exists(Path.Combine(root, "pubspec.yaml")))
        {
            Console.Error.WriteLine("ERROR: Unable to locate pubspec.yaml");
            return 1;
        }

        DartIndex index;
        try
        {
            index = Build(root, indexPubspec, verbose ? Console.Error.WriteLine : null);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or IOException)
        {
            Console.Error.WriteLine($"ERROR: {e.Message}");
            return 1;
        }

        if (options.OutputPath == "-")
        {
            using var stdout = Console.OpenStandardOutput();
            Write(index, stdout);
        }
        else
        {
            await using var file = File.Create(options.OutputPath);
            Write(index, file);
        }

        return 0;
    }

    /// <summary>Indexes the Dart package rooted at <paramref name="root"/> in memory.</summary>
    public static DartIndex Build(string root, bool indexPubspec = false, Action<string>? log = null) =>
        new ProjectIndexer(Path.GetFullPath(root), log).Build(indexPubspec);

    /// <summary>Streams <paramref name="index"/> with <see cref="IndexWriter"/>.</summary>
    public static void Write(DartIndex index, Stream output)
    {
        using var writer = new IndexWriter(output, leaveOpen: true);
        writer.WriteMetadata(index.Metadata);
        foreach (var document in index.Documents)
        {
            writer.WriteDocument(document);
        }

        foreach (var symbol in index.ExternalSymbols)
        {
            writer.WriteExternalSymbol(symbol);
        }
    }
}

/// <summary>An in-memory SCIP index produced by <see cref="DartIndexer.Build"/>.</summary>
public sealed record DartIndex(Metadata Metadata, IReadOnlyList<Document> Documents, IReadOnlyList<SymbolInformation> ExternalSymbols);
