using System.CommandLine;
using Scip.Cli.Linting;

namespace Scip.Cli.Commands;

/// <summary><c>scip lint</c> (port of <c>lint.go</c>).</summary>
internal static class LintCommand
{
    public static Command Create()
    {
        var path = new Argument<string>("path")
        {
            Description = "Path to SCIP index file",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = static _ => "",
        };
        var cmd = new Command("lint", "Flag potential issues with a SCIP index")
        {
            Description = "Example usage:\n\n  scip lint /path/to/index.scip\n\n"
                + "You may want to filter the output using `grep -v <pattern>`\n"
                + "to narrow down on certain classes of errors.",
        };
        cmd.Arguments.Add(path);
        cmd.SetAction(parse =>
        {
            var indexPath = parse.GetValue(path);
            if (string.IsNullOrEmpty(indexPath))
            {
                throw new CliException("missing argument for path to SCIP index");
            }

            var diagnostics = IndexLinter.Lint(CommonOptions.ReadIndex(indexPath));
            if (diagnostics.Count == 0)
            {
                return 0;
            }

            throw new CliException(string.Join('\n', diagnostics.Select(static d => d.Message)));
        });
        return cmd;
    }
}
