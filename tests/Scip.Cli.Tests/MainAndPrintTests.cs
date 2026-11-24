using System.Text.Json;
using Scip.Cli.Commands;
using static Scip.Cli.Tests.TestHelpers;

namespace Scip.Cli.Tests;

public class MainAndPrintTests
{
    [Test]
    public async Task VersionComesFromVersionTxt()
    {
        var expected = "v" + (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Scip.Cli", "version.txt"))).Trim();
        await Assert.That(CliApp.Version).IsEqualTo(expected);
        var (code, output, _) = await RunCli("--version");
        await Assert.That(code).IsEqualTo(0);
        await Assert.That(output.Trim()).IsEqualTo($"scip version {expected}");
    }

    /// <summary>Counterpart of Go <c>TestCLIReferenceInSync</c>: every documented command exists and renders its help.</summary>
    [Test]
    [Arguments("lint")]
    [Arguments("print")]
    [Arguments("snapshot")]
    [Arguments("stats")]
    [Arguments("test")]
    [Arguments("expt-convert")]
    [Arguments("index")]
    public async Task EveryCommandHasHelp(string command)
    {
        var (code, output, _) = await RunCli(command, "--help");
        await Assert.That(code).IsEqualTo(0);
        await Assert.That(output).Contains(command);
    }

    [Test]
    public async Task RootHelpListsAllCommands()
    {
        var (code, output, _) = await RunCli("--help");
        await Assert.That(code).IsEqualTo(0);
        foreach (var name in new[] { "lint", "print", "snapshot", "stats", "test", "expt-convert", "index" })
        {
            await Assert.That(output).Contains(name);
        }
    }

    [Test]
    public async Task SnapshotAndStatsHelpListGlobalFlags()
    {
        var (_, snapshot, _) = await RunCli("snapshot", "--help");
        await Assert.That(snapshot).Contains("--from").And.Contains("--comment-syntax").And.Contains("--project-root");
        var (_, stats, _) = await RunCli("stats", "--help");
        await Assert.That(stats).Contains("--from").And.Contains("--project-root");
        var (_, test, _) = await RunCli("test", "--help");
        await Assert.That(test).Contains("--from").And.Contains("--comment-syntax").And.Contains("--filter");
    }

    [Test]
    public async Task JsonPrinting()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var index = MakeIndex(["a"], new() { ["f"] = ["b"] }, new() { ["f"] = ["a", "b"] });
            index.Metadata = new Metadata { ProjectRoot = "howdy" };
            var path = WriteIndex(dir, index);

            var (code, output, _) = await RunCli("print", "--json", path);
            await Assert.That(code).IsEqualTo(0);

            using var json = JsonDocument.Parse(output);
            await Assert.That(json.RootElement.GetProperty("metadata").GetProperty("project_root").GetString()).IsEqualTo("howdy");
            var documents = json.RootElement.GetProperty("documents");
            await Assert.That(documents.GetArrayLength()).IsEqualTo(1);
            await Assert.That(documents[0].GetProperty("relative_path").GetString()).IsEqualTo("f");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task TextPrintingWithAndWithoutColor()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var index = new Index { Metadata = new Metadata { ProjectRoot = "howdy" } };
            index.Documents.Add(new Document { RelativePath = "a.txt", Occurrences = { Occ("local 1", new Range(0, 1, 0, 2)) } });
            var path = WriteIndex(dir, index);

            var (_, plain, _) = await RunCli("print", "--color", "false", path);
            await Assert.That(plain).Contains("&scip.Index{");
            await Assert.That(Regex.IsMatch(plain, @"ProjectRoot:\s+""howdy""")).IsTrue();
            await Assert.That(Regex.IsMatch(plain, @"RelativePath:\s+""a\.txt""")).IsTrue();
            await Assert.That(plain).DoesNotContain("\u001b[");

            var (_, colored, _) = await RunCli("print", path);
            await Assert.That(colored).Contains("\u001b[");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task PrintRejectsMissingAndWrongExtension()
    {
        var (code, _, error) = await RunCli("print");
        await Assert.That(code).IsEqualTo(1);
        await Assert.That(error).Contains("missing argument for path to SCIP index");

        var (code2, _, error2) = await RunCli("print", "index.txt");
        await Assert.That(code2).IsEqualTo(1);
        await Assert.That(error2).Contains("expected file with .scip extension");
    }

    [Test]
    public async Task ReadIndexFromStdin()
    {
        var index = new Index { Metadata = new Metadata { ProjectRoot = "stdin-root" } };
        using var stream = new MemoryStream(Google.Protobuf.MessageExtensions.ToByteArray(index));
        var read = CommonOptions.ReadIndex("-", stream);
        await Assert.That(read.Metadata.ProjectRoot).IsEqualTo("stdin-root");
    }
}
