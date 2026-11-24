using System.CommandLine;
using Google.Protobuf;

namespace Scip.Cli.Commands;

/// <summary><c>scip print</c> (port of <c>print.go</c>).</summary>
internal static class PrintCommand
{
    private static readonly JsonFormatter Json = new(JsonFormatter.Settings.Default.WithPreserveProtoFieldNames(true));

    public static Command Create()
    {
        var path = new Argument<string>("path")
        {
            Description = "Path to SCIP index file",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = static _ => "",
        };
        var json = new Option<bool>("--json") { Description = "Output in JSON format" };
        var color = new Option<bool>("--color")
        {
            Description = "Enable color output for TTY (no effect for JSON) (default: true)",
            DefaultValueFactory = static _ => true,
        };
        var cmd = new Command("print", "Print a SCIP index for debugging")
        {
            Description = "WARNING: The TTY output may change over time.\nDo not rely on non-JSON output in scripts",
        };
        cmd.Arguments.Add(path);
        cmd.Options.Add(json);
        cmd.Options.Add(color);
        cmd.SetAction(parse =>
        {
            var indexPath = parse.GetValue(path);
            if (string.IsNullOrEmpty(indexPath))
            {
                throw new CliException("missing argument for path to SCIP index");
            }

            var colorOutput = parse.GetValue(color);
            // Following https://no-color.org/
            if (Environment.GetEnvironmentVariable("NO_COLOR") is { Length: > 0 })
            {
                colorOutput = false;
            }

            Run(indexPath, colorOutput, parse.GetValue(json), parse.InvocationConfiguration.Output);
            return 0;
        });
        return cmd;
    }

    public static void Run(string indexPath, bool color, bool json, TextWriter output)
    {
        var index = CommonOptions.ReadIndex(indexPath);
        if (json)
        {
            output.Write(Json.Format(index));
            output.Write('\n');
        }
        else
        {
            output.Write(new PrettyPrinter(color).Print(index));
        }
    }
}
