using System.CommandLine;
using ScipSharp.Indexing;

namespace ScipSharp.Cli.Commands;

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
            // Indexers without an upstream scip-* project report a parenthesised note such as "(none: native implementation)".
            var origin = indexer.UpstreamProject.StartsWith('(') ? "native, no upstream indexer" : $"port of {indexer.UpstreamProject}";
            var sub = new Command(indexer.Name, $"{indexer.Languages} ({origin})") { root, output };
            // Indexer-specific options (e.g. --jobs, --project-name) are parsed by the indexer itself.
            sub.TreatUnmatchedTokensAsErrors = false;
            // On a large stack: deeply nested source must not overflow the parsers (see LargeStackThread).
            sub.SetAction((parse, ct) => LargeStackThread.Run(() => indexer.IndexAsync(
                new IndexerOptions(parse.GetRequiredValue(root).FullName, parse.GetRequiredValue(output), parse.UnmatchedTokens),
                ct)));
            index.Subcommands.Add(sub);
        }

        return index;
    }
}
