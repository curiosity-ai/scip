using System.Text.Json;

namespace Scip.Indexer.Python.Tests;

/// <summary>Runs the indexer over an upstream snapshot input directory and renders each document like scip-python does.</summary>
internal static class SnapshotHarness
{
    public static string SnapshotRoot => Path.Combine(AppContext.BaseDirectory, "snapshots");

    public static IEnumerable<string> AllSnapshots() =>
        Directory.EnumerateDirectories(Path.Combine(SnapshotRoot, "input")).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal)!;

    /// <summary>Indexes <c>snapshots/input/&lt;name&gt;</c> and returns rendered output keyed by relative path.</summary>
    public static Dictionary<string, string> Render(string name, string? environmentOverride = null)
    {
        var projectRoot = Path.Combine(SnapshotRoot, "input", name);
        using var info = JsonDocument.Parse(File.ReadAllText(Path.Combine(SnapshotRoot, "packageInfo.json")));
        string? projectName = null;
        string? projectVersion = null;
        if (!File.Exists(Path.Combine(projectRoot, "pyproject.toml")))
        {
            projectName = info.RootElement.GetProperty("default").GetProperty("name").GetString();
            projectVersion = info.RootElement.GetProperty("default").GetProperty("version").GetString();
        }

        if (info.RootElement.GetProperty("special").TryGetProperty(name, out var special))
        {
            projectName = special.GetProperty("name").GetString();
            projectVersion = special.GetProperty("version").GetString();
        }

        var output = Path.Combine(Path.GetTempPath(), "scip-python-test-" + Guid.NewGuid().ToString("N") + ".scip");
        try
        {
            var options = new PythonIndexOptions
            {
                ProjectName = projectName ?? "",
                ProjectVersion = projectVersion,
                EnvironmentFile = environmentOverride ?? Path.Combine(SnapshotRoot, "testEnv.json"),
                PythonVersion = "3.11",
                InferVersionFromCommit = false,
                Quiet = true,
            };
            var code = PythonIndexer.Run(projectRoot, output, options);
            if (code != 0)
            {
                throw new InvalidOperationException($"indexer exited with {code}");
            }

            var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            var result = new Dictionary<string, string>();
            foreach (var doc in index.Documents)
            {
                var text = File.ReadAllText(Path.Combine(projectRoot, doc.RelativePath));
                result[doc.RelativePath] = SnapshotFormatter.Format(text, doc, index.ExternalSymbols);
                if (Environment.GetEnvironmentVariable("SCIP_PYTHON_SNAPSHOT_DUMP") is { Length: > 0 } dump)
                {
                    var target = Path.Combine(dump, name, doc.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllText(target, result[doc.RelativePath]);
                }
            }

            return result;
        }
        finally
        {
            File.Delete(output);
        }
    }

    public static string Expected(string name, string relativePath) =>
        File.ReadAllText(Path.Combine(SnapshotRoot, "output", name, relativePath));
}
