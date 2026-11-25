using System.CommandLine;
using Google.Protobuf;

namespace ScipSharp.Cli.Commands;

/// <summary>Flags shared by several subcommands (port of <c>main.go</c> flag helpers and <c>option_from.go</c>).</summary>
internal static class CommonOptions
{
    public static Option<string> From() => new("--from")
    {
        Description = "Path to SCIP index file",
        DefaultValueFactory = static _ => "index.scip",
    };

    public static Option<string> CommentSyntax() => new("--comment-syntax")
    {
        Description = "Comment syntax to use for snapshot files",
        DefaultValueFactory = static _ => "//",
    };

    public static Option<string> ProjectRoot() => new("--project-root")
    {
        Description = "Override project root in the SCIP file. "
            + "This can be helpful when the SCIP index was created on another computer",
        DefaultValueFactory = static _ => "",
    };

    /// <summary>
    /// Reads a SCIP index. <c>-</c> reads standard input; anything else must be a path ending in <c>.scip</c>.
    /// </summary>
    public static Index ReadIndex(string fromPath, Stream? stdin = null)
    {
        byte[] bytes;
        try
        {
            if (fromPath == "-")
            {
                using var input = stdin ?? Console.OpenStandardInput();
                using var buffer = new MemoryStream();
                input.CopyTo(buffer);
                bytes = buffer.ToArray();
            }
            else if (!fromPath.EndsWith(".scip", StringComparison.Ordinal))
            {
                throw new CliException($"expected file with .scip extension but found {fromPath}");
            }
            else
            {
                bytes = File.ReadAllBytes(fromPath);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new CliException($"failed to read SCIP index at path {fromPath}: {e.Message}");
        }

        try
        {
            return Index.Parser.ParseFrom(bytes);
        }
        catch (InvalidProtocolBufferException e)
        {
            throw new CliException($"failed to parse SCIP index at path {fromPath}: {e.Message}");
        }
    }
}
