using System.Text.RegularExpressions;

namespace Scip.Indexer.Dart;

/// <summary>The subset of <c>pubspec.yaml</c> the indexer needs, parsed natively (no YAML library).</summary>
internal sealed partial class Pubspec
{
    public static readonly string[] DependencySections = ["dependencies", "dev_dependencies", "dependency_overrides"];

    public required string Name { get; init; }

    /// <summary>Null when the pubspec has no <c>version</c>.</summary>
    public string? Version { get; init; }

    public string? PublishTo { get; init; }

    /// <summary>Dependency names per section (<see cref="DependencySections"/>), in file order.</summary>
    public required Dictionary<string, List<string>> Dependencies { get; init; }

    public static Pubspec Parse(string text)
    {
        string? name = null, version = null, publishTo = null;
        var deps = DependencySections.ToDictionary(s => s, _ => new List<string>());
        string? section = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            var m = KeyValue().Match(line.TrimStart());
            if (!m.Success)
            {
                continue;
            }

            var key = m.Groups[1].Value.Trim('"', '\'');
            var value = StripComment(m.Groups[2].Value).Trim().Trim('"', '\'');
            if (indent == 0)
            {
                section = deps.ContainsKey(key) ? key : null;
                switch (key)
                {
                    case "name": name = value; break;
                    case "version": version = value.Length > 0 ? value : null; break;
                    case "publish_to": publishTo = value; break;
                }
            }
            else if (section is not null && indent == FirstIndent(text) && !deps[section].Contains(key))
            {
                deps[section].Add(key);
            }
        }

        return new Pubspec
        {
            Name = name ?? throw new InvalidDataException("pubspec.yaml has no 'name'"),
            Version = version,
            PublishTo = publishTo,
            Dependencies = deps,
        };
    }

    private static int FirstIndent(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith(' '))
            {
                return line.Length - line.TrimStart().Length;
            }
        }

        return 2;
    }

    private static string StripComment(string value)
    {
        var quote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '#' && (i == 0 || char.IsWhiteSpace(value[i - 1])))
            {
                return value[..i];
            }
        }

        return value;
    }

    [GeneratedRegex(@"^([^:#\s][^:]*?):(?:\s+(.*))?$")]
    private static partial Regex KeyValue();

    /// <summary>Parses <c>pubspec.lock</c> into a package name to version map.</summary>
    public static Dictionary<string, string> ParseLock(string text)
    {
        var result = new Dictionary<string, string>();
        var inPackages = false;
        string? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            if (indent == 0)
            {
                inPackages = line.StartsWith("packages:");
                current = null;
            }
            else if (inPackages && indent == 2 && line.TrimEnd().EndsWith(':'))
            {
                current = line.Trim()[..^1].Trim('"', '\'');
            }
            else if (inPackages && current is not null && indent == 4 && line.TrimStart().StartsWith("version:"))
            {
                result[current] = line.Trim()["version:".Length..].Trim().Trim('"', '\'');
            }
        }

        return result;
    }
}
