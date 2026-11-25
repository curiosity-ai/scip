using System.Text.RegularExpressions;
using ScipSharp.Indexer.TypeScript.Project;
using ScipSharp.Indexer.TypeScript.Syntax;

namespace ScipSharp.Indexer.TypeScript.Semantics;

/// <summary>
/// The built-in global declarations (TypeScript's default library, <c>lib.*.d.ts</c>), embedded as resources and parsed
/// lazily. Symbols declared there are named <c>scip-typescript npm typescript &lt;version&gt; lib/`lib.es5.d.ts`/Array#</c>
/// exactly as the real indexer names them, so references to <c>Array</c>, <c>Promise</c>, DOM types, ... link up.
/// </summary>
public sealed class LibSet
{
    internal LibSet(List<TsSourceFile> files, Dictionary<string, List<Node>> globals)
    {
        Files = files;
        Globals = globals;
    }

    public List<TsSourceFile> Files { get; }

    /// <summary>Top-level declarations of every lib file by name (interfaces/vars/namespaces merge into one list).</summary>
    public Dictionary<string, List<Node>> Globals { get; }

    public static LibSet Empty { get; } = new([], new Dictionary<string, List<Node>>());

    public List<Node>? Lookup(string name, Meaning meaning) =>
        Globals.TryGetValue(name, out var decls) && Binder.HasMeaning(decls, meaning) ? decls : null;

    /// <summary>The first declaration list of a global used as a type, e.g. <c>String</c> for <c>string</c>.</summary>
    public List<Node> TypeDecls(string name) => Globals.TryGetValue(name, out var decls) ? decls.Where(d => d.Kind is SK.InterfaceDeclaration or SK.ClassDeclaration).ToList() : [];
}

public static partial class LibStore
{
    /// <summary>The npm <c>typescript</c> version the embedded lib files come from (used in symbol names).</summary>
    public const string Version = "6.0.3";

    public static TsProgram Program { get; } = new();

    private static readonly object Gate = new();
    private static readonly Dictionary<string, TsSourceFile?> Files = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, LibSet> Sets = new(StringComparer.Ordinal);

    [GeneratedRegex(@"///\s*<reference\s+lib=""([^""]+)""\s*/>", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceLib();

    private static string FileNameFor(string libName) => libName switch
    {
        "es6" => "lib.es6.d.ts",
        "es7" => "lib.es2016.d.ts",
        "es5" => "lib.es5.d.ts",
        "lib" => "lib.d.ts",
        _ => $"lib.{libName}.d.ts",
    };

    private static string DefaultFor(string? target) => (target ?? "es2025").ToLowerInvariant() switch
    {
        "es3" or "es5" => "lib.d.ts",
        "es6" or "es2015" => "lib.es6.d.ts",
        "esnext" => "lib.esnext.full.d.ts",
        var t => $"lib.{t}.full.d.ts",
    };

    public static LibSet For(ParsedConfig config)
    {
        if (config.NoLib) { return LibSet.Empty; }
        var roots = config.Lib is { Count: > 0 } ? config.Lib.Select(l => FileNameFor(l.ToLowerInvariant())).ToList() : [DefaultFor(config.Target)];
        var key = string.Join("|", roots);
        lock (Gate)
        {
            if (Sets.TryGetValue(key, out var cached)) { return cached; }
            var ordered = new List<TsSourceFile>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in roots) { Load(root, ordered, seen); }
            var globals = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
            foreach (var file in ordered)
            {
                foreach (var (name, decls) in Program.BinderFor(file).FileScope.Table)
                {
                    if (!globals.TryGetValue(name, out var list)) { globals[name] = list = []; }
                    list.AddRange(decls);
                }
            }

            var set = new LibSet(ordered, globals);
            // Lib files resolve names through the set they belong to (the one with the most files wins when sets differ).
            foreach (var file in ordered)
            {
                if (file.Libs is null || file.Libs.Files.Count < ordered.Count) { file.Libs = set; }
            }

            return Sets[key] = set;
        }
    }

    private static void Load(string fileName, List<TsSourceFile> ordered, HashSet<string> seen)
    {
        if (!seen.Add(fileName)) { return; }
        var file = Parse(fileName);
        if (file is null) { return; }
        foreach (Match m in ReferenceLib().Matches(file.Text)) { Load(FileNameFor(m.Groups[1].Value.ToLowerInvariant()), ordered, seen); }
        ordered.Add(file);
    }

    private static TsSourceFile? Parse(string fileName)
    {
        if (Files.TryGetValue(fileName, out var existing)) { return existing; }
        TsSourceFile? parsed = null;
        using var stream = typeof(LibStore).Assembly.GetManifestResourceStream("tslib/" + fileName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            parsed = Parser.Parse("/$typescript/lib/" + fileName, reader.ReadToEnd());
            parsed.IsLib = true;
        }

        Files[fileName] = parsed;
        return parsed;
    }

    /// <summary>Package symbol path of a lib file: <c>typescript 6.0.3 lib/`lib.es5.d.ts`/</c>.</summary>
    public static ScipSymbol SymbolOf(TsSourceFile file)
    {
        var name = file.FileName[(file.FileName.LastIndexOf('/') + 1)..];
        var package = ScipSymbol.Package("typescript", Version);
        return ScipSymbol.Global(ScipSymbol.Global(package, SymbolDescriptor.Types.Suffix.Namespace, "lib"), SymbolDescriptor.Types.Suffix.Namespace, name);
    }
}
