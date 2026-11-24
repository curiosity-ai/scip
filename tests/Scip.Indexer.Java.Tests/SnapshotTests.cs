using Scip.Indexer.Java.Project;

namespace Scip.Indexer.Java.Tests;

/// <summary>
/// Compares the native indexer's output against the upstream scip-java snapshots (<c>scip-snapshots/expected/java</c>).
/// Skipped when <c>.reference</c> is not available. JDK symbols use the JDK 17 label to match the upstream goldens.
/// </summary>
public class SnapshotTests
{
    private static string? ExpectedDir(string root) => Path.Combine(root, "scip-snapshots", "expected", "java", "common", "scip-snapshots", "cases", "java", "common", "src", "main", "java", "minimized");

    internal static Dictionary<string, (string Actual, string Expected)> Run(string reference, string? dump = null)
    {
        var sourceRoot = Path.Combine(reference, "scip-snapshots", "cases", "java", "common");
        var output = Path.Combine(Directory.CreateTempSubdirectory("scipjava-snap").FullName, "index.scip");
        var project = new ProjectDiscovery(new ArtifactLocator()).Discover(sourceRoot);
        var reporter = new AggregatorReporter();
        var rc = NativeProjectIndexer.RunAsync(project, output, "17", [], reporter, CancellationToken.None).GetAwaiter().GetResult();
        if (rc != 0) throw new InvalidOperationException("indexing failed");
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        var result = new Dictionary<string, (string, string)>();
        var expectedDir = ExpectedDir(reference)!;
        foreach (var doc in index.Documents)
        {
            var name = Path.GetFileName(doc.RelativePath);
            var expectedFile = Path.Combine(expectedDir, name);
            if (!File.Exists(expectedFile)) continue;
            var abs = Path.Combine(sourceRoot, doc.RelativePath);
            var actual = SnapshotFormatter.FormatSnapshot(doc, "//", SymbolFormatter.Verbose, abs).Text;
            if (dump is not null)
            {
                Directory.CreateDirectory(dump);
                File.WriteAllText(Path.Combine(dump, name + ".actual"), actual);
            }
            result[name] = (actual, File.ReadAllText(expectedFile));
        }
        return result;
    }

    [Test]
    public async Task MatchesUpstreamSnapshots()
    {
        var reference = ParserTests.FindReference();
        if (reference is null) return;
        var results = Run(reference, Environment.GetEnvironmentVariable("SCIP_SNAPSHOT_DUMP"));
        var ok = results.Where(kv => Normalize(kv.Value.Actual) == Normalize(kv.Value.Expected)).Select(kv => kv.Key).Order().ToList();
        var bad = results.Keys.Except(ok).Order().ToList();
        Console.WriteLine($"snapshots matching: {ok.Count}/{results.Count}");
        Console.WriteLine("passing: " + string.Join(", ", ok));
        Console.WriteLine("failing: " + string.Join(", ", bad));
        await Assert.That(results.Count).IsGreaterThan(10);
        await Assert.That(ok.Count).IsGreaterThanOrEqualTo(MinimumMatching);
    }

    [Test]
    public async Task EverySymbolHasInformation()
    {
        var reference = ParserTests.FindReference();
        if (reference is null) return;
        var sourceRoot = Path.Combine(reference, "scip-snapshots", "cases", "java", "common");
        var output = Path.Combine(Directory.CreateTempSubdirectory("scipjava-lint").FullName, "index.scip");
        var project = new ProjectDiscovery(new ArtifactLocator()).Discover(sourceRoot);
        var rc = await NativeProjectIndexer.RunAsync(project, output, "17", [], new AggregatorReporter(), CancellationToken.None);
        await Assert.That(rc).IsEqualTo(0);
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        var external = index.ExternalSymbols.Select(s => s.Symbol).ToHashSet();
        var order = new Dictionary<string, int>();
        for (var i = 0; i < index.Documents.Count; i++) foreach (var s in index.Documents[i].Symbols) order.TryAdd(s.Symbol, i);
        var missing = index.Documents.SelectMany(d => d.Occurrences).Select(o => o.Symbol)
            .Where(s => !s.StartsWith("local ", StringComparison.Ordinal) && !external.Contains(s) && !order.ContainsKey(s)).Distinct().ToList();
        await Assert.That(missing).IsEmpty();
        var badRelations = new List<string>();
        for (var i = 0; i < index.Documents.Count; i++)
        {
            foreach (var info in index.Documents[i].Symbols)
            {
                foreach (var rel in info.Relationships)
                {
                    var ok = order.ContainsKey(rel.Symbol) || external.Contains(rel.Symbol);
                    if (!ok) badRelations.Add(info.Symbol + " -> " + rel.Symbol);
                }
            }
        }
        await Assert.That(badRelations).IsEmpty();
    }

    // LombokBuilder.java needs annotation processing (generated code) and is the one known mismatch.
    private const int MinimumMatching = 25;

    /// <summary>
    /// The goldens come from the newer scip-java (<c>scip-java</c> scheme, trimmed doc comments); this indexer mirrors
    /// the 0.12.3 release (<c>semanticdb</c> scheme, javadoc text exactly as javac delivers it). Both are normalised.
    /// </summary>
    private static string Normalize(string s)
    {
        var lines = s.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("semanticdb maven ", "scip-java maven ", StringComparison.Ordinal).Split('\n').ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var m = System.Text.RegularExpressions.Regex.Match(lines[i], @"^(//\s+>)\s?(.*?)\s*$");
            if (m.Success) lines[i] = (m.Groups[1].Value + " " + m.Groups[2].Value).TrimEnd();
        }
        // the 0.12.3 release also references the package parts of the `package` declaration
        for (var i = lines.Count - 1; i > 0; i--)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"^//\s+\^+ reference scip-java maven \. \. \S+/$")) continue;
            var j = i - 1;
            while (j > 0 && System.Text.RegularExpressions.Regex.IsMatch(lines[j], @"^//\s+\^+ reference ")) j--;
            if (lines[j].TrimStart().StartsWith("package ", StringComparison.Ordinal)) lines.RemoveAt(i);
        }
        // a documentation block ends with the empty line javac leaves after the last comment line
        for (var i = lines.Count - 1; i > 0; i--)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"^//\s+>$") && !(i + 1 < lines.Count && lines[i + 1].Contains("> ", StringComparison.Ordinal))) lines.RemoveAt(i);
        }
        return string.Join('\n', lines).TrimEnd();
    }
}
