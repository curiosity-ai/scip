namespace Scip.Indexer.Clang;

/// <summary>
/// <c>scip lint</c> validates relationships document by document and only sees symbols defined in documents that come
/// earlier in the index (or external symbols). Emitting documents so that every relationship target's document precedes
/// the document holding the relationship keeps the index lint-clean (cycles fall back to the original order).
/// </summary>
internal static class DocumentOrdering
{
    public static List<Document> ByRelationshipDependencies(IEnumerable<Document> documents)
    {
        var docs = documents.ToList();
        Dictionary<string, int> owner = new(StringComparer.Ordinal);
        for (var i = 0; i < docs.Count; i++)
        {
            foreach (var s in docs[i].Symbols)
            {
                owner.TryAdd(s.Symbol, i);
            }
        }

        var state = new byte[docs.Count]; // 0 new, 1 visiting, 2 done
        List<Document> result = new(docs.Count);

        void Visit(int i)
        {
            if (state[i] != 0)
            {
                return;
            }

            state[i] = 1;
            foreach (var s in docs[i].Symbols)
            {
                foreach (var r in s.Relationships)
                {
                    if (owner.TryGetValue(r.Symbol, out var j) && j != i)
                    {
                        Visit(j);
                    }
                }
            }

            state[i] = 2;
            result.Add(docs[i]);
        }

        for (var i = 0; i < docs.Count; i++)
        {
            Visit(i);
        }

        return result;
    }
}
