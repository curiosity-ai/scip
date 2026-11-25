using System.Text.Json;

namespace ScipSharp.Indexer.TypeScript.Tests;

/// <summary>
/// Port of upstream <c>main.test.ts</c>: indexes every project under <c>.reference/scip-typescript/snapshots/input</c> and
/// compares each document with the checked-in snapshot under <c>snapshots/output</c>.
/// </summary>
public class SnapshotTests
{
    private static readonly object BaselineLock = new();
    private const string Missing = "<missing upstream snapshots>";

    public static IEnumerable<string> SnapshotNames()
    {
        var snapshots = Fixture.FindUpstreamSnapshots();
        if (snapshots is null)
        {
            yield return Missing;
            yield break;
        }

        foreach (var dir in Directory.GetDirectories(Path.Combine(snapshots, "input")).OrderBy(d => d, StringComparer.Ordinal))
        {
            yield return Path.GetFileName(dir);
        }
    }

    [Test]
    [MethodDataSource(nameof(SnapshotNames))]
    public async Task Snapshot(string name)
    {
        var snapshots = Fixture.FindUpstreamSnapshots();
        if (snapshots is null || name == Missing)
        {
            Skip.Test(".reference/scip-typescript/snapshots not found");
            return;
        }

        var nodeModulesRoot = await Fixture.RequireAsync();

        // Svelte (svelte2tsx) and JSX/TSX are not supported by the native parser (see README.md).
        if (name is "svelte")
        {
            Skip.Test("Svelte support (svelte2tsx) is not ported");
        }

        var inputRoot0 = Path.Combine(snapshots, "input", name);
        var packageJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(inputRoot0, "package.json"))).RootElement;
        var hasWorkspaces = packageJson.TryGetProperty("workspaces", out _);
        var packageManager = packageJson.TryGetProperty("packageManager", out var pm) ? pm.GetString() ?? "" : "";
        var isNpm = packageManager.StartsWith("npm@", StringComparison.Ordinal);
        var isPnpm = packageManager.Contains("pnpm", StringComparison.Ordinal);
        if (isPnpm && Fixture.FindOnPath("pnpm") is null)
        {
            Skip.Test("pnpm is not installed");
        }

        if (hasWorkspaces && !isNpm && Fixture.FindOnPath("yarn") is null)
        {
            Skip.Test("yarn is not installed");
        }

        var work = Path.Combine(nodeModulesRoot, "work", Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(work, "input");
        var inputRoot = Path.Combine(inputDirectory, name);
        CopyDirectory(inputRoot0, inputRoot);
        LinkWorkspacePackages(inputRoot);
        try
        {
            var tsconfig = Path.Combine(inputRoot, "tsconfig.json");
            var inferTsconfig = !File.Exists(tsconfig);
            var output = Path.Combine(inputRoot, "index.scip");
            var options = new TypeScriptOptions
            {
                Cwd = inputRoot,
                Output = output,
                InferTsconfig = inferTsconfig,
                YarnWorkspaces = hasWorkspaces && !isNpm,
                PnpmWorkspaces = isPnpm,
                NpmWorkspaces = isNpm,
            };
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exit = await new TypeScriptIndexer().IndexAsync(options, stdout, stderr, CancellationToken.None);
            await Assert.That(exit).IsEqualTo(0).Because($"stdout: {stdout}\nstderr: {stderr}");

            if (inferTsconfig && File.Exists(tsconfig))
            {
                File.Delete(tsconfig);
            }

            Index index;
            await using (var stream = File.OpenRead(output))
            {
                index = Index.Parser.ParseFrom(stream);
            }

            await Assert.That(index.Documents.Count).IsGreaterThan(0);
            await CheckInvariants(index);

            var outputDirectory = Path.Combine(snapshots, "output");
            var baseline = Baseline.Load();
            var regressions = new List<string>();
            var matched = 0;
            var matchedKeys = new List<string>();
            var diffDir = Environment.GetEnvironmentVariable("SCIP_TS_DIFF_DIR");
            foreach (var document in index.Documents)
            {
                var inputPath = Path.Combine(inputRoot, document.RelativePath);
                var expectedPath = Path.Combine(outputDirectory, name, document.RelativePath);
                var expected = File.Exists(expectedPath) ? await File.ReadAllTextAsync(expectedPath) : "";
                var obtained = ExternalSymbolLines.Replace(SnapshotFormatter.Format(await File.ReadAllTextAsync(inputPath), document), "");
                var expectedComparable = ExternalSymbolLines.Replace(expected, "");
                var key = $"{name}/{document.RelativePath}";
                if (obtained == expectedComparable)
                {
                    matched++;
                    matchedKeys.Add(key);
                    continue;
                }

                if (diffDir is not null)
                {
                    Directory.CreateDirectory(diffDir);
                    var safe = key.Replace('/', '_');
                    await File.WriteAllTextAsync(Path.Combine(diffDir, safe + ".expected"), expectedComparable);
                    await File.WriteAllTextAsync(Path.Combine(diffDir, safe + ".obtained"), obtained);
                }

                if (baseline.Contains(key)) { regressions.Add($"--- {expectedPath}\n{FirstDifference(expectedComparable, obtained)}"); }
            }

            Console.WriteLine($"snapshot {name}: {matched}/{index.Documents.Count} documents match upstream (references into lib.d.ts / node_modules packages excluded)");
            if (Environment.GetEnvironmentVariable("SCIP_TS_BASELINE_OUT") is { } baselineOut)
            {
                lock (BaselineLock) { File.AppendAllLines(baselineOut, matchedKeys); }
            }

            await Assert.That(regressions).IsEmpty().Because(string.Join("\n\n", regressions.Take(3)));
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    private static readonly System.Text.RegularExpressions.Regex ExternalSymbolLines = new(
        @"^//[ ]*\^+ (?:definition|reference) (?:typescript |@types/|react |csstype |@sourcegraph/)[^\n]*\n(?://[ ]*(?:documentation|signature_documentation|kind|relationship)[^\n]*\n)*",
        System.Text.RegularExpressions.RegexOptions.Multiline);

    private static async Task CheckInvariants(Index index)
    {
        var paths = new HashSet<string>();
        foreach (var document in index.Documents)
        {
            await Assert.That(paths.Add(document.RelativePath)).IsTrue().Because($"duplicate document {document.RelativePath}");
            await Assert.That(document.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
            await Assert.That(document.Language).IsNotEmpty().Because(document.RelativePath);
        }

        var symbolInformation = index.Documents.SelectMany(d => d.Symbols.Select(s => s.Symbol)).ToHashSet();
        var documented = symbolInformation.Concat(index.ExternalSymbols.Select(s => s.Symbol)).ToHashSet();
        var indexedPackages = symbolInformation.Where(s => !s.StartsWith("local ", StringComparison.Ordinal)).Select(SymbolPackage).ToHashSet();
        foreach (var document in index.Documents)
        {
            var duplicateSymbols = document.Symbols.GroupBy(s => s.Symbol).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            await Assert.That(duplicateSymbols).IsEmpty().Because($"{document.RelativePath} should not contain duplicate SymbolInformation");
            var duplicateOccurrences = document.Occurrences
                .GroupBy(o => $"{SnapshotFormatter.RangeOf(o)} {o.SymbolRoles} {o.Symbol}")
                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            await Assert.That(duplicateOccurrences).IsEmpty().Because($"{document.RelativePath} should not contain duplicate occurrences");
            var missing = document.Occurrences.Select(o => o.Symbol)
                .Where(s => s.Length > 0 && indexedPackages.Contains(SymbolPackage(s)) && !symbolInformation.Contains(s)).ToList();
            await Assert.That(missing).IsEmpty().Because($"{document.RelativePath} occurrences in indexed packages should have SymbolInformation");
            var localInDocument = document.Symbols.Select(x => x.Symbol).ToHashSet();
            var lintMissing = document.Occurrences.Select(o => o.Symbol)
                .Where(x => x.Length > 0 && (x.StartsWith("local ", StringComparison.Ordinal) ? !localInDocument.Contains(x) : !documented.Contains(x))).Distinct().ToList();
            await Assert.That(lintMissing).IsEmpty().Because($"{document.RelativePath}: scip lint requires SymbolInformation for every occurrence symbol");
            if (document.RelativePath == "src/symbol-kinds.ts")
            {
                var unspecified = document.Symbols.Where(s => s.Kind == SymbolInformation.Types.Kind.UnspecifiedKind).Select(s => s.Symbol).ToList();
                await Assert.That(unspecified).IsEmpty();
            }
        }
    }

    private static string SymbolPackage(string symbol) => string.Join(' ', symbol.Split(' ').Take(4));

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

    /// <summary>Mimics a workspace install: links every named package into <c>node_modules/&lt;name&gt;</c>.</summary>
    private static void LinkWorkspacePackages(string root)
    {
        foreach (var pj in Directory.GetFiles(root, "package.json", SearchOption.AllDirectories))
        {
            var dir = Path.GetDirectoryName(pj)!;
            if (dir == root || dir.Contains("node_modules", StringComparison.Ordinal)) { continue; }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(pj));
                if (doc.RootElement.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                {
                    var link = Path.Combine(root, "node_modules", n.GetString()!);
                    Directory.CreateDirectory(Path.GetDirectoryName(link)!);
                    if (!Directory.Exists(link)) { Directory.CreateSymbolicLink(link, dir); }
                }
            }
            catch (JsonException)
            {
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            if (Path.GetFileName(dir) is "node_modules")
            {
                continue;
            }

            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }
}
