using Microsoft.Extensions.FileSystemGlobbing;

namespace ScipSharp.Indexer.DotNet;

/// <summary>Parsed <c>scip-dotnet index</c> options.</summary>
internal sealed record DotNetIndexOptions(
    DirectoryInfo WorkingDirectory,
    FileInfo Output,
    IReadOnlyList<FileInfo> Projects,
    Matcher Matcher,
    bool AllowGlobalSymbolDefinitions,
    int DotnetRestoreTimeout,
    bool SkipDotnetRestore,
    FileInfo? NugetConfigPath)
{
    public const int DefaultRestoreTimeout = 300000;

    /// <summary>Parses the arguments of the upstream <c>index</c> command. Returns null (after printing why) on error.</summary>
    public static DotNetIndexOptions? Parse(string workingDirectory, string output, IReadOnlyList<string> args)
    {
        var projects = new List<FileInfo>();
        List<string> include = [], exclude = [];
        bool allowGlobal = false, skipRestore = false;
        var timeout = DefaultRestoreTimeout;
        FileInfo? nugetConfig = null;
        var cwd = workingDirectory;
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            var eq = a.StartsWith("--", StringComparison.Ordinal) ? a.IndexOf('=') : -1;
            var name = eq >= 0 ? a[..eq] : a;

            string? Value()
            {
                if (eq >= 0)
                {
                    return a[(eq + 1)..];
                }

                return i + 1 < args.Count ? args[++i] : null;
            }

            switch (name)
            {
                case "--output": output = Value() ?? output; break;
                case "--working-directory": cwd = Value() ?? cwd; break;
                case "--include": if (Value() is { } inc) { include.Add(inc); } break;
                case "--exclude": if (Value() is { } exc) { exclude.Add(exc); } break;
                case "--allow-global-symbol-definitions": allowGlobal = true; break;
                case "--skip-dotnet-restore": skipRestore = true; break;
                case "--dotnet-restore-timeout":
                    if (!int.TryParse(Value(), out timeout))
                    {
                        Console.Error.WriteLine("error: --dotnet-restore-timeout expects an integer.");
                        return null;
                    }

                    break;
                case "--nuget-config-path":
                    nugetConfig = Value() is { } n ? new FileInfo(Path.GetFullPath(n)) : null;
                    break;
                default:
                    if (a.StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"error: unrecognized option '{a}'.");
                        return null;
                    }

                    projects.Add(new FileInfo(Path.GetFullPath(a)));
                    break;
            }
        }

        var matcher = new Matcher();
        matcher.AddIncludePatterns(include.Count == 0 ? ["**"] : include);
        matcher.AddExcludePatterns(exclude);
        var wd = new DirectoryInfo(Path.GetFullPath(cwd));
        var outFile = Path.IsPathRooted(output) ? new FileInfo(output) : new FileInfo(Path.Join(wd.FullName, output));
        return new DotNetIndexOptions(wd, outFile, projects, matcher, allowGlobal, timeout, skipRestore, nugetConfig);
    }
}
