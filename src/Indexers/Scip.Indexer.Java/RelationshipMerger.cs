namespace Scip.Indexer.Java;

/// <summary>
/// Makes every (symbol, related symbol) relationship pair occur exactly once across the whole index, OR-ing the flags
/// of duplicates into the first occurrence. Duplicates arise when the same symbol is defined in several documents
/// (multiple source sets, generic overrides collapsing to one symbol) or when forward and inverse relationships meet.
/// </summary>
public static class RelationshipMerger
{
    private readonly record struct Pair(string Symbol, string Target);

    /// <summary>Returns the number of relationships removed by merging.</summary>
    public static int Merge(IEnumerable<Document> documents)
    {
        var first = new Dictionary<Pair, Relationship>();
        var removed = 0;
        foreach (var doc in documents)
        {
            foreach (var info in doc.Symbols)
            {
                if (info.Relationships.Count == 0) continue;
                var kept = new List<Relationship>(info.Relationships.Count);
                foreach (var rel in info.Relationships)
                {
                    var key = new Pair(info.Symbol, rel.Symbol);
                    if (first.TryGetValue(key, out var existing))
                    {
                        existing.IsReference |= rel.IsReference;
                        existing.IsImplementation |= rel.IsImplementation;
                        existing.IsTypeDefinition |= rel.IsTypeDefinition;
                        existing.IsDefinition |= rel.IsDefinition;
                        removed++;
                    }
                    else
                    {
                        first[key] = rel;
                        kept.Add(rel);
                    }
                }
                if (kept.Count != info.Relationships.Count)
                {
                    info.Relationships.Clear();
                    info.Relationships.AddRange(kept);
                }
            }
        }
        return removed;
    }
}
