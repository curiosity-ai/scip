using System.Text.RegularExpressions;

namespace Scip.Indexer.Ruby;

/// <summary>Name and version identifying the gem being indexed (port of <c>SCIPGemMetadata</c>).</summary>
public sealed record GemMetadata(string Name, string Version)
{
    /// <summary>Parses <c>name@version</c>; returns null for anything else.</summary>
    public static GemMetadata? TryParse(string? nameAndVersion)
    {
        var parts = (nameAndVersion ?? "").Split('@');
        return parts is [{ Length: > 0 } name, { Length: > 0 } version] ? new GemMetadata(name, version) : null;
    }

    /// <summary>Gem used for the Ruby core/stdlib, as in upstream.</summary>
    public static GemMetadata Stdlib { get; } = new("ruby", "latest");

    /// <summary>Placeholder gem owning global variables, as in upstream.</summary>
    public static GemMetadata GlobalPlaceholder { get; } = new("_global_", "latest");
}

public enum GemMetadataErrorKind { Warning, Error }

public sealed record GemMetadataError(GemMetadataErrorKind Kind, string Message)
{
    public static readonly GemMetadataError ConfigNotFound = new(GemMetadataErrorKind.Error, "Failed to find .gemspec file for identifying gem version");
    public static readonly GemMetadataError FailedToParseGemspec = new(GemMetadataErrorKind.Warning, "Failed to parse .gemspec file for inferring gem name and version. Consider passing --gem-metadata name@version explicitly instead");
    public static readonly GemMetadataError FailedToParseName = new(GemMetadataErrorKind.Warning, "Failed to parse gem name from .gemspec file; using the .gemspec's base name as a proxy");
    public static readonly GemMetadataError FailedToParseVersion = new(GemMetadataErrorKind.Warning, "Failed to parse gem version from .gemspec file");
    public static readonly GemMetadataError FailedToParseGemfileLock = new(GemMetadataErrorKind.Warning, "Failed to extract name and version from Gemfile.lock");
}

/// <summary>Best-effort inference of the current gem from <c>Gemfile.lock</c> / <c>*.gemspec</c> / the directory name.</summary>
public static partial class GemMetadataReader
{
    [GeneratedRegex(@"^\s+([A-Za-z0-9_\-]+)\s*\((.+)\)\s*$")]
    private static partial Regex SpecLine();

    [GeneratedRegex(@"^\s*[""'](.+)[""'](\.freeze)?\s*$")]
    private static partial Regex StringValue();

    public static (string Name, string Version, List<GemMetadataError> Errors) ReadFromGemfileLock(string contents)
    {
        var sawPath = false;
        var sawSpecs = false;
        string? name = null, version = null;
        foreach (var line in contents.Split('\n'))
        {
            if (line.StartsWith("PATH", StringComparison.Ordinal)) { sawPath = true; continue; }
            if (sawPath && line.Contains("specs:", StringComparison.Ordinal)) { sawSpecs = true; continue; }
            if (sawSpecs)
            {
                if (SpecLine().Match(line.TrimEnd('\r')) is { Success: true } m)
                {
                    (name, version) = (m.Groups[1].Value, m.Groups[2].Value);
                }

                break;
            }
        }

        var errors = new List<GemMetadataError>();
        if (name is null)
        {
            errors.Add(GemMetadataError.FailedToParseGemfileLock);
        }

        return (name ?? "", version ?? "", errors);
    }

    public static (string Name, string Version, List<GemMetadataError> Errors) ReadFromGemspec(string contents)
    {
        string? name = null, version = null;
        var errors = new List<GemMetadataError>();
        static string ReadValue(string line)
        {
            var entries = line.Split('=');
            return entries.Length == 2 && StringValue().Match(entries[1].TrimEnd('\r')) is { Success: true } m ? m.Groups[1].Value : "";
        }

        foreach (var line in contents.Split('\n'))
        {
            if (name is not null && version is not null)
            {
                break;
            }

            if (name is null && line.Contains(".name =", StringComparison.Ordinal))
            {
                name = ReadValue(line);
                if (name.Length == 0) { errors.Add(GemMetadataError.FailedToParseName); continue; }
            }

            if (version is null && line.Contains(".version =", StringComparison.Ordinal))
            {
                version = ReadValue(line);
                if (version.Length == 0) { errors.Add(GemMetadataError.FailedToParseVersion); continue; }
            }
        }

        // Matches upstream: a key that was present but unparsable already produced its own, more specific warning.
        if (name is null || version is null)
        {
            errors.Add(GemMetadataError.FailedToParseGemspec);
        }

        return (name ?? "", version ?? "", errors);
    }

    /// <summary>Same fallback chain as upstream <c>GemMetadata::readFromConfig</c>.</summary>
    public static (GemMetadata Metadata, List<GemMetadataError> Errors) ReadFromDirectory(string dir)
    {
        var errors = new List<GemMetadataError>();
        var dirName = Path.GetFileName(Path.GetFullPath(dir).TrimEnd('/', '\\'));
        var files = Directory.EnumerateFiles(dir)
            .Where(f => f.EndsWith(".lock", StringComparison.Ordinal) || f.EndsWith(".gemspec", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            errors.Add(GemMetadataError.ConfigNotFound);
            return (new GemMetadata(dirName, "latest"), errors);
        }

        string? name = null, version = null;
        void Copy(string n, string v, List<GemMetadataError> errs)
        {
            name = n.Length == 0 ? name : n;
            version = v.Length == 0 ? version : v;
            errors.AddRange(errs);
        }

        foreach (var f in files.Where(f => f.EndsWith("Gemfile.lock", StringComparison.Ordinal)))
        {
            var (n, v, errs) = ReadFromGemfileLock(File.ReadAllText(f));
            if (n.Length > 0 && v.Length > 0)
            {
                return (new GemMetadata(n, v), []);
            }

            Copy(n, v, errs);
            break;
        }

        var gemspec = "";
        foreach (var f in files.Where(f => f.EndsWith(".gemspec", StringComparison.Ordinal)))
        {
            gemspec = f;
            var (n, v, errs) = ReadFromGemspec(File.ReadAllText(f));
            if (n.Length > 0 && v.Length > 0)
            {
                return (new GemMetadata(n, v), []);
            }

            Copy(n, v, errs);
            break;
        }

        if (name is not null && version is not null)
        {
            errors.Clear();
        }

        if (name is null && gemspec.Length > 0)
        {
            name = Path.GetFileName(gemspec)[..^".gemspec".Length];
        }

        return (new GemMetadata(name ?? dirName, version ?? "latest"), errors);
    }
}
