using System.Text.RegularExpressions;

namespace Scip.Indexer.Rust;

/// <summary>A cargo target (lib, bin, example, test, bench, build script, ...) of a package.</summary>
/// <param name="Kind">Normalised kind, see <see cref="RustMonikers.NormalizeTargetKind"/>.</param>
public sealed record CargoTarget(string Name, string Kind, string SourcePath);

/// <summary>A workspace package.</summary>
public sealed record CargoPackage(string Name, string? Version, string ManifestPath, IReadOnlyList<CargoTarget> Targets)
{
    public PackageInformation Information => new(Name, Version);
}

/// <summary>
/// Native package metadata handling: a minimal <c>Cargo.toml</c> reader (package name/version, workspace members,
/// <c>[lib]</c>, <c>[[bin]]</c>) plus cargo's target auto-discovery conventions. No cargo process is ever run.
/// </summary>
public static partial class CargoWorkspace
{
    /// <summary>Reads <c>Cargo.toml</c> natively (no cargo process) and auto-discovers targets like cargo does.</summary>
    public static IReadOnlyList<CargoPackage> Load(string root) => ReadManifests(root);

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

        foreach (var bin in Regex.Split(toml, @"^\[\[bin\]\]\s*$", RegexOptions.Multiline).Skip(1))
        {
            var body = bin.Split("\n[", 2)[0];
            var binName = Value(body, "name");
            var binPath = Value(body, "path") ?? (binName is null ? null : $"src/bin/{binName}.rs");
            if (binName is not null && binPath is not null && File.Exists(Path.Combine(dir, binPath)))
            {
                targets.Add(new(binName, "bin", Path.GetFullPath(Path.Combine(dir, binPath))));
            }
        }

        var mainRs = Path.Combine(dir, "src", "main.rs");
        if (File.Exists(mainRs))
        {
            targets.Add(new(name, "bin", mainRs));
        }

        AddDiscovered(targets, dir, Path.Combine("src", "bin"), "bin");
        AddDiscovered(targets, dir, "examples", "example");
        AddDiscovered(targets, dir, "tests", "test");
        AddDiscovered(targets, dir, "benches", "bench");
        var buildRs = Path.Combine(dir, "build.rs");
        if (File.Exists(buildRs))
        {
            targets.Add(new("build-script-build", "custom-build", buildRs));
        }

        // explicit [[bin]] entries may also be auto-discovered; keep the first of each source file
        return new(name, version, manifestPath, [.. targets.DistinctBy(t => (t.Kind, t.SourcePath))]);
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
