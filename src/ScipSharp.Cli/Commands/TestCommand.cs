using System.CommandLine;
using ScipSharp.Testing;

namespace ScipSharp.Cli.Commands;

/// <summary><c>scip test</c> (port of <c>test.go</c>; the logic lives in <see cref="TestRunner"/>).</summary>
internal static class TestCommand
{
    public static Command Create()
    {
        var dir = new Argument<string>("directory") { Description = "Directory containing the test files" };
        var from = CommonOptions.From();
        var commentSyntax = CommonOptions.CommentSyntax();
        var filter = new Option<string[]>("--filter", "-f")
        {
            Description = "Explicit list of test files to check. Can be specified multiple times. If not specified, all files are tested.",
            AllowMultipleArgumentsPerToken = false,
            DefaultValueFactory = static _ => [],
        };
        var checkDocuments = new Option<bool>("--check-documents")
        {
            Description = "Whether or not to validate whether every file in the test directory has a correlating document in the SCIP index.",
        };

        var cmd = new Command("test", "Validate a SCIP index against test files")
        {
            Description = $"""
                Validates whether the SCIP data present in an index
                matches that specified in human-readable test files, using syntax
                similar to the 'snapshot' subcommand. Test file syntax reference:

                    https://github.com/scip-code/scip/blob/{CliApp.Version}/docs/test_file_format.md

                The test files are located based on the relative_path field
                in the SCIP document, interpreted relative to the the directory
                the CLI is invoked in.

                If you want to instead check all the data in a SCIP index,
                use the 'snapshot' subcommand.
                """,
        };
        cmd.Arguments.Add(dir);
        cmd.Options.Add(from);
        cmd.Options.Add(commentSyntax);
        cmd.Options.Add(filter);
        cmd.Options.Add(checkDocuments);
        cmd.SetAction(parse =>
        {
            var index = CommonOptions.ReadIndex(parse.GetRequiredValue(from));
            try
            {
                TestRunner.Run(
                    parse.GetRequiredValue(dir),
                    parse.GetRequiredValue(filter),
                    parse.GetValue(checkDocuments),
                    index,
                    parse.GetRequiredValue(commentSyntax),
                    parse.InvocationConfiguration.Output);
            }
            catch (TestRunner.TestFailedException)
            {
                throw new CliException("", 1);
            }

            return 0;
        });
        return cmd;
    }
}
