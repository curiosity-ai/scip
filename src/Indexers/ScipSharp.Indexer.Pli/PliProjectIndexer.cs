using System.Reflection;
using ScipSharp.Indexer.Pli.Semantics;
using ScipSharp.Indexer.Pli.Syntax;

namespace ScipSharp.Indexer.Pli;

/// <summary>
/// Indexes every PL/I compilation unit below a project root: discovers sources, parses each unit with its %INCLUDE
/// members, links external names across units and produces one SCIP document per source or include file.
/// </summary>
internal sealed class PliProjectIndexer
{
    private static readonly string[] SkippedDirectories = ["node_modules", "artifacts", "bin", "obj"];

    private readonly string _root;
    private readonly PliOptions _options;
    private readonly IReadOnlyList<string> _arguments;
    private readonly Action<string>? _warn;
    private readonly FileTable _files = new();
    private IncludeResolver _includes = null!;

    public PliProjectIndexer(string root, PliOptions options, IReadOnlyList<string>? arguments = null, Action<string>? warn = null)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _options = options;
        _arguments = arguments ?? [];
        _warn = warn;
    }

    public static string ToolVersion =>
        typeof(PliProjectIndexer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public Metadata CreateMetadata()
    {
        var meta = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(_root + Path.DirectorySeparatorChar).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = SymbolNamer.Scheme, Version = ToolVersion },
        };
        meta.ToolInfo.Arguments.AddRange(_arguments);
        return meta;
    }

    /// <summary>Builds the whole index in memory.</summary>
    public Index Build()
    {
        var index = new Index();
        var external = Run(index.Documents.Add, m => index.Metadata = m);
        index.ExternalSymbols.AddRange(external);
        return index;
    }

    /// <summary>Indexes the project, streaming documents; returns the external (built-in) symbols.</summary>
    public List<SymbolInformation> Run(Action<Document> onDocument, Action<Metadata>? onMetadata = null)
    {
        if (!Directory.Exists(_root))
        {
            throw new InvalidOperationException($"project root does not exist: {_root}");
        }

        onMetadata?.Invoke(CreateMetadata());

        var all = Discover();
        var sources = all.Where(IsSource).ToList();
        var includeFiles = all.Where(IsInclude).ToList();
        _includes = new IncludeResolver(_root, _options, all);

        var parser = new UnitParser(_options.CaseSensitive, LoadInclude);
        var units = new List<Unit>();
        foreach (var path in sources)
        {
            units.Add(parser.Parse(Load(path)));
        }

        // Include members nobody includes are still indexed on their own.
        var included = units.SelectMany(static u => u.Files.Skip(1)).ToHashSet();
        foreach (var path in includeFiles)
        {
            var entry = Load(path);
            if (!included.Contains(entry.Id))
            {
                units.Add(parser.Parse(entry));
            }
        }

        var namer = new SymbolNamer(_options.PackageName ?? Path.GetFileName(_root), _options.PackageVersion);
        foreach (var u in units)
        {
            namer.RegisterGlobals(u);
        }

        foreach (var u in units)
        {
            namer.AssignSymbols(u);
        }

        var resolver = new Resolver(namer, parser.Normalize);
        var occurrences = new List<(Unit Unit, List<RawOccurrence> Occurrences)>();
        foreach (var u in units)
        {
            try
            {
                occurrences.Add((u, resolver.Resolve(u)));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // One malformed unit must not abort the whole project: index its siblings.
                _warn?.Invoke($"{u.File.RelativePath}: skipped, {e.GetType().Name}: {e.Message}");
            }

            foreach (var w in u.Warnings)
            {
                _warn?.Invoke(w);
            }
        }

        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, list) in occurrences)
        {
            foreach (var o in list)
            {
                if ((o.Roles & (int)SymbolRole.Definition) != 0 && o.Decl.Symbol is { } s)
                {
                    defined.Add(s);
                }
            }
        }

        var docs = new SortedDictionary<string, DocumentBuilder>(StringComparer.Ordinal);
        foreach (var f in _files.Files)
        {
            docs[f.RelativePath] = new DocumentBuilder(f.RelativePath);
        }

        foreach (var (_, list) in occurrences)
        {
            foreach (var o in list)
            {
                var doc = docs[_files[o.Token.File].RelativePath];
                doc.Add(o, defined);
            }
        }

        foreach (var doc in docs.Values)
        {
            onDocument(doc.Build());
        }

        return namer.UsedBuiltins
            .OrderBy(static d => d.Symbol, StringComparer.Ordinal)
            .Select(static d => DocumentBuilder.Info(d.Symbol!, d))
            .ToList();
    }

    private bool IsInclude(string path) =>
        _options.IncludeExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private bool IsSource(string path) =>
        !IsInclude(path) && _options.Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private List<string> Discover()
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(_root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (!name.StartsWith('.') && !SkippedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(sub);
                }
            }

            result.AddRange(Directory.EnumerateFiles(dir).Where(f => IsSource(f) || IsInclude(f)));
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private SourceFileEntry? LoadInclude(string member, int fromFile)
    {
        var path = _includes.Resolve(member, _files[fromFile].AbsolutePath);
        return path is null || !path.StartsWith(_root, StringComparison.Ordinal) ? null : Load(path);
    }

    private SourceFileEntry Load(string absolutePath)
    {
        var relative = Path.GetRelativePath(_root, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
        if (_files.TryGet(relative, out var existing))
        {
            return existing;
        }

        var text = SourceText.Decode(File.ReadAllBytes(absolutePath));
        var source = SourceText.Create(text, _options.Margins, _options.MarginMode);
        var id = _files.Files.Count;
        return _files.Add(absolutePath, relative, source, Lexer.Tokenize(source.Masked, id));
    }
}
