using System.Diagnostics;
using Scip.Indexing;

namespace Scip.Indexer.Rust;

/// <summary>
/// Rust indexer (port of the SCIP emission of <c>rust-analyzer scip</c>), fully native: Cargo.toml reader plus a
/// token-level scanner. No external toolchain is run. See README.md.
/// </summary>
public sealed class RustIndexer() : IIndexer
{
    public string Name => "rust";

    public string Languages => "Rust";

    public string UpstreamProject => "rust-analyzer";

    public Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken) =>
        Task.FromResult(Run(options, Console.Error, cancellationToken));

    internal static int Run(IndexerOptions options, TextWriter log, CancellationToken cancellationToken)
    {
        var settings = RustIndexerSettings.Parse(options.Arguments);
        var root = Path.GetFullPath(options.ProjectRoot);
        var output = Path.GetFullPath(options.OutputPath);
        if (!Directory.Exists(root))
        {
            log.WriteLine($"Project root does not exist: {root}");
            return 1;
        }

        log.WriteLine("Generating SCIP start...");
        var watch = Stopwatch.StartNew();
        var temp = output + ".tmp";
        try
        {
            using (var stream = File.Create(temp))
            {
                if (!RunNative(settings, root, stream, log))
                {
                    return 1;
                }
            }

            File.Move(temp, output, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }

        log.WriteLine($"Generating SCIP finished {watch.Elapsed}");
        return 0;
    }

    private static string ProjectRootUri(string root) => new Uri(root.TrimEnd('/') + "/").ToString().TrimEnd('/');

    private static bool RunNative(RustIndexerSettings settings, string root, Stream output, TextWriter log)
    {
        var packages = CargoWorkspace.Load(root);
        if (packages.Count == 0)
        {
            log.WriteLine($"No Cargo.toml found in {root}.");
            return false;
        }

        HashSet<string> seen = [];
        List<Document> documents = [];
        foreach (var package in packages)
        {
            foreach (var target in package.Targets)
            {
                var scanner = new RustCrateScanner(package, target, seen);
                scanner.Scan();
                foreach (var file in scanner.Files)
                {
                    var relative = Path.GetRelativePath(root, file.Path).Replace('\\', '/');
                    if (relative.StartsWith("../", StringComparison.Ordinal))
                    {
                        continue; // outside the project root: not part of this index
                    }

                    documents.Add(RustIndexAssembler.ToDocument(file, relative));
                }
            }
        }

        var metadata = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ToolInfo = new ToolInfo { Name = "scip-rust", Version = typeof(RustIndexer).Assembly.GetName().Version?.ToString() ?? "" },
            ProjectRoot = ProjectRootUri(root),
            TextDocumentEncoding = TextEncoding.Utf8,
        };
        metadata.ToolInfo.Arguments.AddRange(settings.Raw);
        RustIndexAssembler.Write(output, metadata, documents, [], log);
        return true;
    }
}

/// <summary>Arguments understood by the Rust indexer (currently none are required; kept for CLI compatibility).</summary>
internal sealed class RustIndexerSettings
{
    public IReadOnlyList<string> Raw { get; private set; } = [];

    public static RustIndexerSettings Parse(IReadOnlyList<string> args) => new() { Raw = args };
}
