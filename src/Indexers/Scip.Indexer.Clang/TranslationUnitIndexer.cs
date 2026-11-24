namespace Scip.Indexer.Clang;

/// <summary>Occurrences and symbols collected for one file (document).</summary>
public sealed class DocumentAccumulator
{
    public HashSet<(int Line, int StartCol, int EndCol, string Symbol, int Roles)> Occurrences { get; } = [];

    public Dictionary<string, SymbolInformation> Symbols { get; } = [];
}

/// <summary>Everything one translation unit contributed.</summary>
public sealed class TranslationUnitResult
{
    /// <summary>Documents keyed by project-relative, '/'-separated path.</summary>
    public Dictionary<string, DocumentAccumulator> Documents { get; } = [];

    /// <summary>Symbols referenced but defined outside the project root.</summary>
    public Dictionary<string, SymbolInformation> ExternalSymbols { get; } = [];
}

/// <summary>Runs preprocessor and parser for one compile command and converts the findings to SCIP occurrences.</summary>
public sealed class TranslationUnitIndexer : IOccurrenceSink
{
    private readonly FileTable _files;
    private readonly TranslationUnitResult _result = new();

    private TranslationUnitIndexer(string root) => _files = new FileTable(root);

    public static string FileSymbol(string relativePath)
    {
        var s = new Symbol { Scheme = "cxx", Package = new Package { Manager = ".", Name = "", Version = "$" } };
        s.Descriptors.Add(new SymbolDescriptor { Name = "<file>/" + relativePath, Suffix = SymbolDescriptor.Types.Suffix.Namespace });
        return SymbolFormatter.Verbose.Format(s);
    }

    public static TranslationUnitResult Index(string root, CompileCommand cmd)
    {
        var indexer = new TranslationUnitIndexer(Path.GetFullPath(root));
        TranslationUnitResult? result = null;
        Exception? error = null;
        // The parser recurses over nested statements; give it room.
        var thread = new Thread(() =>
        {
            try
            {
                result = indexer.Run(cmd);
            }
            catch (Exception e)
            {
                error = e;
            }
        }, 256 * 1024 * 1024);
        thread.Start();
        thread.Join();
        return error is null ? result! : throw error;
    }

