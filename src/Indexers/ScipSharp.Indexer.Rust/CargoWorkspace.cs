using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Rust;

/// <summary>A cargo target (lib, bin, example, test, bench, build script, ...) of a package.</summary>
/// <param name="Kind">Normalised kind, see <see cref="RustMonikers.NormalizeTargetKind"/>.</param>
public sealed record CargoTarget(string Name, string Kind, string SourcePath);

/// <summary>A workspace package.</summary>
public sealed record CargoPackage(string Name, string? Version, string ManifestPath, IReadOnlyList<CargoTarget> Targets,
    IReadOnlyList<CargoDependency>? Dependencies = null)
{
    public PackageInformation Information => new(Name, Version);

    public IReadOnlyList<CargoDependency> Deps => Dependencies ?? [];
}

/// <summary>A dependency edge: <paramref name="CrateName"/> is the name used in code (<c>-</c> replaced by <c>_</c>).</summary>
public sealed record CargoDependency(string CrateName, string PackageName, string? Path);

/// <summary>
/// Native package metadata handling: a minimal <c>Cargo.toml</c> reader (package name/version, workspace members,
/// <c>[lib]</c>, <c>[[bin]]</c>) plus cargo's target auto-discovery conventions. No cargo process is ever run.
/// </summary>
public static partial class CargoWorkspace
{
    /// <summary>Reads <c>Cargo.toml</c> natively (no cargo process) and auto-discovers targets like cargo does.</summary>
    public static IReadOnlyList<CargoPackage> Load(string root) => AddPathDependencies(ReadManifests(root));

