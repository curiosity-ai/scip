using System.CommandLine;
using Google.Protobuf;
using Scip.Indexing;

namespace Scip.Cli;

internal static class CliApp
{
    public static RootCommand Build()
    {
        var root = new RootCommand("SCIP Code Intelligence Protocol CLI");
        root.Subcommands.Add(BuildPrint());
        root.Subcommands.Add(BuildIndex());
        return root;
    }

    private static Command BuildPrint()
    {
        var path = new Argument<FileInfo>("path") { Description = "Path to a SCIP index file" };
        var cmd = new Command("print", "Print a human-readable (JSON) view of a SCIP index") { path };
        cmd.SetAction(parse =>
        {
            var file = parse.GetRequiredValue(path);
            using var stream = file.OpenRead();
            var index = Index.Parser.ParseFrom(stream);
            Console.WriteLine(JsonFormatter.Default.Format(index));
            return 0;
        });
        return cmd;
    }

    private static Command BuildIndex()
    {
        var index = new Command("index", "Index a project using a per-language indexer");
        foreach (var indexer in IndexerRegistry.All)
        {
            var root = new Option<DirectoryInfo>("--project-root") { Description = "Project directory", DefaultValueFactory = _ => new DirectoryInfo(".") };
            var output = new Option<string>("--output") { Description = "Output file", DefaultValueFactory = _ => IndexerOptions.DefaultOutputPath };
            var sub = new Command(indexer.Name, $"{indexer.Languages} (port of {indexer.UpstreamProject})") { root, output };
            sub.SetAction((parse, ct) => indexer.IndexAsync(
                new IndexerOptions(parse.GetRequiredValue(root).FullName, parse.GetRequiredValue(output), parse.UnmatchedTokens),
                ct));
            index.Subcommands.Add(sub);
        }

        return index;
    }
}
