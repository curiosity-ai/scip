using System.Text.RegularExpressions;

namespace Scip.Indexer.Clang;

/// <summary>
/// Turns a build-system compile command into arguments suitable for <c>clang -fsyntax-only</c>
/// (port of upstream <c>CommandLineCleaner</c>): strips output/dependency flags and GCC-only ABI flags.
/// </summary>
public static partial class CommandLineCleaner
{
    private static readonly HashSet<string> ZapNoArg = ["-c", "-MD", "-MMD", "-MP", "-MG", "-fcolor-diagnostics", "-fdiagnostics-color", "-pipe", "-save-temps"];
    private static readonly HashSet<string> ZapWithArg = ["-o", "-MF", "-MT", "-MQ", "-march", "-mcpu", "-mtune"];

    [GeneratedRegex("^-m(no-)?fix-.*$")]
    private static partial Regex FixFlags();

    /// <summary>Returns arguments without the compiler (element 0) and without the main source file.</summary>
    public static List<string> Clean(IReadOnlyList<string> commandLine, string mainFile, string directory)
    {
        var result = new List<string>();
        var mainFull = Path.GetFullPath(mainFile, directory);
        for (var i = 1; i < commandLine.Count; i++)
        {
            var arg = commandLine[i];
            if (!arg.StartsWith('-'))
            {
                if (Path.GetFullPath(arg, directory) == mainFull)
                {
                    continue;
                }

                result.Add(arg);
                continue;
            }

            var eq = arg.IndexOf('=');
            var flag = eq < 0 ? arg : arg[..eq];
            if (ZapNoArg.Contains(arg) || arg.StartsWith("-Werror", StringComparison.Ordinal) || FixFlags().IsMatch(arg))
            {
                continue;
            }

            if (ZapWithArg.Contains(flag))
            {
                if (eq < 0)
                {
                    i++; // separate argument
                }

                continue;
            }

            if (arg.StartsWith("-o", StringComparison.Ordinal) && arg.Length > 2 && !arg.StartsWith("-objc", StringComparison.Ordinal))
            {
                continue; // -ofile
            }

            result.Add(arg);
        }

        return result;
    }

    /// <summary>Include directories (<c>-I</c>, <c>-iquote</c>, <c>-isystem</c>) from cleaned arguments, absolute.</summary>
    public static List<string> IncludeDirs(IReadOnlyList<string> args, string directory)
    {
        var dirs = new List<string>();
        string[] prefixes = ["-I", "-iquote", "-isystem"];
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            string? value = null;
            foreach (var p in prefixes)
            {
                if (a == p && i + 1 < args.Count)
                {
                    value = args[++i];
                }
                else if (a.StartsWith(p, StringComparison.Ordinal) && a.Length > p.Length)
                {
                    value = a[p.Length..];
                }

                if (value is not null)
                {
                    break;
                }
            }

            if (value is not null)
            {
                dirs.Add(Path.GetFullPath(value, directory));
            }
        }

        return dirs;
    }
}
