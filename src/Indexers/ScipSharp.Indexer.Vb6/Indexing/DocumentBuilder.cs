using ScipSharp.Indexer.Vb6.Semantics;
using ScipSharp.Indexer.Vb6.Syntax;
using Kind = ScipSharp.SymbolInformation.Types.Kind;

namespace ScipSharp.Indexer.Vb6.Indexing;

/// <summary>Accumulates the occurrences and symbol information of one SCIP document.</summary>
internal sealed class DocumentBuilder(string relativePath, string language)
{
    private readonly List<Occurrence> _occurrences = [];
    private readonly HashSet<(string, int, int, int, int, int)> _seen = [];
    private readonly Dictionary<string, SymbolInformation> _infos = new(StringComparer.Ordinal);
    private int _nextLocal;

    public string RelativePath { get; } = relativePath;

    /// <summary>Global symbols referenced (or related to) from this document.</summary>
    public HashSet<Sym> Referenced { get; } = [];

    public string NewLocal() => $"local {_nextLocal++}";

    public void Reference(Sym sym, Span span, int roles = 0) => Add(sym, new Range(span.Line, span.Col, span.Line, span.EndCol), roles, null);

    public void Reference(Sym sym, Token token, int roles = 0) => Reference(sym, SpanOf(token), roles);

    public void Definition(Sym sym, Span span, Range? enclosing = null) =>
        Add(sym, new Range(span.Line, span.Col, span.Line, span.EndCol), (int)SymbolRole.Definition, enclosing);

    public void Definition(Sym sym, Token token, Range? enclosing = null) => Definition(sym, SpanOf(token), enclosing);

    public static Span SpanOf(Token t) => new(t.Line, t.Col, t.EndCol);

    private void Add(Sym sym, Range range, int roles, Range? enclosing)
    {
        if (range.End.Character < range.Start.Character || range.Start.Character < 0)
        {
            return;
        }

        if (!_seen.Add((sym.Scip, range.Start.Line, range.Start.Character, range.End.Line, range.End.Character, roles)))
        {
            return;
        }

        var occ = new Occurrence { Symbol = sym.Scip, SymbolRoles = roles };
        occ.SetSourceRange(range);
        if (enclosing is { } e)
        {
            occ.SetEnclosingSourceRange(e);
        }

        _occurrences.Add(occ);
        if (!sym.IsLocal)
        {
            Referenced.Add(sym);
        }
    }

    /// <summary>Records that a relationship of this document points at <paramref name="target"/>.</summary>
    public void Related(Sym target)
    {
        if (!target.IsLocal)
        {
            Referenced.Add(target);
        }
    }

    /// <summary>Adds the SymbolInformation of a symbol defined in this document (once).</summary>
    public void Define(Sym sym)
    {
        if (_infos.ContainsKey(sym.Scip))
        {
            return;
        }

        _infos[sym.Scip] = ToInfo(sym, includeRelationships: true);
    }

    public bool IsDefined(string scip) => _infos.ContainsKey(scip);

    public Document Build(string? text)
    {
        var doc = new Document
        {
            Language = language,
            RelativePath = RelativePath,
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
        };
        if (text is not null)
        {
            doc.Text = text;
        }

        doc.Occurrences.AddRange(_occurrences.OrderBy(o => o, Comparer<Occurrence>.Create(static (a, b) => a.CompareOccurrence(b))));
        doc.Symbols.AddRange(_infos.Values.OrderBy(s => s.Symbol, StringComparer.Ordinal));
        return doc;
    }

    public static SymbolInformation ToInfo(Sym sym, bool includeRelationships)
    {
        var info = new SymbolInformation
        {
            Symbol = sym.Scip,
            Kind = KindOf(sym),
            DisplayName = sym.Name,
        };
        if (sym.Signature.Length > 0)
        {
            info.SignatureDocumentation = new Signature { Language = "VisualBasic", Text = sym.Signature };
        }

        info.Documentation.AddRange(sym.Documentation);
        if (sym.IsLocal && sym.Parent is { IsLocal: false } parent)
        {
            info.EnclosingSymbol = parent.Scip;
        }

        if (includeRelationships)
        {
            info.Relationships.AddRange(sym.Relationships.OrderBy(r => r.Symbol, StringComparer.Ordinal));
        }

        return info;
    }

    private static Kind KindOf(Sym sym) => sym.Kind switch
    {
        SymKind.Project => Kind.Package,
        SymKind.StdModule => Kind.Module,
        SymKind.Class or SymKind.Form or SymKind.UserControl or SymKind.UserDocument or SymKind.PropertyPage or SymKind.Designer => Kind.Class,
        SymKind.Sub or SymKind.Function => sym.Parent?.Kind == SymKind.StdModule ? Kind.Function : Kind.Method,
        SymKind.Property => Kind.Property,
        SymKind.Declare => Kind.Function,
        SymKind.Variable => sym.Parent?.Kind == SymKind.StdModule ? Kind.Variable : Kind.Field,
        SymKind.Constant or SymKind.LocalConstant => Kind.Constant,
        SymKind.Event or SymKind.ExternalEvent => Kind.Event,
        SymKind.Udt => Kind.Struct,
        SymKind.UdtField or SymKind.Control => Kind.Field,
        SymKind.Enum => Kind.Enum,
        SymKind.EnumMember => Kind.EnumMember,
        SymKind.Parameter => Kind.Parameter,
        SymKind.LocalVariable => Kind.Variable,
        SymKind.ExternalClass => Kind.Class,
        _ => Kind.UnspecifiedKind,
    };
}
