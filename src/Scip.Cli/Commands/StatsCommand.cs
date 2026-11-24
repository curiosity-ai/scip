using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;

namespace Scip.Cli.Commands;

/// <summary><c>scip stats</c> (port of <c>stats.go</c>).</summary>
internal static class StatsCommand
{
    public static Command Create()
    {
        var from = CommonOptions.From();
        var projectRoot = CommonOptions.ProjectRoot();
        var cmd = new Command("stats", "Output useful statistics about a SCIP index");
        cmd.Options.Add(from);
        cmd.Options.Add(projectRoot);
        cmd.SetAction(parse =>
        {
            var fromPath = parse.GetRequiredValue(from);
            var index = CommonOptions.ReadIndex(fromPath);
            if (index.Metadata is null)
            {
                throw new CliException($"Index.Metadata is nil (--from={fromPath})");
            }

            var stats = IndexStatistics.Count(index, parse.GetRequiredValue(projectRoot), parse.InvocationConfiguration.Error);
            parse.InvocationConfiguration.Output.WriteLine(JsonSerializer.Serialize(stats, StatsJson.Default.IndexStatistics));
            return 0;
        });
        return cmd;
    }
}

public sealed class PercentileStats
{
    [JsonPropertyName("50")]
    public int Fifty { get; set; }

    [JsonPropertyName("90")]
    public int Ninety { get; set; }

    [JsonPropertyName("95")]
    public int NinetyFive { get; set; }

    [JsonPropertyName("99")]
    public int NinetyNine { get; set; }

    [JsonPropertyName("99.9")]
    public int NinetyNinePointNine { get; set; }
}

/// <summary>Summary statistics of a sample (port of the Go <c>Stats</c> struct; all values truncated to integers).</summary>
public sealed class Stats
{
    public PercentileStats Percentiles { get; set; } = new();

    [JsonPropertyName("mean")]
    public int Mean { get; set; }

    [JsonPropertyName("stddev")]
    public int Stddev { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }

    [JsonPropertyName("sum")]
    public int Sum { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    public static Stats Create(IReadOnlyList<double> values)
    {
        var mean = values.Count == 0 ? 0 : values.Sum() / values.Count;
        var variance = values.Count == 0 ? 0 : values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return new Stats
        {
            Percentiles = new PercentileStats
            {
                Fifty = Percentile(values, 50),
                Ninety = Percentile(values, 90),
                NinetyFive = Percentile(values, 95),
                NinetyNine = Percentile(values, 99),
                NinetyNinePointNine = Percentile(values, 99.9),
            },
            Mean = (int)mean,
            Stddev = (int)Math.Sqrt(variance),
            Max = values.Count == 0 ? 0 : (int)values.Max(),
            Sum = (int)values.Sum(),
        };
    }

    /// <summary>The percentile as computed by <c>montanaflynn/stats</c> (interpolating when the rank is whole), truncated to an integer.</summary>
    public static int Percentile(IReadOnlyList<double> values, double percent)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        if (values.Count == 1)
        {
            return (int)values[0];
        }

        var sorted = values.Order().ToArray();
        var rank = percent / 100 * sorted.Length;
        var i = (int)rank;
        if (i < 1 || i > sorted.Length)
        {
            return 0;
        }

        return rank == i && i < sorted.Length ? (int)((sorted[i - 1] + sorted[i]) / 2) : (int)sorted[i - 1];
    }
}

public sealed class IndexStatistics
{
    [JsonPropertyName("documents")]
    public int Documents { get; set; }

    [JsonPropertyName("documentSizes")]
    public Stats DocumentSizes { get; set; } = new();

    [JsonPropertyName("linesOfCode")]
    public int LinesOfCode { get; set; }

    [JsonPropertyName("occurrences")]
    public int Occurrences { get; set; }

    [JsonPropertyName("occurrenceCounts")]
    public Stats OccurrenceCounts { get; set; } = new();

    [JsonPropertyName("definitions")]
    public int Definitions { get; set; }

    [JsonPropertyName("definitionCounts")]
    public Stats DefinitionCounts { get; set; } = new();

