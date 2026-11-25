using ScipSharp.Indexer.Python.Syntax;

namespace ScipSharp.Indexer.Python.Tests;

/// <summary>
/// Parses and indexes real-world Python if some is installed on the machine (a CPython standard library). The tests pass
/// trivially when none is found; they never run a Python toolchain, they only read source files.
/// </summary>
public class RealWorldSmokeTests
{
    private static string? StdlibDirectory() =>
        new[] { "/usr/lib/python3.13", "/usr/lib/python3.12", "/usr/lib/python3.11", "/usr/local/lib/python3.13", "/usr/local/lib/python3.12" }
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "argparse.py")));

    [Test]
    public async Task StdlibModulesParseWithFewSyntaxErrors()
    {
        var dir = StdlibDirectory();
        if (dir is null)
        {
            return;
        }

        var files = Directory.EnumerateFiles(dir, "*.py", SearchOption.AllDirectories).ToList();
        var failing = new List<string>();
        foreach (var f in files)
        {
            if (Parser.Parse(File.ReadAllText(f)).SyntaxErrors > 0)
            {
                failing.Add(Path.GetRelativePath(dir, f));
            }
        }

        // A handful of files may use syntax that is deliberately invalid or unsupported (e.g. lib2to3 test data).
        await Assert.That(failing.Count).IsLessThanOrEqualTo(files.Count / 100 + 2);
    }

    [Test]
    public async Task IndexesAStdlibSubset()
    {
        var dir = StdlibDirectory();
        if (dir is null)
        {
            return;
        }

        var temp = Path.Combine(Path.GetTempPath(), "scip-python-stdlib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        foreach (var f in new[] { "argparse.py", "dataclasses.py", "typing.py", "functools.py", "enum.py" })
        {
            File.Copy(Path.Combine(dir, f), Path.Combine(temp, f));
        }

        var output = Path.Combine(temp, "index.scip");
        var code = PythonIndexer.Run(temp, output, new PythonIndexOptions { ProjectName = "std", ProjectVersion = "1", InferVersionFromCommit = false, Quiet = true });
        await Assert.That(code).IsEqualTo(0);
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        await Assert.That(index.Documents.Count).IsEqualTo(5);
        await Assert.That(index.Documents.Sum(d => d.Occurrences.Count)).IsGreaterThan(1000);
    }
}
