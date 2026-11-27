using System.Text;
using ScipSharp.Indexer.Pascal.Semantics;
using ScipSharp.Indexer.Pascal.Syntax;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Pascal;

/// <summary>
/// Native SCIP indexer for Pascal: ISO 7185 / Extended Pascal (best effort), Turbo/Borland Pascal and Free Pascal
/// (fpc/objfpc/delphi/tp/iso/macpas modes) including Lazarus projects (<c>.lpi</c>), packages (<c>.lpk</c>) and forms
/// (<c>.lfm</c>). Pure managed code: no fpc, lazbuild or language server is involved. See README.md.
/// </summary>
public sealed class PascalIndexer : IIndexer
{
    public const string ToolName = "scip-pascal";
    public const string ToolVersion = "0.1.0";

    private const string Usage = """
        usage: scip index pascal [options]

        scip-pascal indexes Pascal sources (ISO, Turbo Pascal, Free Pascal, Lazarus) natively.

        Options:
          -h --help                    display this help and exit
             --search-path=<dirs>      extra unit directories (';' or ',' separated, repeatable; also -Fu<dir>)
             --include-path=<dirs>     extra include directories for {$I} (repeatable; also -Fi<dir>)
             --define=<NAME[=value]>   define a conditional symbol (repeatable; also -d<NAME>)
             --undefine=<NAME>         remove a default conditional symbol (repeatable)
             --mode=<mode>             default compiler mode: fpc, objfpc, delphi, tp, iso, extendedpascal, macpas
                                       (default: objfpc; '.p' files: iso; overridden by .lpi/.lpk and {$mode})
             --encoding=<name>         code page for files that are not valid UTF-8 (default: windows-1252;
                                       e.g. cp437, cp850, cp1251, iso-8859-1)
             --package-name=<name>     package name of project symbols (default: .lpk/.lpi name or directory name)
             --package-version=<v>     package version of project symbols (default: .lpk version or '.')
             --no-forms                do not index .lfm form files
        """;

    public string Name => "pascal";

    public string Languages => "Pascal (ISO, Turbo Pascal, Free Pascal)";

    public string UpstreamProject => "(none: native implementation)";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        PascalOptions opts;
        try
        {
            var parsed = ParseArguments(options.ProjectRoot, options.Arguments);
            if (parsed is null)
            {
                await Console.Out.WriteLineAsync(Usage.AsMemory(), cancellationToken);
                return 0;
            }

            opts = parsed;
        }
        catch (ArgumentException e)
        {
            await Console.Error.WriteLineAsync(e.Message.AsMemory(), cancellationToken);
            await Console.Error.WriteLineAsync(Usage.AsMemory(), cancellationToken);
            return 1;
        }

        if (!Directory.Exists(opts.Root))
        {
            await Console.Error.WriteLineAsync($"project root does not exist: {opts.Root}".AsMemory(), cancellationToken);
            return 1;
        }

        var (metadata, documents, external) = Build(opts, options.Arguments, msg => Console.Error.WriteLine(msg));
        await using var stream = File.Create(options.OutputPath);
        using var writer = new IndexWriter(stream, leaveOpen: true);
        writer.WriteMetadata(metadata);
        foreach (var doc in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteDocument(doc);
        }

        foreach (var s in external)
        {
            writer.WriteExternalSymbol(s);
        }

