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

    /// <summary>
    /// <c>scip lint</c> (like its Go original) resolves a relationship target only if the document defining it was
    /// already visited, so documents are emitted dependencies-first (best effort; cycles keep their input order).
    /// </summary>
    public static IEnumerable<Document> OrderForLint(IReadOnlyList<Document> documents)
    {
        var definedIn = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < documents.Count; i++)
        {
            foreach (var info in documents[i].Symbols)
            {
                definedIn.TryAdd(info.Symbol, i);
            }
        }

        var state = new byte[documents.Count]; // 0 = new, 1 = visiting, 2 = emitted
        var result = new List<Document>(documents.Count);
        var stack = new Stack<(int Index, IEnumerator<int> Dependencies)>();
        for (var root = 0; root < documents.Count; root++)
        {
            if (state[root] != 0)
            {
                continue;
            }

            state[root] = 1;
            stack.Push((root, Dependencies(documents[root], root, definedIn).GetEnumerator()));
            while (stack.Count > 0)
            {
                var (index, dependencies) = stack.Peek();
                if (dependencies.MoveNext())
                {
                    var next = dependencies.Current;
                    if (state[next] == 0)
                    {
                        state[next] = 1;
                        stack.Push((next, Dependencies(documents[next], next, definedIn).GetEnumerator()));
                    }
                }
                else
                {
                    stack.Pop();
                    state[index] = 2;
                    result.Add(documents[index]);
                }
            }
        }

        return result;
    }

    private static IEnumerable<int> Dependencies(Document document, int self, Dictionary<string, int> definedIn)
    {
        foreach (var info in document.Symbols)
        {
            foreach (var relationship in info.Relationships)
            {
                if (definedIn.TryGetValue(relationship.Symbol, out var other) && other != self)
                {
                    yield return other;
                }
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
