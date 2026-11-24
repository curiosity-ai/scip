using System.IO.Compression;
using System.Text.Json;

namespace Scip.Indexer.Dart;

/// <summary>A library of a dependency or of the SDK: its own public top-level declarations plus its export/import directives.</summary>
internal sealed class ExtLibrary(string id, ExternalPackage package)
{
    public string Id { get; } = id;

    public ExternalPackage Package { get; } = package;

    public Dictionary<string, Decl> Declared { get; } = new(StringComparer.Ordinal);

    public List<ExtDirective> Exports { get; } = [];

    public List<ExtDirective> Imports { get; } = [];

    /// <summary>Path (relative to the package root, or the SDK uri path) of the library's main file.</summary>
    public string MainFile { get; init; } = "";

    internal Dictionary<string, Decl>? ExportedCache { get; set; }

    internal Dictionary<string, Decl>? VisibleCache { get; set; }

    internal bool Building { get; set; }
}

internal sealed record ExtDirective(string Uri, string FromFile, List<string>? Show, List<string>? Hide, string? Prefix);

/// <summary>
/// Resolves <c>dart:</c> and <c>package:</c> libraries without any Dart toolchain: SDK libraries come from an embedded
/// symbol table (generated from the SDK sources), package libraries are parsed from the sources listed in
/// <c>.dart_tool/package_config.json</c>.
/// </summary>
internal sealed class ExternalLibraries
{
    /// <summary>SDK language version the embedded table was generated from; used when the project has no package_config.</summary>
    public const string DefaultSdkVersion = "3.14.0";

    private readonly Dictionary<string, (string Root, string Version)> _packages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExtLibrary?> _libraries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileModel?> _parsed = new(StringComparer.Ordinal);
    private Dictionary<string, ExtLibrary>? _sdk;

    public ExternalLibraries(string projectRoot)
    {
        SdkVersion = DefaultSdkVersion;
        var config = Path.Combine(projectRoot, ".dart_tool", "package_config.json");
        if (!File.Exists(config))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(config));
            if (doc.RootElement.TryGetProperty("generatorVersion", out var gv) && gv.GetString() is { } v)
            {
                var parts = v.Split('.');
                if (parts.Length >= 2)
                {
                    SdkVersion = $"{parts[0]}.{parts[1]}.0";
                }
            }

            if (!doc.RootElement.TryGetProperty("packages", out var pkgs))
            {
                return;
            }

