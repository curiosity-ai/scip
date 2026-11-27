using System.Reflection;
using ScipSharp.Indexer.Cobol.Analysis;
using ScipSharp.Indexer.Cobol.Project;
using ScipSharp.Indexer.Cobol.Syntax;

namespace ScipSharp.Indexer.Cobol;

/// <summary>Settings of a COBOL indexing run.</summary>
public sealed record CobolIndexerOptions
{
    public static readonly IReadOnlyList<string> DefaultProgramExtensions = ["cbl", "cob", "cobol", "pco", "sqb", "ccp"];

    public static readonly IReadOnlyList<string> DefaultCopybookExtensions = ["cpy", "copy", "cpb", "cbk", "inc", "cblcpy"];

    public SourceFormat Format { get; init; } = SourceFormat.Auto;

    /// <summary>Treat <c>D</c> lines (indicator column) as code instead of comments.</summary>
    public bool DebugLines { get; init; }

    /// <summary>Copybook search directories, relative to the project root (or absolute).</summary>
    public IReadOnlyList<string> CopybookDirectories { get; init; } = [];

    public IReadOnlyList<string> ProgramExtensions { get; init; } = DefaultProgramExtensions;

    public IReadOnlyList<string> CopybookExtensions { get; init; } = DefaultCopybookExtensions;

    /// <summary>Index copybooks that no program copies on their own.</summary>
    public bool IndexUnusedCopybooks { get; init; } = true;

    /// <summary>Package name of the symbols; defaults to the project directory name.</summary>
    public string? PackageName { get; init; }

    public string PackageVersion { get; init; } = "0.0.0";

    /// <summary>Directories (relative to the root) to skip.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>Arguments recorded in the index metadata.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];
}

