using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Abap;

/// <summary>
/// Native C# SCIP indexer for ABAP: indexes a folder of abapGit sources without an SAP system or any external tool.
/// Built on a C# port of the abaplint parser and syntax check (see README.md in this project).
/// </summary>
public sealed class AbapIndexer : IIndexer
{
    private const string Usage = """
        usage: scip index abap [options]

        scip-abap is a SCIP indexer for ABAP sources in abapGit layout (native C# port of abaplint).

        Options:
          -h --help                    display this help and exit
             --release=<name>          ABAP release for parsing, e.g. v740sp08, v757, Cloud (default: from abaplint.json, else the newest)
             --config=<path>           abaplint.json to read (default: abaplint.json in the project root, if any)
             --project-name=<name>     package name of the symbols (default: name of the project folder)
             --project-version=<v>     package version of the symbols (default: .)
        """;

    public string Name => "abap";

    public string Languages => "ABAP";

    public string UpstreamProject => "abaplint";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var settings = new AbapIndexerSettings(Arguments: options.Arguments);
        foreach (var arg in options.Arguments)
        {
            if (arg is "-h" or "--help")
            {
                Console.Out.WriteLine(Usage);
                return 0;
            }

            settings = arg switch
            {
                _ when Value(arg, "--release") is { } v => settings with { Release = v },
                _ when Value(arg, "--config") is { } v => settings with { ConfigPath = Path.GetFullPath(v) },
                _ when Value(arg, "--project-name") is { } v => settings with { PackageName = v },
                _ when Value(arg, "--project-version") is { } v => settings with { PackageVersion = v },
                _ => settings,
            };
        }

        try
        {
            var indexer = new AbapProjectIndexer(options.ProjectRoot, settings, Console.Error.WriteLine);
            await using var stream = File.Create(options.OutputPath);
            using var writer = new IndexWriter(stream, leaveOpen: true);
            writer.WriteMetadata(indexer.CreateMetadata());
            var count = 0;
            foreach (var external in indexer.Run(
                         doc =>
                         {
                             writer.WriteDocument(doc);
                             count++;
                         },
                         cancellationToken))
            {
                writer.WriteExternalSymbol(external);
            }

            Console.Error.WriteLine($"scip-abap: {count} documents indexed");
            return 0;
        }
        catch (InvalidOperationException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    private static string? Value(string arg, string option) =>
        arg.StartsWith(option + "=", StringComparison.Ordinal) ? arg[(option.Length + 1)..] : null;
}
