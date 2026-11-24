using System.Text.Json;
using Scip.Indexer.TypeScript.Syntax;

namespace Scip.Indexer.TypeScript.Semantics;

/// <summary>A SCIP symbol string built through <see cref="Scip.SymbolFormatter"/> (port of <c>ScipSymbol.ts</c>).</summary>
public sealed class ScipSymbol
{
    private readonly Symbol? _symbol;

    private ScipSymbol(Symbol? symbol, string value)
    {
        _symbol = symbol;
        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => Value.Length == 0;

    public bool IsLocal => Value.StartsWith("local ", StringComparison.Ordinal);

    public static ScipSymbol Empty { get; } = new(null, "");

    public static ScipSymbol Local(int counter) => new(null, "local " + counter);

    public static ScipSymbol Package(string name, string version)
    {
        var symbol = new Symbol
        {
            Scheme = "scip-typescript",
            Package = new Package { Manager = "npm", Name = name.Length == 0 ? "." : name, Version = version.Length == 0 ? "." : version },
        };
        return new ScipSymbol(symbol, SymbolFormatter.Verbose.Format(symbol));
    }

    public static ScipSymbol AnonymousPackage() => Package(".", ".");

    public static ScipSymbol Global(ScipSymbol owner, SymbolDescriptor.Types.Suffix suffix, string name)
    {
        var symbol = owner._symbol!.Clone();
        symbol.Descriptors.Add(new SymbolDescriptor { Name = name, Suffix = suffix });
        return new ScipSymbol(symbol, SymbolFormatter.Verbose.Format(symbol));
    }
}

/// <summary>Port of <c>Packages.ts</c>: maps a file to the npm package that owns it.</summary>
public sealed class Packages
{
    private readonly Dictionary<string, ScipSymbol> _cache = new(StringComparer.Ordinal);
    private readonly string _projectRoot;

    public Packages(string projectRoot)
    {
        var absolute = Path.GetFullPath(projectRoot);
        if (File.Exists(absolute)) { absolute = Path.GetDirectoryName(absolute)!; }
        _projectRoot = absolute.Replace('\\', '/').TrimEnd('/');
    }

    public ScipSymbol Symbol(string fileName)
    {
        var owner = DirectorySymbol(DirName(fileName));
        return ScipSymbol.Global(owner.IsEmpty ? ScipSymbol.AnonymousPackage() : owner, SymbolDescriptor.Types.Suffix.Namespace, BaseName(fileName));
    }

    private static string DirName(string p)
    {
        var i = p.LastIndexOf('/');
        return i <= 0 ? "/" : p[..i];
    }

    private static string BaseName(string p) => p[(p.LastIndexOf('/') + 1)..];

    private ScipSymbol DirectorySymbol(string directory)
    {
        if (_cache.TryGetValue(directory, out var cached)) { return cached; }
        var packageJsonPath = directory.TrimEnd('/') + "/package.json";
        try
        {
            if (File.Exists(packageJsonPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                var root = doc.RootElement;
                var name = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                var version = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                if (name is not null) { return Cache(directory, ScipSymbol.Package(name, version ?? "HEAD")); }
            }
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"error parsing {packageJsonPath}");
            return Cache(directory, ScipSymbol.AnonymousPackage());
        }

        if (directory == _projectRoot) { return Cache(directory, ScipSymbol.AnonymousPackage()); }
        var parent = DirName(directory);
        if (parent == directory || directory == "/") { return Cache(directory, ScipSymbol.Empty); }
        var owner = DirectorySymbol(parent);
        if (owner.IsEmpty) { return Cache(directory, owner); }
        return Cache(directory, ScipSymbol.Global(owner, SymbolDescriptor.Types.Suffix.Namespace, BaseName(directory)));
    }

    private ScipSymbol Cache(string directory, ScipSymbol symbol)
    {
        _cache[directory] = symbol;
        return symbol;
    }
}

/// <summary>Minimal JSDoc handling: description text, <c>@param</c> and <c>@deprecated</c>.</summary>
public static class JsDoc
{
    private static List<string> Lines(string raw)
    {
        var body = raw;
        if (body.StartsWith("/**", StringComparison.Ordinal)) { body = body[3..]; }
        if (body.EndsWith("*/", StringComparison.Ordinal)) { body = body[..^2]; }
        var lines = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            var t = line.TrimEnd('\r').TrimStart();
            if (t.StartsWith('*')) { t = t[1..]; if (t.StartsWith(' ')) { t = t[1..]; } }
            lines.Add(t.TrimEnd());
        }

        return lines;
    }

    public static string Description(string raw)
    {
        var lines = Lines(raw);
        var end = lines.FindIndex(l => l.TrimStart().StartsWith('@'));
        if (end >= 0) { lines = lines[..end]; }
        var text = string.Join("\n", lines).Trim();
        return NormalizeLinks(text);
    }

    /// <summary>TypeScript renders inline links as <c>{@link name }</c>.</summary>
    public static string NormalizeLinks(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\{@(link|linkcode|linkplain) ([^}\s][^}]*?)\s*\}", "{@$1 $2 }");

    public static IEnumerable<(string Tag, string Text)> Tags(string raw)
    {
        var lines = Lines(raw);
        string? tag = null;
        var text = new List<string>();
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith('@'))
            {
                if (tag is not null) { yield return (tag, NormalizeLinks(string.Join("\n", text).Trim())); }
                var t = line.TrimStart()[1..];
                var space = t.IndexOfAny([' ', '\t']);
                tag = space < 0 ? t : t[..space];
                text = [space < 0 ? "" : t[(space + 1)..]];
            }
            else if (tag is not null)
            {
                text.Add(line);
            }
        }

        if (tag is not null) { yield return (tag, NormalizeLinks(string.Join("\n", text).Trim())); }
    }
}
