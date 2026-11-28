using System.Xml;
using System.Xml.Linq;
using ScipSharp.Indexer.Pascal.Syntax;

namespace ScipSharp.Indexer.Pascal.Project;

/// <summary>
/// The parts of a Lazarus project (<c>.lpi</c>) or package (<c>.lpk</c>) the indexer needs: name, version, search
/// paths (<c>OtherUnitFiles</c>, <c>IncludeFiles</c>), syntax mode, defines from custom options and the listed files.
/// </summary>
internal sealed class LazarusProject
{
    private static readonly string[] VersionParts = ["Major", "Minor", "Release", "Build"];

    public required string FilePath { get; init; }

    public string Directory => System.IO.Path.GetDirectoryName(FilePath)!;

    public bool IsPackage { get; init; }

    public string Name { get; set; } = "";

    public string Version { get; set; } = ".";

    public List<string> UnitPaths { get; } = [];

    public List<string> IncludePaths { get; } = [];

    public List<string> Defines { get; } = [];

    public HashSet<string> Files { get; } = new(StringComparer.Ordinal);

    public PascalMode? Mode { get; set; }

    public static LazarusProject? Load(string path, Action<string>? warn = null)
    {
        XDocument doc;
        try
        {
            using var stream = File.OpenRead(path);
            doc = XDocument.Load(stream, LoadOptions.None);
        }
        catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"{path}: cannot read Lazarus project: {e.Message}");
            return null;
        }

        var isPackage = path.EndsWith(".lpk", StringComparison.OrdinalIgnoreCase);
        var p = new LazarusProject { FilePath = Path.GetFullPath(path), IsPackage = isPackage };
        var root = doc.Root;
        if (root is null)
        {
            return p;
        }

        if (isPackage)
        {
            var pkg = root.Element("Package");
            p.Name = Value(pkg?.Element("Name")) ?? Path.GetFileNameWithoutExtension(path);
            if (pkg?.Element("Version") is { } v)
            {
                var parts = VersionParts.Select(a => (string?)v.Attribute(a) ?? "0").ToList();
                while (parts.Count > 2 && parts[^1] == "0")
                {
                    parts.RemoveAt(parts.Count - 1);
                }

                p.Version = string.Join('.', parts);
            }

            foreach (var item in pkg?.Element("Files")?.Elements("Item") ?? [])
            {
                if (Value(item.Element("Filename")) is { } f)
                {
                    p.Files.Add(p.Resolve(f));
                }
            }

            p.ReadCompilerOptions(pkg?.Element("CompilerOptions"));
        }
        else
        {
            var opts = root.Element("ProjectOptions");
            p.Name = Value(opts?.Element("General")?.Element("Title")) ?? Path.GetFileNameWithoutExtension(path);
            foreach (var unit in opts?.Element("Units")?.Elements() ?? [])
            {
                if (Value(unit.Element("Filename")) is { } f)
                {
                    p.Files.Add(p.Resolve(f));
                }
            }

            p.ReadCompilerOptions(root.Element("CompilerOptions"));
        }

        return p;
    }

    private void ReadCompilerOptions(XElement? options)
    {
        if (options is null)
        {
            return;
        }

        var search = options.Element("SearchPaths");
        AddPaths(UnitPaths, Value(search?.Element("OtherUnitFiles")));
        AddPaths(IncludePaths, Value(search?.Element("IncludeFiles")));
        if (Value(options.Element("Parsing")?.Element("SyntaxOptions")?.Element("SyntaxMode")) is { } mode)
        {
            Mode = Preprocessor.ParseMode(mode);
        }

        if (Value(options.Element("Other")?.Element("CustomOptions")) is { } custom)
        {
            foreach (var opt in custom.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (opt.StartsWith("-d", StringComparison.Ordinal) && opt.Length > 2)
                {
                    Defines.Add(opt[2..]);
                }
                else if (opt.StartsWith("-M", StringComparison.Ordinal) && Preprocessor.ParseMode(opt[2..]) is { } m)
                {
                    Mode = m;
                }
                else if (opt.StartsWith("-Fu", StringComparison.Ordinal))
                {
                    AddPaths(UnitPaths, opt[3..]);
                }
                else if (opt.StartsWith("-Fi", StringComparison.Ordinal))
                {
                    AddPaths(IncludePaths, opt[3..]);
                }
            }
        }
    }

    private void AddPaths(List<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains("$(", StringComparison.Ordinal))
            {
                continue; // macros such as $(ProjOutDir) or $(LazarusDir) cannot be expanded natively
            }

            target.Add(Resolve(part));
        }
    }

    private string Resolve(string relative)
    {
        var normalized = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Directory, normalized));
    }

    private static string? Value(XElement? e) => (string?)e?.Attribute("Value");
}
