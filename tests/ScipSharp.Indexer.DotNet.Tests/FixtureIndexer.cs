using System.Text.RegularExpressions;
using ScipSharp.Indexing;
using Index = ScipSharp.Index;

namespace ScipSharp.Indexer.DotNet.Tests;

/// <summary>Copies a fixture to a temp directory, retargets it to the running framework and indexes it in-process.</summary>
internal static partial class FixtureIndexer
{
    public static string Framework => $"net{Environment.Version.Major}.0";

    public static string FixturesDirectory { get; } = FindFixtures();

    public static string CopyFixture(string name)
    {
        var target = Path.Combine(Path.GetTempPath(), "scip-dotnet-tests", Path.GetRandomFileName());
        CopyDirectory(Path.Combine(FixturesDirectory, name), target);
        foreach (var project in Directory.EnumerateFiles(target, "*proj", SearchOption.AllDirectories))
        {
            // Upstream fixtures multi-target net6.0..net10.0; only the running framework is available here.
            var text = File.ReadAllText(project);
            text = TargetFrameworksRegex().Replace(text, $"<TargetFramework>{Framework}</TargetFramework>");
            text = TargetFrameworkRegex().Replace(text, $"<TargetFramework>{Framework}</TargetFramework>");
            File.WriteAllText(project, text);
        }

        return target;
    }

    public static async Task<Index> IndexAsync(string directory, params string[] arguments)
    {
        var exit = await new DotNetIndexer().IndexAsync(
            new IndexerOptions(directory, "index.scip", arguments), CancellationToken.None);
        if (exit != 0)
        {
            throw new InvalidOperationException($"indexer exited with {exit} for {directory}");
        }

        return Index.Parser.ParseFrom(File.ReadAllBytes(Path.Combine(directory, "index.scip")));
    }

    public static void Cleanup(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static string FindFixtures()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "ScipSharp.Indexer.DotNet.Tests", "Fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not locate tests/ScipSharp.Indexer.DotNet.Tests/Fixtures.");
    }

    [GeneratedRegex("<TargetFrameworks>[^<]*</TargetFrameworks>")]
    private static partial Regex TargetFrameworksRegex();

    [GeneratedRegex("<TargetFramework>[^<]*</TargetFramework>")]
    private static partial Regex TargetFrameworkRegex();
}
