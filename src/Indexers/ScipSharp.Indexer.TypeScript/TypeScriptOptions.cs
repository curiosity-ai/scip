using System.Globalization;

namespace ScipSharp.Indexer.TypeScript;

/// <summary>Command line options of <c>scip-typescript index</c> (port of <c>CommandLineOptions.ts</c>).</summary>
public sealed record TypeScriptOptions
{
    public required string Cwd { get; init; }
    public required string Output { get; init; }
    public bool InferTsconfig { get; init; }
    public bool YarnWorkspaces { get; init; }
    public bool YarnBerryWorkspaces { get; init; }
    public bool PnpmWorkspaces { get; init; }
    public bool NpmWorkspaces { get; init; }
    public long MaxFileByteSize { get; init; } = 1_000_000;

    public IReadOnlyList<string> Projects { get; init; } = [];

    /// <summary>Parses the pass-through arguments; returns null and sets <paramref name="error"/> on invalid input.</summary>
    public static TypeScriptOptions? Parse(string projectRoot, string output, IReadOnlyList<string> args, out string? error)
    {
        error = null;
        var cwd = Path.GetFullPath(projectRoot);
        var infer = false;
        var yarn = false;
        var yarnBerry = false;
        var pnpm = false;
        var npm = false;
        long maxSize = 1_000_000;
        var projects = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a is "--cwd" or "--output" or "--max-file-byte-size" && i + 1 >= args.Count)
            {
                error = $"missing value for {a}";
                return null;
            }

            switch (a)
            {
                case "--cwd": cwd = Path.GetFullPath(args[++i], cwd); break;
                case "--output": output = args[++i]; break;
                case "--infer-tsconfig": infer = true; break;
                case "--yarn-workspaces": yarn = true; break;
                case "--yarn-berry-workspaces": yarnBerry = true; break;
                case "--pnpm-workspaces": pnpm = true; break;
                case "--npm-workspaces": npm = true; break;
                case "--progress-bar" or "--no-progress-bar" or "--no-global-caches": break;
                case "--max-file-byte-size":
                    var raw = args[++i];
                    var parsed = ParseHumanByteSize(raw);
                    if (double.IsNaN(parsed))
                    {
                        error = $"invalid byte size '{raw}'. To fix this problem, change the value of the flag --max-file-byte-size to use a valid byte size format: 1kb, 1mb, 1gb.";
                        return null;
                    }

                    maxSize = (long)parsed;
                    break;
                default:
                    if (a.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"unknown option '{a}'";
                        return null;
                    }

                    projects.Add(a);
                    break;
            }
        }

        return new TypeScriptOptions
        {
            Cwd = cwd,
            Output = Path.GetFullPath(output, cwd),
            InferTsconfig = infer,
            YarnWorkspaces = yarn,
            YarnBerryWorkspaces = yarnBerry,
            PnpmWorkspaces = pnpm,
            NpmWorkspaces = npm,
            MaxFileByteSize = maxSize,
            Projects = projects,
        };
    }

    /// <summary>Port of <c>parseHumanByteSizeIntoNumber</c> (decimal units, like upstream). Returns NaN when invalid.</summary>
    public static double ParseHumanByteSize(string text)
    {
        var value = text.ToLowerInvariant();
        double multiplier = 1;
        if (value.EndsWith("kb", StringComparison.Ordinal)) { multiplier = 1_000; value = value[..^2]; }
        else if (value.EndsWith("mb", StringComparison.Ordinal)) { multiplier = 1_000_000; value = value[..^2]; }
        else if (value.EndsWith("gb", StringComparison.Ordinal)) { multiplier = 1_000_000_000; value = value[..^2]; }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number * multiplier : double.NaN;
    }
}
