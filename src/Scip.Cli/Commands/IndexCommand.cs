using System.CommandLine;
using Scip.Indexing;

namespace Scip.Cli.Commands;

/// <summary><c>scip index &lt;lang&gt;</c>: entry points of the per-language indexers.</summary>
internal static class IndexCommand
{
    public static Command Create(IEnumerable<IIndexer> indexers)
    {
        var index = new Command("index", "Index a project using a per-language indexer");
        foreach (var indexer in indexers)
        {
            var root = new Option<DirectoryInfo>("--project-root")
            {
                Description = "Project directory",
                DefaultValueFactory = _ => new DirectoryInfo("."),
            };
            var output = new Option<string>("--output")
            {
                Description = "Output file",
                DefaultValueFactory = _ => IndexerOptions.DefaultOutputPath,
            };
            var sub = new Command(indexer.Name, $"{indexer.Languages} (port of {indexer.UpstreamProject})") { root, output };
            // Indexer-specific options (e.g. --jobs, --project-name) are parsed by the indexer itself.
            sub.TreatUnmatchedTokensAsErrors = false;
            sub.SetAction((parse, ct) => indexer.IndexAsync(
                new IndexerOptions(parse.GetRequiredValue(root).FullName, parse.GetRequiredValue(output), parse.UnmatchedTokens),
                ct));
            index.Subcommands.Add(sub);
        }

        return index;
    }
}
