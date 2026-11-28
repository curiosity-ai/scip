using System.Text;
using ScipSharp.Indexer.Abap.Files;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Objects;
using ScipSharp.Indexer.Abap.Syntax;
using ScipSharp.Indexer.Abap.Types;
using Ex = ScipSharp.Indexer.Abap.Statements.Expressions;
using Id = ScipSharp.Indexer.Abap.FileInformation.Identifier;
using Kind = ScipSharp.SymbolInformation.Types.Kind;
using St = ScipSharp.Indexer.Abap.Statements.Statements;

namespace ScipSharp.Indexer.Abap;

/// <summary>Settings of one indexing run.</summary>
/// <param name="Release">abaplint release name (e.g. <c>v740sp08</c>); null keeps the configured or default one.</param>
/// <param name="ConfigPath">An <c>abaplint.json</c> to read; null looks for one in the project root.</param>
public sealed record AbapIndexerSettings(
    string? Release = null,
    string? ConfigPath = null,
    string? PackageName = null,
    string PackageVersion = ".",
    IReadOnlyList<string>? Arguments = null);

/// <summary>
/// Indexes a folder of ABAP sources in abapGit layout (<c>zcl_foo.clas.abap</c>, <c>zprog.prog.abap</c>,
/// <c>ztable.tabl.xml</c>, …): parses them, runs the syntax check and turns its scopes and references into SCIP.
/// </summary>
internal sealed class AbapProjectIndexer
{
    public const string ToolName = "scip-abap";
    public const string ToolVersion = "0.1.0";

    private readonly string _root;
    private readonly AbapIndexerSettings _settings;
    private readonly Action<string> _log;
    private readonly AbapSymbols _symbols;

    private readonly Dictionary<string, FileState> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<DeclKey, string> _declarations = [];
    private readonly Dictionary<string, SymbolState> _symbolStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _objectSymbolByXml = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SymbolInformation> _external = new(StringComparer.Ordinal);
    private readonly HashSet<string> _programs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _functionModules = new(StringComparer.Ordinal);
    private readonly HashSet<string> _globalOo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ddicByName = new(StringComparer.Ordinal);
    private readonly List<(IInterfaceDefinition Def, string Symbol)> _ooDefinitions = [];
    private Dictionary<string, IInterfaceDefinition>? _ooBySymbol;
    private int _localCounter;

