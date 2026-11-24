namespace Scip.Indexer.Dart.Tests;

internal static class Fixtures
{
    /// <summary>Upstream <c>snapshots/</c> directory (read-only reference material).</summary>
    public static string SnapshotsRoot { get; } = FindUpwards(".reference/scip-dart/snapshots");

    public static string Input(string project) => Path.Combine(SnapshotsRoot, "input", project);

    public static string Output(string project) => Path.Combine(SnapshotsRoot, "output", project);

    public static string FindUpwards(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(relative);
    }

    /// <summary>Creates a temporary Dart package from a map of relative path to content.</summary>
    public static string TempPackage(string name, Dictionary<string, string> files, string version = "1.0.0")
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-dart-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pubspec.yaml"), $"name: {name}\nversion: {version}\n");
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return dir;
    }
}
