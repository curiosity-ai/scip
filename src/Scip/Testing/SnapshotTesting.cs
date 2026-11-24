using System.Text;

namespace Scip.Testing;

/// <summary>A snapshot whose obtained text differs from the checked-in expectation.</summary>
/// <param name="TestCase">Name of the input sub-directory.</param>
/// <param name="RelativePath">Path of the snapshot file.</param>
/// <param name="Diff">Unified diff (expected to obtained).</param>
public sealed record SnapshotMismatch(string TestCase, string RelativePath, string Diff);

/// <summary>
/// Golden-file helpers for indexer snapshot tests (port of <c>testutil/snapshot_testing.go</c>).
/// Layout: <c>&lt;baseDir&gt;/snapshots/input/&lt;case&gt;/...</c> and <c>&lt;baseDir&gt;/snapshots/output/&lt;case&gt;/...</c>.
/// </summary>
public static class SnapshotTesting
{
    /// <summary>True when the environment variable <c>SCIP_UPDATE_SNAPSHOTS</c> is set to a non-empty value other than 0/false.</summary>
    public static bool UpdateRequested =>
        Environment.GetEnvironmentVariable("SCIP_UPDATE_SNAPSHOTS") is { Length: > 0 } v && v is not ("0" or "false");

    /// <summary>
    /// Runs <paramref name="indexFunction"/> for every test case directory below <paramref name="baseDir"/>/snapshots/input
    /// and compares (or, with <paramref name="update"/>, rewrites) the results under <c>snapshots/output</c>.
    /// </summary>
    public static IReadOnlyList<SnapshotMismatch> Run(
        string baseDir,
        Func<string, string, IReadOnlyList<SourceFile>, IEnumerable<SourceFile>> indexFunction,
        bool? update = null) =>
        RunDirectories(Path.Combine(baseDir, "snapshots", "input"), Path.Combine(baseDir, "snapshots", "output"), indexFunction, update);

    /// <summary>Like <see cref="Run"/> with explicit input/output directories. The function receives (inputDir, outputDir, sources).</summary>
    public static IReadOnlyList<SnapshotMismatch> RunDirectories(
        string inputDirectory,
        string outputDirectory,
        Func<string, string, IReadOnlyList<SourceFile>, IEnumerable<SourceFile>> indexFunction,
        bool? update = null)
    {
        var doUpdate = update ?? UpdateRequested;
        if (doUpdate && Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var testCases = Directory.GetFileSystemEntries(inputDirectory).Order(StringComparer.Ordinal).ToList();
        if (testCases.Count == 0)
        {
            throw new InvalidOperationException($"no subdirectories: {inputDirectory}");
        }

        var mismatches = new List<SnapshotMismatch>();
        foreach (var testCase in testCases)
        {
            if (!Directory.Exists(testCase))
            {
                throw new InvalidOperationException($"not a directory: {Path.GetFileName(testCase)}");
            }

            var name = Path.GetFileName(testCase);
            var baseOutput = Path.Combine(outputDirectory, name);
            var sources = SourceFile.FromDirectory(testCase);
            var obtained = indexFunction(testCase, baseOutput, sources);
            mismatches.AddRange(Compare(name, baseOutput, obtained, doUpdate));
        }

        return mismatches;
    }

    /// <summary>Compares obtained snapshots with the files below <paramref name="outputDirectory"/> (or writes them when updating).</summary>
    public static IReadOnlyList<SnapshotMismatch> Compare(string testCase, string outputDirectory, IEnumerable<SourceFile> obtained, bool update)
    {
        var mismatches = new List<SnapshotMismatch>();
        foreach (var document in obtained)
        {
            var outputFile = Path.Combine(outputDirectory, document.RelativePath);
            if (update)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.WriteAllText(outputFile, document.Text);
                continue;
            }

            var expected = File.Exists(outputFile) ? File.ReadAllText(outputFile) : "";
            if (expected != document.Text)
            {
                mismatches.Add(new SnapshotMismatch(
                    testCase,
                    document.RelativePath,
                    UnifiedDiff(outputFile + " (obtained)", outputFile + " (expected)", expected, document.Text)));
            }
        }

        return mismatches;
    }

    /// <summary>A simple unified diff (single hunk-per-change, 3 lines of context) based on a longest-common-subsequence.</summary>
    public static string UnifiedDiff(string fromName, string toName, string from, string to)
    {
        var a = from.Split('\n');
        var b = to.Split('\n');
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var ops = new List<(char Op, string Line)>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                ops.Add((' ', a[x]));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                ops.Add(('-', a[x++]));
            }
            else
            {
                ops.Add(('+', b[y++]));
            }
        }

        while (x < a.Length)
        {
            ops.Add(('-', a[x++]));
        }

        while (y < b.Length)
        {
            ops.Add(('+', b[y++]));
        }

        const int context = 3;
        var sb = new StringBuilder();
        sb.Append("--- ").Append(fromName).Append('\n').Append("+++ ").Append(toName).Append('\n');
        var idx = 0;
        while (idx < ops.Count)
        {
            if (ops[idx].Op == ' ')
            {
                idx++;
                continue;
            }

            var start = Math.Max(0, idx - context);
            var end = idx;
            while (end < ops.Count)
            {
                var nextChange = end;
                while (nextChange < ops.Count && ops[nextChange].Op == ' ')
                {
                    nextChange++;
                }

                if (nextChange == ops.Count || nextChange - end > 2 * context)
                {
                    end = Math.Min(ops.Count, end + context);
                    break;
                }

                end = nextChange + 1;
            }

            var aStart = ops.Take(start).Count(static o => o.Op != '+') + 1;
            var bStart = ops.Take(start).Count(static o => o.Op != '-') + 1;
            var hunk = ops.Skip(start).Take(end - start).ToList();
            sb.Append($"@@ -{aStart},{hunk.Count(static o => o.Op != '+')} +{bStart},{hunk.Count(static o => o.Op != '-')} @@\n");
            foreach (var (op, line) in hunk)
            {
                sb.Append(op).Append(line).Append('\n');
            }

            idx = end;
        }

        return sb.ToString();
    }
}
