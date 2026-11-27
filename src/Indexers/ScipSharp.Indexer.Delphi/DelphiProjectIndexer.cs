using System.Text;
using ScipSharp.Indexer.Delphi.Emit;
using ScipSharp.Indexer.Delphi.Forms;
using ScipSharp.Indexer.Delphi.Project;
using ScipSharp.Indexer.Delphi.Semantics;
using ScipSharp.Indexer.Delphi.Syntax;
using ScipSharp.Indexer.Delphi.Text;

namespace ScipSharp.Indexer.Delphi;

/// <summary>
/// Indexes a directory of Delphi sources (units, programs, packages, include files and text form files) into a SCIP
/// index. Two passes: every file is parsed and declared first, then names are resolved across units.
/// </summary>
internal sealed class DelphiProjectIndexer
{
    public const string ToolName = "scip-delphi";
    public const string ToolVersion = "0.1.0";
    public const string Language = "Delphi";

    private static readonly string[] SourceExtensions = [".pas", ".dpr", ".dpk", ".pp"];
    private static readonly string[] FormExtensions = [".dfm", ".fmx", ".lfm"];
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", "__history", "__recovery", "node_modules", ".vs", ".idea",
    };

    /// <summary>Unit scope names tried for unqualified unit names that are not part of the project (RAD Studio defaults).</summary>
    private static readonly string[] DefaultNamespaces =
        ["System", "System.Win", "Winapi", "Vcl", "Vcl.Imaging", "FMX", "Data", "Data.Win", "Xml", "Web", "Soap", "Datasnap", "Bde"];

    private readonly string _root;
    private readonly DelphiIndexerOptions _options;
    private readonly IReadOnlyList<string> _arguments;
    private readonly Action<string> _warn;

    private readonly List<string> _filePaths = [];
    private readonly Dictionary<string, int> _fileIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> _texts = [];
    private readonly List<SourceInfo> _sources = [];
    private readonly List<SourceInfo> _stubs = [];
    private readonly HashSet<int> _includeFiles = [];
    private readonly Dictionary<UsesItem, Entity> _usesTargets = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Entity> _unknownUnits = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _formFiles = [];
    private readonly List<DprojInfo> _dprojs = [];
    private readonly List<string> _searchPaths = [];
    private readonly List<string> _namespaces = [];
    private readonly Dictionary<string, string> _unitAliases = new(StringComparer.OrdinalIgnoreCase);

    private Model _model = null!;
    private Binder _binder = null!;
    private PackageInfo _package = null!;

    public DelphiProjectIndexer(string root, DelphiIndexerOptions options, IReadOnlyList<string>? arguments = null, Action<string>? warn = null)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _options = options;
        _arguments = arguments ?? [];
        _warn = warn ?? (_ => { });
    }

    /// <summary>Builds the whole index in memory (tests and small projects).</summary>
    public Index Build()
    {
        var index = new Index();
        Run(m => index.Metadata = m, index.Documents.Add, index.ExternalSymbols.Add);
        return index;
    }

    /// <summary>Indexes the project, streaming the metadata, each document and the external symbols.</summary>
    public void Run(Action<Metadata> onMetadata, Action<Document> onDocument, Action<SymbolInformation> onExternal)
    {
        var metadata = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(_root + Path.DirectorySeparatorChar).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = ToolName, Version = ToolVersion },
        };
        metadata.ToolInfo.Arguments.AddRange(_arguments);
        onMetadata(metadata);

        Discover();
        LoadProjects();
        var settings = CompilerSettings.Create(
            _options.Platform,
            _options.CompilerVersion,
            _options.Defines.Concat(_options.UseDproj ? _dprojs.SelectMany(d => d.Defines) : []),
            _options.Undefines);
        _package = new PackageInfo("delphi", ProjectName(), ProjectVersion());

        _model = new Model();
        _binder = new Binder(_model);
        LoadStubs();
        ParseSources(settings);
        ParseLibraries(settings);
        foreach (var src in _sources)
        {
            _binder.DeclareUnit(src, _package, external: false);
        }

        foreach (var (src, package) in _libraries)
        {
            _binder.DeclareUnit(src, package, external: true);
        }

        foreach (var src in _stubs.Concat(_libraries.Select(l => l.Source)).Concat(_sources))
        {
            ResolveUses(src);
        }

        _binder.LinkImplementations();
        _binder.AssignDisambiguators();

        var sink = new OccurrenceSink();
        foreach (var src in _sources)
        {
            sink.AddDocument(new DocCollector(src.FileId, src.RelativePath, Language));
        }

        foreach (var inc in _includeFiles.Order())
        {
            if (!sink.IsIndexed(inc))
            {
                sink.AddDocument(new DocCollector(inc, Relative(_filePaths[inc]), Language));
            }
        }

        foreach (var src in _sources)
        {
            try
            {
                new Walker(_model, _binder, sink, src, _usesTargets).Run();
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException or KeyNotFoundException or InvalidCastException)
            {
                _warn($"warning: {src.RelativePath}: indexing stopped early: {e.Message}");
            }
        }

        if (_options.IndexForms)
        {
            IndexForms(sink);
        }

        new DocumentAssembler(_model, sink, _texts).Emit(onDocument, onExternal);
    }

    // -------------------------------------------------------------------------------------------------------------
    // Discovery

    private string Relative(string path) => Path.GetRelativePath(_root, path).Replace(Path.DirectorySeparatorChar, '/');

    private int FileId(string path)
    {
        var full = Path.GetFullPath(path);
        if (!_fileIds.TryGetValue(full, out var id))
        {
            id = _filePaths.Count;
            _filePaths.Add(full);
            _fileIds[full] = id;
        }

        return id;
    }

    private void Discover()
    {
        var all = new List<string>();
        var pending = new Stack<string>();
        pending.Push(_root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> files, dirs;
            try
            {
                files = Directory.EnumerateFiles(dir).ToList();
                dirs = Directory.EnumerateDirectories(dir).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _warn($"warning: cannot list {dir}: {e.Message}");
                continue;
            }

            all.AddRange(files);
            foreach (var d in dirs)
            {
                var name = Path.GetFileName(d);
                if (ExcludedDirectories.Contains(name) || _options.Excludes.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFullPath(Path.Combine(_root, x)).TrimEnd(Path.DirectorySeparatorChar), d, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                pending.Push(d);
            }
        }

        all.Sort((a, b) => string.CompareOrdinal(Relative(a), Relative(b)));
        foreach (var f in all)
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            if (SourceExtensions.Contains(ext))
            {
                FileId(f);
            }
            else if (FormExtensions.Contains(ext))
            {
                _formFiles.Add(f);
            }
            else if (ext == ".dproj" && _options.UseDproj)
            {
                if (DprojInfo.Load(f, _warn) is { } d)
                {
                    _dprojs.Add(d);
                }
            }
        }
    }

    private void LoadProjects()
    {
        foreach (var p in _options.SearchPaths)
        {
            _searchPaths.Add(Path.GetFullPath(Path.Combine(_root, p)));
        }

        _namespaces.AddRange(_options.Namespaces);
        foreach (var d in _dprojs)
        {
            _searchPaths.AddRange(d.SearchPaths.Where(p => !_searchPaths.Contains(p)));
            _namespaces.AddRange(d.Namespaces.Where(n => !_namespaces.Contains(n, StringComparer.OrdinalIgnoreCase)));
            foreach (var (k, v) in d.UnitAliases)
            {
                _unitAliases.TryAdd(k, v);
            }
        }
    }

    private string ProjectName()
    {
        if (_options.ProjectName.Length > 0)
        {
            return _options.ProjectName;
        }

        var topLevel = _dprojs.Where(d => string.Equals(d.Directory, _root, StringComparison.OrdinalIgnoreCase)).ToList();
        if (topLevel.Count == 1)
        {
            return Path.GetFileNameWithoutExtension(topLevel[0].Path);
        }

        if (_dprojs.Count == 1)
        {
            return Path.GetFileNameWithoutExtension(_dprojs[0].Path);
        }

        var mains = _filePaths.Where(p => string.Equals(Path.GetDirectoryName(p), _root, StringComparison.OrdinalIgnoreCase) &&
                                          Path.GetExtension(p).ToLowerInvariant() is ".dpr" or ".dpk").ToList();
        return mains.Count == 1 ? Path.GetFileNameWithoutExtension(mains[0]) : Path.GetFileName(_root);
    }

    private string ProjectVersion()
    {
        if (_options.ProjectVersion.Length > 0)
        {
            return _options.ProjectVersion;
        }

        return _dprojs.Select(d => d.Version).FirstOrDefault(v => v is not null && v != "1.0.0.0") ?? ".";
    }

    // -------------------------------------------------------------------------------------------------------------
    // Parsing

    private string ReadText(int fileId)
    {
        if (!_texts.TryGetValue(fileId, out var text))
        {
            try
            {
                text = SourceDecoder.ReadFile(_filePaths[fileId]);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _warn($"warning: cannot read {_filePaths[fileId]}: {e.Message}");
                text = "";
            }

            _texts[fileId] = text;
        }

        return text;
    }

    private (int FileId, string Text)? ResolveInclude(string includingPath, string name)
    {
        var normalized = name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var candidates = new List<string>
        {
            Path.Combine(Path.GetDirectoryName(includingPath) ?? _root, normalized),
            Path.Combine(_root, normalized),
        };
        candidates.AddRange(_searchPaths.Select(p => Path.Combine(p, normalized)));
        if (!Path.HasExtension(normalized))
        {
            candidates.AddRange(candidates.ToList().Select(c => c + ".inc"));
            candidates.AddRange(candidates.ToList().Select(c => c + ".pas"));
        }

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            var match = FindFileCaseInsensitive(full);
            if (match is not null && (match.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || _searchPaths.Any(p => match.StartsWith(p, StringComparison.OrdinalIgnoreCase))))
            {
                var id = FileId(match);
                return (id, ReadText(id));
            }
        }

        return null;
    }

    private static string? FindFileCaseInsensitive(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var dir = Path.GetDirectoryName(path);
        if (dir is null || !Directory.Exists(dir))
        {
            return null;
        }

        var name = Path.GetFileName(path);
        return Directory.EnumerateFiles(dir).FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
    }

    private void ParseSources(CompilerSettings settings)
    {
        var count = _filePaths.Count;
        for (var id = 0; id < count; id++)
        {
            var path = _filePaths[id];
            try
            {
                var text = ReadText(id);
                var raw = Lexer.Tokenize(text, id);
                var pp = new Preprocessor(settings, ResolveInclude);
                var tokens = pp.Process(raw, path, id);
                _includeFiles.UnionWith(pp.IncludedFiles);
                var syntax = Parser.ParseFile(tokens);
                _sources.Add(new SourceInfo(id, path, Relative(path), syntax));
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
            {
                _warn($"warning: {Relative(path)}: cannot parse: {e.Message}");
            }
        }

        // A .pas file that is only ever {$I}-included (not a unit itself) should not be indexed as a separate unit.
        _sources.RemoveAll(s => _includeFiles.Contains(s.FileId) && !s.Syntax.HasHeader);
    }

    private readonly List<(SourceInfo Source, PackageInfo Package)> _libraries = [];

    /// <summary>
    /// Units in search paths outside the project root (e.g. a sibling library checkout): parsed so that references
    /// resolve, but not indexed as documents. Their symbols are external, in a package named after the directory.
    /// </summary>
    private void ParseLibraries(CompilerSettings settings)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in _searchPaths)
        {
            var full = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || string.Equals(full, _root, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(full) || !seen.Add(full))
            {
                continue;
            }

            var package = new PackageInfo("delphi", Path.GetFileName(full), ".");
            foreach (var file in Directory.EnumerateFiles(full, "*.pas").Order(StringComparer.Ordinal))
            {
                try
                {
                    var id = FileId(file);
                    var tokens = new Preprocessor(settings, ResolveInclude).Process(Lexer.Tokenize(ReadText(id), id), file, id);
                    _libraries.Add((new SourceInfo(id, file, file, Parser.ParseFile(tokens)), package));
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
                {
                    _warn($"warning: {file}: cannot parse: {e.Message}");
                }
            }
        }
    }

    private void LoadStubs()
    {
        var asm = typeof(DelphiProjectIndexer).Assembly;
        var names = asm.GetManifestResourceNames().Where(n => n.StartsWith("Stubs.", StringComparison.Ordinal) && n.EndsWith(".pas", StringComparison.Ordinal))
            .OrderBy(n => n == "Stubs.System.pas" ? 0 : 1).ThenBy(n => n, StringComparer.Ordinal).ToList();
        var stubId = -2;
        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = reader.ReadToEnd();
            var id = stubId--;
            var tokens = new Preprocessor(CompilerSettings.Default, static (_, _) => null).Process(Lexer.Tokenize(text, id), name, id);
            var syntax = Parser.ParseFile(tokens);
            var src = new SourceInfo(id, name, name, syntax) { IsStub = true };
            var unitName = syntax.Name;
            var package = new PackageInfo("delphi", unitName.Split('.')[0].ToLowerInvariant() switch
            {
                "vcl" => "vcl",
                "fmx" => "fmx",
                _ => "rtl",
            }, ".");
            var unit = _binder.DeclareUnit(src, package, external: true);
            if (unitName == "System")
            {
                _model.SystemUnit = unit;
                var str = new Entity(EntityKind.OtherType, "string", unit) { Unit = unit, Package = package, IsExternal = true };
                unit.Members.Add(str);
                var file = new Entity(EntityKind.OtherType, "file", unit) { Unit = unit, Package = package, IsExternal = true };
                unit.Members.Add(file);
            }

            _stubs.Add(src);
        }
    }

    // -------------------------------------------------------------------------------------------------------------
    // Unit resolution

    private void ResolveUses(SourceInfo src)
    {
        var unit = src.Unit!;
        foreach (var item in src.Syntax.InterfaceUses)
        {
            var target = ResolveUnit(src, item);
            unit.InterfaceUses.Add(target);
            _usesTargets[item] = target;
        }

        foreach (var item in src.Syntax.ImplementationUses)
        {
            var target = ResolveUnit(src, item);
            unit.ImplementationUses.Add(target);
            _usesTargets[item] = target;
        }
    }

    private Entity ResolveUnit(SourceInfo from, UsesItem item)
    {
        var target = ResolveUnitCore(from, item);
        if (!string.Equals(string.Join(".", target.UnitSegments), item.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (!_model.UnitAliases.TryGetValue(target, out var set))
            {
                _model.UnitAliases[target] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            set.Add(item.Name);
        }

        return target;
    }

    private Entity ResolveUnitCore(SourceInfo from, UsesItem item)
    {
        if (item.InPath is { Length: > 0 } inPath && !from.IsStub)
        {
            var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(from.Path) ?? _root,
                inPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
            var match = FindFileCaseInsensitive(full);
            if (match is not null && _sources.FirstOrDefault(s => string.Equals(s.Path, match, StringComparison.OrdinalIgnoreCase)) is { Unit: { } u })
            {
                return u;
            }
        }

        var candidates = new List<string>();
        if (_unitAliases.TryGetValue(item.Name, out var alias))
        {
            candidates.Add(alias);
        }

        candidates.Add(item.Name);
        foreach (var ns in _namespaces)
        {
            candidates.Add(ns + "." + item.Name);
        }

        if (!from.IsStub)
        {
            foreach (var c in candidates)
            {
                if (_model.UnitsByName.TryGetValue(c, out var units) && units.Where(u => !u.IsExternal).ToList() is { Count: > 0 } project)
                {
                    return BestUnit(from, project);
                }
            }
        }

        foreach (var ns in DefaultNamespaces)
        {
            candidates.Add(ns + "." + item.Name);
        }

        foreach (var c in candidates)
        {
            if (_model.UnitsByName.TryGetValue(c, out var units) && units.FirstOrDefault(u => u.IsExternal) is { } stub)
            {
                return stub;
            }
        }

        if (!_unknownUnits.TryGetValue(item.Name, out var unknown))
        {
            unknown = new Entity(EntityKind.Unit, item.Name, null)
            {
                IsExternal = true,
                IsUnknownUnit = true,
                UnitSegments = item.Name.Split('.'),
                Package = new PackageInfo("delphi", "unknown", "."),
            };
            unknown.Unit = unknown;
            _unknownUnits[item.Name] = unknown;
        }

        return unknown;
    }

    /// <summary>Several files may declare the same unit (e.g. copies in tests and examples): prefer the closest one.</summary>
    private Entity BestUnit(SourceInfo from, List<Entity> units)
    {
        if (units.Count == 1)
        {
            return units[0];
        }

        var dir = Path.GetDirectoryName(from.Path) ?? "";
        var sameDir = units.FirstOrDefault(u => string.Equals(Path.GetDirectoryName(u.Source?.Path), dir, StringComparison.OrdinalIgnoreCase));
        if (sameDir is not null)
        {
            return sameDir;
        }

        foreach (var sp in _searchPaths)
        {
            var onPath = units.FirstOrDefault(u => string.Equals(Path.GetDirectoryName(u.Source?.Path), sp.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
            if (onPath is not null)
            {
                return onPath;
            }
        }

        return units.OrderBy(u => CommonPrefixLength(u.Source?.Path ?? "", dir)).ThenBy(u => u.Source?.RelativePath, StringComparer.Ordinal).Last();
    }

    private static int CommonPrefixLength(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && char.ToUpperInvariant(a[n]) == char.ToUpperInvariant(b[n]))
        {
            n++;
        }

        return n;
    }

    // -------------------------------------------------------------------------------------------------------------
    // Forms

    private void IndexForms(OccurrenceSink sink)
    {
        foreach (var form in _formFiles)
        {
            try
            {
                IndexForm(form, sink);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or IndexOutOfRangeException)
            {
                _warn($"warning: {Relative(form)}: {e.Message}");
            }
        }
    }

    private void IndexForm(string path, OccurrenceSink sink)
    {
        var bytes = File.ReadAllBytes(path);
        if (DfmParser.IsBinary(bytes))
        {
            return;
        }

        var baseName = Path.GetFileNameWithoutExtension(path);
        var dir = Path.GetDirectoryName(path);
        var unitSrc = _sources.FirstOrDefault(s => string.Equals(Path.GetDirectoryName(s.Path), dir, StringComparison.OrdinalIgnoreCase) &&
                                                   string.Equals(Path.GetFileNameWithoutExtension(s.Path), baseName, StringComparison.OrdinalIgnoreCase) &&
                                                   s.Unit is not null);
        if (unitSrc is null)
        {
            return;
        }

        var text = SourceDecoder.Decode(bytes);
        var id = FileId(path);
        _texts[id] = text;
        var doc = new DocCollector(id, Relative(path), Language);
        sink.AddDocument(doc);
        var unit = unitSrc.Unit!;
        var scope = _binder.UnitScopes[unit].Implementation;
        foreach (var root in DfmParser.Parse(text))
        {
            var rootClass = ResolveFormType(root.TypeName, scope);
            if (root.Name is { } rootName && scope.Lookup(rootName, static e => e.Kind == EntityKind.Var).FirstOrDefault() is { } formVar)
            {
                sink.Add(id, new Range(root.Line, root.NameColumn, root.Line, root.NameColumn + rootName.Length), formVar, 0, null);
            }

            VisitFormObject(root, rootClass, rootClass, scope, id, sink);
        }
    }

    private Entity? ResolveFormType(string typeName, Scope scope)
    {
        if (typeName.Length == 0)
        {
            return null;
        }

        var parts = typeName.Split('.');
        var found = scope.Lookup(parts[^1], static e => e.IsType);
        return found.FirstOrDefault();
    }

    private void VisitFormObject(DfmObject obj, Entity? type, Entity? owner, Scope scope, int fileId, OccurrenceSink sink)
    {
        if (type is not null && obj.TypeName.Length > 0)
        {
            var simple = obj.TypeName.Split('.')[^1];
            var col = obj.TypeColumn + obj.TypeName.Length - simple.Length;
            sink.Add(fileId, new Range(obj.Line, col, obj.Line, col + simple.Length), type, 0, null);
        }

        foreach (var prop in obj.Properties)
        {
            // Property names resolve against the component's class (`OnClick` of TButton, `Caption` of TForm).
            Ty? receiver = type is null ? null : NamedTy.Of(type);
            var col = prop.Column;
            foreach (var part in prop.Name.Split('.'))
            {
                var found = _model.FindMember(receiver, part).Found;
                var member = found.Count > 0 ? found[0] : null;
                if (member is null)
                {
                    break;
                }

                sink.Add(fileId, new Range(prop.Line, col, prop.Line, col + part.Length), member, 0, null);
                receiver = _model.TypeOf(member);
                col += part.Length + 1;
            }

            // Values that name a method (event handler) or a component of the owning form.
            if (prop.IdentifierValue is { } value && owner is not null && !value.Contains('.', StringComparison.Ordinal))
            {
                var target = _model.FindMember(NamedTy.Of(owner), value).Found.FirstOrDefault(e => e.IsRoutine || e.Kind == EntityKind.Field);
                if (target is not null && !target.IsExternal)
                {
                    sink.Add(fileId, new Range(prop.Line, prop.ValueColumn, prop.Line, prop.ValueColumn + value.Length), target, 0, null);
                }
            }
        }

        foreach (var child in obj.Children)
        {
            Entity? field = null;
            if (child.Name is { } name && owner is not null)
            {
                field = _model.FindMember(NamedTy.Of(owner), name).Found.FirstOrDefault(e => e.Kind == EntityKind.Field);
                if (field is not null && !field.IsExternal)
                {
                    sink.Add(fileId, new Range(child.Line, child.NameColumn, child.Line, child.NameColumn + name.Length), field, 0, null);
                }
            }

            var childType = ResolveFormType(child.TypeName, scope);
            if (childType is null && field is not null && _model.TypeOf(field) is NamedTy ft)
            {
                childType = ft.Decl;
            }

            // Components inside an embedded frame (`inline Frame1: TFrame1`) belong to the frame's class.
            var childOwner = child.Keyword == "inline" && childType is { IsExternal: false } ? childType : owner;
            VisitFormObject(child, childType, childOwner, scope, fileId, sink);
        }
    }
}
