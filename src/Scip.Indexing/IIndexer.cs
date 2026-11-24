namespace Scip.Indexing;

/// <summary>Options common to every language indexer.</summary>
/// <param name="ProjectRoot">Directory of the project to index.</param>
/// <param name="OutputPath">Where to write the SCIP index (conventionally <c>index.scip</c>).</param>
/// <param name="Arguments">Language-specific arguments passed through from the CLI.</param>
public sealed record IndexerOptions(string ProjectRoot, string OutputPath, IReadOnlyList<string> Arguments)
{
    public static string DefaultOutputPath => "index.scip";
}

/// <summary>A per-language SCIP indexer (the C# port of one of the upstream <c>scip-*</c> projects).</summary>
public interface IIndexer
{
    /// <summary>CLI name, e.g. <c>typescript</c>.</summary>
    string Name { get; }

    /// <summary>Human readable languages handled by this indexer.</summary>
    string Languages { get; }

    /// <summary>Upstream project this indexer is ported from (a folder under <c>.reference/</c>).</summary>
    string UpstreamProject { get; }

    /// <summary>Runs the indexer. Returns a process exit code.</summary>
    Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken);
}