        return 0;
    }

    /// <summary>Parses indexer arguments; returns null when help was requested.</summary>
    internal static PascalOptions? ParseArguments(string root, IReadOnlyList<string> args)
    {
        var search = new List<string>();
        var include = new List<string>();
        var defines = new List<string>();
        var undefines = new List<string>();
        PascalMode? mode = null;
        Encoding legacy = Encoding.Latin1;
        string? packageName = null, packageVersion = null;
        var forms = true;
        var legacySet = false;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string Value(string prefix)
            {
                if (arg.Length > prefix.Length && arg[prefix.Length] == '=')
                {
                    return arg[(prefix.Length + 1)..];
                }

                if (i + 1 < args.Count)
                {
                    return args[++i];
                }

                throw new ArgumentException($"missing value for {prefix}");
            }

            static IEnumerable<string> Split(string v) => v.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (arg is "-h" or "--help")
            {
                return null;
            }

            if (arg.StartsWith("--search-path", StringComparison.Ordinal))
            {
                search.AddRange(Split(Value("--search-path")));
            }
            else if (arg.StartsWith("--include-path", StringComparison.Ordinal))
            {
                include.AddRange(Split(Value("--include-path")));
            }
            else if (arg.StartsWith("--define", StringComparison.Ordinal))
            {
                defines.Add(Value("--define"));
            }
            else if (arg.StartsWith("--undefine", StringComparison.Ordinal))
            {
                undefines.Add(Value("--undefine"));
            }
            else if (arg.StartsWith("--mode", StringComparison.Ordinal))
            {
                var m = Value("--mode");
                mode = Preprocessor.ParseMode(m) ?? throw new ArgumentException($"unknown mode: {m}");
            }
            else if (arg.StartsWith("--encoding", StringComparison.Ordinal))
            {
                legacy = GetEncoding(Value("--encoding"));
                legacySet = true;
            }
            else if (arg.StartsWith("--package-name", StringComparison.Ordinal))
            {
                packageName = Value("--package-name");
            }
            else if (arg.StartsWith("--package-version", StringComparison.Ordinal))
            {
                packageVersion = Value("--package-version");
            }
            else if (arg == "--no-forms")
            {
                forms = false;
            }
            else if (arg.StartsWith("-Fu", StringComparison.Ordinal) && arg.Length > 3)
            {
                search.Add(arg[3..]);
            }
            else if (arg.StartsWith("-Fi", StringComparison.Ordinal) && arg.Length > 3)
            {
                include.Add(arg[3..]);
            }
            else if (arg.StartsWith("-d", StringComparison.Ordinal) && arg.Length > 2)
            {
                defines.Add(arg[2..]);
            }
            else
            {
                throw new ArgumentException($"unknown argument: {arg}");
            }
        }

        if (!legacySet)
        {
            legacy = GetEncoding("windows-1252");
        }

        var fullRoot = Path.GetFullPath(root);
        return new PascalOptions
        {
            Root = fullRoot,
            SearchPaths = [.. search.Select(p => Path.GetFullPath(p, fullRoot))],
            IncludePaths = [.. include.Select(p => Path.GetFullPath(p, fullRoot))],
            Defines = defines,
            Undefines = undefines,
            Mode = mode,
            LegacyEncoding = legacy,
            PackageName = packageName,
            PackageVersion = packageVersion,
            IndexForms = forms,
        };
    }

    private static Encoding GetEncoding(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return name.StartsWith("cp", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(2), System.Globalization.CultureInfo.InvariantCulture, out var cp)
                ? Encoding.GetEncoding(cp)
                : Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException($"unknown encoding: {name}");
        }
    }

    /// <summary>Indexes a project (on a large-stack thread: Pascal nesting and unit chains recurse deeply).</summary>
    internal static (Metadata Metadata, List<Document> Documents, List<SymbolInformation> External) Build(
        PascalOptions options, IReadOnlyList<string> arguments, Action<string>? warn = null)
    {
        List<Document> documents = [];
        List<SymbolInformation> external = [];
        Exception? failure = null;
        var ws = new Workspace(options, warn);
        var thread = new Thread(() =>
        {
            try
            {
                (documents, external) = ws.Run();
            }
            catch (Exception e)
            {
                failure = e;
            }
        }, 512 * 1024 * 1024);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("Pascal indexing failed", failure);
        }

        var metadata = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(ws.Root).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = ToolName, Version = ToolVersion },
        };
        metadata.ToolInfo.Arguments.AddRange(arguments);
        return (metadata, documents, external);
    }

    /// <summary>Builds the complete in-memory index (used by tests).</summary>
    internal static Index BuildIndex(string root, params string[] args)
    {
        var options = ParseArguments(root, args) ?? throw new ArgumentException("help requested");
        var (metadata, documents, external) = Build(options, args);
        var index = new Index { Metadata = metadata };
        index.Documents.AddRange(documents);
        index.ExternalSymbols.AddRange(external);
        return index;
    }
}
