using System.Globalization;
using ScipSharp.Indexer.Delphi.Project;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Delphi;

/// <summary>
/// Native SCIP indexer for Delphi / Object Pascal (Delphi 7 … Delphi 12): units, programs, packages, include files and
/// text form files. No Delphi compiler, RAD Studio or language server is needed. See README.md for scope and gaps.
/// </summary>
public sealed class DelphiIndexer : IIndexer
{
    internal const string Usage = """
        usage: scip index delphi [--project-root <dir>] [--output <file>] [options]

        scip-delphi indexes Delphi / Object Pascal sources (.pas, .dpr, .dpk, {$I} includes, text .dfm/.fmx forms).

        Options:
          -h --help                      display this help and exit
             --project-name=<name>       package name of the project's symbols
                                         (default: the single .dproj/.dpr/.dpk in the root, else the directory name)
             --project-version=<v>       package version (default: .dproj FileVersion, else '.')
             --search-path=<dirs>        extra unit search paths, ';'-separated, relative to the root (repeatable)
             --namespace=<names>         unit scope names for unqualified uses (e.g. System;Vcl) (repeatable)
          -D<sym>, --define=<sym>        define a conditional symbol (repeatable, ';'-separated)
             --undefine=<sym>            undefine a predefined conditional symbol (repeatable)
             --platform=<p>              Win32 (default), Win64, Linux64, OSX64, OSXARM64, Android, Android64, iOSDevice64
             --compiler-version=<n>      CompilerVersion for {$IF} (default 36 = Delphi 12; 15 = Delphi 7)
             --exclude=<dir>             skip a directory (name or path relative to the root; repeatable)
             --no-forms                  do not index .dfm/.fmx form files
             --no-dproj                  ignore .dproj files (search paths, namespaces, defines)
        """;

    public string Name => "delphi";

    public string Languages => "Delphi / Object Pascal";

    public string UpstreamProject => "(none: native implementation)";

    /// <summary>Parses indexer arguments. Returns null and sets <paramref name="error"/> on invalid input.</summary>
    internal static DelphiIndexerOptions? ParseArguments(IReadOnlyList<string> args, out string? error, out bool help)
    {
        error = null;
        help = false;
        var o = new DelphiIndexerOptions();
        string projectName = o.ProjectName, projectVersion = o.ProjectVersion, platform = o.Platform;
        var compilerVersion = o.CompilerVersion;
        var forms = true;
        var dproj = true;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? Value(string name)
            {
                if (arg.StartsWith(name + "=", StringComparison.Ordinal))
                {
                    return arg[(name.Length + 1)..];
                }

                if (arg == name && i + 1 < args.Count)
                {
                    return args[++i];
                }

                return null;
            }

            static IEnumerable<string> List(string v) => v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (arg is "-h" or "--help")
            {
                help = true;
                return o;
            }

            if (arg == "--no-forms")
            {
                forms = false;
            }
            else if (arg == "--no-dproj")
            {
                dproj = false;
            }
            else if (arg.StartsWith("-D", StringComparison.Ordinal) && arg.Length > 2 && !arg.StartsWith("--", StringComparison.Ordinal))
            {
                o.Defines.AddRange(List(arg[2..]));
            }
            else if (Value("--define") is { } d)
            {
                o.Defines.AddRange(List(d));
            }
            else if (Value("--undefine") is { } u)
            {
                o.Undefines.AddRange(List(u));
            }
            else if (Value("--search-path") is { } sp)
            {
                o.SearchPaths.AddRange(List(sp));
            }
            else if (Value("--namespace") is { } ns)
            {
                o.Namespaces.AddRange(List(ns));
            }
            else if (Value("--exclude") is { } ex)
            {
                o.Excludes.AddRange(List(ex));
            }
            else if (Value("--project-name") is { } pn)
            {
                projectName = pn;
            }
            else if (Value("--project-version") is { } pv)
            {
                projectVersion = pv;
            }
            else if (Value("--platform") is { } pl)
            {
                platform = pl;
            }
            else if (Value("--compiler-version") is { } cv)
            {
                if (!double.TryParse(cv, NumberStyles.Float, CultureInfo.InvariantCulture, out compilerVersion))
                {
                    error = $"invalid --compiler-version: {cv}";
                    return null;
                }
            }
            else
            {
                error = $"unknown option: {arg}";
                return null;
            }
        }

        return o with
        {
            ProjectName = projectName,
            ProjectVersion = projectVersion,
            Platform = platform,
            CompilerVersion = compilerVersion,
            IndexForms = forms,
            UseDproj = dproj,
        };
    }

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var parsed = ParseArguments(options.Arguments, out var error, out var help);
        if (help)
        {
            await Console.Out.WriteLineAsync(Usage.AsMemory(), cancellationToken);
            return 0;
        }

        if (parsed is null)
        {
            await Console.Error.WriteLineAsync($"error: {error}\n\n{Usage}".AsMemory(), cancellationToken);
            return 1;
        }

        if (!Directory.Exists(options.ProjectRoot))
        {
            await Console.Error.WriteLineAsync($"error: project root does not exist: {options.ProjectRoot}".AsMemory(), cancellationToken);
            return 1;
        }

        var indexer = new DelphiProjectIndexer(options.ProjectRoot, parsed, options.Arguments, Console.Error.WriteLine);
        await using var stream = File.Create(options.OutputPath);
        using var writer = new IndexWriter(stream, leaveOpen: true);
        var documents = 0;
        indexer.Run(
            writer.WriteMetadata,
            doc =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteDocument(doc);
                documents++;
            },
            writer.WriteExternalSymbol);
        await Console.Error.WriteLineAsync($"scip-delphi: indexed {documents} documents into {options.OutputPath}".AsMemory(), cancellationToken);
        return 0;
    }
}
