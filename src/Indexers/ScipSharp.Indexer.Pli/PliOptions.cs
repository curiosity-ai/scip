namespace ScipSharp.Indexer.Pli;

/// <summary>Source margins (1-based, inclusive columns). <see cref="None"/> means free form (every column is source).</summary>
/// <param name="Left">First source column (1-based).</param>
/// <param name="Right">Last source column (1-based, inclusive); 0 means "to the end of the line".</param>
public readonly record struct Margins(int Left, int Right)
{
    /// <summary>The IBM default margins, <c>MARGINS(2,72)</c>: column 1 is carriage control, 73-80 hold sequence numbers.</summary>
    public static Margins Default { get; } = new(2, 72);

    /// <summary>Free form: no margins (also what <c>NOMARGINS</c> and Multics PL/I mean).</summary>
    public static Margins None { get; } = new(1, 0);

    public bool IsNone => Left <= 1 && Right <= 0;

    public override string ToString() => IsNone ? "none" : $"{Left},{Right}";
}

/// <summary>How margins are chosen for a file that does not set them with <c>*PROCESS MARGINS(..)</c>.</summary>
public enum MarginMode
{
    /// <summary>Use <see cref="PliOptions.Margins"/> for every file.</summary>
    Fixed,

    /// <summary>Per file: free form unless the file looks like fixed-format (column-1 carriage control, sequence numbers).</summary>
    Auto,
}

/// <summary>Options of the PL/I indexer (see <see cref="Usage"/>).</summary>
public sealed record PliOptions
{
    public const string Usage = """
        usage: scip index pli [options]

        Native C# SCIP indexer for PL/I (IBM Enterprise PL/I, PL/I for AIX/Open PL/I, Iron Spring, Multics PL/I).
        No PL/I compiler or preprocessor is required.

        Options:
          -h --help                    display this help and exit
             --margins=<l>,<r>|none|auto
                                       source margins (default: 2,72). `none` = free form (e.g. Multics),
                                       `auto` = per file, free form unless the file looks column-oriented.
                                       *PROCESS MARGINS(l,r) / NOMARGINS at the top of a file override this.
             --include-dir=<dir>       directory searched for %INCLUDE members (repeatable, relative to the project root).
                                       The including file's directory, the project root and finally any project file
                                       whose name matches are searched as well.
             --extensions=<list>       comma separated source file extensions (default: .pli,.pl1,.plx,.pls)
             --include-extensions=<list>
                                       comma separated include file extensions (default: .inc,.incl.pl1,.plinc,.cpy)
             --case-sensitive          treat identifiers case-sensitively (Multics); default is case-insensitive
             --package-name=<name>     package name in global symbols (default: name of the project directory)
             --package-version=<v>     package version in global symbols (default: .)
        """;

    public Margins Margins { get; init; } = Margins.Default;

    public MarginMode MarginMode { get; init; } = MarginMode.Fixed;

    public IReadOnlyList<string> IncludeDirectories { get; init; } = [];

    public IReadOnlyList<string> Extensions { get; init; } = [".pli", ".pl1", ".plx", ".pls"];

    public IReadOnlyList<string> IncludeExtensions { get; init; } = [".inc", ".incl.pl1", ".plinc", ".cpy"];

    public bool CaseSensitive { get; init; }

    public string? PackageName { get; init; }

    public string PackageVersion { get; init; } = "";

    /// <summary>True when <c>--help</c> was requested.</summary>
    public bool Help { get; init; }

    /// <summary>Parses command line arguments; throws <see cref="ArgumentException"/> on malformed values.</summary>
    public static PliOptions Parse(IEnumerable<string> args)
    {
        var o = new PliOptions();
        var includes = new List<string>();
        foreach (var arg in args)
        {
            var (key, value) = Split(arg);
            switch (key)
            {
                case "-h" or "--help":
                    o = o with { Help = true };
                    break;
                case "--margins":
                    if (value is null)
                    {
                        throw new ArgumentException("--margins needs a value, e.g. --margins=2,72");
                    }

                    o = value.Equals("auto", StringComparison.OrdinalIgnoreCase)
                        ? o with { MarginMode = MarginMode.Auto }
                        : o with { Margins = ParseMargins(value), MarginMode = MarginMode.Fixed };
                    break;
                case "--include-dir" or "-I":
                    includes.Add(value ?? throw new ArgumentException("--include-dir needs a value"));
                    break;
                case "--extensions":
                    o = o with { Extensions = ParseList(value) };
                    break;
                case "--include-extensions":
                    o = o with { IncludeExtensions = ParseList(value) };
                    break;
                case "--case-sensitive":
                    o = o with { CaseSensitive = value is null || value is "true" or "1" };
                    break;
                case "--package-name":
                    o = o with { PackageName = value };
                    break;
                case "--package-version":
                    o = o with { PackageVersion = value ?? "" };
                    break;
                default:
                    throw new ArgumentException($"unknown option: {arg} (see --help)");
            }
        }

        return o with { IncludeDirectories = includes };
    }

    /// <summary>Parses <c>l,r</c>, <c>l</c> (right margin = end of line) or <c>none</c>.</summary>
    public static Margins ParseMargins(string value)
    {
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase) || value.Equals("free", StringComparison.OrdinalIgnoreCase))
        {
            return Margins.None;
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 3
            || !int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var l)
            || l < 1)
        {
            throw new ArgumentException($"invalid margins '{value}': expected <left>,<right> (e.g. 2,72) or none");
        }

        var r = 0;
        if (parts.Length >= 2 && (!int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out r) || (r != 0 && r < l)))
        {
            throw new ArgumentException($"invalid margins '{value}': the right margin must be >= the left margin");
        }

        return new Margins(l, r);
    }

    private static List<string> ParseList(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(static e => e.StartsWith('.') ? e : "." + e)
            .ToList();

    private static (string Key, string? Value) Split(string arg)
    {
        var eq = arg.IndexOf('=', StringComparison.Ordinal);
        return eq < 0 ? (arg, null) : (arg[..eq], arg[(eq + 1)..]);
    }
}