            var configDir = Path.GetDirectoryName(config)!;
            foreach (var p in pkgs.EnumerateArray())
            {
                var name = p.GetProperty("name").GetString()!;
                var rootUri = p.GetProperty("rootUri").GetString()!;
                string root;
                if (rootUri.StartsWith("file:", StringComparison.Ordinal))
                {
                    root = new Uri(rootUri).LocalPath;
                }
                else
                {
                    root = Path.GetFullPath(Path.Combine(configDir, Uri.UnescapeDataString(rootUri)));
                }

                var version = ".";
                var pubspec = Path.Combine(root, "pubspec.yaml");
                if (File.Exists(pubspec))
                {
                    version = Pubspec.Parse(File.ReadAllText(pubspec)).Version ?? ".";
                }

                _packages[name] = (root, version);
            }
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or UriFormatException)
        {
            // an unreadable config simply means no dependency resolution
        }
    }

    public string SdkVersion { get; }

    public bool HasPackages => _packages.Count > 0;

    /// <summary>Resolves the library named by an import/export URI relative to <paramref name="fromFile"/> of <paramref name="from"/>.</summary>
    public ExtLibrary? Resolve(string uri, ExtLibrary? from, string fromFile)
    {
        if (uri.StartsWith("dart:", StringComparison.Ordinal))
        {
            return Sdk().GetValueOrDefault(uri);
        }

        if (uri.StartsWith("package:", StringComparison.Ordinal))
        {
            var rest = uri["package:".Length..];
            var slash = rest.IndexOf('/');
            return slash < 0 ? null : Package(rest[..slash], "lib/" + rest[(slash + 1)..]);
        }

        if (from is { Package.Name: var pkg } && !from.Id.StartsWith("dart:", StringComparison.Ordinal) && !uri.Contains(':'))
        {
            return Package(pkg, ResolvePath(fromFile, uri));
        }

        if (from is not null && from.Id.StartsWith("dart:", StringComparison.Ordinal))
        {
            return null;
        }

        return null;
    }

    public ExtLibrary? Package(string package, string relPath)
    {
        var key = package + "|" + relPath;
        if (_libraries.TryGetValue(key, out var cached))
        {
            return cached;
        }

        _libraries[key] = null; // cycle guard
        if (!_packages.TryGetValue(package, out var info))
        {
            return null;
        }

        var model = Parse(package, info.Root, info.Version, relPath);
        if (model is null)
        {
            return null;
        }

        var lib = new ExtLibrary("package:" + package + "/" + relPath[4..], new ExternalPackage(package, info.Version)) { MainFile = relPath };
        _libraries[key] = lib;
        AddFile(lib, model, true);
        foreach (var part in model.Directives.Where(d => d.Text == "part"))
        {
            var partPath = ResolvePath(relPath, StringValue(part.Uri.Text));
            if (Parse(package, info.Root, info.Version, partPath) is { } pm)
            {
                AddFile(lib, pm, false);
            }
        }

        return lib;
    }

    private FileModel? Parse(string package, string root, string version, string relPath)
    {
        var key = package + "|" + relPath;
        if (_parsed.TryGetValue(key, out var cached))
        {
            return cached;
        }

        FileModel? model = null;
        var full = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            if (File.Exists(full))
            {
                model = DartParser.Parse(relPath, File.ReadAllText(full), new ExternalPackage(package, version));
            }
        }
        catch (IOException)
        {
            model = null;
        }

        _parsed[key] = model;
        return model;
    }

    private static void AddFile(ExtLibrary lib, FileModel model, bool main)
    {
        foreach (var d in model.TopLevel)
        {
            if (!d.Name.StartsWith('_') && d.Kind != DeclKind.ImportPrefix && !lib.Declared.ContainsKey(d.Name))
            {
                d.Lib = lib;
                lib.Declared[d.Name] = d;
            }
        }

        if (!main)
        {
            return;
        }

        foreach (var dir in model.Directives)
        {
            var d = new ExtDirective(StringValue(dir.Uri.Text), model.RelativePath, dir.Show, dir.Hide, dir.Prefix);
            if (dir.Text == "export")
            {
                lib.Exports.Add(d);
            }
            else if (dir.Text == "import")
            {
                lib.Imports.Add(d);
            }
        }
    }

    /// <summary>Names a library exports: its own public declarations plus (filtered) re-exports.</summary>
    public Dictionary<string, Decl> Exported(ExtLibrary lib)
    {
        if (lib.ExportedCache is { } c)
        {
            return c;
        }

        if (lib.Building)
        {
            return lib.Declared;
        }

        lib.Building = true;
        var result = new Dictionary<string, Decl>(lib.Declared, StringComparer.Ordinal);
        foreach (var e in lib.Exports)
        {
            if (Resolve(e.Uri, lib, e.FromFile) is not { } target)
            {
                continue;
            }

            foreach (var (name, decl) in Exported(target))
            {
                if (name.StartsWith('_') || result.ContainsKey(name) || !Allowed(name, e))
                {
                    continue;
                }

                result[name] = decl;
            }
        }

        lib.Building = false;
        lib.ExportedCache = result;
        return result;
    }

    public static bool Allowed(string name, ExtDirective e) =>
        (e.Show is null || e.Show.Contains(name)) && (e.Hide is null || !e.Hide.Contains(name));

    /// <summary>Names visible inside the library itself: declared + (non-prefixed) imports + <c>dart:core</c>.</summary>
    public Dictionary<string, Decl> Visible(ExtLibrary lib)
    {
        if (lib.VisibleCache is { } c)
        {
            return c;
        }

        var result = new Dictionary<string, Decl>(lib.Declared, StringComparer.Ordinal);
        lib.VisibleCache = result;
        foreach (var e in lib.Imports.Where(i => i.Prefix is null))
        {
            if (Resolve(e.Uri, lib, e.FromFile) is { } target)
            {
                foreach (var (name, decl) in Exported(target))
                {
                    if (Allowed(name, e))
                    {
                        result.TryAdd(name, decl);
                    }
                }
            }
        }

        if (Sdk().GetValueOrDefault("dart:core") is { } core && lib.Id != "dart:core")
        {
            foreach (var (name, decl) in Exported(core))
            {
                result.TryAdd(name, decl);
            }
        }

        return result;
    }

    private static string ResolvePath(string fromFile, string uri)
    {
        var segments = new List<string>(fromFile.Split('/')[..^1]);
        foreach (var s in uri.Split('/'))
        {
            if (s == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (s is not ("." or ""))
            {
                segments.Add(s);
            }
        }

        return string.Join('/', segments);
    }

    internal static string StringValue(string literal)
    {
        var s = literal.TrimStart('r');
        var q = s.Length >= 3 && s[1] == s[0] && s[2] == s[0] ? 3 : 1;
        return s.Length >= 2 * q ? s[q..^q] : s;
    }

    private Dictionary<string, ExtLibrary> Sdk() => _sdk ??= SdkTable.Load(SdkVersion);
}

