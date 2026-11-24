namespace Scip.Indexing;

/// <summary>Base for indexers whose port has not started yet; reports so and exits non-zero.</summary>
public abstract class NotImplementedIndexer(string name, string languages, string upstream) : IIndexer
{
    public string Name { get; } = name;

    public string Languages { get; } = languages;

    public string UpstreamProject { get; } = upstream;

    public Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"The '{Name}' indexer ({Languages}) is not ported yet. See TODO.md and .reference/{UpstreamProject}.");
        return Task.FromResult(2);
    }
}
