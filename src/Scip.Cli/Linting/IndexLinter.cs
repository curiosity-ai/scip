#pragma warning disable CS0612 // deprecated range fields are intentionally supported

namespace Scip.Cli.Linting;

/// <summary>Checks a SCIP index for inconsistencies (port of the pure part of <c>lint.go</c>).</summary>
public static class IndexLinter
{
    /// <summary>Only set to true in tests to allow using more succinct symbols.</summary>
    public static bool SkipSymbolParseCheck { get; set; }

    /// <summary>Lints <paramref name="index"/> and returns every diagnostic in discovery order.</summary>
    public static List<LintDiagnostic> Lint(Index index)
    {
        var table = new SymbolTable();
        var errs = new List<LintDiagnostic>();

        foreach (var ext in index.ExternalSymbols)
        {
            if (ext.Symbol.Length == 0)
            {
                errs.Add(new EmptyStringError("symbol", "external symbols"));
            }
            else if (table.ExternalSymbols.ContainsKey(ext.Symbol))
            {
                errs.Add(new DuplicateSymbolInfoWarning(ext.Symbol));
            }
            else
            {
                table.ExternalSymbols[ext.Symbol] = [];
            }
        }

        var docs = new Dictionary<string, Document>(StringComparer.Ordinal);
        foreach (var doc in index.Documents)
        {
            if (doc.RelativePath.Length == 0)
            {
                errs.Add(new EmptyStringError("document", "index"));
                errs.Add(new Note("empty documents will be skipped for diagnostics"));
                continue;
            }

            if (!docs.TryAdd(doc.RelativePath, doc))
            {
                errs.Add(new DuplicateDocumentWarning(doc.RelativePath));
                errs.Add(new Note("out of documents with identical paths, all but one will be skipped for diagnostics"));
            }
        }

        // Deliberately order-independent (unlike the Go lint, which only accepts a relationship target from the same
        // or an earlier document): first register every symbol of every document, then check all relationships.
        foreach (var (path, doc) in docs)
        {
            foreach (var sym in doc.Symbols)
            {
                AddIfNotNull(errs, table.AddFileForSymbol(sym.Symbol, path));
            }
        }

        foreach (var (path, doc) in docs)
        {
            foreach (var sym in doc.Symbols)
            {
                foreach (var rel in sym.Relationships)
                {
                    AddIfNotNull(errs, table.AddRelationship(sym.Symbol, path, rel));
                }
            }
        }

        foreach (var ext in index.ExternalSymbols)
        {
            foreach (var rel in ext.Relationships)
            {
                AddIfNotNull(errs, table.AddRelationshipForExternalSymbol(ext.Symbol, rel));
            }
        }

        foreach (var (path, doc) in docs)
        {
            foreach (var occ in doc.Occurrences)
            {
                AddIfNotNull(errs, table.AddOccurrence(path, occ));
            }
        }

        return errs;
    }

    private static void AddIfNotNull(List<LintDiagnostic> list, LintDiagnostic? d)
    {
        if (d is not null)
        {
            list.Add(d);
        }
    }

    internal static LintDiagnostic? LintSymbolString(string symbol, string context)
    {
        if (symbol.Length == 0)
        {
            return new EmptyStringError("symbol", context);
        }

        if (!SymbolParser.TryParse(symbol, out var parsed, out var error))
        {
            return SkipSymbolParseCheck ? null : new InvalidSymbolError(symbol, error, context);
        }

        var formatted = SymbolFormatter.Verbose.Format(parsed);
        return symbol != formatted ? new NonCanonicalSymbolError(symbol, formatted, context) : null;
    }

    private readonly record struct OccurrenceKey(Range Range, int Roles);

