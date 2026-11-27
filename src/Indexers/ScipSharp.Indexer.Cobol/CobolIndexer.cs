using ScipSharp.Indexer.Cobol.Syntax;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Cobol;

/// <summary>
/// Native C# SCIP indexer for COBOL (no compiler or toolchain needed). See README.md in this project for scope,
/// symbol scheme and known gaps.
/// </summary>
public sealed class CobolIndexer : IIndexer
{
    internal const string Usage = """
        usage: scip index cobol [--project-root <dir>] [--output index.scip] [options]

        scip-cobol indexes COBOL programs and copybooks (native C#; no COBOL compiler needed).

        Options:
          -h --help                        display this help and exit
             --source-format=<f>           auto (default), fixed, free or variable
             --copybook-dir=<dir>          copybook search directory, relative to the project root (repeatable,
                                           or a ',' / path-separator separated list). The including file's
                                           directory and every project file matching the copybook name are
                                           searched as well.
             --program-extensions=<list>   program file extensions (default: cbl,cob,cobol,pco,sqb,ccp)
             --copybook-extensions=<list>  copybook file extensions (default: cpy,copy,cpb,cbk,inc,cblcpy)
             --debug-lines                 treat 'D' indicator lines as code (WITH DEBUGGING MODE)
             --no-unused-copybooks         do not index copybooks that no program copies
             --exclude=<dir>               skip a directory (relative to the project root; repeatable)
             --project-name=<name>         package name used in symbols (default: project directory name)
             --project-version=<version>   package version used in symbols (default: 0.0.0)
             --quiet                       do not print warnings (e.g. missing copybooks)
        """;

    public string Name => "cobol";

    public string Languages => "COBOL";

    public string UpstreamProject => "(none: native implementation)";

    /// <summary>Parses indexer-specific arguments; returns null (and an error message) when they are invalid.</summary>
    internal static (CobolIndexerOptions? Options, bool Help, bool Quiet, string? Error) ParseArguments(IReadOnlyList<string> args)
    {
        var options = new CobolIndexerOptions { Arguments = [.. args] };
        var dirs = new List<string>();
        var excludes = new List<string>();
        var quiet = false;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "--help")
            {
                return (null, true, quiet, null);
            }

            string name, value = "";
            var eq = arg.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0)
            {
                name = arg[..eq];
                value = arg[(eq + 1)..];
            }
            else
            {
                name = arg;
            }

            var takesValue = name is "--source-format" or "--copybook-dir" or "--program-extensions" or "--copybook-extensions"
                or "--exclude" or "--project-name" or "--project-version";
            if (takesValue && eq < 0)
            {
                if (i + 1 >= args.Count)
                {
                    return (null, false, quiet, $"missing value for {name}");
                }

                value = args[++i];
            }

            switch (name)
            {
                case "--source-format":
                    if (!Enum.TryParse<SourceFormat>(value, ignoreCase: true, out var format))
                    {
                        return (null, false, quiet, $"unknown source format '{value}' (expected auto, fixed, free or variable)");
                    }

                    options = options with { Format = format };
                    break;
                case "--copybook-dir":
                    dirs.AddRange(SplitList(value, Path.PathSeparator));
                    break;
                case "--program-extensions":
                    options = options with { ProgramExtensions = [.. SplitExtensions(value)] };
                    break;
                case "--copybook-extensions":
                    options = options with { CopybookExtensions = [.. SplitExtensions(value)] };
                    break;
                case "--debug-lines":
                    options = options with { DebugLines = true };
                    break;
                case "--no-unused-copybooks":
                    options = options with { IndexUnusedCopybooks = false };
                    break;
                case "--exclude":
                    excludes.AddRange(SplitList(value));
                    break;
                case "--project-name":
                    options = options with { PackageName = value };
                    break;
                case "--project-version":
                    options = options with { PackageVersion = value };
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                default:
                    return (null, false, quiet, $"unknown option '{arg}' (see --help)");
            }
        }

        return (options with { CopybookDirectories = dirs, Exclude = excludes }, false, quiet, null);
    }

    private static string[] SplitList(string value, char extra = ',') =>
        value.Split([',', extra], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> SplitExtensions(string value) =>
        SplitList(value).Select(e => e.TrimStart('*').TrimStart('.')).Where(e => e.Length > 0);

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var (parsed, help, quiet, error) = ParseArguments(options.Arguments);
        if (help)
        {
            await Console.Out.WriteLineAsync(Usage.AsMemory(), cancellationToken);
            return 0;
        }

        if (parsed is null)
        {
            await Console.Error.WriteLineAsync(("scip-cobol: " + error).AsMemory(), cancellationToken);
            return 2;
        }

        if (!Directory.Exists(options.ProjectRoot))
        {
            await Console.Error.WriteLineAsync(($"scip-cobol: project root not found: {options.ProjectRoot}").AsMemory(), cancellationToken);
            return 1;
        }

        var indexer = new CobolProjectIndexer(options.ProjectRoot, parsed, quiet ? null : m => Console.Error.WriteLine("scip-cobol: " + m));
        var documents = 0;
        await using (var stream = File.Create(options.OutputPath))
        {
            using var writer = new IndexWriter(stream, leaveOpen: true);
            var external = indexer.Run(
                doc =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.WriteDocument(doc);
                    documents++;
                },
                writer.WriteMetadata);
            foreach (var symbol in external)
            {
                writer.WriteExternalSymbol(symbol);
            }
        }

        if (!quiet)
        {
            await Console.Error.WriteLineAsync(($"scip-cobol: indexed {documents} documents into {options.OutputPath}").AsMemory(), cancellationToken);
        }

        return 0;
    }
}
