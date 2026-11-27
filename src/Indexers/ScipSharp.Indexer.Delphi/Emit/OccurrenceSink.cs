using ScipSharp.Indexer.Delphi.Semantics;
using ScipSharp.Indexer.Delphi.Syntax;

namespace ScipSharp.Indexer.Delphi.Emit;

/// <summary>Occurrences and locals collected for one output document.</summary>
internal sealed class DocCollector(int fileId, string relativePath, string language)
{
    private readonly HashSet<(Range, string, int)> _seen = [];
    private int _nextLocal;

    public int FileId { get; } = fileId;

    public string RelativePath { get; } = relativePath;

    public string Language { get; } = language;

    public List<Occurrence> Occurrences { get; } = [];

    public Dictionary<Entity, string> Locals { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Global entities whose definition occurrence lives in this document (in order).</summary>
    public List<Entity> Definitions { get; } = [];

    public string LocalSymbol(Entity e)
    {
        if (!Locals.TryGetValue(e, out var s))
        {
            s = $"local {_nextLocal++}";
            Locals[e] = s;
        }

        return s;
    }

    public Occurrence? Add(Range range, string symbol, int roles, Range? enclosing)
    {
        if (!_seen.Add((range, symbol, roles)))
        {
            return null;
        }

        var occ = new Occurrence { Symbol = symbol, SymbolRoles = roles };
        occ.SetSourceRange(range);
        if (enclosing is { } enc && (enc.Start < range.Start || enc.Start == range.Start) && enc.End >= range.End)
        {
            occ.SetEnclosingSourceRange(enc);
        }

        Occurrences.Add(occ);
        return occ;
    }
}

/// <summary>Receives occurrences from the walkers, routes them to their document and tracks referenced symbols.</summary>
internal sealed class OccurrenceSink
{
    private readonly Dictionary<int, DocCollector> _docs = [];

    public IReadOnlyDictionary<int, DocCollector> Documents => _docs;

    /// <summary>Global entities referenced (or targeted by relationships) anywhere.</summary>
    public HashSet<Entity> Referenced { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Document in which each global entity's SymbolInformation is emitted.</summary>
    public Dictionary<Entity, int> DefinitionDoc { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Extra relationships recorded while walking (method resolution clauses).</summary>
    public Dictionary<Entity, List<(Entity Target, bool Implementation, bool Reference)>> ExtraRelationships { get; } = new(ReferenceEqualityComparer.Instance);

    public void AddDocument(DocCollector doc) => _docs[doc.FileId] = doc;

    public bool IsIndexed(int fileId) => _docs.ContainsKey(fileId);

    public Occurrence? Add(Token tok, Entity e, SymbolRole roles = 0, Range? enclosing = null) =>
        Add(tok.File, tok.Range, e, (int)roles, enclosing);

    public Occurrence? Add(Token first, Token last, Entity e, SymbolRole roles = 0, Range? enclosing = null) =>
        first.File == last.File
            ? Add(first.File, new Range(first.Line, first.Column, last.EndLine, last.EndColumn), e, (int)roles, enclosing)
            : Add(first, e, roles, enclosing);

    public Occurrence? Add(int fileId, Range range, Entity e, int roles, Range? enclosing)
    {
        if (!_docs.TryGetValue(fileId, out var doc))
        {
            return null;
        }

        string symbol;
        if (e.IsLocal)
        {
            symbol = doc.LocalSymbol(e);
        }
        else
        {
            symbol = SymbolNames.Global(e);
            if ((roles & (int)SymbolRole.Definition) != 0 && !e.IsExternal && DefinitionDoc.TryAdd(e, fileId))
            {
                doc.Definitions.Add(e);
            }
            else
            {
                Referenced.Add(e);
            }
        }

        return doc.Add(range, symbol, roles, enclosing);
    }

    public void AddRelationship(Entity from, Entity to, bool implementation, bool reference)
    {
        if (!ExtraRelationships.TryGetValue(from, out var list))
        {
            ExtraRelationships[from] = list = [];
        }

        list.Add((to, implementation, reference));
    }

    public static Range? Span(IReadOnlyList<Token> tokens, int first, int lastExclusive)
    {
        if (first < 0 || lastExclusive <= first || first >= tokens.Count)
        {
            return null;
        }

        var a = tokens[first];
        var last = Math.Min(lastExclusive - 1, tokens.Count - 1);
        while (last > first && (tokens[last].File != a.File || tokens[last].IsEof))
        {
            last--;
        }

        var b = tokens[last];
        return new Range(a.Line, a.Column, b.EndLine, b.EndColumn);
    }
}