    private TranslationUnitResult Run(CompileCommand cmd)
    {
        var args = CommandLineCleaner.Clean(cmd.Arguments, cmd.File, cmd.Directory);
        var defines = new Dictionary<string, string?>
        {
            ["__STDC__"] = "1",
            ["__cplusplus"] = IsC(cmd.File) ? null : CppVersion(args),
            ["__GNUC__"] = "4",
        };
        if (defines["__cplusplus"] is null)
        {
            defines.Remove("__cplusplus");
        }

        var forced = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a.StartsWith("-D", StringComparison.Ordinal))
            {
                var d = a.Length > 2 ? a[2..] : i + 1 < args.Count ? args[++i] : "";
                var eq = d.IndexOf('=');
                defines[eq < 0 ? d : d[..eq]] = eq < 0 ? "1" : d[(eq + 1)..];
            }
            else if (a.StartsWith("-U", StringComparison.Ordinal))
            {
                defines.Remove(a.Length > 2 ? a[2..] : i + 1 < args.Count ? args[++i] : "");
            }
            else if (a == "-include" && i + 1 < args.Count)
            {
                forced.Add(Path.GetFullPath(args[++i], cmd.Directory));
            }
        }

        var includeDirs = CommandLineCleaner.IncludeDirs(args, cmd.Directory);
        var pp = new Preprocessor(_files, includeDirs, defines);
        pp.Run(cmd.FullPath, forced);

        var parser = new CppParser(pp.Output, _files, this);
        parser.Run();

        for (var f = 0; f < _files.Count; f++)
        {
            if (_files.Relative(f) is { } rel)
            {
                Doc(rel);
            }
        }

        foreach (var inc in pp.Includes)
        {
            if (_files.Relative(inc.File) is { } from && _files.Relative(inc.TargetFile) is { } to)
            {
                Doc(from).Occurrences.Add((inc.Line, inc.Col, inc.Col + inc.Len, FileSymbol(to), 0));
            }
        }

        foreach (var ev in pp.MacroEvents)
        {
            if (ev.Macro.File < 0 || _files.Relative(ev.At.File) is not { } rel)
            {
                continue;
            }

            var defRel = _files.Relative(ev.Macro.File) ?? _files.Path(ev.Macro.File);
            var s = new Symbol { Scheme = "cxx", Package = new Package { Manager = ".", Name = "", Version = "$" } };
            s.Descriptors.Add(new SymbolDescriptor { Name = $"{defRel}:{ev.Macro.Line + 1}:{ev.Macro.Col + 1}", Suffix = SymbolDescriptor.Types.Suffix.Macro });
            var sym = SymbolFormatter.Verbose.Format(s);
            var doc = Doc(rel);
            doc.Occurrences.Add((ev.At.Line, ev.At.Col, ev.At.Col + ev.At.Len, sym, ev.Def ? (int)SymbolRole.Definition : 0));
            if (ev.Def)
            {
                doc.Symbols.TryAdd(sym, new SymbolInformation { Symbol = sym, Kind = SymbolInformation.Types.Kind.Macro, DisplayName = ev.Macro.Name });
            }
        }

        return _result;
    }

    private static bool IsC(string file) => file.EndsWith(".c", StringComparison.OrdinalIgnoreCase);

    private static string CppVersion(List<string> args)
    {
        foreach (var a in args)
        {
            if (a.StartsWith("-std=", StringComparison.Ordinal) || a.StartsWith("--std=", StringComparison.Ordinal))
            {
                var v = a[(a.IndexOf('=') + 1)..];
                return v switch
                {
                    _ when v.EndsWith("98", StringComparison.Ordinal) || v.EndsWith("03", StringComparison.Ordinal) => "199711L",
                    _ when v.EndsWith("11", StringComparison.Ordinal) || v.EndsWith("0x", StringComparison.Ordinal) => "201103L",
                    _ when v.EndsWith("14", StringComparison.Ordinal) || v.EndsWith("1y", StringComparison.Ordinal) => "201402L",
                    _ when v.EndsWith("20", StringComparison.Ordinal) || v.EndsWith("2a", StringComparison.Ordinal) => "202002L",
                    _ when v.EndsWith("23", StringComparison.Ordinal) || v.EndsWith("2b", StringComparison.Ordinal) => "202302L",
                    _ => "201703L",
                };
            }
        }

        return "201703L";
    }

    private DocumentAccumulator Doc(string rel)
    {
        if (!_result.Documents.TryGetValue(rel, out var doc))
        {
            _result.Documents[rel] = doc = new DocumentAccumulator();
        }

        return doc;
    }

    public void Emit(Tok at, Entity entity, bool definition)
    {
        if (at.Macro || at.Len <= 0 || at.File < 0)
        {
            return;
        }

        var isLocal = entity.Symbol.StartsWith("local ", StringComparison.Ordinal);
        if (!definition && !entity.InProject && !isLocal)
        {
            _result.ExternalSymbols.TryAdd(entity.Symbol, new SymbolInformation { Symbol = entity.Symbol, Kind = entity.SymbolKind, DisplayName = entity.Name });
        }

        if (_files.Relative(at.File) is not { } rel)
        {
            return;
        }

        var doc = Doc(rel);
        doc.Occurrences.Add((at.Line, at.Col, at.Col + at.Len, entity.Symbol, definition ? (int)SymbolRole.Definition : 0));
        if (definition && !doc.Symbols.ContainsKey(entity.Symbol))
        {
            doc.Symbols[entity.Symbol] = new SymbolInformation { Symbol = entity.Symbol, Kind = entity.SymbolKind, DisplayName = entity.Name };
        }
    }
}
