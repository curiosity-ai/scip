namespace ScipSharp.Indexer.Cobol.Analysis;

/// <summary>Collects occurrences and symbol information of one document, de-duplicating repeated copybook expansions.</summary>
internal sealed class DocumentAccumulator(string relativePath)
{
    private readonly Dictionary<(int, int, int, int, string, int), Occurrence> _occurrences = [];
    private readonly Dictionary<string, SymbolInformation> _symbols = new(StringComparer.Ordinal);

    public string RelativePath { get; } = relativePath;

    public bool IsEmpty => _occurrences.Count == 0 && _symbols.Count == 0;

    public void AddOccurrence(Range range, string symbol, SymbolRole roles, Range? enclosing)
    {
        var key = (range.Start.Line, range.Start.Character, range.End.Line, range.End.Character, symbol, (int)roles);
        if (_occurrences.ContainsKey(key))
        {
            return;
        }

        var occ = new Occurrence { Symbol = symbol, SymbolRoles = (int)roles };
        occ.SetSourceRange(range);
        if (enclosing is { } e)
        {
            occ.SetEnclosingSourceRange(e);
        }

        _occurrences[key] = occ;
    }

    public void AddSymbol(SymbolInformation info) => _symbols.TryAdd(info.Symbol, info);

    public Document Build()
    {
        var doc = new Document
        {
            RelativePath = RelativePath,
            Language = "COBOL",
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
        };
        doc.Occurrences.AddRange(_occurrences.Values.OrderBy(o => o, Comparer<Occurrence>.Create(static (a, b) => a.CompareOccurrence(b))));
        doc.Symbols.AddRange(_symbols.Values.OrderBy(s => s.Symbol, StringComparer.Ordinal));
        return doc;
    }
}

/// <summary>Programs, entry points, methods and functions of the whole project, for CALL / INVOKE / FUNCTION resolution.</summary>
internal sealed class ProjectCatalog
{
    public Dictionary<string, (string Symbol, string Display)> Programs { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Entries { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<string>> Methods { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Functions { get; } = new(StringComparer.Ordinal);

    /// <summary>Files reached by COPY from some program (they are emitted after every program).</summary>
    public HashSet<int> Copybooks { get; } = [];

    public void Add(ProgramUnit unit, SymbolNamer namer)
    {
        if (unit.IsPseudo)
        {
            return;
        }

        var symbol = namer.ForUnit(unit);
        switch (unit.UnitKind)
        {
            case UnitKind.Program when unit.Parent is null || unit.Parent.IsPseudo:
                Programs.TryAdd(unit.Name, (symbol, unit.DisplayName));
                break;
            case UnitKind.Function:
                Functions.TryAdd(unit.Name, symbol);
                break;
            case UnitKind.Method:
                if (!Methods.TryGetValue(unit.Name, out var list))
                {
                    Methods[unit.Name] = list = [];
                }

                list.Add(symbol);
                break;
            case UnitKind.Class when unit.Parent is null:
                Programs.TryAdd(unit.Name, (symbol, unit.DisplayName));
                break;
        }

        foreach (var entry in unit.Entries)
        {
            Entries.TryAdd(entry.Name, namer.ForSimple(unit, entry));
        }
    }
}
