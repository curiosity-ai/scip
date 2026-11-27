using ScipSharp.Indexer.Sql.Lexing;

namespace ScipSharp.Indexer.Sql.Semantics;

/// <summary>Accumulates the occurrences and symbol information of one SCIP document.</summary>
internal sealed class DocumentBuilder(string relativePath, SourceText source)
{
    private readonly Dictionary<string, SymbolInformation> _infos = new(StringComparer.Ordinal);
    private readonly HashSet<(int Start, int End, string Symbol, int Roles)> _seen = [];
    private readonly List<Occurrence> _occurrences = [];
    private int _nextLocal;

    public SourceText Source { get; } = source;

    public string NewLocal() => $"local {_nextLocal++}";

    public bool HasInfo(string symbol) => _infos.ContainsKey(symbol);

    public SymbolInformation? Info(string symbol) => _infos.GetValueOrDefault(symbol);

    /// <summary>Adds symbol information unless the document already has some for <paramref name="symbol"/>.</summary>
    public SymbolInformation AddInfo(string symbol, SymbolInformation.Types.Kind kind, string display, IEnumerable<string> docs, IEnumerable<Relationship>? relationships = null)
    {
        if (_infos.TryGetValue(symbol, out var existing))
        {
            return existing;
        }

        var info = new SymbolInformation { Symbol = symbol, Kind = kind, DisplayName = display };
        info.Documentation.AddRange(docs.Where(d => d.Length > 0));
        if (relationships is not null)
        {
            info.Relationships.AddRange(relationships.Select(r => r.Clone()));
        }

        _infos[symbol] = info;
        return info;
    }

    public void Add(int start, int end, string symbol, SymbolRole role, (int Start, int End)? enclosing = null)
    {
        if (end < start || !_seen.Add((start, end, symbol, (int)role)))
        {
            return;
        }

        var occ = new Occurrence { Symbol = symbol, SymbolRoles = (int)role };
        var (sl, sc) = Source.Position(start);
        var (el, ec) = Source.Position(end);
        if (sl == el)
        {
            occ.SingleLineRange = new SingleLineRange { Line = sl, StartCharacter = sc, EndCharacter = ec };
        }
        else
        {
            occ.MultiLineRange = new MultiLineRange { StartLine = sl, StartCharacter = sc, EndLine = el, EndCharacter = ec };
        }

        if (enclosing is { } enc && enc.End >= enc.Start)
        {
            var (esl, esc) = Source.Position(enc.Start);
            var (eel, eec) = Source.Position(enc.End);
            if (esl == eel)
            {
                occ.SingleLineEnclosingRange = new SingleLineRange { Line = esl, StartCharacter = esc, EndCharacter = eec };
            }
            else
            {
                occ.MultiLineEnclosingRange = new MultiLineRange { StartLine = esl, StartCharacter = esc, EndLine = eel, EndCharacter = eec };
            }
        }

        _occurrences.Add(occ);
    }

    public Document Build()
    {
        var doc = new Document
        {
            RelativePath = relativePath,
            Language = "SQL",
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
        };
        doc.Occurrences.AddRange(_occurrences);
        doc.Symbols.AddRange(_infos.Values);
        return doc;
    }
}
