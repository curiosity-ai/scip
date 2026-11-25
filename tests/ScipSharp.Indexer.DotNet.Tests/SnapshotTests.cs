using Index = ScipSharp.Index;

namespace ScipSharp.Indexer.DotNet.Tests;

[NotInParallel]
public class SnapshotTests
{
    private static string ExpectedDirectory(string fixture) =>
        Path.Combine(FixtureIndexer.FixturesDirectory, $"expected-{FixtureIndexer.Framework}", fixture);

    [Test]
    [Arguments("project-beside-solution")]
    [Arguments("symbol-kinds")]
    [Arguments("syntax")]
    [Arguments("syntax-slnx")]
    public async Task Snapshot(string fixture)
    {
        var directory = FixtureIndexer.CopyFixture(fixture);
        try
        {
            var index = await FixtureIndexer.IndexAsync(directory);
            // Generated files under obj/ are indexed too (as upstream, which git-ignores them); skip them here.
            var snapshots = index.Documents
                .Where(document => !document.RelativePath.Split('/', '\\').Contains("obj"))
                .DistinctBy(document => document.RelativePath)
                .ToDictionary(
                    document => document.RelativePath,
                    document => SnapshotFormatter.FormatDocument(index, document, directory));

            var outputDirectory = ExpectedDirectory(fixture);
            if (Environment.GetEnvironmentVariable("SCIP_UPDATE_SNAPSHOTS") != null)
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }

                foreach (var (relativePath, snapshot) in snapshots)
                {
                    var documentPath = Path.Combine(outputDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
                    File.WriteAllText(documentPath, snapshot);
                }

                return;
            }

            var failures = new List<string>();
            var expectedFiles = Directory.Exists(outputDirectory)
                ? Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories)
                : [];
            await Assert.That(expectedFiles).IsNotEmpty();
            foreach (var path in expectedFiles)
            {
                var relativePath = Path.GetRelativePath(outputDirectory, path);
                if (!snapshots.TryGetValue(relativePath, out var obtained))
                {
                    failures.Add($"{relativePath}: no document was indexed");
                    continue;
                }

                var expected = File.ReadAllText(path);
                if (expected != obtained)
                {
                    failures.Add($"{relativePath}: {FirstDifference(expected, obtained)}");
                }
            }

            foreach (var relativePath in snapshots.Keys)
            {
                if (!File.Exists(Path.Combine(outputDirectory, relativePath)))
                {
                    failures.Add($"{relativePath}: missing expected output. Run with SCIP_UPDATE_SNAPSHOTS=true to create it.");
                }
            }

            await Assert.That(failures).IsEmpty();
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }

    private static string FirstDifference(string expected, string obtained)
    {
        var e = expected.Split('\n');
        var o = obtained.Split('\n');
        for (var i = 0; i < Math.Max(e.Length, o.Length); i++)
        {
            var el = i < e.Length ? e[i] : "<eof>";
            var ol = i < o.Length ? o[i] : "<eof>";
            if (el != ol)
            {
                return $"line {i + 1}\n  expected: {el}\n  obtained: {ol}";
            }
        }

        return "(identical)";
    }

    [Test]
    [Arguments("auto")]
    [Arguments("solution-first")]
    [Arguments("project-first")]
    [Arguments("project-only")]
    [Arguments("repeated-project")]
    [Arguments("repeated-solution")]
    public async Task ProjectBesideSolutionIsIndexedOnce(string order)
    {
        var directory = FixtureIndexer.CopyFixture("project-beside-solution");
        try
        {
            var solution = Path.Combine(directory, "ProjectBesideSolution.sln");
            var project = Path.Combine(directory, "ProjectBesideSolution.csproj");
            var roots = order switch
            {
                "solution-first" => new[] { solution, project },
                "project-first" => [project, solution],
                "project-only" => [project],
                "repeated-project" => [project, project],
                "repeated-solution" => [solution, solution, project],
                _ => Array.Empty<string>(),
            };
            Index index = await FixtureIndexer.IndexAsync(directory, roots);
            var paths = index.Documents.Select(document => document.RelativePath).ToList();
            await Assert.That(paths.Count(p => p == "Program.cs")).IsEqualTo(1);
            await Assert.That(paths.Distinct().Count()).IsEqualTo(paths.Count);
            var program = index.Documents.Single(document => document.RelativePath == "Program.cs");
            await Assert.That(program.Occurrences.Select(o => o.Symbol))
                .Contains($"scip-dotnet nuget System.Console {Environment.Version.Major}.0.0.0 System/Console#");
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }

    [Test]
    public async Task NoSolutionOrProjectFailsWithExitCodeOne()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scip-dotnet-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            var exit = await new DotNetIndexer().IndexAsync(
                new ScipSharp.Indexing.IndexerOptions(directory, "index.scip", ["--skip-dotnet-restore"]),
                CancellationToken.None);
            await Assert.That(exit).IsEqualTo(1);
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }
}