    private sealed class SymbolTable
    {
        // symbol -> path -> occurrence key -> seen
        public Dictionary<string, Dictionary<string, HashSet<OccurrenceKey>>> ExternalSymbols { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, Dictionary<string, HashSet<OccurrenceKey>>> LocalSymbols { get; } = new(StringComparer.Ordinal);

        private readonly HashSet<(string, string)> _relationships = [];

        public LintDiagnostic? AddFileForSymbol(string sym, string path)
        {
            if (sym.Length == 0)
            {
                return new EmptyStringError("symbol", $"symbols for document {path}");
            }

            if (ExternalSymbols.ContainsKey(sym))
            {
                return new BothLocalAndExternalSymbolError(sym, path);
            }

            if (LocalSymbols.TryGetValue(sym, out var occsForSym))
            {
                if (occsForSym.ContainsKey(path))
                {
                    return new DuplicateSymbolInfoWarning(sym, path);
                }

                occsForSym[path] = [];
            }
            else
            {
                LocalSymbols[sym] = new Dictionary<string, HashSet<OccurrenceKey>>(StringComparer.Ordinal) { [path] = [] };
            }

            return null;
        }

        public LintDiagnostic? AddRelationshipForExternalSymbol(string sym, Relationship rel)
        {
            if (sym.Length == 0)
            {
                return null; // Errors for this are emitted earlier
            }

            if (rel.Symbol.Length == 0)
            {
                return new EmptyStringError("symbol", $"relationships for external symbol {sym}");
            }

            if (!HasAnyFlag(rel))
            {
                return new MissingRelationshipFlagError(sym, "external symbols");
            }

            if (!ExternalSymbols.ContainsKey(rel.Symbol))
            {
                return new MissingSymbolInRelationshipError(sym, "external symbols", rel.Symbol, "external symbols");
            }

            return _relationships.Add((sym, rel.Symbol)) ? null : new MultipleRelationshipWarning(sym, rel.Symbol);
        }

        public LintDiagnostic? AddRelationship(string sym, string path, Relationship rel)
        {
            if (sym.Length == 0)
            {
                return null; // Errors for this are emitted earlier
            }

            if (rel.Symbol.Length == 0)
            {
                return new EmptyStringError("symbol", $"relationships for {sym}");
            }

            if (!HasAnyFlag(rel))
            {
                return new MissingRelationshipFlagError(sym, $"symbols for file {path}");
            }

            if (!ExternalSymbols.ContainsKey(rel.Symbol) && !LocalSymbols.ContainsKey(rel.Symbol))
            {
                return new MissingSymbolInRelationshipError(
                    sym, $"symbols for file {path}", rel.Symbol, "external symbols or some other document");
            }

            // `local N` symbols are only unique within their document, so scope them by path.
            return _relationships.Add((Scoped(sym, path), Scoped(rel.Symbol, path)))
                ? null
                : new MultipleRelationshipWarning(sym, rel.Symbol);
        }

        private static string Scoped(string symbol, string path) =>
            SymbolParser.IsLocalSymbol(symbol) ? path + "\0" + symbol : symbol;

        public LintDiagnostic? AddOccurrence(string path, Occurrence occ)
        {
            if (!occ.TryGetSourceRange(out var range))
            {
                return new InvalidOccurrenceRangeError(occ.Symbol, path);
            }

            var symbolError = LintSymbolString(occ.Symbol, $"occurrence at {path} @ {RangeFormat.Describe(range)}");
            if (symbolError is not null)
            {
                return symbolError;
            }

            if (SymbolRole.Definition.Matches(occ) && SymbolRole.ForwardDefinition.Matches(occ))
            {
                return new ForwardDefIsDefinitionError(occ.Symbol, path, range);
            }

            var key = new OccurrenceKey(range, occ.SymbolRoles);
            LintDiagnostic? TryInsert(Dictionary<string, HashSet<OccurrenceKey>> perFile)
            {
                if (perFile.TryGetValue(path, out var fileOccs))
                {
                    return fileOccs.Add(key) ? null : new DuplicateOccurrenceWarning(occ.Symbol, path, range, occ.SymbolRoles);
                }

                // An occurrence for a symbol in a file other than the one it was defined in.
                perFile[path] = [key];
                return null;
            }

            if (ExternalSymbols.TryGetValue(occ.Symbol, out var ext))
            {
                return TryInsert(ext);
            }

            if (LocalSymbols.TryGetValue(occ.Symbol, out var local))
            {
                return TryInsert(local);
            }

            return new MissingSymbolForOccurrenceError(occ.Symbol, path, range);
        }

        private static bool HasAnyFlag(Relationship rel) =>
            rel.IsDefinition || rel.IsReference || rel.IsImplementation || rel.IsTypeDefinition;
    }
}
