using System.Text;
using ScipSharp.Indexer.Pascal.Project;
using ScipSharp.Indexer.Pascal.Syntax;
using ScipKind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Pascal.Semantics;

/// <summary>Options of a Pascal indexing run.</summary>
internal sealed record PascalOptions
{
    public required string Root { get; init; }

    public string? PackageName { get; init; }

    public string? PackageVersion { get; init; }

    public List<string> SearchPaths { get; init; } = [];

    public List<string> IncludePaths { get; init; } = [];

    public List<string> Defines { get; init; } = [];

    public List<string> Undefines { get; init; } = [];

    public PascalMode? Mode { get; init; }

    public Encoding LegacyEncoding { get; init; } = Encoding.Latin1;

    public bool IndexForms { get; init; } = true;
}

/// <summary>
/// The project being indexed: discovers sources, Lazarus projects/packages, resolves <c>uses</c> and includes, and
/// drives the two-phase compilation of every unit (in dependency order, on demand).
/// </summary>
internal sealed class Workspace
{
    public static readonly string[] SourceExtensions = [".pas", ".pp", ".p", ".lpr"];

    private readonly PascalOptions _options;
    private readonly Action<string>? _warn;
    private readonly Dictionary<string, CompileUnit> _builtins;
    private readonly Dictionary<string, List<CompileUnit>> _units = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CompileUnit> _byPath = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CompileUnit> _unknown = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SourceText> _sources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _overloads = new(StringComparer.Ordinal);
    private readonly Dictionary<CompileUnit, LazarusProject?> _configOf = [];
    private readonly Dictionary<CompileUnit, Compiler> _compilers = [];
    private readonly Dictionary<CompileUnit, IReadOnlyList<CompileUnit>> _usedUnits = [];
    private readonly List<LazarusProject> _projects = [];
    private readonly List<string> _lfmFiles = [];
    private readonly Dictionary<string, Dictionary<string, string>> _dirListing = new(StringComparer.Ordinal);

    public Workspace(PascalOptions options, Action<string>? warn = null)
    {
        _options = options with { Root = Path.GetFullPath(options.Root).TrimEnd(Path.DirectorySeparatorChar) };
        _warn = warn;
        _builtins = Builtins.Build(Types);
        System = _builtins["System"];
        Emit = new Emitter(RelativePath);
    }

    public string Root => _options.Root;

    public TypeSystem Types { get; } = new();

    public Emitter Emit { get; }

    public CompileUnit System { get; }

    public IReadOnlyList<CompileUnit> ProjectUnits => [.. _byPath.Values.Where(static u => u.InProject).OrderBy(static u => u.Path, StringComparer.Ordinal)];

    private string? RelativePath(SourceText source)
    {
        var full = source.Path;
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return Path.GetRelativePath(Root, full).Replace(Path.DirectorySeparatorChar, '/');
    }

    // ---------------------------------------------------------------- discovery

    public void Discover()
    {
        var files = new List<string>();
        Walk(Root, files);
        files.Sort(StringComparer.Ordinal);
        foreach (var f in files)
        {
            if (f.EndsWith(".lpi", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".lpk", StringComparison.OrdinalIgnoreCase))
            {
                if (LazarusProject.Load(f, _warn) is { } p)
                {
                    _projects.Add(p);
                }
            }
        }

        foreach (var f in files)
        {
            var ext = Path.GetExtension(f);
            if (SourceExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                Register(f, inProject: true);
            }
            else if (_options.IndexForms && ext.Equals(".lfm", StringComparison.OrdinalIgnoreCase))
            {
                _lfmFiles.Add(f);
            }
        }
    }

