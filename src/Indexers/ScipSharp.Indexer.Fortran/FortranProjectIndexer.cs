using System.Text;
using System.Text.RegularExpressions;
using ScipSharp.Indexer.Fortran.Semantics;
using ScipSharp.Indexer.Fortran.Syntax;

namespace ScipSharp.Indexer.Fortran;

/// <summary>
/// Indexes every Fortran source below a project root. Two passes: all files are collected first (so modules,
/// external procedures and COMMON blocks are known program-wide regardless of file order), then names are resolved
/// and one document per file is streamed out. Pure managed code: no compiler, preprocessor or language server.
/// </summary>
public sealed partial class FortranProjectIndexer(string projectRoot, FortranOptions? options = null, IReadOnlyList<string>? arguments = null, Action<string>? warn = null)
{
    public const string ToolName = "scip-fortran";

    public const string ToolVersion = "0.1.0";

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase) { "build", "node_modules", "_build" };

    private readonly string _root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private readonly FortranOptions _options = options ?? new FortranOptions();
    private readonly Dictionary<string, SourceDoc> _includeDocs = new(StringComparer.Ordinal);

    // INCLUDE lookups repeat in every file (often for missing headers such as mpif.h): cache results and the
    // case-insensitive directory listings, which would otherwise be re-enumerated per include line.
    private readonly Dictionary<(string Path, string FromDir), string?> _findCache = [];
    private readonly Dictionary<string, Dictionary<string, string>?> _dirListings = new(StringComparer.Ordinal);

    [GeneratedRegex(@"^\s*(?:\d+\s+)?include\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortranIncludeLine();

    [GeneratedRegex(@"^\s*#\s*include\s*[""<]([^"">]+)["">]", RegexOptions.CultureInvariant)]
    private static partial Regex CppIncludeLine();

    /// <summary>Names in expressions that could not be resolved during the last run (diagnostics).</summary>
    internal IReadOnlyList<Token> Unresolved { get; private set; } = [];

    /// <summary>Builds the whole index in memory (tests, small projects).</summary>
    public Index Build()
    {
        var index = new Index();
        Run(m => index.Metadata = m, index.Documents.Add, index.ExternalSymbols.Add);
        return index;
    }

    /// <summary>Indexes the project, streaming metadata, documents and external symbols to the callbacks.</summary>
    public void Run(Action<Metadata> onMetadata, Action<Document> onDocument, Action<SymbolInformation> onExternal)
    {
        var manifest = FpmManifest.Load(_root);
        var namer = new SymbolNamer(
            _options.PackageManager ?? (manifest is null ? "" : "fpm"),
            _options.PackageName ?? manifest?.Name ?? Path.GetFileName(_root),
            _options.PackageVersion ?? manifest?.Version ?? "");
        var metadata = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(_root).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = ToolName, Version = ToolVersion },
        };
        metadata.ToolInfo.Arguments.AddRange(arguments ?? []);
        onMetadata(metadata);

        var files = DiscoverFiles();
        var included = PrescanIncludes(files);
        var ws = new Workspace();
        var collector = new Collector(ws, ResolveInclude, ReadStatements);
        var units = new List<(SourceDoc Doc, List<Stmt> Statements)>();
        foreach (var (abs, rel) in files)
        {
            if (included.Contains(abs))
            {
                continue;
            }

            var doc = LoadDoc(abs, rel, null);
            if (doc is null)
            {
                continue;
            }

            var (_, statements) = collector.Collect(doc, ReadStatements(doc));
            units.Add((doc, statements));
        }

        Linker.Link(ws);
        var resolver = new Resolver(ws, namer);
        Unresolved = resolver.Unresolved;
        foreach (var (doc, statements) in units)
        {
            resolver.Run(statements);
            onDocument(resolver.Take(doc) ?? EmptyDocument(doc));
        }

        foreach (var doc in resolver.OpenDocuments.OrderBy(static d => d.RelativePath, StringComparer.Ordinal))
        {
            if (resolver.Take(doc) is { } d)
            {
                onDocument(d);
            }
        }

        foreach (var ext in resolver.ExternalSymbols())
        {
            onExternal(ext);
        }
    }

    private static Document EmptyDocument(SourceDoc doc) => new()
    {
        RelativePath = doc.RelativePath,
        Language = "Fortran",
        PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
    };

    private List<Stmt> ReadStatements(SourceDoc doc)
    {
        var pp = new Preprocessor(_options.Evaluate, _options.Defines);
        foreach (var name in _options.Defines.Keys)
        {
            doc.Macros.Add(name.ToLowerInvariant());
        }

        return StatementReader.Read(doc, _options.FixedLineLength, pp);
    }

    private List<(string Abs, string Rel)> DiscoverFiles()
    {
        var result = new List<(string, string)>();
        if (!Directory.Exists(_root))
        {
            throw new InvalidOperationException($"project root does not exist: {_root}");
        }

        var stack = new Stack<string>();
        stack.Push(_root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                var rel = Path.GetRelativePath(_root, entry).Replace(Path.DirectorySeparatorChar, '/');
                if (IsExcluded(name, rel))
                {
                    continue;
                }

                if (Directory.Exists(entry))
                {
                    if (!name.StartsWith('.') && !SkippedDirectories.Contains(name) && (File.GetAttributes(entry) & FileAttributes.ReparsePoint) == 0)
                    {
                        stack.Push(entry);
                    }
                }
                else if (SourceForms.IsSourceFile(entry))
                {
                    result.Add((entry, rel));
                }
            }
        }

        result.Sort(static (a, b) => string.CompareOrdinal(a.Item2, b.Item2));
        return result;
    }

    private bool IsExcluded(string name, string rel)
    {
        foreach (var ex in _options.Exclude)
        {
            var e = ex.Replace('\\', '/').Trim('/');
            if (name.Equals(e, StringComparison.Ordinal) || rel.Equals(e, StringComparison.Ordinal) || rel.StartsWith(e + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Files named by an INCLUDE line / #include are indexed only through their includers.</summary>
    private HashSet<string> PrescanIncludes(List<(string Abs, string Rel)> files)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (abs, _) in files)
        {
            string[] lines;
            try
            {
                lines = ReadLines(abs);
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var line in lines)
            {
                var m = FortranIncludeLine().Match(line);
                if (!m.Success)
                {
                    m = CppIncludeLine().Match(line);
                }

                if (m.Success && FindInclude(m.Groups[1].Value, Path.GetDirectoryName(abs)!) is { } target)
                {
                    result.Add(target);
                }
            }
        }

        return result;
    }

    private SourceDoc? ResolveInclude(string path, SourceDoc from)
    {
        var abs = FindInclude(path, Path.GetDirectoryName(from.AbsolutePath) ?? _root);
        if (abs is null)
        {
            return null;
        }

        if (_includeDocs.TryGetValue(abs, out var doc))
        {
            return doc;
        }

        var rel = Path.GetRelativePath(_root, abs).Replace(Path.DirectorySeparatorChar, '/');
        doc = LoadDoc(abs, rel, from.Form);
        if (doc is not null)
        {
            doc.IsInclude = true;
            _includeDocs[abs] = doc;
        }

        return doc;
    }

    private string? FindInclude(string path, string fromDir)
    {
        if (!_findCache.TryGetValue((path, fromDir), out var found))
        {
            found = FindIncludeUncached(path, fromDir);
            _findCache[(path, fromDir)] = found;
        }

        return found;
    }

    private string? FindIncludeUncached(string path, string fromDir)
    {
        var candidates = new List<string> { fromDir, _root };
        candidates.AddRange(_options.IncludeDirs.Select(d => Path.IsPathRooted(d) ? d : Path.Combine(_root, d)));
        foreach (var dir in candidates)
        {
            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(dir, path));
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue; // only files inside the project become documents
            }

            if (File.Exists(full))
            {
                return full;
            }

            // Case-insensitive fallback (INCLUDE 'PARAMS.INC' for params.inc).
            var parent = Path.GetDirectoryName(full);
            if (parent is not null && DirListing(parent) is { } listing && listing.TryGetValue(Path.GetFileName(full), out var match))
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Files of <paramref name="dir"/> by case-insensitive name (first in enumeration order wins); null when missing.</summary>
    private Dictionary<string, string>? DirListing(string dir)
    {
        if (!_dirListings.TryGetValue(dir, out var listing))
        {
            if (Directory.Exists(dir))
            {
                listing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir))
                    {
                        listing.TryAdd(Path.GetFileName(f), f);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            _dirListings[dir] = listing;
        }

        return listing;
    }

    private SourceDoc? LoadDoc(string abs, string rel, SourceForm? includerForm)
    {
        string[] lines;
        try
        {
            lines = ReadLines(abs);
        }
        catch (IOException e)
        {
            warn?.Invoke($"cannot read {rel}: {e.Message}");
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            warn?.Invoke($"cannot read {rel}: {e.Message}");
            return null;
        }

        var form = _options.ForcedForm ?? SourceForms.FromExtension(abs) ?? includerForm ?? SourceForm.Free;
        if (_options.ForcedForm is null && form == SourceForm.Fixed && SourceForms.LooksLikeFreeForm(lines))
        {
            form = SourceForm.Free;
        }

        return new SourceDoc(abs, rel, lines, form);
    }

    private static string[] ReadLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = new UTF8Encoding(false, false).GetString(bytes);
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].EndsWith('\r'))
            {
                lines[i] = lines[i][..^1];
            }
        }

        return lines;
    }
}
