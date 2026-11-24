using System.Text;
using Scip.Indexing;

namespace Scip.Indexer.Ruby;

/// <summary>
/// Native C# indexer for Ruby, ported from the SCIP emission of <c>scip-ruby</c> (a Sorbet fork). It does not run
/// Sorbet, Ruby or any other external tool: it lexes and structurally parses Ruby itself. See README.md for what is
/// and is not covered compared to upstream.
/// </summary>
public sealed class RubyIndexer : IIndexer
{
    private static readonly string[] Extensions = [".rb", ".rbi", ".rake", ".gemspec"];

    private static readonly HashSet<string> SkippedDirectories =
        [".git", ".bundle", "node_modules", "vendor", "tmp", "coverage", "log", ".hg", ".svn"];

    public string Name => "ruby";

    public string Languages => "Ruby";

    public string UpstreamProject => "scip-ruby";

    public async Task<int> IndexAsync(IndexerOptions options, CancellationToken cancellationToken)
    {
        string? gemMetadata = null;
        var args = options.Arguments;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--gem-metadata" && i + 1 < args.Count)
            {
                gemMetadata = args[++i];
            }
            else if (args[i].StartsWith("--gem-metadata=", StringComparison.Ordinal))
            {
                gemMetadata = args[i]["--gem-metadata=".Length..];
            }
        }

        var root = Path.GetFullPath(options.ProjectRoot);
        GemMetadata gem;
        if (gemMetadata is not null)
        {
            gem = GemMetadata.TryParse(gemMetadata) ?? throw new ArgumentException($"--gem-metadata expects name@version, got '{gemMetadata}'");
        }
        else
        {
            var (inferred, errors) = GemMetadataReader.ReadFromDirectory(root);
            gem = inferred;
            foreach (var e in errors)
            {
                await Console.Error.WriteLineAsync($"{(e.Kind == GemMetadataErrorKind.Error ? "error" : "warning")}: {e.Message}".AsMemory(), cancellationToken);
            }
        }

        var files = EnumerateRubyFiles(root).ToList();
        var documents = Index(root, files, gem, cancellationToken, out _, out var externals);

