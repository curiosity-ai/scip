using System.CommandLine;
using ScipSharp.Cli.Convert;

namespace ScipSharp.Cli.Commands;

/// <summary><c>scip expt-convert</c> (port of <c>convert.go</c>): SCIP index to SQLite database.</summary>
internal static class ConvertCommand
{
    public static Command Create()
    {
        var path = new Argument<string>("path")
        {
            Description = "Path to SCIP index file",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = static _ => "",
        };
        var output = new Option<string>("--output")
        {
            Description = "Path to output SQLite database file",
            DefaultValueFactory = static _ => "index.db",
        };
        var cpuProfile = new Option<string>("--cpu-profile")
        {
            Description = "Path to output prof file",
            DefaultValueFactory = static _ => "",
        };
        var cmd = new Command("expt-convert", "[EXPERIMENTAL] Convert a SCIP index to a SQLite database")
        {
            Description = """
                Converts a SCIP index to a SQLite database.

                For inspecting the data, use the SQLite CLI.
                For inspecting the schema, use .schema.

                Occurrences are stored opaquely as a blob to prevent the DB size from growing very quickly.
                """,
        };
        cmd.Arguments.Add(path);
        cmd.Options.Add(output);
        cmd.Options.Add(cpuProfile);
        cmd.SetAction(parse =>
        {
            var indexPath = parse.GetValue(path);
            if (string.IsNullOrEmpty(indexPath))
            {
                throw new CliException("missing argument for path to SCIP index");
            }

            // --cpu-profile is accepted for compatibility with the Go CLI but has no effect on .NET.
            var outputPath = parse.GetRequiredValue(output);
            Run(indexPath, outputPath, SqliteConverter.ChunkSizeHint);
            parse.InvocationConfiguration.Output.WriteLine($"Successfully converted SCIP index to SQLite database at {outputPath}");
            return 0;
        });
        return cmd;
    }

    public static void Run(string indexPath, string sqliteDbPath, int chunkSize)
    {
        var index = CommonOptions.ReadIndex(indexPath);
        using var connection = SqliteConverter.CreateDatabase(sqliteDbPath);
        using var converter = new SqliteConverter(connection, chunkSize);
        converter.Convert(index);
        SqliteConverter.PrepareIndexes(connection);
    }
}
