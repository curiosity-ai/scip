using System.CommandLine;
using System.CommandLine.Invocation;
using ScipSharp.Cli.Commands;
using ScipSharp.Indexing;

namespace ScipSharp.Cli;

/// <summary>An expected failure: the message is printed to stderr and the process exits with <see cref="ExitCode"/>.</summary>
public sealed class CliException(string message, int exitCode = 1) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}

/// <summary>Root of the <c>scip</c> command line (port of <c>cmd/scip/main.go</c>).</summary>
public static class CliApp
{
    /// <summary>The CLI version, e.g. <c>v0.10.0</c> (from the embedded <c>version.txt</c>).</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        using var stream = typeof(CliApp).Assembly.GetManifestResourceStream("version.txt")
            ?? throw new InvalidOperationException("embedded version.txt is missing");
        using var reader = new StreamReader(stream);
        return "v" + reader.ReadToEnd().Trim();
    }

    public const string Description =
        "For more details, see the project README at:\n\n\thttps://github.com/scip-code/scip";

    /// <summary>Builds the command tree.</summary>
    public static RootCommand Build()
    {
        var root = new RootCommand("SCIP Code Intelligence Protocol CLI\n\n" + Description);

        // Replace the built-in --version (assembly informational version) with the CLI version.
        foreach (var builtin in root.Options.OfType<VersionOption>().ToList())
        {
            root.Options.Remove(builtin);
        }

        var version = new Option<bool>("--version", "-v") { Description = "print the version" };
        version.Action = new VersionAction();
        root.Options.Add(version);

        root.Subcommands.Add(LintCommand.Create());
        root.Subcommands.Add(PrintCommand.Create());
        root.Subcommands.Add(SnapshotCommand.Create());
        root.Subcommands.Add(StatsCommand.Create());
        root.Subcommands.Add(TestCommand.Create());
        root.Subcommands.Add(ConvertCommand.Create());
        root.Subcommands.Add(IndexCommand.Create(IndexerRegistry.All));
        return root;
    }

    /// <summary>Runs the CLI with the given streams and returns the process exit code.</summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            EnableDefaultExceptionHandler = false,
        };
        try
        {
            return await Build().Parse([.. args]).InvokeAsync(configuration, cancellationToken);
        }
        catch (CliException e)
        {
            if (e.Message.Length > 0)
            {
                error.WriteLine(e.Message);
            }

            return e.ExitCode;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or Google.Protobuf.InvalidProtocolBufferException
                                      or UnauthorizedAccessException or SymbolParseException or InvalidOperationException)
        {
            error.WriteLine(e.Message);
            return 1;
        }
    }

    private sealed class VersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine($"scip version {Version}");
            return 0;
        }
    }

}
