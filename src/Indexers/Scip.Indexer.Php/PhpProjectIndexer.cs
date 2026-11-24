using Scip.Indexer.Php.Composer;

namespace Scip.Indexer.Php;

/// <summary>
/// Port of <c>ScipPhp\Indexer</c>: indexes a composer project. Pure managed code; it never starts PHP or composer.
/// </summary>
public sealed class PhpProjectIndexer
{
    /// <summary>
    /// PHP version attributed to built-in symbols. Upstream uses the version of the running PHP interpreter; a native
    /// indexer cannot ask one, so it is configurable (<c>--php-version</c>) and defaults to this value.
    /// </summary>
    public const string DefaultPhpVersion = "8.4.0";

    private readonly string _projectRoot;
    private readonly string _toolVersion;
    private readonly IReadOnlyList<string> _args;
    private readonly string _phpVersion;
    private readonly Action<string>? _warn;

    public PhpProjectIndexer(
        string projectRoot,
        string toolVersion,
        IReadOnlyList<string>? args = null,
        string phpVersion = DefaultPhpVersion,
        Action<string>? warn = null)
    {
        _projectRoot = projectRoot;
        _toolVersion = toolVersion;
        _args = args ?? [];
        _phpVersion = phpVersion;
        _warn = warn;
    }

    public Metadata CreateMetadata(string projectRoot)
    {
        var meta = new Metadata
        {
            Version = (ProtocolVersion)1,
            ProjectRoot = $"file://{projectRoot}",
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = "scip-php", Version = _toolVersion },
        };
        meta.ToolInfo.Arguments.AddRange(_args);
        return meta;
    }

    /// <summary>Runs the indexer, handing every finished document to <paramref name="onDocument"/>; returns metadata and external symbols.</summary>
    public (Metadata Metadata, List<SymbolInformation> ExternalSymbols) Run(Action<Document> onDocument, Action<Metadata>? onMetadata = null)
    {
        var fs = new PhpFs();
        var composer = new ComposerProject(_projectRoot, _phpVersion, fs, _warn);
        var namer = new SymbolNamer(composer);
        var types = new Types(composer, namer, fs, _warn);
        var metadata = CreateMetadata(composer.Root);

        onMetadata?.Invoke(metadata);
        var projectFiles = composer.ProjectFiles;
        types.Collect(projectFiles);

        var defined = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var ext = new Dictionary<string, SymbolInformation>(StringComparer.Ordinal);
        foreach (var file in projectFiles)
        {
            var parsed = fs.Parse(file, _warn);
            if (parsed == null)
            {
                continue;
            }

            var indexer = new DocIndexer(composer, namer, types, parsed.Source);
            try
            {
                foreach (var node in parsed.PostOrder())
                {
                    indexer.Index(node);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
            {
                _warn?.Invoke($"{file}: indexing failed: {e.Message}");
                fs.Forget(file);
                continue;
            }

            var doc = new Document
            {
                Language = "PHP",
                RelativePath = Path.GetRelativePath(composer.Root, file).Replace(Path.DirectorySeparatorChar, '/'),
                PositionEncoding = PositionEncoding.Utf8CodeUnitOffsetFromLineStart,
            };
            doc.Occurrences.AddRange(indexer.Occurrences);
            doc.Symbols.AddRange(indexer.Symbols.Values);
            foreach (var (symbol, info) in indexer.ExtSymbols)
            {
                ext[symbol] = info;
            }

            foreach (var occ in doc.Occurrences)
            {
                if (!occ.Symbol.StartsWith("local ", StringComparison.Ordinal))
                {
                    referenced.Add(occ.Symbol);
                }
            }

            defined.UnionWith(indexer.Symbols.Keys);
            onDocument(doc);
            fs.Forget(file);
        }

        // Safety net: every occurrence symbol needs a SymbolInformation somewhere (lint). Anything referenced but not
        // defined by a document (e.g. a function name differing only in case) becomes an external symbol.
        // Conversely, a symbol defined by the project (e.g. a polyfill of a built-in function) must not also be external.
        foreach (var symbol in defined)
        {
            ext.Remove(symbol);
        }

        foreach (var symbol in referenced)
        {
            if (!defined.Contains(symbol) && !ext.ContainsKey(symbol))
            {
                ext[symbol] = new SymbolInformation { Symbol = symbol };
            }
        }

        return (metadata, [.. ext.Values]);
    }

    /// <summary>Builds the complete in-memory index (used by tests).</summary>
    public Index Build()
    {
        var documents = new List<Document>();
        var (metadata, ext) = Run(documents.Add);
        var index = new Index { Metadata = metadata };
        index.Documents.AddRange(documents);
        index.ExternalSymbols.AddRange(ext);
        return index;
    }
}
