using System.Text.RegularExpressions;

namespace Scip.Indexer.Ruby.Tests;

/// <summary>An expected occurrence parsed from an upstream <c>*.snapshot.rb</c> annotation line.</summary>
public sealed record ExpectedOccurrence(int Line, int Col, int Length, bool IsDefinition, string Symbol);

/// <summary>Parses the scip-ruby snapshot format: source lines are prefixed with one space, annotations start with <c>#</c>.</summary>
public sealed partial class SnapshotFile
{
    [GeneratedRegex(@"^#(\s*)(\^+) (definition|reference)(?: \((?:write|read)\))? (\[\.\.\]|\[global\]|local) ?(.*)$")]
    private static partial Regex Annotation();

    public required string Source { get; init; }

    public required List<ExpectedOccurrence> Expected { get; init; }

    public static SnapshotFile Parse(string snapshotText)
    {
        var source = new List<string>();
        var expected = new List<ExpectedOccurrence>();
        foreach (var raw in snapshotText.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith(' '))
            {
                source.Add(line[1..]);
            }
            else if (Annotation().Match(line) is { Success: true } m && m.Groups[4].Value == "[..]")
            {
                // Local and global-variable symbols are not compared textually.
                expected.Add(new ExpectedOccurrence(source.Count - 1, m.Groups[2].Index - 1, m.Groups[2].Length,
                    m.Groups[3].Value == "definition", m.Groups[5].Value.Trim()));
            }
        }

        return new SnapshotFile { Source = string.Join('\n', source) + "\n", Expected = expected };
    }
}