    public AbapProjectIndexer(string root, AbapIndexerSettings settings, Action<string> log)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        _settings = settings;
        _log = log;
        _symbols = new AbapSymbols(settings.PackageName ?? Path.GetFileName(_root), settings.PackageVersion);
    }

    /// <summary>Number of syntax check runs that threw (the objects are indexed without their references).</summary>
    public int FailedSyntaxRuns { get; private set; }

    public Metadata CreateMetadata()
    {
        var meta = new Metadata
        {
            Version = ProtocolVersion.UnspecifiedProtocolVersion,
            ProjectRoot = new Uri(_root + Path.DirectorySeparatorChar).AbsoluteUri,
            TextDocumentEncoding = TextEncoding.Utf8,
            ToolInfo = new ToolInfo { Name = ToolName, Version = ToolVersion },
        };
        meta.ToolInfo.Arguments.AddRange(_settings.Arguments ?? []);
        return meta;
    }

    /// <summary>Indexes the project: every document goes to <paramref name="onDocument"/>; returns the external symbols.</summary>
    public List<SymbolInformation> Run(Action<Document> onDocument, CancellationToken cancellationToken = default)
    {
        var reg = LoadRegistry();
        var objects = reg.GetObjects().OrderBy(o => o.GetType(), StringComparer.Ordinal).ThenBy(o => o.GetName(), StringComparer.Ordinal).ToList();

        var results = new List<ISyntaxResult>();
        foreach (var o in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (o is not ABAPObject abap)
            {
                continue;
            }

            try
            {
                results.Add(new SyntaxLogic(reg, abap).Run());
            }
#pragma warning disable CA1031 // one object the checker cannot handle must not stop the run
            catch (Exception e)
#pragma warning restore CA1031
            {
                FailedSyntaxRuns++;
                _log($"syntax check of {o.GetType()} {o.GetName()} failed: {e.Message}");
            }
        }

        DeclareObjects(objects);
        var scopes = results.SelectMany(r => AllScopes(r.Spaghetti.GetTop(), [])).ToList();
        DeclareObjectOriented(objects, scopes);
        DeclareScopes(scopes);
        AddRelationships();
        foreach (var (node, _) in scopes)
        {
            foreach (var r in node.GetData().References)
            {
                AddReference(r);
            }
        }

        foreach (var f in _files.Values.Where(f => f.Abap != null))
        {
            StatementOccurrences(f);
        }

        // grouped once: picking each file's symbols out of all symbols made writing the documents quadratic
        var symbolsByFile = _symbolStates.Values.ToLookup(s => s.File, StringComparer.Ordinal);
        foreach (var f in _files.Values.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = f.Build(_symbolStates, symbolsByFile[f.Path], _external);
            if (doc.Occurrences.Count > 0 || doc.Symbols.Count > 0 || f.Abap != null)
            {
                onDocument(doc);
            }
        }

        return [.. _external.Values.OrderBy(s => s.Symbol, StringComparer.Ordinal)];
    }

    // ------------------------------------------------------------------ loading
    private static readonly string[] ConfigNames = ["abaplint.json", "abaplint.jsonc", ".abaplint.json"];

    private Registry LoadRegistry()
    {
        var configPath = _settings.ConfigPath ?? ConfigNames
            .Select(n => Path.Combine(_root, n)).FirstOrDefault(File.Exists);
        var config = configPath != null ? new Config(File.ReadAllText(configPath)) : Config.GetDefault();
        var conf = config.Get();
        if (_settings.Release != null)
        {
            if (!Release.TryGet(_settings.Release, out _))
            {
                throw new InvalidOperationException($"unknown ABAP release '{_settings.Release}'");
            }

            conf.Syntax.Version = VersionOldOrNew.FromRelease(_settings.Release, LanguageVersion.Normal);
        }

        var reg = new Registry(new Config(conf));
        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
                     .Where(IsSource)
                     .Select(p => Path.GetRelativePath(_root, p).Replace(Path.DirectorySeparatorChar, '/'))
                     .Where(p => !p.Split('/').Any(part => part.StartsWith('.') || part is "node_modules" or "bin" or "obj"))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var text = new UTF8Encoding(false, false).GetString(File.ReadAllBytes(Path.Combine(_root, path)));
            reg.AddFile(new MemoryFile(path, text));
            _files[path] = new FileState(path, text);
        }

        reg.Parse();
        foreach (var o in reg.GetObjects())
        {
            foreach (var file in o.GetFiles())
            {
                if (_files.TryGetValue(file.GetFilename(), out var state))
                {
                    state.Owner = o;
                    state.Abap = (o as ABAPObject)?.GetABAPFileByName(file.GetFilename());
                }
            }
        }

        return reg;
    }

    private static bool IsSource(string path) =>
        path.EndsWith(".abap", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ declarations
    private void DeclareObjects(List<IObject> objects)
    {
        foreach (var o in objects)
        {
            var name = o.GetName();
            switch (o)
            {
                case Class or Interface:
                    _globalOo.Add(AbapSymbols.Upper(name));
                    break;
                case Program or FunctionGroup or TypePool:
                    _programs.Add(AbapSymbols.Upper(ProgramName(o)));
                    var ns = ObjectNamespace(ProgramName(o));
                    var main = (o as ABAPObject)?.GetMainABAPFile() ?? (o as ABAPObject)?.GetABAPFiles().FirstOrDefault();
                    if (main != null && _files.TryGetValue(main.GetFilename(), out var mf))
                    {
                        Info(ns, ProgramName(o), o is FunctionGroup ? Kind.Module : Kind.Namespace, mf, o.GetDescription());
                    }

                    if (o is FunctionGroup fg)
                    {
                        foreach (var m in fg.GetModules())
                        {
                            _functionModules.Add(AbapSymbols.Upper(m.GetName()));
                        }
                    }

                    break;
                default:
                    var xml = o.GetXMLFile();
                    if (xml != null && _files.TryGetValue(xml.GetFilename(), out var xf))
                    {
                        var symbol = _symbols.Global(AbapSymbols.Type(name));
                        _objectSymbolByXml[xf.Path] = symbol;
                        _ddicByName[AbapSymbols.Upper(name)] = symbol;
                        Info(symbol, name, DdicKind(o.GetType()), xf, o.GetDescription(), $"{o.GetType()} {AbapSymbols.Upper(name)}");
                        var at = xf.Find(name);
                        if (at is { } range)
                        {
                            xf.Add(range, symbol, SymbolRole.Definition);
                        }
                    }

                    break;
            }
        }

        // object identifiers of classes/interfaces also point at their XML (1,1)
        foreach (var o in objects.Where(o => o is Class or Interface))
        {
            if (o.GetXMLFile() is { } xml && _files.ContainsKey(xml.GetFilename()))
            {
                _objectSymbolByXml[xml.GetFilename()] = _symbols.Global(AbapSymbols.Type(o.GetName()));
            }
        }
    }

    private static Kind DdicKind(string type) => type switch
    {
        "TABL" or "VIEW" or "DDLS" => Kind.Struct,
        "DTEL" or "TTYP" => Kind.TypeAlias,
        "DOMA" => Kind.Type,
        "MSAG" => Kind.Namespace,
        "TRAN" => Kind.Function,
        _ => Kind.Type,
    };

    private string ObjectNamespace(string objectName) => _symbols.Global(AbapSymbols.Namespace(objectName));

    /// <summary>The program an object's code runs in: a function group's globals belong to its main program SAPL&lt;name&gt;.</summary>
    private static string ProgramName(IObject o)
    {
        if (o is not FunctionGroup)
        {
            return o.GetName();
        }

        var name = o.GetName();
        var slash = name.LastIndexOf('/');
        return slash > 0 ? name[..(slash + 1)] + "SAPL" + name[(slash + 1)..] : "SAPL" + name;
    }

    private string OwnerName(string filename) =>
        _files.TryGetValue(filename, out var f) && f.Owner != null ? ProgramName(f.Owner) : Path.GetFileName(filename).Split('.')[0];

    private void DeclareObjectOriented(List<IObject> objects, List<(ISpaghettiScopeNode Node, IReadOnlyList<ISpaghettiScopeNode> Chain)> scopes)
    {
        var defs = new List<IInterfaceDefinition>();
        foreach (var o in objects)
        {
            switch (o)
            {
                case Class c when c.GetDefinition() is { } cd: defs.Add(cd); break;
                case Interface i when i.GetDefinition() is { } id: defs.Add(id); break;
            }
        }

        foreach (var (node, _) in scopes)
        {
            defs.AddRange(node.GetData().Cdefs.Values);
            defs.AddRange(node.GetData().Idefs.Values);
        }

        foreach (var def in defs)
        {
            if (!_files.ContainsKey(def.GetFilename()))
            {
                continue;
            }

            var key = Key(def.GetFilename(), def.GetToken(), def.GetName());
            if (_declarations.ContainsKey(key))
            {
                continue;
            }

            var isClass = def is IClassDefinition;
            var symbol = def.IsGlobal()
                ? _symbols.Global(AbapSymbols.Type(def.GetName()))
                : _symbols.Global(AbapSymbols.Namespace(OwnerName(def.GetFilename())), AbapSymbols.Type(def.GetName()));
            Declare(def.GetFilename(), def.GetToken(), def.GetName(), symbol, isClass ? Kind.Class : Kind.Interface);
            _ooDefinitions.Add((def, symbol));

            foreach (var m in def.GetMethodDefinitions()?.GetAll() ?? [])
            {
                if (m.GetName().IndexOf('~', StringComparison.Ordinal) is var tilde and > 0)
                {
                    // METHODS zif_x~meth REDEFINITION: the interface's method, not a new one
                    var target = AbapSymbols.Child(ResolveOo(m.GetName()[..tilde], m.GetFilename()), AbapSymbols.Method(m.GetName()[(tilde + 1)..]));
                    _declarations.TryAdd(Key(m.GetFilename(), m.GetToken(), m.GetName()), target);
                    EnsureExternal(target, Kind.Method);
                    if (_files.TryGetValue(m.GetFilename(), out var mf) && m.GetToken().GetStart() is not VirtualPosition)
                    {
                        var r = FileState.RangeOf(m.GetToken());
                        mf.Add(r.Length == 3 ? [r[0], r[1] + tilde + 1, r[2]] : r, target, SymbolRole.UnspecifiedSymbolRole);
                    }

                    continue;
                }

                var ms = AbapSymbols.Child(symbol, AbapSymbols.Method(m.GetName()));
                var mk = m.GetName().Equals("constructor", StringComparison.OrdinalIgnoreCase) || m.GetName().Equals("class_constructor", StringComparison.OrdinalIgnoreCase)
                    ? Kind.Constructor
                    : m.IsStatic() ? Kind.StaticMethod : m.IsAbstract() ? Kind.AbstractMethod : Kind.Method;
                Declare(m.GetFilename(), m.GetToken(), m.GetName(), ms, mk);
                foreach (var p in m.GetParameters()?.GetAll() ?? [])
                {
                    Declare(p.GetFilename(), p.GetToken(), p.GetName(), AbapSymbols.Child(ms, AbapSymbols.Parameter(p.GetName())), Kind.Parameter);
                }
            }

            var attributes = def.GetAttributes();
            foreach (var a in attributes?.GetAll() ?? [])
            {
                var kind = a.GetMeta().Contains(IdentifierMeta.Static) ? Kind.StaticField : Kind.Field;
                Declare(a.GetFilename(), a.GetToken(), a.GetName(), AbapSymbols.Child(symbol, AbapSymbols.Term(a.GetName())), kind);
            }

            foreach (var c in attributes?.GetConstants() ?? [])
            {
                Declare(c.GetFilename(), c.GetToken(), c.GetName(), AbapSymbols.Child(symbol, AbapSymbols.Term(c.GetName())), Kind.Constant);
            }

            foreach (var t in def.GetTypeDefinitions()?.GetAll() ?? [])
            {
                Declare(t.Type.GetFilename(), t.Type.GetToken(), t.Type.GetName(), AbapSymbols.Child(symbol, AbapSymbols.Type(t.Type.GetName())), Kind.Type);
            }

            foreach (var e in def.GetEvents() ?? [])
            {
                if (e is not Id eid)
                {
                    continue;
                }

                var es = AbapSymbols.Child(symbol, AbapSymbols.Term(e.GetName()));
                Declare(eid.GetFilename(), eid.GetToken(), e.GetName(), es, Kind.Event);
                foreach (var p in e.GetParameters() ?? [])
                {
                    Declare(p.GetFilename(), p.GetToken(), p.GetName(), AbapSymbols.Child(es, AbapSymbols.Parameter(p.GetName())), Kind.Parameter);
                }
            }

            foreach (var a in def.GetAliases() ?? [])
            {
                Declare(a.GetFilename(), a.GetToken(), a.GetName(), AbapSymbols.Child(symbol, AbapSymbols.Term(a.GetName())), Kind.Method);
            }
        }
    }

    private static readonly HashSet<string> ProcedureScopes = new(StringComparer.Ordinal)
    {
        ScopeType.Method, ScopeType.MethodInstance, ScopeType.MethodDefinition, ScopeType.Form, ScopeType.FunctionModule,
    };

    private static readonly HashSet<string> ProgramScopes = new(StringComparer.Ordinal)
    {
        ScopeType.Program, ScopeType.FunctionGroup, ScopeType.TypePool, ScopeType.SelectionEvent,
    };

    private void DeclareScopes(List<(ISpaghettiScopeNode Node, IReadOnlyList<ISpaghettiScopeNode> Chain)> scopes)
    {
        // forms and function modules first: their parameters are declared with them
        foreach (var (node, _) in scopes)
        {
            foreach (var form in node.GetData().Forms)
            {
                if (!_files.ContainsKey(form.GetFilename()))
                {
                    continue;
                }

                var fs = _symbols.Global(AbapSymbols.Namespace(OwnerName(form.GetFilename())), AbapSymbols.Method(form.GetName()));
                Declare(form.GetFilename(), form.GetToken(), form.GetName(), fs, Kind.Function);
                foreach (var p in form.GetTablesParameters().Concat(form.GetUsingParameters()).Concat(form.GetChangingParameters()))
                {
                    Declare(p.GetFilename(), p.GetToken(), p.GetName(), AbapSymbols.Child(fs, AbapSymbols.Parameter(p.GetName())), Kind.Parameter);
                }
            }
        }

        foreach (var (node, chain) in scopes)
        {
            var data = node.GetData();
            foreach (var (isType, id) in data.Types.Values.Select(t => (true, t)).Concat(data.Vars.Values.Select(v => (false, v))))
            {
                if (!_files.ContainsKey(id.GetFilename()) || _declarations.ContainsKey(Key(id.GetFilename(), id.GetToken(), id.GetName())))
                {
                    continue;
                }

                var meta = id.GetMeta();
                var fm = chain.LastOrDefault(s => s.GetIdentifier().Stype == ScopeType.FunctionModule);
                if (fm != null && meta.Any(m => m.StartsWith("function_module_", StringComparison.Ordinal)))
                {
                    var fms = _symbols.Global(AbapSymbols.Method(fm.GetIdentifier().Sname));
                    Declare(id.GetFilename(), id.GetToken(), id.GetName(), AbapSymbols.Child(fms, AbapSymbols.Parameter(id.GetName())), Kind.Parameter, occurrence: false);
                    continue;
                }

                var inProcedure = chain.Any(s => ProcedureScopes.Contains(s.GetIdentifier().Stype));
                var stype = node.GetIdentifier().Stype;
                string symbol;
                Kind kind;
                if (!inProcedure && ProgramScopes.Contains(stype))
                {
                    var owner = AbapSymbols.Namespace(OwnerName(id.GetFilename()));
                    symbol = _symbols.Global(owner, isType ? AbapSymbols.Type(id.GetName()) : AbapSymbols.Term(id.GetName()));
                    kind = isType ? Kind.Type : meta.Contains(IdentifierMeta.ReadOnly) ? Kind.Constant : Kind.Variable;
                }
                else
                {
                    symbol = $"local {_localCounter++}";
                    kind = isType ? Kind.Type : Kind.Variable;
                }

                Declare(id.GetFilename(), id.GetToken(), id.GetName(), symbol, kind);
            }
        }
    }

    private static IEnumerable<(ISpaghettiScopeNode Node, IReadOnlyList<ISpaghettiScopeNode> Chain)> AllScopes(ISpaghettiScopeNode node, List<ISpaghettiScopeNode> chain)
    {
        var mine = new List<ISpaghettiScopeNode>(chain) { node };
        yield return (node, mine);
        foreach (var c in node.GetChildren())
        {
            foreach (var x in AllScopes(c, mine))
            {
                yield return x;
            }
        }
    }

    private static DeclKey Key(string file, AbstractToken token, string name) =>
        new(file, token.GetStart().GetRow(), token.GetStart().GetCol(), AbapSymbols.Upper(name));

    /// <summary>Records a declaration; adds a definition occurrence when the token really spells the name.</summary>
    private void Declare(string file, AbstractToken token, string name, string symbol, Kind kind, bool occurrence = true)
    {
        if (!_files.TryGetValue(file, out var f))
        {
            return;
        }

        var key = Key(file, token, name);
        if (!_declarations.TryAdd(key, symbol))
        {
            return;
        }

        if (_symbolStates.ContainsKey(symbol))
        {
            return; // e.g. the same parameter seen from two syntax runs at another position
        }

        var spelled = occurrence && AbapSymbols.Upper(token.GetStr()) == AbapSymbols.Upper(name) && token.GetStart() is not VirtualPosition;
        var docs = spelled ? f.Documentation(token) : null;
        Info(symbol, name.TrimStart('!'), kind, f, null, docs);
        if (spelled)
        {
            f.Add(FileState.RangeOf(token), symbol, SymbolRole.Definition);
        }
    }

    private void Info(string symbol, string displayName, Kind kind, FileState file, string? description, string? signature = null)
    {
        if (_symbolStates.ContainsKey(symbol))
        {
            return;
        }

        var info = new SymbolInformation { Symbol = symbol, DisplayName = displayName, Kind = kind };
        if (signature != null)
        {
            info.Documentation.Add($"```abap\n{signature}\n```");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            info.Documentation.Add(description);
        }

        _symbolStates[symbol] = new SymbolState(info, file.Path);
    }

    // ------------------------------------------------------------------ relationships
    private void AddRelationships()
    {
        foreach (var (def, symbol) in _ooDefinitions)
        {
            var info = _symbolStates[symbol].Info;
            var supers = new List<string>();
            if (def.GetSuperClass() is { Length: > 0 } super)
            {
                supers.Add(ResolveOo(super, def.GetFilename()));
            }

            foreach (var i in def.GetImplementing() ?? [])
            {
                supers.Add(ResolveOo(i.Name, def.GetFilename()));
            }

            foreach (var s in supers.Distinct(StringComparer.Ordinal))
            {
                info.Relationships.Add(new Relationship { Symbol = s, IsImplementation = true, IsReference = true });
                EnsureExternal(s, Kind.Class);
            }

            if (supers.Count > 0 && def.GetSuperClass() is { Length: > 0 })
            {
                foreach (var m in (def.GetMethodDefinitions()?.GetAll() ?? []).Where(m => m.IsRedefinition() && !m.GetName().Contains('~', StringComparison.Ordinal)))
                {
                    var ms = AbapSymbols.Child(symbol, AbapSymbols.Method(m.GetName()));
                    if (_symbolStates.TryGetValue(ms, out var state) && RedefinedMethod(supers[0], m.GetName()) is { } target)
                    {
                        state.Info.Relationships.Add(new Relationship { Symbol = target, IsImplementation = true, IsReference = true });
                        EnsureExternal(target, Kind.Method);
                    }
                }
            }
        }
    }

    /// <summary>The method a redefinition overrides: the nearest super class that declares it, or the external super class.</summary>
    private string? RedefinedMethod(string superSymbol, string method)
    {
        var bySymbol = _ooBySymbol ??= _ooDefinitions.GroupBy(d => d.Symbol, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Def, StringComparer.Ordinal);
        var current = superSymbol;
        for (var depth = 0; depth < 50; depth++)
        {
            var candidate = AbapSymbols.Child(current, AbapSymbols.Method(method));
            if (_symbolStates.ContainsKey(candidate) || AbapSymbols.IsExternal(current))
            {
                return candidate;
            }

            if (!bySymbol.TryGetValue(current, out var def) || def.GetSuperClass() is not { Length: > 0 } next)
            {
                return null;
            }

            current = ResolveOo(next, def.GetFilename());
        }

        return null;
    }

    /// <summary>The symbol of a class or interface named in <paramref name="fromFile"/>: local to its object, project-global or external.</summary>
    private string ResolveOo(string name, string fromFile)
    {
        var local = _symbols.Global(AbapSymbols.Namespace(OwnerName(fromFile)), AbapSymbols.Type(name));
        if (_symbolStates.ContainsKey(local))
        {
            return local;
        }

        var global = _symbols.Global(AbapSymbols.Type(name));
        return _symbolStates.ContainsKey(global) || _globalOo.Contains(AbapSymbols.Upper(name)) ? global : _symbols.External(AbapSymbols.Type(name));
    }

    private void EnsureExternal(string symbol, Kind kind)
    {
        if (AbapSymbols.IsExternal(symbol) && !_external.ContainsKey(symbol))
        {
            var parsed = SymbolParser.Parse(symbol);
            _external[symbol] = new SymbolInformation { Symbol = symbol, DisplayName = parsed.Descriptors[^1].Name, Kind = kind };
        }
    }

    // ------------------------------------------------------------------ references
    private void AddReference(IReference r)
    {
        var file = r.Position.GetFilename();
        if (!_files.TryGetValue(file, out var f) || f.Abap == null)
        {
            return;
        }

        var token = r.Position.GetToken();
        if (token.GetStart() is VirtualPosition)
        {
            return;
        }

        var (symbol, kind) = Resolve(r, token);
        if (symbol == null)
        {
            return;
        }

        if (SymbolParser.IsLocalSymbol(symbol) && (!_symbolStates.TryGetValue(symbol, out var local) || local.File != file))
        {
            return;
        }

        var role = r.ReferenceType switch
        {
            ReferenceType.DataWriteReference => SymbolRole.WriteAccess,
            ReferenceType.DataReadReference => SymbolRole.ReadAccess,
            _ => SymbolRole.UnspecifiedSymbolRole,
        };
        var range = FileState.RangeOf(token);
        var tilde = token.GetStr().IndexOf('~', StringComparison.Ordinal);
        if (tilde > 0 && range.Length == 3)
        {
            // zif_x~meth: the interface on the left, the component on the right
            range = symbol.EndsWith('#') ? [range[0], range[1], range[1] + tilde] : [range[0], range[1] + tilde + 1, range[2]];
        }

        if (symbol.StartsWith(AbapSymbols.Scheme, StringComparison.Ordinal) && AbapSymbols.IsExternal(symbol))
        {
            EnsureExternal(symbol, kind);
        }

        f.Add(range, symbol, role);
    }

    private (string? Symbol, Kind Kind) Resolve(IReference r, AbstractToken token)
    {
        switch (r.ReferenceType)
        {
            case ReferenceType.BuiltinMethodReference:
            case ReferenceType.BuiltinTypeReference:
            case ReferenceType.InferredType:
            case ReferenceType.ConstructorReference:
            case ReferenceType.ObjectOrientedUnknownReference:
                return (null, default);
        }

        var name = token.GetStr();
        if (r.Resolved is { } resolved)
        {
            var rf = resolved.GetFilename();
            if (_declarations.TryGetValue(Key(rf, resolved.GetToken(), resolved.GetName()), out var declared))
            {
                return (declared, default);
            }

            if (_objectSymbolByXml.TryGetValue(rf, out var objectSymbol))
            {
                return (objectSymbol, default);
            }

            if (_files.ContainsKey(rf) || rf == BuiltIn.filename)
            {
                return (null, default);
            }

            // declared outside the sources (a dependency): name it by what the reference says
            return r.ReferenceType switch
            {
                ReferenceType.ObjectOrientedReference => (_symbols.External(AbapSymbols.Type(r.Extra?.OoName ?? resolved.GetName())), Kind.Class),
                ReferenceType.TableReference or ReferenceType.TypeReference => (_symbols.External(AbapSymbols.Type(resolved.GetName())), Kind.Type),
                _ => (null, default),
            };
        }

        return r.ReferenceType switch
        {
            ReferenceType.ObjectOrientedVoidReference => (_symbols.External(AbapSymbols.Type(r.Extra?.OoName ?? name)), Kind.Class),
            ReferenceType.TableVoidReference => (_symbols.External(AbapSymbols.Type(name)), Kind.Struct),
            ReferenceType.VoidType when IsName(name) => (_symbols.External(AbapSymbols.Type(name)), Kind.Type),
            _ => (null, default),
        };
    }

    private static bool IsName(string s) => s.Length > 0 && s.All(c => char.IsLetterOrDigit(c) || c is '_' or '/' or '!');

    // ------------------------------------------------------------------ statement based occurrences
    private void StatementOccurrences(FileState f)
    {
        foreach (var s in f.Abap!.GetStatements())
        {
            // the syntax check resolves dictionary types and tables without recording a reference
            foreach (var e in s.FindAllExpressionsRecursive<Ex.TypeName>().Concat(s.FindAllExpressionsRecursive<Ex.DatabaseTable>()))
            {
                var t = e.GetFirstToken();
                if (t.GetStart() is not VirtualPosition && _ddicByName.TryGetValue(AbapSymbols.Upper(t.GetStr()), out var ddic))
                {
                    f.Add(FileState.RangeOf(t), ddic, SymbolRole.UnspecifiedSymbolRole);
                }
            }

            switch (s.Get())
            {
                case St.ClassImplementation:
                    if (s.FindDirectExpression<Ex.ClassName>()?.GetFirstToken() is { } ct && ct.GetStart() is not VirtualPosition)
                    {
                        var cls = ResolveOo(ct.GetStr(), f.Path);
                        if (!AbapSymbols.IsExternal(cls))
                        {
                            f.Add(FileState.RangeOf(ct), cls, SymbolRole.UnspecifiedSymbolRole);
                        }
                    }

                    break;
                case St.FunctionModule:
                    if (s.FindFirstExpression<Ex.Field>()?.GetFirstToken() is { } ft && ft.GetStart() is not VirtualPosition)
                    {
                        var symbol = _symbols.Global(AbapSymbols.Method(ft.GetStr()));
                        Info(symbol, ft.GetStr(), Kind.Function, f, null, f.Documentation(ft));
                        f.Add(FileState.RangeOf(ft), symbol, SymbolRole.Definition);
                    }

                    break;
                case St.CallFunction:
                    if (s.FindDirectExpression<Ex.FunctionName>()?.FindDirectExpression<Ex.Constant>()?.GetFirstToken() is { } cf)
                    {
                        AddQuotedName(f, cf, n => _functionModules.Contains(n) ? _symbols.Global(AbapSymbols.Method(n)) : _symbols.External(AbapSymbols.Method(n)), Kind.Function);
                    }

                    break;
                case St.Include:
                case St.Submit:
                    if (s.FindDirectExpression<Ex.IncludeName>()?.GetFirstToken() is { } it && it.GetStart() is not VirtualPosition && IsName(it.GetStr()))
                    {
                        var n = AbapSymbols.Upper(it.GetStr());
                        var symbol = _programs.Contains(n) ? ObjectNamespace(n) : _symbols.External(AbapSymbols.Namespace(n));
                        EnsureExternal(symbol, Kind.Namespace);
                        f.Add(FileState.RangeOf(it), symbol, SymbolRole.Import);
                    }

                    break;
                case St.Report:
                case St.Program:
                case St.FunctionPool:
                case St.TypePool:
                    if (s.FindFirstExpression<Ex.ReportName>() is { } rn && f.Owner != null
                        && AbapSymbols.Upper(rn.GetFirstToken().GetStr()) == AbapSymbols.Upper(f.Owner.GetName()))
                    {
                        f.Add(FileState.RangeOf(rn.GetFirstToken()), ObjectNamespace(ProgramName(f.Owner)), SymbolRole.Definition);
                    }

                    break;
            }
        }
    }

    private void AddQuotedName(FileState f, AbstractToken token, Func<string, string> symbolOf, Kind kind)
    {
        var text = token.GetStr();
        if (token.GetStart() is VirtualPosition || text.Length < 3 || text[0] is not ('\'' or '`'))
        {
            return;
        }

        var name = text[1..^1].Trim();
        if (!IsName(name))
        {
            return;
        }

        var symbol = symbolOf(AbapSymbols.Upper(name));
        EnsureExternal(symbol, kind);
        var start = token.GetStart();
        f.Add([start.GetRow() - 1, start.GetCol(), start.GetCol() + name.Length], symbol, SymbolRole.UnspecifiedSymbolRole);
    }

    // ------------------------------------------------------------------ state
    private readonly record struct DeclKey(string File, int Row, int Col, string Name);

    private sealed record SymbolState(SymbolInformation Info, string File);

    private sealed class FileState(string path, string text)
    {
        private readonly Dictionary<(int, int, int, int, string), int> _occurrences = [];
        private readonly List<(int[] Range, Occurrence Occurrence)> _list = [];
        private string[]? _rows;

        public string Path { get; } = path;

        public IObject? Owner { get; set; }

        public ABAPFile? Abap { get; set; }

        private string[] Rows => _rows ??= text.Split('\n');

        public static int[] RangeOf(AbstractToken token)
        {
            var s = token.GetStart();
            var e = token.GetEnd();
            return s.GetRow() == e.GetRow()
                ? [s.GetRow() - 1, s.GetCol() - 1, e.GetCol() - 1]
                : [s.GetRow() - 1, s.GetCol() - 1, e.GetRow() - 1, e.GetCol() - 1];
        }

        public void Add(int[] range, string symbol, SymbolRole role)
        {
            var key = range.Length == 3
                ? (range[0], range[1], range[0], range[2], symbol)
                : (range[0], range[1], range[2], range[3], symbol);
            if (_occurrences.TryGetValue(key, out var at))
            {
                _list[at].Occurrence.SymbolRoles |= (int)role;
                return;
            }

            var occurrence = new Occurrence { Symbol = symbol, SymbolRoles = (int)role };
            occurrence.SetSourceRange(ScipSharp.Range.CreateUnchecked(range));
            _occurrences[key] = _list.Count;
            _list.Add((range, occurrence));
        }

        /// <summary>The range of the first spelling of <paramref name="name"/> as an XML element value, e.g. <c>&gt;ZTABLE&lt;</c>.</summary>
        public int[]? Find(string name)
        {
            var needle = ">" + name + "<";
            for (var i = 0; i < Rows.Length; i++)
            {
                var col = Rows[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (col >= 0)
                {
                    return [i, col + 1, col + 1 + name.Length];
                }
            }

            return null;
        }

        private Dictionary<int, List<StatementNode>>? _statementsByRow;

        /// <summary>
        /// The statements that span the row, in file order. Looking the statement of a declaration up among all statements
        /// of the file made declaring a file's names quadratic.
        /// </summary>
        private List<StatementNode> StatementsOnRow(int row)
        {
            if (_statementsByRow == null)
            {
                _statementsByRow = [];
                foreach (var s in Abap!.GetStatements())
                {
                    for (var r = s.GetStart().GetRow(); r <= s.GetEnd().GetRow(); r++)
                    {
                        if (!_statementsByRow.TryGetValue(r, out var list))
                        {
                            _statementsByRow[r] = list = [];
                        }
                        list.Add(s);
                    }
                }
            }

            return _statementsByRow.TryGetValue(row, out var found) ? found : [];
        }

        /// <summary>The declaring statement, preceded by its ABAP Doc comment (<c>"!</c> lines).</summary>
        public string? Documentation(AbstractToken token)
        {
            if (Abap == null)
            {
                return null;
            }

            var p = token.GetStart();
            var statement = StatementsOnRow(p.GetRow()).FirstOrDefault(s => !p.IsBefore(s.GetStart()) && !s.GetEnd().IsBefore(p));
            if (statement == null)
            {
                return null;
            }

            var b = new StringBuilder();
            var doc = new List<string>();
            for (var row = statement.GetStart().GetRow() - 2; row >= 0 && row < Rows.Length; row--)
            {
                var line = Rows[row].Trim();
                if (!line.StartsWith("\"!", StringComparison.Ordinal))
                {
                    break;
                }

                doc.Insert(0, line);
            }

            foreach (var line in doc)
            {
                b.Append(line).Append('\n');
            }

            var text = statement.ConcatTokens();
            return b.Append(text.Length > 400 ? text[..400] + " …" : text).ToString();
        }

        public Document Build(Dictionary<string, SymbolState> symbols, IEnumerable<SymbolState> declaredHere, Dictionary<string, SymbolInformation> external)
        {
            bool Known(string symbol) => SymbolParser.IsLocalSymbol(symbol) || symbols.ContainsKey(symbol) || external.ContainsKey(symbol);

            var doc = new Document
            {
                RelativePath = Path,
                Language = Path.EndsWith(".abap", StringComparison.OrdinalIgnoreCase) ? "abap" : "xml",
                PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
            };
            doc.Occurrences.AddRange(_list.Where(o => Known(o.Occurrence.Symbol)).OrderBy(o => o.Range[0]).ThenBy(o => o.Range[1]).Select(o => o.Occurrence));
            foreach (var info in declaredHere.Select(s => s.Info).OrderBy(s => s.Symbol, StringComparer.Ordinal))
            {
                var dangling = info.Relationships.Where(r => !Known(r.Symbol)).ToList();
                foreach (var r in dangling)
                {
                    info.Relationships.Remove(r);
                }

                doc.Symbols.Add(info);
            }
            return doc;
        }
    }
}
