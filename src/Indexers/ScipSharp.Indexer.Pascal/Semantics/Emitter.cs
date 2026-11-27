using System.Globalization;
using ScipSharp.Indexer.Pascal.Syntax;
using ScipRange = ScipSharp.Range;

namespace ScipSharp.Indexer.Pascal.Semantics;

/// <summary>Collects the occurrences and symbol information of one SCIP document.</summary>
internal sealed class DocumentBuilder
{
    private readonly HashSet<(ScipRange, string, int)> _seen = [];
    private readonly Dictionary<Sym, string> _locals = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _infos = new(StringComparer.Ordinal);
    private int _nextLocal;

    public DocumentBuilder(string relativePath, PositionEncoding encoding, string language)
    {
        Document = new Document { RelativePath = relativePath, Language = language, PositionEncoding = encoding };
    }

    public Document Document { get; }

    public Occurrence? Add(ScipRange range, string symbol, int roles, ScipRange? enclosing)
    {
        if (!_seen.Add((range, symbol, roles)))
        {
            return null;
        }

        var occ = new Occurrence { Symbol = symbol, SymbolRoles = roles };
        occ.SetSourceRange(range);
        if (enclosing is { } e)
        {
            occ.SetEnclosingSourceRange(e);
        }

        Document.Occurrences.Add(occ);
        return occ;
    }

    public void AddInfo(SymbolInformation info)
    {
        if (_infos.Add(info.Symbol))
        {
            Document.Symbols.Add(info);
        }
    }

    /// <summary>The document-local id of <paramref name="sym"/> (allocated, with its SymbolInformation, on first use).</summary>
    public string LocalId(Sym sym)
    {
        if (_locals.TryGetValue(sym, out var id))
        {
            return id;
        }

        id = "local " + (_nextLocal++).ToString(CultureInfo.InvariantCulture);
        _locals[sym] = id;
        AddInfo(Emitter.CreateInfo(sym, id));
        return id;
    }
}

/// <summary>Routes occurrences to the documents of their source files and tracks external symbols.</summary>
internal sealed class Emitter(Func<SourceText, string?> relativePathOf)
{
    private readonly Dictionary<string, DocumentBuilder> _docs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Sym> _referenced = new(StringComparer.Ordinal);
    private readonly List<Sym> _withInfo = [];

    public IEnumerable<DocumentBuilder> Documents => _docs.Values;

    public DocumentBuilder? DocFor(SourceText source, string language = "pascal")
    {
        var rel = relativePathOf(source);
        if (rel is null)
        {
            return null;
        }

        if (!_docs.TryGetValue(rel, out var doc))
        {
            _docs[rel] = doc = new DocumentBuilder(rel, source.PositionEncoding, language);
        }

        return doc;
    }

    public static ScipRange RangeOf(Token tok) => tok.Source.RangeOf(tok.Escaped ? tok.Start + 1 : tok.Start, tok.End);

    public Occurrence? Definition(Token name, Sym sym, Token? enclosingStart = null, Token? enclosingEnd = null, bool forward = false) =>
        Definition(name, name, sym, enclosingStart, enclosingEnd, forward);

    /// <summary>Definition spanning <paramref name="first"/>..<paramref name="last"/> (dotted unit names).</summary>
    public Occurrence? Definition(Token first, Token last, Sym sym, Token? enclosingStart = null, Token? enclosingEnd = null, bool forward = false)
    {
        var doc = DocFor(first.Source);
        if (doc is null)
        {
            if (!sym.IsLocal)
            {
                sym.External = true;
            }

            return null;
        }

        ScipRange? enclosing = null;
        if (enclosingStart is not null && enclosingEnd is not null && ReferenceEquals(enclosingStart.Source, first.Source) && ReferenceEquals(enclosingEnd.Source, first.Source))
        {
            enclosing = first.Source.RangeOf(enclosingStart.Start, enclosingEnd.End);
        }

        var symbol = SymbolIn(doc, sym, first.Source);
        if (!sym.IsLocal)
        {
            sym.External = false;
            sym.DefinedInDocument = true;
            if (sym.Info is null)
            {
                sym.Info = CreateInfo(sym, symbol);
                _withInfo.Add(sym);
                doc.AddInfo(sym.Info);
            }
        }

        var range = ReferenceEquals(first, last) ? RangeOf(first) : first.Source.RangeOf(first.Start, last.End);
        return doc.Add(range, symbol, forward ? (int)SymbolRole.ForwardDefinition : (int)SymbolRole.Definition, enclosing);
    }

    public Occurrence? Reference(Token tok, Sym sym, int roles = 0) => Reference(tok, tok, sym, roles);

    public Occurrence? Reference(Token first, Token last, Sym sym, int roles = 0)
    {
        if (sym.Kind == SymKind.Self)
        {
            return null;
        }

        var doc = DocFor(first.Source);
        if (doc is null)
        {
            return null;
        }

        var symbol = SymbolIn(doc, sym, first.Source);
        if (!sym.IsLocal)
        {
            _referenced.TryAdd(symbol, sym);
        }

        var range = ReferenceEquals(first, last) ? RangeOf(first) : first.Source.RangeOf(first.Start, last.End);
        return doc.Add(range, symbol, roles, null);
    }

    /// <summary>Marks a global symbol as needed (relationship targets).</summary>
    public void Require(Sym sym)
    {
        if (sym.Symbol is { } s)
        {
            _referenced.TryAdd(s, sym);
        }
    }

    private static string SymbolIn(DocumentBuilder doc, Sym sym, SourceText _) => sym.Symbol ?? doc.LocalId(sym);

    /// <summary>Updates documentation of an already-defined symbol (e.g. a body with a comment above it).</summary>
    public static void UpdateDocumentation(Sym sym, string? doc)
    {
        if (doc is null || sym.Documentation is not null)
        {
            return;
        }

        sym.Documentation = doc;
        sym.Info?.Documentation.Add(doc);
    }

    public static SymbolInformation CreateInfo(Sym sym, string symbol)
    {
        var info = new SymbolInformation
        {
            Symbol = symbol,
            Kind = sym.ScipKind,
            DisplayName = sym.Name,
        };
        if (sym.Signature is { Length: > 0 } sig)
        {
            info.SignatureDocumentation = new Signature { Language = "pascal", Text = sig };
        }

        if (sym.Documentation is { Length: > 0 } d)
        {
            info.Documentation.Add(d);
        }

        return info;
    }

    /// <summary>Copies relationships onto symbol information and returns the external symbols.</summary>
    public List<SymbolInformation> Finish()
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in _docs.Values)
        {
            foreach (var info in doc.Document.Symbols)
            {
                defined.Add(info.Symbol);
            }
        }

        foreach (var sym in _withInfo)
        {
            foreach (var rel in sym.Relationships)
            {
                if (sym.Info!.Relationships.All(r => r.Symbol != rel.Symbol))
                {
                    sym.Info.Relationships.Add(rel);
                }
            }
        }

        var external = new List<SymbolInformation>();
        foreach (var (symbol, sym) in _referenced.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            if (defined.Contains(symbol))
            {
                continue;
            }

            external.Add(CreateInfo(sym, symbol));
        }

        return external;
    }
}
