using System.Xml.Linq;

namespace ScipSharp.Indexer.Delphi.Project;

/// <summary>Settings of one RAD Studio project file (<c>.dproj</c>) relevant for indexing.</summary>
/// <param name="Path">Absolute path of the <c>.dproj</c>.</param>
/// <param name="MainSource">Absolute path of the <c>.dpr</c>/<c>.dpk</c> it builds, if any.</param>
/// <param name="SearchPaths">Absolute directories from <c>DCC_UnitSearchPath</c>.</param>
/// <param name="Namespaces">Unit scope names from <c>DCC_Namespace</c> (e.g. <c>System</c>, <c>Vcl</c>).</param>
/// <param name="Defines">Conditional defines from <c>DCC_Define</c>.</param>
/// <param name="UnitAliases">Unit aliases from <c>DCC_UnitAlias</c> (<c>WinTypes=Winapi.Windows</c>).</param>
/// <param name="Version">File version from the version info keys, if present.</param>
internal sealed record DprojInfo(
    string Path,
    string? MainSource,
    IReadOnlyList<string> SearchPaths,
    IReadOnlyList<string> Namespaces,
    IReadOnlyList<string> Defines,
    IReadOnlyDictionary<string, string> UnitAliases,
    string? Version)
{
    public string Directory => System.IO.Path.GetDirectoryName(Path) ?? "";

    /// <summary>
    /// Reads a <c>.dproj</c>. Property values are merged over every property group except those that only apply to a
    /// "Release" configuration (best effort: MSBuild conditions are not evaluated). <c>$(…)</c> macros are dropped.
    /// </summary>
    public static DprojInfo? Load(string path, Action<string>? warn = null)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (Exception e) when (e is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"warning: cannot read {path}: {e.Message}");
            return null;
        }

        var dir = System.IO.Path.GetDirectoryName(path) ?? "";
        string? main = null;
        var search = new List<string>();
        var namespaces = new List<string>();
        var defines = new List<string>();
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? version = null;
        foreach (var group in doc.Descendants().Where(e => e.Name.LocalName == "PropertyGroup"))
        {
            var condition = (string?)group.Attribute("Condition") ?? "";
            var releaseOnly = condition.Contains("Cfg_2", StringComparison.OrdinalIgnoreCase) ||
                              condition.Contains("Release", StringComparison.OrdinalIgnoreCase);
            foreach (var prop in group.Elements())
            {
                var value = prop.Value.Trim();
                switch (prop.Name.LocalName)
                {
                    case "MainSource" when main is null && value.Length > 0:
                        main = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, Normalize(value)));
                        break;
                    case "DCC_UnitSearchPath":
                        foreach (var p in SplitList(value))
                        {
                            var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, Normalize(p)));
                            if (!search.Contains(full, StringComparer.Ordinal))
                            {
                                search.Add(full);
                            }
                        }

                        break;
                    case "DCC_Namespace":
                        foreach (var p in SplitList(value))
                        {
                            if (!namespaces.Contains(p, StringComparer.OrdinalIgnoreCase))
                            {
                                namespaces.Add(p);
                            }
                        }

                        break;
                    case "DCC_Define" when !releaseOnly:
                        foreach (var p in SplitList(value))
                        {
                            if (!defines.Contains(p, StringComparer.OrdinalIgnoreCase))
                            {
                                defines.Add(p);
                            }
                        }

                        break;
                    case "DCC_UnitAlias":
                        foreach (var p in SplitList(value))
                        {
                            var eq = p.IndexOf('=', StringComparison.Ordinal);
                            if (eq > 0)
                            {
                                aliases.TryAdd(p[..eq].Trim(), p[(eq + 1)..].Trim());
                            }
                        }

                        break;
                    case "VerInfo_Keys" when version is null:
                        foreach (var kv in value.Split(';'))
                        {
                            if (kv.StartsWith("FileVersion=", StringComparison.OrdinalIgnoreCase))
                            {
                                version = kv["FileVersion=".Length..].Trim();
                            }
                        }

                        break;
                }
            }
        }

        return new DprojInfo(path, main, search, namespaces, defines, aliases, string.IsNullOrEmpty(version) ? null : version);
    }

    private static string Normalize(string p) => p.Replace('\\', System.IO.Path.DirectorySeparatorChar).Replace('/', System.IO.Path.DirectorySeparatorChar);

    private static IEnumerable<string> SplitList(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.Contains("$(", StringComparison.Ordinal) && !p.Contains('%', StringComparison.Ordinal));
}

/// <summary>Command line / API options of the Delphi indexer.</summary>
internal sealed record DelphiIndexerOptions
{
    public string ProjectName { get; init; } = "";

    public string ProjectVersion { get; init; } = "";

    public List<string> SearchPaths { get; init; } = [];

    public List<string> Namespaces { get; init; } = [];

    public List<string> Defines { get; init; } = [];

    public List<string> Undefines { get; init; } = [];

    public List<string> Excludes { get; init; } = [];

    public string Platform { get; init; } = "Win32";

    public double CompilerVersion { get; init; } = 36;

    public bool IndexForms { get; init; } = true;

    public bool UseDproj { get; init; } = true;
}