        var output = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await using var stream = File.Create(output);
        using var writer = new IndexWriter(stream, leaveOpen: true);
        writer.WriteMetadata(new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ToolInfo = new ToolInfo { Name = "scip-ruby", Version = "0.1.0-dotnet", Arguments = { args } },
            ProjectRoot = new Uri(root.TrimEnd('/') + "/").AbsoluteUri.TrimEnd('/'),
            TextDocumentEncoding = TextEncoding.Utf8,
        });
        foreach (var doc in documents)
        {
            writer.WriteDocument(doc);
        }

        foreach (var ext in externals)
        {
            writer.WriteExternalSymbol(ext);
        }

        return 0;
    }

    /// <summary>Indexes the given files (paths relative to <paramref name="root"/>) into SCIP documents.</summary>
    public static List<Document> Index(string root, IEnumerable<string> relativePaths, GemMetadata gem, CancellationToken ct, out int symbolCount)
        => Index(root, relativePaths, gem, ct, out symbolCount, out _);

    /// <summary>As above, also returning <c>external_symbols</c> for referenced symbols defined in no document (core/stdlib/unresolved).</summary>
    public static List<Document> Index(string root, IEnumerable<string> relativePaths, GemMetadata gem, CancellationToken ct, out int symbolCount, out List<SymbolInformation> externals)
    {
        var project = new RubyProject(gem);
        var models = new List<DocModel>();
        foreach (var rel in relativePaths)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = File.ReadAllText(Path.Combine(root, rel));
                var model = new DocModel(rel.Replace('\\', '/'), text);
                RubyWalker.Walk(project, model);
                models.Add(model);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"warning: skipping {rel}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return Build(project, models, out symbolCount, out externals);
    }

    /// <summary>Indexes in-memory sources (used by tests).</summary>
    public static List<Document> IndexSources(IEnumerable<(string Path, string Text)> sources, GemMetadata gem)
        => IndexSources(sources, gem, out _);

    /// <summary>As above, also returning <c>external_symbols</c>.</summary>
    public static List<Document> IndexSources(IEnumerable<(string Path, string Text)> sources, GemMetadata gem, out List<SymbolInformation> externals)
    {
        var project = new RubyProject(gem);
        var models = new List<DocModel>();
        foreach (var (path, text) in sources)
        {
            try
            {
                var model = new DocModel(path, text);
                RubyWalker.Walk(project, model);
                models.Add(model);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"warning: skipping {path}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return Build(project, models, out _, out externals);
    }

    private static List<Document> Build(RubyProject project, List<DocModel> models, out int symbolCount, out List<SymbolInformation> externals)
    {
        project.InvalidateAncestors();
        var docs = new List<Document>();
        var total = 0;
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new Dictionary<string, SymbolInformation.Types.Kind>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            var lines = model.Text.Split('\n');
            var doc = new Document
            {
                RelativePath = model.RelativePath,
                Language = "Ruby",
                PositionEncoding = PositionEncoding.Utf8CodeUnitOffsetFromLineStart,
            };
            var infos = new Dictionary<string, SymbolInformation>(StringComparer.Ordinal);
            foreach (var occ in model.Occurrences.OrderBy(o => o.Line).ThenBy(o => o.Col))
            {
                var symbol = occ.Symbol ?? occ.Resolver?.Invoke(project);
                if (symbol is null)
                {
                    continue;
                }

                var o = new Occurrence { Symbol = symbol, SymbolRoles = occ.Roles };
                o.SingleLineRange = new SingleLineRange
                {
                    Line = occ.Line,
                    StartCharacter = Utf8Col(lines, occ.Line, occ.Col),
                    EndCharacter = Utf8Col(lines, occ.Line, occ.EndCol),
                };
                if (occ.Enclosing is { } e)
                {
                    if (e[0] == e[2])
                    {
                        o.SingleLineEnclosingRange = new SingleLineRange { Line = e[0], StartCharacter = Utf8Col(lines, e[0], e[1]), EndCharacter = Utf8Col(lines, e[2], e[3]) };
                    }
                    else
                    {
                        o.MultiLineEnclosingRange = new MultiLineRange { StartLine = e[0], StartCharacter = Utf8Col(lines, e[0], e[1]), EndLine = e[2], EndCharacter = Utf8Col(lines, e[2], e[3]) };
                    }
                }

                doc.Occurrences.Add(o);
                var isLocal = symbol.StartsWith("local ", StringComparison.Ordinal);
                var isDef = (occ.Roles & (int)SymbolRole.Definition) != 0;
                if (isLocal || isDef)
                {
                    if (!infos.TryGetValue(symbol, out var existing))
                    {
                        infos[symbol] = new SymbolInformation { Symbol = symbol, Kind = occ.Kind };
                    }
                    else if (isDef && existing.Kind == SymbolInformation.Types.Kind.UnspecifiedKind)
                    {
                        existing.Kind = occ.Kind;
                    }

                    if (isDef && !isLocal)
                    {
                        defined.Add(symbol);
                    }
                }
                else
                {
                    referenced.TryAdd(symbol, GuessKind(symbol));
                }
            }

            doc.Symbols.AddRange(infos.Values);
            total += infos.Count;
            docs.Add(doc);
        }

        symbolCount = total;
        externals = [];
        foreach (var (sym, kind) in referenced.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!defined.Contains(sym))
            {
                externals.Add(new SymbolInformation { Symbol = sym, Kind = kind });
            }
        }

        return docs;
    }

    private static SymbolInformation.Types.Kind GuessKind(string symbol) => symbol switch
    {
        _ when symbol.EndsWith(").", StringComparison.Ordinal) => SymbolInformation.Types.Kind.Method,
        _ when symbol.EndsWith('#') => SymbolInformation.Types.Kind.Class,
        _ when symbol.EndsWith('.') => SymbolInformation.Types.Kind.Constant,
        _ => SymbolInformation.Types.Kind.UnspecifiedKind,
    };

    private static int Utf8Col(string[] lines, int line, int utf16Col)
    {
        if (line >= lines.Length)
        {
            return utf16Col;
        }

        var text = lines[line];
        return Encoding.UTF8.GetByteCount(text.AsSpan(0, Math.Min(utf16Col, text.Length)));
    }

    private static List<string> EnumerateRubyFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        var results = new List<string>();
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                var rel = Path.GetRelativePath(root, sub).Replace('\\', '/');
                // Externally-owned RBIs would need the per-file gem mapping of upstream; see README.
                if (SkippedDirectories.Contains(name) || rel is "sorbet/rbi/gems" or "sorbet/rbi/annotations" or "sorbet/rbi/dsl")
                {
                    continue;
                }

                stack.Push(sub);
            }

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (Extensions.Contains(Path.GetExtension(file), StringComparer.Ordinal))
                {
                    results.Add(Path.GetRelativePath(root, file));
                }
            }
        }

        results.Sort(StringComparer.Ordinal);
        return results;
    }
}
