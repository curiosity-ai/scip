using System.Globalization;
using ScipSharp.Indexer.Pli.Semantics;

namespace ScipSharp.Indexer.Pli;

/// <summary>Collects the occurrences and symbol information of one SCIP document (a source or include file).</summary>
internal sealed class DocumentBuilder(string relativePath)
{
    public const string Language = "PLI";

    private readonly Dictionary<Decl, string> _locals = [];
    private readonly HashSet<(int, int, int, int, string, int)> _seen = [];
    private readonly List<Occurrence> _occurrences = [];
    private readonly Dictionary<string, SymbolInformation> _symbols = new(StringComparer.Ordinal);

    public string RelativePath { get; } = relativePath;

    /// <summary>Adds an occurrence; <paramref name="defined"/> holds every global symbol with a definition somewhere.</summary>
    public void Add(RawOccurrence o, HashSet<string> defined)
    {
        var decl = o.Decl;
        var symbol = decl.Symbol ?? Local(decl);
        var t = o.Token;
        if (!_seen.Add((t.Line, t.Column, t.EndLine, t.EndColumn, symbol, o.Roles)))
        {
            return;
        }

        var occ = new Occurrence { Symbol = symbol, SymbolRoles = o.Roles };
        occ.SetSourceRange(new Range(t.Line, t.Column, t.EndLine, t.EndColumn));
        var isDefinition = (o.Roles & (int)SymbolRole.Definition) != 0;
        if (isDefinition && decl.Extent is var (start, end) && start.File == t.File && end.File == t.File)
        {
            occ.SetEnclosingSourceRange(new Range(start.Line, start.Column, end.EndLine, end.EndColumn));
        }

        _occurrences.Add(occ);
        if (decl.Kind == DeclKind.Builtin)
        {
            return;
        }

        if (isDefinition || decl.Symbol is null || !defined.Contains(symbol))
        {
            _symbols.TryAdd(symbol, Info(symbol, decl));
        }
    }

    private string Local(Decl decl)
    {
        if (!_locals.TryGetValue(decl, out var s))
        {
            s = "local " + _locals.Count.ToString(CultureInfo.InvariantCulture);
            _locals[decl] = s;
        }

        return s;
    }

    public static SymbolInformation Info(string symbol, Decl decl)
    {
        var info = new SymbolInformation { Symbol = symbol, DisplayName = decl.DisplayName, Kind = KindOf(decl) };
        if (decl.Signature is { Length: > 0 } sig)
        {
            info.Documentation.Add("```pli\n" + sig + "\n```");
        }

        return info;
    }

    private static SymbolInformation.Types.Kind KindOf(Decl d) => d.Kind switch
    {
        DeclKind.Variable => SymbolInformation.Types.Kind.Variable,
        DeclKind.Member => SymbolInformation.Types.Kind.Field,
        DeclKind.Parameter => SymbolInformation.Types.Kind.Parameter,
        DeclKind.Procedure or DeclKind.Entry or DeclKind.EntryDeclaration or DeclKind.Builtin or DeclKind.Generic
            => SymbolInformation.Types.Kind.Function,
        DeclKind.Label => SymbolInformation.Types.Kind.Constant,
        DeclKind.BeginBlock => SymbolInformation.Types.Kind.Namespace,
        DeclKind.Package => SymbolInformation.Types.Kind.Package,
        DeclKind.Condition => SymbolInformation.Types.Kind.Event,
        DeclKind.File => SymbolInformation.Types.Kind.Variable,
        DeclKind.Type => d.Children.Count > 0 ? SymbolInformation.Types.Kind.Struct : SymbolInformation.Types.Kind.Type,
        DeclKind.OrdinalValue => SymbolInformation.Types.Kind.EnumMember,
        DeclKind.PreprocessorVariable or DeclKind.PreprocessorProcedure => SymbolInformation.Types.Kind.Macro,
        _ => SymbolInformation.Types.Kind.Variable,
    };

    public Document Build()
    {
        var doc = new Document
        {
            RelativePath = RelativePath,
            Language = Language,
            PositionEncoding = PositionEncoding.Utf16CodeUnitOffsetFromLineStart,
        };
        _occurrences.Sort(static (a, b) => a.CompareOccurrence(b));
        doc.Occurrences.AddRange(_occurrences);
        doc.Symbols.AddRange(_symbols.Values.OrderBy(static s => s.Symbol, StringComparer.Ordinal));
        return doc;
    }
}
