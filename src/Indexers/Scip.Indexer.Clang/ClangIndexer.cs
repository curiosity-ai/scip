using Scip.Indexing;

namespace Scip.Indexer.Clang;

/// <summary>
/// Port of <c>scip-clang</c> implemented natively in C#: a preprocessor, lexer and tolerant C++ parser with approximate
/// name resolution (no clang, libclang or any child process). See README.md in this project for the design and gaps.
/// </summary>
/// <remarks>
/// Arguments: <c>--compdb-path FILE</c>, <c>--jobs N</c>.
/// </remarks>
public sealed partial class ClangIndexer : IIndexer
{
    private static readonly string[] CompdbCandidates = ["compile_commands.json", "build/compile_commands.json"];
    private static readonly string[] SourceExtensions = [".c", ".cc", ".cpp", ".cxx", ".c++", ".cu"];

    public string Name => "clang";

    public string Languages => "C, C++";

    public string UpstreamProject => "scip-clang";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(options.ProjectRoot);
        string? compdb = null;
        var jobs = Environment.ProcessorCount;
        for (var i = 0; i < options.Arguments.Count; i++)
        {
            switch (options.Arguments[i])
            {
                case "--compdb-path" when i + 1 < options.Arguments.Count: compdb = options.Arguments[++i]; break;
                case "--jobs" when i + 1 < options.Arguments.Count && int.TryParse(options.Arguments[i + 1], out var j): jobs = Math.Max(1, j); i++; break;
            }
        }

        IReadOnlyList<CompileCommand> commands;
        compdb ??= CompdbCandidates.Select(p => Path.Combine(root, p)).FirstOrDefault(File.Exists);
        if (compdb is not null)
        {
            commands = CompilationDatabase.Load(compdb);
        }
        else
        {
            commands = DiscoverSources(root);
            Console.Error.WriteLine($"scip-clang: no compile_commands.json found; indexing {commands.Count} discovered source files with default flags.");
        }

        var merged = new Dictionary<string, DocumentAccumulator>();
        var external = new Dictionary<string, SymbolInformation>();
        var failures = 0;
        var gate = new object();
        var seen = new HashSet<string>();
        var work = commands.Where(c => seen.Add(c.FullPath + "\0" + string.Join('\0', c.Arguments))).ToList();

        await Parallel.ForEachAsync(work, new ParallelOptions { MaxDegreeOfParallelism = jobs, CancellationToken = cancellationToken }, async (cmd, ct) =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var tu = await Task.Run(() => TranslationUnitIndexer.Index(root, cmd), ct);
                lock (gate)
                {
                    Merge(merged, external, tu);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Interlocked.Increment(ref failures);
                Console.Error.WriteLine($"scip-clang: failed to index {cmd.File}: {e.Message}");
            }
        });

        AddFileDocuments(root, merged);
        Write(options.OutputPath, root, merged, external);
        return failures == 0 ? 0 : 1;
    }

    private static bool IsCSource(string file) => file.EndsWith(".c", StringComparison.OrdinalIgnoreCase);

    private static void Merge(Dictionary<string, DocumentAccumulator> merged, Dictionary<string, SymbolInformation> external, TranslationUnitResult tu)
    {
        foreach (var (path, doc) in tu.Documents)
        {
            if (!merged.TryGetValue(path, out var target))
            {
                merged[path] = target = new DocumentAccumulator();
            }

            target.Occurrences.UnionWith(doc.Occurrences);
            foreach (var (sym, info) in doc.Symbols)
            {
                target.Symbols.TryAdd(sym, info);
            }
        }

        foreach (var (sym, info) in tu.ExternalSymbols)
        {
            external.TryAdd(sym, info);
        }
    }

    /// <summary>Each document gets a definition of its own <c>&lt;file&gt;/path</c> symbol (as upstream does).</summary>
    private static void AddFileDocuments(string root, Dictionary<string, DocumentAccumulator> merged)
    {
        foreach (var (path, doc) in merged)
        {
            var sym = TranslationUnitIndexer.FileSymbol(path);
            doc.Occurrences.Add((0, 0, 0, sym, (int)SymbolRole.Definition));
        }
    }

    private static void Write(string output, string root, Dictionary<string, DocumentAccumulator> merged, Dictionary<string, SymbolInformation> external)
    {
        using var stream = File.Create(output);
        using var writer = new IndexWriter(stream);
        writer.WriteMetadata(new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(root.EndsWith('/') ? root : root + "/").AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = "scip-clang", Version = "0.1.0-dotnet" },
        });

        foreach (var (path, acc) in merged.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            var doc = new Document
            {
                RelativePath = path,
                Language = IsCSource(path) ? "c" : "cpp",
                PositionEncoding = PositionEncoding.Utf8CodeUnitOffsetFromLineStart,
            };
            foreach (var o in acc.Occurrences.OrderBy(static o => o.Line).ThenBy(static o => o.StartCol).ThenBy(static o => o.Symbol, StringComparer.Ordinal))
            {
                var occ = new Occurrence { Symbol = o.Symbol, SymbolRoles = o.Roles };
                occ.SingleLineRange = new SingleLineRange { Line = o.Line, StartCharacter = o.StartCol, EndCharacter = o.EndCol };
                doc.Occurrences.Add(occ);
            }

            doc.Symbols.AddRange(acc.Symbols.Values.OrderBy(static s => s.Symbol, StringComparer.Ordinal));
            writer.WriteDocument(doc);
        }

        foreach (var info in external.Values.OrderBy(static s => s.Symbol, StringComparer.Ordinal))
        {
            writer.WriteExternalSymbol(info);
        }
    }

    private static List<CompileCommand> DiscoverSources(string root)
    {
        var skip = new[] { "/.git/", "/node_modules/", "/artifacts/", "/bin/", "/obj/" };
        return [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => SourceExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                && !skip.Any(s => f.Replace('\\', '/').Contains(s, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .Select(f => new CompileCommand(root, f, ["cc", "-I" + root]))];
    }
}