/// <summary>
/// Indexes every COBOL program below a root directory. Two passes: the first collects program-ids, entry points and the
/// set of copybooks reached by COPY; the second resolves references and streams one document per program. Copybook
/// documents (whose occurrences come from every program that copies them) are emitted last.
/// </summary>
public sealed class CobolProjectIndexer
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase) { "node_modules", "bin", "obj", "target" };

    private readonly string _root;
    private readonly CobolIndexerOptions _options;
    private readonly Action<string>? _warn;
    private readonly SourceFiles _files;
    private readonly Dictionary<int, List<Token>> _lexCache = [];
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

    public CobolProjectIndexer(string root, CobolIndexerOptions? options = null, Action<string>? warn = null)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (_root.Length == 0)
        {
            _root = Path.DirectorySeparatorChar.ToString();
        }

        _options = options ?? new CobolIndexerOptions();
        _warn = warn;
        _files = new SourceFiles(_root);
    }

    /// <summary>Files that failed to index (the run continues without them); empty on a healthy run.</summary>
    public List<string> Errors { get; } = [];

    public static string ToolVersion =>
        typeof(CobolProjectIndexer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public Metadata CreateMetadata()
    {
        var meta = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(_root + Path.DirectorySeparatorChar).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = "scip-cobol", Version = ToolVersion },
        };
        meta.ToolInfo.Arguments.AddRange(_options.Arguments);
        return meta;
    }

    /// <summary>Builds the whole index in memory (tests, small projects).</summary>
    public Index Build()
    {
        var index = new Index();
        var external = Run(index.Documents.Add, m => index.Metadata = m);
        index.ExternalSymbols.AddRange(external);
        return index;
    }

    /// <summary>Indexes the project, handing every finished document to <paramref name="onDocument"/>; returns the external symbols.</summary>
    public List<SymbolInformation> Run(Action<Document> onDocument, Action<Metadata>? onMetadata = null)
    {
        onMetadata?.Invoke(CreateMetadata());
        var projectFiles = EnumerateFiles().ToList();
        var resolver = new CopybookResolver(_root, _options.CopybookDirectories, _options.CopybookExtensions, _options.ProgramExtensions, projectFiles);
        var programExt = Extensions(_options.ProgramExtensions);
        var copybookExt = Extensions(_options.CopybookExtensions);
        var programFiles = projectFiles.Where(f => programExt.Contains(Path.GetExtension(f))).Select(_files.GetOrAdd).ToList();
        var copybookFiles = projectFiles.Where(f => copybookExt.Contains(Path.GetExtension(f))).Select(_files.GetOrAdd).ToList();
        var packageName = _options.PackageName is { Length: > 0 } n ? n : Path.GetFileName(_root) is { Length: > 0 } d ? d : "project";
        var namer = new SymbolNamer(packageName, _options.PackageVersion, id => _files[id].RelativePath);
        var preprocessor = new Preprocessor(
            LexCopybook,
            (name, library, includer) => resolver.Resolve(name, library, Path.GetDirectoryName(_files[includer].FullPath)) is { } p ? _files.GetOrAdd(p) : -1,
            Warn);

        // Pass 1: program-ids, entries, copybook usage.
        var catalog = new ProjectCatalog();
        var mains = new List<int>();
        var standalone = new List<int>();
        foreach (var id in programFiles)
        {
            try
            {
                var pre = preprocessor.Process(id, Lex(id));
                var parsed = StructureParser.Parse(pre.Tokens, id);
                catalog.Copybooks.UnionWith(pre.IncludedFiles);
                if (parsed.Units.Count == 0)
                {
                    standalone.Add(id);
                    continue;
                }

                mains.Add(id);
                foreach (var unit in parsed.Units)
                {
                    catalog.Add(unit, namer);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Fail(id, e);
            }
            finally
            {
                _files[id].Release();
            }
        }

        if (_options.IndexUnusedCopybooks)
        {
            standalone.AddRange(copybookFiles);
            standalone.RemoveAll(catalog.Copybooks.Contains);
        }
        else
        {
            standalone.Clear();
        }

        // Pass 2: resolve and emit.
        var documents = new Dictionary<int, DocumentAccumulator>();
        var external = new Dictionary<string, SymbolInformation>(StringComparer.Ordinal);
        DocumentAccumulator? Document(int id)
        {
            if (id < 0 || _files[id].RelativePath is not { } rel)
            {
                return null;
            }

            if (!documents.TryGetValue(id, out var doc))
            {
                documents[id] = doc = new DocumentAccumulator(rel);
            }

            return doc;
        }

        foreach (var id in mains)
        {
            try
            {
                var pre = preprocessor.Process(id, Lex(id));
                var parsed = StructureParser.Parse(pre.Tokens, id);
                new Resolver(parsed, namer, catalog, Document, external).Run(pre.Copies);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Fail(id, e);
            }
            finally
            {
                _files[id].Release();
            }

            if (!catalog.Copybooks.Contains(id))
            {
                onDocument(Document(id)!.Build());
                documents.Remove(id);
            }
        }

        foreach (var id in standalone.Distinct().Order())
        {
            try
            {
                var tokens = LexCopybook(id).Select(t => t with { Origin = id }).ToList();
                var pre = preprocessor.Process(id, tokens);
                var parsed = StructureParser.Parse(pre.Tokens, id, standalone: true);
                var standaloneResolver = new Resolver(parsed, namer, catalog, Document, external);
                standaloneResolver.DefineCopybook(id);
                standaloneResolver.Run(pre.Copies);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Fail(id, e);
            }
        }

        foreach (var doc in documents.Values.OrderBy(d => d.RelativePath, StringComparer.Ordinal))
        {
            onDocument(doc.Build());
        }

        return [.. external.Values.OrderBy(s => s.Symbol, StringComparer.Ordinal)];
    }

    private static HashSet<string> Extensions(IEnumerable<string> list) =>
        new(list.Select(e => "." + e.TrimStart('.')), StringComparer.OrdinalIgnoreCase);

    private List<Token> Lex(int id) => Lexer.Lex(_files[id].Lines, id, _options.Format, _options.DebugLines);

    private List<Token> LexCopybook(int id)
    {
        if (!_lexCache.TryGetValue(id, out var tokens))
        {
            tokens = Lex(id);
            _files[id].Release();
            _lexCache[id] = tokens;
        }

        return tokens;
    }

    private void Warn(string message)
    {
        if (_warned.Add(message))
        {
            _warn?.Invoke(message);
        }
    }

    private void Fail(int id, Exception e)
    {
        var message = $"{_files[id].RelativePath ?? _files[id].FullPath}: {e.GetType().Name}: {e.Message}";
        Errors.Add(message);
        _warn?.Invoke("error: " + message);
    }

    private List<string> EnumerateFiles()
    {
        var excluded = _options.Exclude.Select(e => Path.GetFullPath(Path.Combine(_root, e)).TrimEnd(Path.DirectorySeparatorChar)).ToList();
        var pending = new Stack<string>();
        pending.Push(_root);
        var result = new List<string>();
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            try
            {
                result.AddRange(Directory.EnumerateFiles(dir));
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (name.StartsWith('.') || SkippedDirectories.Contains(name) || excluded.Contains(sub, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    pending.Push(sub);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
