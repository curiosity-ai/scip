using System.Globalization;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Fortran;

/// <summary>
/// Native C# SCIP indexer for Fortran: FORTRAN 66/77 fixed form through Fortran 2023 free form. Lexing, statement
/// classification, scoping (use/host association, implicit typing) and cross-file module resolution are implemented
/// in C#; no compiler or preprocessor is run. See README.md for the symbol scheme and known gaps.
/// </summary>
public sealed class FortranIndexer : IIndexer
{
    private const string Usage = """
        usage: scip index fortran [--project-root <dir>] [--output <file>] [options]

        Indexes every Fortran source below the project root (.f .for .ftn .f77 .fpp fixed form,
        .f90 .f95 .f03 .f08 .f18 .f23 free form; upper-case extensions too). INCLUDE / #include files
        (.inc .fi .h ...) are indexed through their includers.

        Options:
          -h --help                    display this help and exit
             --form=<auto|fixed|free>  force the source form of every file (default: auto, by extension and content)
             --fixed-line-length=<n>   last statement column in fixed form (default 72; 0 = unlimited)
          -I <dir> | --include-dir=<dir>
                                       extra directory searched for INCLUDE / #include files (repeatable)
          -D <name[=value]> | --define=<name[=value]>
                                       define a preprocessor macro; switches #if/#ifdef evaluation on (repeatable)
             --cpp=<all|eval>          all: index every #if branch (default without -D); eval: evaluate conditions
             --package-name=<name>     package name in symbols (default: fpm.toml name, else the directory name)
             --package-version=<v>     package version in symbols (default: fpm.toml version, else '.')
             --package-manager=<m>     package manager in symbols (default: 'fpm' when fpm.toml exists, else '.')
             --exclude=<path>          directory name or project-relative path to skip (repeatable)
        """;

    public string Name => "fortran";

    public string Languages => "Fortran (FORTRAN 66/77, Fortran 90-2023)";

    public string UpstreamProject => "(none: native implementation)";

    /// <summary>Parses the indexer-specific arguments. Returns null and prints a message on errors.</summary>
    internal static FortranOptions? ParseArguments(IReadOnlyList<string> args, TextWriter error, out bool help)
    {
        help = false;
        var include = new List<string>();
        var exclude = new List<string>();
        var defines = new Dictionary<string, string>(StringComparer.Ordinal);
        string? form = null, name = null, version = null, manager = null;
        bool? evaluate = null;
        var lineLength = 72;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? Value(string option)
            {
                if (arg.StartsWith(option + "=", StringComparison.Ordinal))
                {
                    return arg[(option.Length + 1)..];
                }

                if (arg == option && i + 1 < args.Count)
                {
                    return args[++i];
                }

                return null;
            }

            if (arg is "-h" or "--help")
            {
                help = true;
                return null;
            }

            if (arg.StartsWith("-I", StringComparison.Ordinal) && arg.Length > 2 && !arg.StartsWith("--", StringComparison.Ordinal))
            {
                include.Add(arg[2..]);
            }
            else if (arg.StartsWith("-D", StringComparison.Ordinal) && arg.Length > 2)
            {
                AddDefine(defines, arg[2..]);
            }
            else if (arg is "-I" or "--include-dir" || arg.StartsWith("--include-dir=", StringComparison.Ordinal))
            {
                if (Value(arg == "-I" ? "-I" : "--include-dir") is { } v)
                {
                    include.Add(v);
                }
            }
            else if (arg is "-D" or "--define" || arg.StartsWith("--define=", StringComparison.Ordinal))
            {
                if (Value(arg == "-D" ? "-D" : "--define") is { } v)
                {
                    AddDefine(defines, v);
                }
            }
            else if (arg.StartsWith("--form", StringComparison.Ordinal))
            {
                form = Value("--form");
                if (form is not ("auto" or "fixed" or "free"))
                {
                    error.WriteLine($"invalid --form value '{form}' (expected auto, fixed or free)");
                    return null;
                }
            }
            else if (arg is "--fixed-form" or "--ffixed-form")
            {
                form = "fixed";
            }
            else if (arg is "--free-form" or "--ffree-form")
            {
                form = "free";
            }
            else if (arg.StartsWith("--fixed-line-length", StringComparison.Ordinal))
            {
                var v = Value("--fixed-line-length");
                if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out lineLength) || lineLength < 0)
                {
                    error.WriteLine($"invalid --fixed-line-length value '{v}'");
                    return null;
                }
            }
            else if (arg.StartsWith("--cpp", StringComparison.Ordinal))
            {
                evaluate = Value("--cpp") switch
                {
                    "eval" => true,
                    "all" => false,
                    var v => throw new ArgumentException($"invalid --cpp value '{v}' (expected all or eval)"),
                };
            }
            else if (arg.StartsWith("--package-name", StringComparison.Ordinal))
            {
                name = Value("--package-name");
            }
            else if (arg.StartsWith("--package-version", StringComparison.Ordinal))
            {
                version = Value("--package-version");
            }
            else if (arg.StartsWith("--package-manager", StringComparison.Ordinal))
            {
                manager = Value("--package-manager");
            }
            else if (arg.StartsWith("--exclude", StringComparison.Ordinal))
            {
                if (Value("--exclude") is { } v)
                {
                    exclude.Add(v);
                }
            }
            else
            {
                error.WriteLine($"unknown option '{arg}' (see --help)");
                return null;
            }
        }

        return new FortranOptions
        {
            Form = form == "auto" ? null : form,
            FixedLineLength = lineLength,
            IncludeDirs = include,
            Defines = defines,
            EvaluateConditionals = evaluate,
            PackageName = name,
            PackageVersion = version,
            PackageManager = manager,
            Exclude = exclude,
        };
    }

    private static void AddDefine(Dictionary<string, string> defines, string spec)
    {
        var eq = spec.IndexOf('=', StringComparison.Ordinal);
        if (eq < 0)
        {
            defines[spec] = "1";
        }
        else
        {
            defines[spec[..eq]] = spec[(eq + 1)..];
        }
    }

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        FortranOptions? parsed;
        bool help;
        try
        {
            parsed = ParseArguments(options.Arguments, Console.Error, out help);
        }
        catch (ArgumentException e)
        {
            await Console.Error.WriteLineAsync(e.Message.AsMemory(), CancellationToken.None);
            return 1;
        }

        if (help)
        {
            await Console.Out.WriteLineAsync(Usage.AsMemory(), CancellationToken.None);
            return 0;
        }

        if (parsed is null)
        {
            return 1;
        }

        try
        {
            var indexer = new FortranProjectIndexer(options.ProjectRoot, parsed, options.Arguments, Console.Error.WriteLine);
            await using var stream = File.Create(options.OutputPath);
            using var writer = new IndexWriter(stream, leaveOpen: true);
            indexer.Run(
                writer.WriteMetadata,
                doc =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    writer.WriteDocument(doc);
                },
                writer.WriteExternalSymbol);
            return 0;
        }
        catch (InvalidOperationException e)
        {
            await Console.Error.WriteLineAsync(e.Message.AsMemory(), CancellationToken.None);
            return 1;
        }
    }
}
