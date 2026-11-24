namespace Scip.Indexer.DotNet;

/// <summary>
/// Keeps an index self-consistent in the way <c>scip lint</c> demands: every symbol used by an occurrence or a
/// relationship has a <see cref="SymbolInformation"/> in some document or in the external symbols.
/// </summary>
internal static class SymbolCompleteness
{
    /// <summary>
    /// Records the document's defined and referenced symbols. Local symbols are only valid inside one document, so
    /// missing local information is added to the document itself; global ones are collected into
    /// <paramref name="referenced"/> for the external-symbols pass.
    /// </summary>
    public static void CompleteDocument(Document document, HashSet<string> defined, HashSet<string> referenced)
    {
        var own = new HashSet<string>(document.Symbols.Count, StringComparer.Ordinal);
        foreach (var info in document.Symbols)
        {
            own.Add(info.Symbol);
            defined.Add(info.Symbol);
            foreach (var relationship in info.Relationships)
            {
                referenced.Add(relationship.Symbol);
            }
        }

        foreach (var occurrence in document.Occurrences)
        {
            var symbol = occurrence.Symbol;
            if (symbol.Length == 0)
            {
                continue;
            }

            if (IsLocal(symbol))
            {
                if (own.Add(symbol))
                {
                    document.Symbols.Add(new SymbolInformation { Symbol = symbol });
                }
            }
            else
            {
                referenced.Add(symbol);
            }
        }
    }

    public static SymbolInformation StubFor(string symbol)
    {
        var info = new SymbolInformation { Symbol = symbol };
        if (symbol.EndsWith('/'))
        {
            info.Kind = SymbolInformation.Types.Kind.Namespace;
        }

        return info;
    }

    private static bool IsLocal(string symbol) => symbol.StartsWith("local ", StringComparison.Ordinal);
}
