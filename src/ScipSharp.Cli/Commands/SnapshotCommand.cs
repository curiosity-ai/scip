using System.CommandLine;

namespace ScipSharp.Cli.Commands;

/// <summary><c>scip snapshot</c> (port of <c>snapshot.go</c>).</summary>
internal static class SnapshotCommand
{
    public static Command Create()
    {
        var from = CommonOptions.From();
        var to = new Option<string>("--to")
        {
            Description = "Path to output directory for snapshot files",
            DefaultValueFactory = static _ => "scip-snapshot",
        };
        var projectRoot = CommonOptions.ProjectRoot();
        var strict = new Option<bool>("--strict")
        {
            Description = "If true, fail fast on errors",
            DefaultValueFactory = static _ => true,
        };
        var commentSyntax = CommonOptions.CommentSyntax();

        var cmd = new Command("snapshot", "Generate snapshot files for golden testing")
        {
            Description = """
                The snapshot subcommand generates snapshot files which
                can be use for inspecting the output of an index in a
                visual way. Occurrences are marked with caret signs (^)
                and symbol information.

                For testing a SCIP indexer, you can either use this subcommand
                along with 'git diff' or equivalent, or you can use the dedicated
                'test' subcommand for more targeted checks.
                """,
        };
        cmd.Options.Add(from);
        cmd.Options.Add(to);
        cmd.Options.Add(projectRoot);
        cmd.Options.Add(strict);
        cmd.Options.Add(commentSyntax);
        cmd.SetAction(parse =>
        {
            Run(
                parse.GetRequiredValue(from),
                parse.GetRequiredValue(to),
                parse.GetRequiredValue(projectRoot),
                parse.GetRequiredValue(strict),
                parse.GetRequiredValue(commentSyntax),
                parse.InvocationConfiguration.Output);
            return 0;
        });
        return cmd;
    }

    public static void Run(string from, string output, string customProjectRoot, bool strict, string commentSyntax, TextWriter stdout)
    {
        var index = CommonOptions.ReadIndex(from);
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }
        else if (File.Exists(output))
        {
            File.Delete(output);
        }

        var formatter = strict ? SymbolFormatter.Verbose : SymbolFormatter.LenientVerbose;
        var (snapshots, errors) = SnapshotFormatter.FormatSnapshots(index, commentSyntax, formatter, customProjectRoot);
        if (strict && errors.Count > 0)
        {
            throw new CliException("use --strict=false to ignore this error: " + string.Join('\n', errors.Select(static e => e.Message)));
        }

        foreach (var snapshot in snapshots)
        {
            var outputPath = Path.Combine(output, snapshot.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, snapshot.Text);
        }

        stdout.WriteLine("done: " + output);
    }
}
