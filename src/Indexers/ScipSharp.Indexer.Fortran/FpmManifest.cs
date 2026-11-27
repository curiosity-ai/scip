namespace ScipSharp.Indexer.Fortran;

/// <summary>The few top-level keys of an <c>fpm.toml</c> manifest the indexer needs (package name and version).</summary>
internal sealed record FpmManifest(string? Name, string? Version)
{
    public static FpmManifest? Load(string root)
    {
        var path = Path.Combine(root, "fpm.toml");
        if (!File.Exists(path))
        {
            return null;
        }

        string? name = null, version = null;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                break; // only the top-level table
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0 || line.StartsWith('#'))
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = Unquote(line[(eq + 1)..].Trim());
            if (value is null)
            {
                continue;
            }

            if (key == "name")
            {
                name = value;
            }
            else if (key == "version")
            {
                version = value;
            }
        }

        // fpm allows `version = "VERSION"`, a file holding the version string.
        if (version is not null && !char.IsDigit(version.FirstOrDefault()) && File.Exists(Path.Combine(root, version)))
        {
            version = File.ReadLines(Path.Combine(root, version)).FirstOrDefault()?.Trim();
        }

        return new FpmManifest(name, version);
    }

    private static string? Unquote(string v)
    {
        if (v.Length >= 2 && v[0] is '"' or '\'')
        {
            var end = v.IndexOf(v[0], 1);
            return end > 0 ? v[1..end] : null;
        }

        return null;
    }
}