    private static void Walk(string dir, List<string> files)
    {
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(dir).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var e in entries)
        {
            var name = Path.GetFileName(e);
            if (Directory.Exists(e))
            {
                if (name.StartsWith('.') || name is "backup" or "__history" or "__recovery" or "node_modules")
                {
                    continue;
                }

                if ((File.GetAttributes(e) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                Walk(e, files);
            }
            else
            {
                files.Add(e);
            }
        }
    }

    private CompileUnit? Register(string path, bool inProject)
    {
        path = Path.GetFullPath(path);
        if (_byPath.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var (kind, name) = ScanHeader(path);
        var config = ConfigFor(path);
        var package = PackageFor(path, config, inProject);
        var unit = new CompileUnit(name, kind, package) { Path = path, InProject = inProject };
        unit.Sym = new Sym(name, kind == UnitFileKind.Unit || kind == UnitFileKind.Module ? SymKind.Unit : SymKind.Program, null)
        {
            Symbol = package.Prefix + Names.Namespace(name),
            ScipKind = kind == UnitFileKind.Library ? ScipKind.Library : ScipKind.Module,
            Unit = unit,
            External = !inProject,
            Signature = $"{kind.ToString().ToLowerInvariant()} {name}",
        };
        unit.Interface = new Scope(unit.Sym);
        unit.Implementation = new Scope(unit.Sym);
        _byPath[path] = unit;
        _configOf[unit] = config;
        if (!_units.TryGetValue(name, out var list))
        {
            _units[name] = list = [];
        }

        list.Add(unit);
        return unit;
    }

    /// <summary>Reads the first tokens of a file to learn whether it is a unit/program/library/module and its name.</summary>
    private (UnitFileKind Kind, string Name) ScanHeader(string path)
    {
        var fallback = Path.GetFileNameWithoutExtension(path);
        SourceText src;
        try
        {
            src = LoadSource(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (UnitFileKind.Program, fallback);
        }

        var lexer = new Lexer(new SourceText(src.Path, src.Text, src.Encoding));
        var first = lexer.Next();
        while (first.Kind == TokenKind.Directive)
        {
            first = lexer.Next();
        }

        var kind = first.Lower switch
        {
            "unit" => UnitFileKind.Unit,
            "library" => UnitFileKind.Library,
            "module" => UnitFileKind.Module,
            _ => UnitFileKind.Program,
        };
        if (first.Lower is not ("unit" or "program" or "library" or "module" or "package"))
        {
            return (UnitFileKind.Program, fallback);
        }

        var sb = new StringBuilder();
        var t = lexer.Next();
        while (t.Kind == TokenKind.Identifier)
        {
            sb.Append(t.Text);
            t = lexer.Next();
            if (t.Kind == TokenKind.Symbol && t.Text == ".")
            {
                sb.Append('.');
                t = lexer.Next();
            }
            else
            {
                break;
            }
        }

        return (kind, sb.Length > 0 ? sb.ToString().TrimEnd('.') : fallback);
    }

    private LazarusProject? ConfigFor(string path)
    {
        // A package listing the file wins; otherwise the nearest project/package in an ancestor directory.
        foreach (var p in _projects)
        {
            if (p.IsPackage && p.Files.Contains(path))
            {
                return p;
            }
        }

        LazarusProject? best = null;
        foreach (var p in _projects)
        {
            if (path.StartsWith(p.Directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && (best is null || p.Directory.Length > best.Directory.Length || (p.Directory.Length == best.Directory.Length && p.Files.Contains(path))))
            {
                best = p;
            }
        }

        return best;
    }

    private PackageInfo PackageFor(string path, LazarusProject? config, bool inProject)
    {
        if (_options.PackageName is { Length: > 0 } forced && inProject)
        {
            return new PackageInfo(".", forced, _options.PackageVersion ?? ".");
        }

        if (config is not null)
        {
            return new PackageInfo("lazarus", config.Name, config.IsPackage ? config.Version : _options.PackageVersion ?? ".");
        }

        if (inProject)
        {
            return new PackageInfo(".", Path.GetFileName(Root), _options.PackageVersion ?? ".");
        }

        return new PackageInfo(".", Path.GetFileName(Path.GetDirectoryName(path)) ?? ".", ".");
    }

    // ---------------------------------------------------------------- sources & includes

    public SourceText LoadSource(string path)
    {
        if (!_sources.TryGetValue(path, out var src))
        {
            src = SourceText.Load(path, _options.LegacyEncoding);
            _sources[path] = src;
        }

        return src;
    }

    private SourceText? ResolveInclude(string name, SourceText includer, CompileUnit unit)
    {
        name = name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var dirs = new List<string> { Path.GetDirectoryName(includer.Path)! };
        if (unit.Path is { } up && Path.GetDirectoryName(up) is { } unitDir && !dirs.Contains(unitDir))
        {
            dirs.Add(unitDir);
        }

        if (_configOf.GetValueOrDefault(unit) is { } cfg)
        {
            dirs.AddRange(cfg.IncludePaths);
            dirs.AddRange(cfg.UnitPaths);
        }

        dirs.AddRange(_options.IncludePaths.Select(Path.GetFullPath));
        dirs.AddRange(_options.SearchPaths.Select(Path.GetFullPath));
        var names = Path.HasExtension(name) ? [name] : new[] { name + ".inc", name + ".pp", name + ".pas", name };
        foreach (var dir in dirs)
        {
            foreach (var n in names)
            {
                if (FindFileIgnoreCase(dir, n) is { } found)
                {
                    try
                    {
                        return LoadSource(found);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        _warn?.Invoke($"{found}: {e.Message}");
                        return null;
                    }
                }
            }
        }

        return null;
    }

    private string? FindFileIgnoreCase(string dir, string relative)
    {
        var candidate = Path.GetFullPath(Path.Combine(dir, relative));
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var parent = Path.GetDirectoryName(candidate);
        if (parent is null || !Directory.Exists(parent))
        {
            return null;
        }

        if (!_dirListing.TryGetValue(parent, out var listing))
        {
            // Keyed by file name, case-insensitively (first in listing order wins): a linear scan per lookup was
            // O(files x uses) on large flat directories.
            listing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var f in Directory.GetFiles(parent))
                {
                    listing.TryAdd(Path.GetFileName(f), f);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }

            _dirListing[parent] = listing;
        }

        return listing.GetValueOrDefault(Path.GetFileName(candidate));
    }

    // ---------------------------------------------------------------- units

    /// <summary>Resolves a <c>uses</c> entry for <paramref name="from"/> and makes sure its interface is compiled.</summary>
    public CompileUnit UseUnit(string name, string? inPath, CompileUnit from)
    {
        CompileUnit? unit = null;
        var fromDir = from.Path is { } fp ? Path.GetDirectoryName(fp) : null;
        if (inPath is not null && fromDir is not null)
        {
            var target = FindFileIgnoreCase(fromDir, inPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            if (target is not null)
            {
                unit = Register(target, inProject: target.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            }
        }

        var searchDirs = new List<string>();
        if (fromDir is not null)
        {
            searchDirs.Add(fromDir);
        }

        if (_configOf.GetValueOrDefault(from) is { } cfg)
        {
            searchDirs.AddRange(cfg.UnitPaths);
        }

        searchDirs.AddRange(_options.SearchPaths.Select(Path.GetFullPath));
        if (unit is null && _units.TryGetValue(name, out var candidates))
        {
            unit = candidates.FirstOrDefault(c => searchDirs.Any(d => string.Equals(Path.GetDirectoryName(c.Path), d, StringComparison.Ordinal)))
                ?? candidates.OrderBy(c => c.InProject ? 0 : 1).ThenBy(static c => c.Path, StringComparer.Ordinal).First();
        }

        if (unit is null)
        {
            foreach (var dir in searchDirs)
            {
                foreach (var ext in SourceExtensions)
                {
                    if (FindFileIgnoreCase(dir, name + ext) is { } found)
                    {
                        unit = Register(found, inProject: found.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal));
                        break;
                    }
                }

                if (unit is not null)
                {
                    break;
                }
            }
        }

        if (unit is null)
        {
            if (_builtins.TryGetValue(name, out var builtin))
            {
                return builtin;
            }

            if (!_unknown.TryGetValue(name, out var unknown))
            {
                unknown = new CompileUnit(name, UnitFileKind.Unit, Builtins.PackageForUnit(name)) { State = UnitState.Done };
                unknown.Sym = new Sym(name, SymKind.Unit, null)
                {
                    Symbol = unknown.Package.Prefix + Names.Namespace(name),
                    ScipKind = ScipKind.Module,
                    External = true,
                    Unit = unknown,
                    Signature = $"unit {name}",
                };
                unknown.Interface = new Scope(unknown.Sym);
                _unknown[name] = unknown;
            }

            return unknown;
        }

        if (!ReferenceEquals(unit, from))
        {
            EnsureInterface(unit);
        }

        return unit;
    }

    public void EnsureInterface(CompileUnit unit)
    {
        if (unit.IsBuiltin || unit.State != UnitState.NotStarted)
        {
            return;
        }

        var compiler = CreateCompiler(unit);
        if (compiler is null)
        {
            unit.State = UnitState.Done;
            return;
        }

        Guard(unit, compiler.CompileInterface);
        if (unit.State == UnitState.Done)
        {
            Finish(unit, compiler);
        }
    }

    public void EnsureDone(CompileUnit unit)
    {
        EnsureInterface(unit);
        if (unit.State == UnitState.InterfaceDone && _compilers.TryGetValue(unit, out var compiler))
        {
            if (unit.InProject)
            {
                Guard(unit, compiler.CompileImplementation);
            }

            unit.State = UnitState.Done;
            Finish(unit, compiler);
        }
    }

    private void Finish(CompileUnit unit, Compiler compiler)
    {
        _usedUnits[unit] = compiler.UsedUnits;
        _compilers.Remove(unit);
    }

    public IReadOnlyList<CompileUnit> UsedUnitsOf(CompileUnit unit) => _usedUnits.GetValueOrDefault(unit) ?? [];

    private void Guard(CompileUnit unit, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException or KeyNotFoundException or InvalidCastException or FormatException or OverflowException or InsufficientExecutionStackException)
        {
            _warn?.Invoke($"{unit.Path}: indexing stopped early: {e.GetType().Name}: {e.Message}");
            unit.State = UnitState.Done;
        }
    }

    private Compiler? CreateCompiler(CompileUnit unit)
    {
        SourceText src;
        try
        {
            src = LoadSource(unit.Path!);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _warn?.Invoke($"{unit.Path}: {e.Message}");
            return null;
        }

        var cfg = _configOf.GetValueOrDefault(unit);
        var mode = _options.Mode ?? cfg?.Mode ?? DefaultModeFor(unit.Path!);
        var defines = new List<string>(Preprocessor.DefaultDefines);
        if (cfg is not null)
        {
            defines.AddRange(cfg.Defines);
        }

        defines.AddRange(_options.Defines);
        defines.RemoveAll(d => _options.Undefines.Contains(d, StringComparer.OrdinalIgnoreCase));
        var (tokens, pp) = Preprocessor.Run(src, mode, defines, (name, includer) => ResolveInclude(name, includer, unit), _options.Mode is not null || cfg?.Mode is not null);
        var compiler = new Compiler(this, unit, tokens, pp.Mode);
        _compilers[unit] = compiler;
        return compiler;
    }

    private static PascalMode DefaultModeFor(string path) =>
        path.EndsWith(".p", StringComparison.OrdinalIgnoreCase) ? PascalMode.Iso : PascalMode.ObjFpc;

    public int NextOverload(string ownerSymbol, string name)
    {
        var key = ownerSymbol + "\0" + name.ToLowerInvariant();
        var n = _overloads.GetValueOrDefault(key);
        _overloads[key] = n + 1;
        return n;
    }

    /// <summary>An external symbol for a name attributed to an unresolved (or only partially described) unit.</summary>
    public static Sym Infer(CompileUnit unit, string name, InferHint hint)
    {
        if (unit.Interface.Find(name) is { } existing)
        {
            return existing[0];
        }

        var owner = unit.Sym!;
        Sym sym;
        switch (hint)
        {
            case InferHint.Type:
                sym = new Sym(name, SymKind.Type, owner) { Symbol = owner.Symbol + Names.Type(name), ScipKind = ScipKind.Type };
                sym.Type = new PType(TypeKind.Unknown) { Decl = sym, Members = new Scope(sym) };
                break;
            case InferHint.Call:
                sym = new Sym(name, SymKind.Routine, owner)
                {
                    Symbol = owner.Symbol + Names.Method(name, 0),
                    ScipKind = ScipKind.Function,
                    Routine = new RoutineInfo(RoutineKind.Function) { MaxArgs = int.MaxValue, HasBody = true },
                };
                break;
            default:
                sym = new Sym(name, SymKind.Var, owner) { Symbol = owner.Symbol + Names.Term(name), ScipKind = ScipKind.UnspecifiedKind };
                break;
        }

        sym.External = true;
        sym.Inferred = true;
        unit.Interface.Add(sym);
        return sym;
    }

    /// <summary>An external member of an external type that the built-in table does not describe.</summary>
    public static Sym InferMember(PType type, string name, bool call)
    {
        if (type.Members!.Find(name) is { } existing)
        {
            return existing[0];
        }

        var owner = type.Decl!;
        var sym = call
            ? new Sym(name, SymKind.Routine, owner)
            {
                Symbol = owner.Symbol + Names.Method(name, 0),
                ScipKind = ScipKind.Method,
                Routine = new RoutineInfo(RoutineKind.Function) { MaxArgs = int.MaxValue, HasBody = true },
            }
            : new Sym(name, SymKind.Field, owner) { Symbol = owner.Symbol + Names.Term(name), ScipKind = ScipKind.UnspecifiedKind };
        sym.External = true;
        sym.Inferred = true;
        type.Members.Add(sym);
        return sym;
    }

    // ---------------------------------------------------------------- run

    /// <summary>Compiles every project unit, indexes forms, and returns documents (sorted) and external symbols.</summary>
    public (List<Document> Documents, List<SymbolInformation> External) Run()
    {
        Discover();
        foreach (var unit in ProjectUnits)
        {
            EnsureDone(unit);
        }

        foreach (var lfm in _lfmFiles)
        {
            new LfmIndexer(this).Index(lfm);
        }

        var docs = Emit.Documents.Select(static d => d.Document).OrderBy(static d => d.RelativePath, StringComparer.Ordinal).ToList();
        var external = Emit.Finish();
        return (docs, external);
    }

    /// <summary>The project unit compiled from <paramref name="path"/> (if any).</summary>
    public CompileUnit? UnitAt(string path) => _byPath.GetValueOrDefault(Path.GetFullPath(path));
}
