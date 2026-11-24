using System.Text.Json;
using Scip.Indexer.TypeScript.Project;
using Scip.Indexer.TypeScript.Semantics;
using Scip.Indexer.TypeScript.Syntax;
using Scip.Indexing;

namespace Scip.Indexer.TypeScript;

/// <summary>
/// Native C# port of <c>scip-typescript</c> (port of <c>main.ts</c> + <c>ProjectIndexer.ts</c>): parses TypeScript and
/// JavaScript with <see cref="Parser"/>, resolves names with <see cref="Checker"/> and emits documents with
/// <see cref="FileIndexer"/>. No Node.js or TypeScript compiler is involved. See README.md for coverage and gaps.
/// </summary>
public sealed class TypeScriptIndexer : IIndexer
{
    /// <summary>Version reported in <c>Metadata.tool_info</c> (the upstream release this port tracks).</summary>
    public const string ToolVersion = "0.6.2";

    public string Name => "typescript";

    public string Languages => "TypeScript, JavaScript";

    public string UpstreamProject => "scip-typescript";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var parsed = TypeScriptOptions.Parse(options.ProjectRoot, options.OutputPath, options.Arguments, out var error);
        if (parsed is null)
        {
            await Console.Error.WriteLineAsync("error: " + error);
            return 1;
        }

