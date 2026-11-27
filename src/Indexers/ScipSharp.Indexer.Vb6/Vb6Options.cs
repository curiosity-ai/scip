using System.Text;
using ScipSharp.Indexer.Vb6.Syntax;
using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6;

/// <summary>Options of <c>scip index vb6</c>.</summary>
public sealed record Vb6Options
{
    public const string Usage = """
        usage: scip index vb6 [options]

        Indexes Visual Basic 6 projects (.vbp/.vbg with .bas/.cls/.frm/.ctl/.pag/.dob/.dsr) and plain VBA module exports
        below the project root. Native C#: no VB6, VBA or COM installation is used.

        Options:
          -h --help                     display this help and exit
             --project=<path>           index only this .vbp or .vbg (repeatable; relative to the project root)
             --include-loose-files      with --project, also index module files no project lists
             --encoding=<name|codepage> ANSI code page of non-UTF-8 sources (default: 1252); UTF-8 (BOM or valid UTF-8
                                        byte sequences) is always detected
             --define=<NAME[=value]>    conditional compilation constant (repeatable; overrides .vbp CondComp);
                                        defaults: Win32=-1 Win16=0 Win64=0 VBA6=-1 Mac=0, VBA7=0 in
                                        .vbp projects and VBA7=-1 for loose (VBA) module files
             --emit-text                embed each decoded source text in its SCIP document
        """;

    /// <summary>.vbp/.vbg files to index; empty means discover all below the root.</summary>
    public IReadOnlyList<string> Projects { get; init; } = [];

    public bool IncludeLooseFiles { get; init; }

    /// <summary>ANSI code page for sources that are not UTF-8.</summary>
    public Encoding Encoding { get; init; } = SourceText.Windows1252;

    /// <summary>Conditional compilation constants from <c>--define</c>.</summary>
    public IReadOnlyDictionary<string, object> Defines { get; init; } = new Dictionary<string, object>();

    public bool EmitText { get; init; }

    public bool ShowHelp { get; init; }

    /// <summary>Parses CLI arguments; throws <see cref="ArgumentException"/> on an invalid argument.</summary>
    public static Vb6Options Parse(IReadOnlyList<string> args)
    {
        var projects = new List<string>();
        var defines = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var options = new Vb6Options();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? Value(string name)
            {
                if (arg.StartsWith(name + "=", StringComparison.Ordinal))
                {
                    return arg[(name.Length + 1)..];
                }

                if (arg == name)
                {
                    return i + 1 < args.Count ? args[++i] : throw new ArgumentException($"{name} needs a value");
                }

                return null;
            }

            if (arg is "-h" or "--help")
            {
                options = options with { ShowHelp = true };
            }
            else if (arg == "--include-loose-files")
            {
                options = options with { IncludeLooseFiles = true };
            }
            else if (arg == "--emit-text")
            {
                options = options with { EmitText = true };
            }
            else if (Value("--project") is { } project)
            {
                projects.Add(project);
            }
            else if (Value("--encoding") is { } encoding)
            {
                try
                {
                    options = options with { Encoding = SourceText.ResolveEncoding(encoding) };
                }
                catch (Exception e) when (e is ArgumentException or NotSupportedException)
                {
                    throw new ArgumentException($"unknown encoding '{encoding}'", e);
                }
            }
            else if (Value("--define") is { } define)
            {
                Preprocessor.ParseDefinitions(define, defines);
            }
            else
            {
                throw new ArgumentException($"unknown argument '{arg}'");
            }
        }

        return options with { Projects = projects, Defines = defines };
    }
}