    /// <summary>Adds packages reachable through <c>path = "..."</c> dependencies that are not workspace members.</summary>
    private static List<CargoPackage> AddPathDependencies(IReadOnlyList<CargoPackage> members)
    {
        List<CargoPackage> all = [.. members];
        HashSet<string> seen = [.. all.Select(p => Path.GetFullPath(p.ManifestPath))];
        for (var i = 0; i < all.Count; i++)
        {
            foreach (var dep in all[i].Deps)
            {
                if (dep.Path is null)
                {
                    continue;
                }

                var manifest = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(all[i].ManifestPath)!, dep.Path, "Cargo.toml"));
                if (File.Exists(manifest) && seen.Add(manifest) && ReadPackage(manifest, File.ReadAllText(manifest)) is { } package)
                {
                    all.Add(package);
                }
            }
        }

        return all;
    }

    private static List<CargoDependency> ReadDependencies(string toml)
    {
        List<CargoDependency> result = [];
        foreach (var section in new[] { "dependencies", "dev-dependencies", "build-dependencies" })
        {
            foreach (var line in Section(toml, section).Split('\n'))
            {
                var m = Regex.Match(line, @"^\s*([A-Za-z0-9_\-]+)\s*=\s*(.*)$");
                if (!m.Success)
                {
                    continue;
                }

                var key = m.Groups[1].Value;
                var rhs = m.Groups[2].Value;
                var path = Value(rhs, "path", inline: true);
                var package = Value(rhs, "package", inline: true) ?? key;
                result.Add(new(key.Replace('-', '_'), package, path));
            }

            // [dependencies.foo] tables
            foreach (Match t in Regex.Matches(toml, $@"^\[{Regex.Escape(section)}\.([A-Za-z0-9_\-]+)\]\s*$", RegexOptions.Multiline))
            {
                var body = Section(toml, $"{section}.{t.Groups[1].Value}");
                result.Add(new(t.Groups[1].Value.Replace('-', '_'), Value(body, "package") ?? t.Groups[1].Value, Value(body, "path")));
            }
        }

        return result;
    }

    private static string? Value(string text, string key, bool inline)
    {
        var m = Regex.Match(text, $@"\b{Regex.Escape(key)}\s*=\s*""([^""]*)""");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Fallback without cargo: reads <c>Cargo.toml</c> (and workspace members) and auto-discovers targets.</summary>
    public static IReadOnlyList<CargoPackage> ReadManifests(string root)
    {
        List<CargoPackage> result = [];
        var rootManifest = Path.Combine(root, "Cargo.toml");
        if (!File.Exists(rootManifest))
        {
            return result;
        }

        var text = File.ReadAllText(rootManifest);
        if (ReadPackage(rootManifest, text) is { } rootPackage)
        {
            result.Add(rootPackage);
        }

        foreach (var member in ReadMembers(text))
        {
            foreach (var dir in ExpandMember(root, member))
            {
                var manifest = Path.Combine(dir, "Cargo.toml");
                if (File.Exists(manifest) && ReadPackage(manifest, File.ReadAllText(manifest)) is { } package)
                {
                    result.Add(package);
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> ExpandMember(string root, string member)
    {
        var path = Path.Combine(root, member);
        if (!member.EndsWith("/*", StringComparison.Ordinal))
        {
            yield return path;
            yield break;
        }

        var parent = Path.Combine(root, member[..^2]);
        if (Directory.Exists(parent))
        {
            foreach (var d in Directory.EnumerateDirectories(parent).Order(StringComparer.Ordinal))
            {
                yield return d;
            }
        }
    }

    private static List<string> ReadMembers(string toml)
    {
        var section = Section(toml, "workspace");
        var m = MembersRegex().Match(section);
        return m.Success ? [.. QuotedRegex().Matches(m.Groups[1].Value).Select(x => x.Groups[1].Value)] : [];
    }

    private static string Section(string toml, string name)
    {
        var header = Regex.Match(toml, $@"^\[{Regex.Escape(name)}\]\s*$", RegexOptions.Multiline);
        if (!header.Success)
        {
            return "";
        }

        var rest = toml[(header.Index + header.Length)..];
        var next = Regex.Match(rest, @"^\[", RegexOptions.Multiline);
        return next.Success ? rest[..next.Index] : rest;
    }

    private static CargoPackage? ReadPackage(string manifestPath, string toml)
    {
        var section = Section(toml, "package");
        var name = Value(section, "name");
        if (name is null)
        {
            return null;
        }

        var dir = Path.GetDirectoryName(manifestPath)!;
        var version = Value(section, "version");
        List<CargoTarget> targets = [];

        var explicitLib = Section(toml, "lib");
        var libPath = Value(explicitLib, "path") ?? "src/lib.rs";
        var libName = Value(explicitLib, "name") ?? name.Replace('-', '_');
        if (File.Exists(Path.Combine(dir, libPath)))
        {
            targets.Add(new(libName, IsProcMacro(explicitLib) ? "proc-macro" : "lib", Path.GetFullPath(Path.Combine(dir, libPath))));
        }

        var pkgSection = Section(toml, "package");
        bool Auto(string key) => !Regex.IsMatch(pkgSection, $@"^\s*{key}\s*=\s*false", RegexOptions.Multiline);
        foreach (var (table, kind, dirName) in new[] { ("bin", "bin", "src/bin"), ("example", "example", "examples"), ("test", "test", "tests"), ("bench", "bench", "benches") })
        {
            foreach (var bin in Regex.Split(toml, $@"^\[\[{table}\]\]\s*$", RegexOptions.Multiline).Skip(1))
            {
                var body = bin.Split("\n[", 2)[0];
                var binName = Value(body, "name");
                var binPath = Value(body, "path") ?? (binName is null ? null
                    : table == "bin" ? $"src/bin/{binName}.rs" : $"{dirName}/{binName}.rs");
                if (binName is not null && binPath is not null && File.Exists(Path.Combine(dir, binPath)))
                {
                    targets.Add(new(binName, kind, Path.GetFullPath(Path.Combine(dir, binPath))));
                }
            }
        }

        var mainRs = Path.Combine(dir, "src", "main.rs");
        if (Auto("autobins") && File.Exists(mainRs))
        {
            targets.Add(new(name, "bin", mainRs));
        }

        if (Auto("autobins"))
        {
            AddDiscovered(targets, dir, Path.Combine("src", "bin"), "bin");
        }

        if (Auto("autoexamples"))
        {
            AddDiscovered(targets, dir, "examples", "example");
        }

        if (Auto("autotests"))
        {
            AddDiscovered(targets, dir, "tests", "test");
        }

        if (Auto("autobenches"))
        {
            AddDiscovered(targets, dir, "benches", "bench");
        }

        var buildRs = Path.Combine(dir, "build.rs");
        if (File.Exists(buildRs))
        {
            targets.Add(new("build-script-build", "custom-build", buildRs));
        }

        // explicit [[bin]] entries may also be auto-discovered; keep the first of each source file
        return new(name, version, manifestPath, [.. targets.DistinctBy(t => (t.Kind, t.SourcePath))], ReadDependencies(toml));
    }

    private static bool IsProcMacro(string libSection) =>
        ProcMacroRegex().IsMatch(libSection);

    private static void AddDiscovered(List<CargoTarget> targets, string dir, string sub, string kind)
    {
        var full = Path.Combine(dir, sub);
        if (!Directory.Exists(full))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(full, "*.rs").Order(StringComparer.Ordinal))
        {
            targets.Add(new(Path.GetFileNameWithoutExtension(file), kind, file));
        }

        foreach (var sd in Directory.EnumerateDirectories(full).Order(StringComparer.Ordinal))
        {
            var main = Path.Combine(sd, "main.rs");
            if (File.Exists(main))
            {
                targets.Add(new(Path.GetFileName(sd), kind, main));
            }
        }
    }

    private static string? Value(string section, string key)
    {
        var m = Regex.Match(section, $@"^\s*{Regex.Escape(key)}\s*=\s*""([^""]*)""", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"members\s*=\s*\[(.*?)\]", RegexOptions.Singleline)]
    private static partial Regex MembersRegex();

    [GeneratedRegex(@"""([^""]*)""")]
    private static partial Regex QuotedRegex();

    [GeneratedRegex(@"^\s*proc-macro\s*=\s*true", RegexOptions.Multiline)]
    private static partial Regex ProcMacroRegex();
}