        return await IndexAsync(parsed, Console.Out, Console.Error, cancellationToken);
    }

    public Task<int> IndexAsync(TypeScriptOptions options, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        var projects = new List<string>(options.Projects);
        try
        {
            if (options.NpmWorkspaces) { projects.AddRange(Workspaces.FromPackageJson(options.Cwd)); }
            else if (options.YarnWorkspaces || options.YarnBerryWorkspaces) { projects.AddRange(Workspaces.FromPackageJson(options.Cwd, requireName: true)); }
            else if (options.PnpmWorkspaces) { projects.AddRange(Workspaces.FromPnpm(options.Cwd)); }
            else if (projects.Count == 0) { projects.Add(options.Cwd); }
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            stderr.WriteLine($"error: cannot list workspaces: {e.Message}");
            return Task.FromResult(1);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(options.Output)!);
        var documentCount = 0;
        var definedSymbols = new HashSet<string>(StringComparer.Ordinal);
        var referencedSymbols = new HashSet<string>(StringComparer.Ordinal);
        using (var file = new FileStream(options.Output, FileMode.Create, FileAccess.Write))
        using (var writer = new IndexWriter(file))
        {
            writer.WriteMetadata(new Metadata
            {
                ProjectRoot = new Uri(Path.TrimEndingDirectorySeparator(options.Cwd) + Path.DirectorySeparatorChar).ToString().TrimEnd('/'),
                TextDocumentEncoding = TextEncoding.Utf8,
                ToolInfo = new ToolInfo { Name = "scip-typescript", Version = ToolVersion },
            });
            var indexedFiles = new HashSet<string>(StringComparer.Ordinal);
            var indexedProjects = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in projects)
            {
                var root = Path.GetFullPath(project, options.Cwd).Replace('\\', '/');
                IndexSingleProject(options, root, project == "." ? options.Cwd : project, indexedProjects, indexedFiles, writer, stdout, stderr, ref documentCount, definedSymbols, referencedSymbols, cancellationToken);
            }

            // Global symbols referenced but never defined in this index (e.g. unresolved externals) need an external
            // SymbolInformation so that every occurrence symbol is documented.
            foreach (var symbol in referencedSymbols.Where(s => !definedSymbols.Contains(s)).Order(StringComparer.Ordinal))
            {
                writer.WriteExternalSymbol(new SymbolInformation { Symbol = symbol });
            }
        }

        if (documentCount == 0)
        {
            File.Delete(options.Output);
            var shown = JsonSerializer.Serialize(projects);
            stdout.WriteLine($"error: no files got indexed. To fix this problem, make sure that the TypeScript projects {shown} contain input files or reference other projects.");
            return Task.FromResult(1);
        }

        stdout.WriteLine($"done {options.Output}");
        return Task.FromResult(0);
    }

    private static void IndexSingleProject(
        TypeScriptOptions options, string projectRoot, string displayName, HashSet<string> indexedProjects, HashSet<string> indexedFiles,
        IndexWriter writer, TextWriter stdout, TextWriter stderr, ref int documentCount,
        HashSet<string> definedSymbols, HashSet<string> referencedSymbols, CancellationToken ct)
    {
        if (!indexedProjects.Add(projectRoot)) { return; }
        string configFile;
        if (Directory.Exists(projectRoot))
        {
            configFile = Path.Combine(projectRoot, "tsconfig.json");
            if (!File.Exists(configFile))
            {
                if (options.InferTsconfig || options.Projects.Count == 0 && !options.NpmWorkspaces && !options.YarnWorkspaces && !options.YarnBerryWorkspaces && !options.PnpmWorkspaces)
                {
                    if (!options.InferTsconfig) { stderr.WriteLine($"info: no tsconfig.json in {displayName}; inferring one (equivalent to --infer-tsconfig)"); }
                    File.WriteAllText(configFile, TsConfig.InferTsconfig(projectRoot));
                }
                else
                {
                    stderr.WriteLine($"- {displayName} (missing tsconfig.json)");
                    return;
                }
            }
        }
        else
        {
            configFile = projectRoot;
            if (!File.Exists(configFile))
            {
                stderr.WriteLine($"- {displayName} (missing tsconfig.json)");
                return;
            }
        }

        var config = TsConfig.Load(configFile, out var error);
        if (config is null)
        {
            stdout.WriteLine(error);
            return;
        }

        foreach (var reference in config.References)
        {
            IndexSingleProject(options, reference, reference, indexedProjects, indexedFiles, writer, stdout, stderr, ref documentCount, definedSymbols, referencedSymbols, ct);
        }

        if (config.FileNames.Count == 0) { return; }
        var started = DateTime.UtcNow;
        var program = new TsProgram { Warn = stderr.WriteLine, Libs = LibStore.For(config), CustomConditions = config.CustomConditions ?? [], Paths = config.Paths, PathsBase = config.PathsBase };
        var checker = new Checker(program);
        var packages = new Packages(projectRoot);
        var symbolCache = new Dictionary<Node, ScipSymbol>();
        var constructors = new Dictionary<Node, bool>();
        var projectFileCount = 0;
        var emitted = 0;
        var projectFiles = new HashSet<string>(config.FileNames.Select(TsProgram.Normalize), StringComparer.Ordinal);
        var ordered = program.ProgramOrder(config.FileNames).Where(projectFiles.Contains).ToList();
        var orderedSet = new HashSet<string>(ordered, StringComparer.Ordinal);
        foreach (var fileName in ordered.Concat(config.FileNames.Select(TsProgram.Normalize).Where(f => !orderedSet.Contains(f))))
        {
            ct.ThrowIfCancellationRequested();
            projectFileCount++;
            if (indexedFiles.Contains(fileName)) { continue; }
            var sourceFile = program.GetFile(fileName);
            if (sourceFile is null) { continue; }
            var document = new Document
            {
                RelativePath = Path.GetRelativePath(options.Cwd, fileName).Replace('\\', '/'),
                PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
            };
            var language = LanguageForFileName(fileName);
            if (language is not null) { document.Language = language; }
            var visitor = new FileIndexer(checker, options.Cwd, options.MaxFileByteSize, document, symbolCache, constructors, packages, sourceFile, stdout.WriteLine);
            try
            {
                visitor.Index();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                stderr.WriteLine($"unexpected error indexing {fileName}: {e}");
            }

            if (document.Occurrences.Count > 0)
            {
                foreach (var info in document.Symbols)
                {
                    definedSymbols.Add(info.Symbol);
                    // The same global symbol can be declared in several documents (e.g. duplicated mock packages);
                    // `scip lint` warns about a (symbol, related symbol) pair repeated across documents. The pair is
                    // tracked in `definedSymbols` as "symbol\0related" (a NUL never occurs in a real symbol).
                    for (var i = info.Relationships.Count - 1; i >= 0; i--)
                    {
                        if (!definedSymbols.Add(info.Symbol + "\0" + info.Relationships[i].Symbol)) { info.Relationships.RemoveAt(i); }
                    }
                }

                foreach (var o in document.Occurrences)
                {
                    if (!o.Symbol.StartsWith("local ", StringComparison.Ordinal)) { referencedSymbols.Add(o.Symbol); }
                }

                foreach (var info in document.Symbols)
                {
                    foreach (var r in info.Relationships.Where(r => !r.Symbol.StartsWith("local ", StringComparison.Ordinal))) { referencedSymbols.Add(r.Symbol); }
                }

                writer.WriteDocument(document);
                documentCount++;
                emitted++;
                indexedFiles.Add(fileName);
            }
        }

        if (emitted == 0 && projectFileCount > 0 && indexedFiles.Count == 0)
        {
            stderr.WriteLine($"error: no indexable files in project '{displayName}'");
        }

        stdout.WriteLine($"+ {displayName} ({(int)(DateTime.UtcNow - started).TotalMilliseconds}ms)");
    }

    /// <summary>Port of <c>languageForFileName</c>: exact names of SCIP's Language enum.</summary>
    public static string? LanguageForFileName(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".tsx" => "TypeScriptReact",
        ".ts" or ".mts" or ".cts" => "TypeScript",
        ".jsx" => "JavaScriptReact",
        ".js" or ".mjs" or ".cjs" => "JavaScript",
        ".json" => "JSON",
        _ => null,
    };
}