    /// <summary>Computes statistics. Failing to count lines of code is non-fatal (reported on <paramref name="log"/>).</summary>
    public static IndexStatistics Count(Index index, string customProjectRoot, TextWriter log)
    {
        var linesOfCode = 0;
        try
        {
            linesOfCode = LocCounter.Count(index, customProjectRoot, log);
        }
        catch (Exception e) when (e is IOException or UriFormatException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Keep this non-fatal so other stats can be measured even if the project is not cloned locally.
            log.WriteLine($"Couldn't count lines of code: {e.Message}");
        }

        var stats = new IndexStatistics { Documents = index.Documents.Count, LinesOfCode = linesOfCode };
        var sizes = new List<double>();
        var occurrenceCounts = new List<double>();
        var definitionCounts = new List<double>();
        foreach (var document in index.Documents)
        {
            sizes.Add(document.CalculateSize());
            stats.Occurrences += document.Occurrences.Count;
            occurrenceCounts.Add(document.Occurrences.Count);
            var definitions = document.Occurrences.Count(static o => SymbolRole.Definition.Matches(o));
            stats.Definitions += definitions;
            definitionCounts.Add(definitions);
        }

        stats.DocumentSizes = Stats.Create(sizes);
        stats.DocumentSizes.Comment = "sizes are in bytes";
        stats.OccurrenceCounts = Stats.Create(occurrenceCounts);
        stats.DefinitionCounts = Stats.Create(definitionCounts);
        stats.DefinitionCounts.Comment = "counted using occurrences";
        return stats;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IndexStatistics))]
internal sealed partial class StatsJson : JsonSerializerContext;

/// <summary>A small stand-in for <c>gocloc</c>: counts non-blank, non-comment lines of the index's documents.</summary>
internal static class LocCounter
{
    public static int Count(Index index, string customProjectRoot, TextWriter log)
    {
        string localSource;
        if (customProjectRoot.Length > 0)
        {
            localSource = customProjectRoot;
        }
        else
        {
            var root = new Uri(index.Metadata.ProjectRoot, UriKind.RelativeOrAbsolute);
            var rootPath = root.IsAbsoluteUri ? root.LocalPath : index.Metadata.ProjectRoot;
            if (!Directory.Exists(rootPath) && !File.Exists(rootPath))
            {
                var cwd = Directory.GetCurrentDirectory();
                log.WriteLine(
                    $"Project root [{rootPath}] doesn't exist, using current working directory [{cwd}] instead. "
                    + "To override this behaviour, specify --project-root=<folder> option");
                localSource = cwd;
            }
            else
            {
                localSource = rootPath;
            }
        }

        if (!Directory.Exists(localSource))
        {
            throw new InvalidOperationException($"Project root [{localSource}] is not a directory");
        }

        var total = 0;
        foreach (var document in index.Documents)
        {
            var path = Path.Combine(localSource, document.RelativePath);
            if (File.Exists(path))
            {
                total += CountFile(path);
            }
        }

        return total;
    }

    private static int CountFile(string path)
    {
        var (line, blockStart, blockEnd) = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".py" or ".rb" or ".sh" or ".yaml" or ".yml" or ".toml" or ".r" or ".pl" => ("#", null, null),
            ".sql" or ".hs" or ".lua" => ("--", null, null),
            ".html" or ".xml" or ".md" => (null, "<!--", "-->"),
            ".txt" => (null, null, null),
            _ => ((string?)"//", "/*", "*/"),
        };

        var count = 0;
        var inBlock = false;
        foreach (var raw in File.ReadLines(path))
        {
            var text = raw.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (inBlock)
            {
                if (blockEnd is not null && text.Contains(blockEnd, StringComparison.Ordinal))
                {
                    inBlock = false;
                }

                continue;
            }

            if (line is not null && text.StartsWith(line, StringComparison.Ordinal))
            {
                continue;
            }

            if (blockStart is not null && text.StartsWith(blockStart, StringComparison.Ordinal))
            {
                inBlock = !text.Contains(blockEnd!, StringComparison.Ordinal);
                continue;
            }

            count++;
        }

        return count;
    }
}