/// <summary>Reads the embedded SDK symbol table (see <c>SdkTableGenerator</c> in the tests).</summary>
internal static class SdkTable
{
    private static readonly char[] Sep = ['\t'];

    public static Dictionary<string, ExtLibrary> Load(string version)
    {
        var result = new Dictionary<string, ExtLibrary>(StringComparer.Ordinal);
        using var stream = typeof(SdkTable).Assembly.GetManifestResourceStream("sdk-symbols.tsv.gz");
        if (stream is null)
        {
            return result;
        }

        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        ExtLibrary? lib = null;
        string file = "";
        Decl? type = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var f = line.Split(Sep);
            switch (f[0])
            {
                case "L":
                    lib = new ExtLibrary(f[1], new ExternalPackage(f[1], version)) { MainFile = f[1] };
                    result[f[1]] = lib;
                    break;
                case "X" when lib is not null:
                    lib.Exports.Add(new ExtDirective(f[1], lib.MainFile, Split(f[2]), Split(f[3]), null));
                    break;
                case "I" when lib is not null:
                    lib.Imports.Add(new ExtDirective(f[1], lib.MainFile, Split(f[2]), Split(f[3]), f.Length > 4 && f[4].Length > 0 ? f[4] : null));
                    break;
                case "F":
                    file = f[1];
                    break;
                case "T" when lib is not null:
                    {
                        // T kind name supers typeParams
                        var kind = f[1] switch
                        {
                            "class" => DeclKind.Class,
                            "mixin" => DeclKind.Mixin,
                            "enum" => DeclKind.Enum,
                            "typedef" => DeclKind.TypeAlias,
                            _ => DeclKind.Extension,
                        };
                        type = New(kind, f[2], null, lib, file, [(f[2], SymbolDescriptor.Types.Suffix.Type)]);
                        type.IsLocal = f[2].StartsWith('_');
                        type.Supers.AddRange(Split(f[3]) ?? []);
                        type.Lib = lib;
                        lib.Declared.TryAdd(type.Name, type);
                        break;
                    }

                case "t" when lib is not null:
                    {
                        // t kind name typeName
                        var (kind, path) = f[1] switch
                        {
                            "f" => (DeclKind.Function, new List<(string, SymbolDescriptor.Types.Suffix)> { (f[2], SymbolDescriptor.Types.Suffix.Method) }),
                            "v" => (DeclKind.Variable, [(f[2], SymbolDescriptor.Types.Suffix.Term)]),
                            "g" => (DeclKind.Property, [("<get>" + f[2], SymbolDescriptor.Types.Suffix.Term)]),
                            _ => (DeclKind.Property, [("<set>" + f[2], SymbolDescriptor.Types.Suffix.Term)]),
                        };
                        var d = New(kind, f[2], null, lib, file, path);
                        d.TypeName = f[3].Length > 0 ? f[3] : null;
                        d.IsGetter = f[1] == "g";
                        d.Lib = lib;
                        lib.Declared.TryAdd(d.Name, d);
                        break;
                    }

                case "m" when lib is not null && type is not null:
                    {
                        // m kind name static typeName
                        var name = f[2];
                        var (kind, path) = f[1] switch
                        {
                            "M" => (DeclKind.Method, [.. type.Path, (name, SymbolDescriptor.Types.Suffix.Method)]),
                            "G" => (DeclKind.Property, [.. type.Path, ("<get>" + name, SymbolDescriptor.Types.Suffix.Term)]),
                            "S" => (DeclKind.Property, [.. type.Path, ("<set>" + name, SymbolDescriptor.Types.Suffix.Term)]),
                            "K" => (DeclKind.Constructor, [.. type.Path, (name.Length == 0 ? "<constructor>" : name, SymbolDescriptor.Types.Suffix.Method)]),
                            _ => (DeclKind.Field, new List<(string, SymbolDescriptor.Types.Suffix)>([.. type.Path, (name, SymbolDescriptor.Types.Suffix.Term)])),
                        };
                        var d = New(kind, name, type, lib, file, path);
                        d.IsStatic = f[3] == "1";
                        d.IsGetter = f[1] == "G";
                        d.TypeName = kind == DeclKind.Constructor ? type.Name : f[4].Length > 0 ? f[4] : null;
                        break;
                    }
            }
        }

        return result;
    }

    private static List<string>? Split(string s) => s.Length == 0 ? null : [.. s.Split(',')];

    private static Decl New(DeclKind kind, string name, Decl? parent, ExtLibrary lib, string file, List<(string, SymbolDescriptor.Types.Suffix)> path)
    {
        var d = new Decl(kind, name, parent, default, file)
        {
            External = new ExternalPackage(lib.Package.Name, lib.Package.Version),
            Path = path,
        };
        parent?.Members.Add(d);
        return d;
    }
}
