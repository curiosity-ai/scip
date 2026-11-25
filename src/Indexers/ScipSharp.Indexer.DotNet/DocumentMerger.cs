namespace ScipSharp.Indexer.DotNet;

/// <summary>
/// A source file can be compiled by several projects or target frameworks. SCIP wants one document per path, so the
/// per-compilation documents are merged: occurrences are unioned (deduplicated by range, symbol and roles),
/// <see cref="SymbolInformation"/> is merged per symbol (first documentation/kind wins) and relationships are merged
/// per (symbol, target) pair with their flags OR-ed.
/// </summary>
internal static class DocumentMerger
{
    /// <summary>Merges documents that share a relative path, preserving the order of first appearance.</summary>
    public static List<Document> MergeByPath(IEnumerable<Document> documents)
    {
        var result = new List<Document>();
        var byPath = new Dictionary<string, Document>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            if (byPath.TryGetValue(document.RelativePath, out var existing))
            {
                existing.Occurrences.AddRange(document.Occurrences);
                existing.Symbols.AddRange(document.Symbols);
                if (existing.Language.Length == 0)
                {
                    existing.Language = document.Language;
                }
            }
            else
            {
                byPath.Add(document.RelativePath, document);
                result.Add(document);
            }
        }

        foreach (var document in result)
        {
            Normalize(document);
        }

        return result;
    }

    /// <summary>Deduplicates occurrences, symbol information and relationships inside one document.</summary>
    public static void Normalize(Document document)
    {
        var seen = new HashSet<(string, string, int)>();
        var occurrences = new List<Occurrence>(document.Occurrences.Count);
        foreach (var occurrence in document.Occurrences)
        {
            var range = occurrence.TryGetSourceRange(out var r) ? string.Join(',', r.ToScipRange()) : "";
            var key = (range, occurrence.Symbol, occurrence.SymbolRoles);
            if (seen.Add(key))
            {
                occurrences.Add(occurrence);
            }
        }

        if (occurrences.Count != document.Occurrences.Count)
        {
            document.Occurrences.Clear();
            document.Occurrences.AddRange(occurrences);
        }

        var infos = new Dictionary<string, SymbolInformation>(StringComparer.Ordinal);
        var merged = new List<SymbolInformation>(document.Symbols.Count);
        foreach (var info in document.Symbols)
        {
            if (!infos.TryGetValue(info.Symbol, out var first))
            {
                infos.Add(info.Symbol, info);
                merged.Add(info);
                continue;
            }

            if (first.Documentation.Count == 0)
            {
                first.Documentation.AddRange(info.Documentation);
            }

            if (first.Kind == SymbolInformation.Types.Kind.UnspecifiedKind)
            {
                first.Kind = info.Kind;
            }

            if (first.DisplayName.Length == 0)
            {
                first.DisplayName = info.DisplayName;
            }

            if (first.EnclosingSymbol.Length == 0)
            {
                first.EnclosingSymbol = info.EnclosingSymbol;
            }

            first.Relationships.AddRange(info.Relationships);
        }

        if (merged.Count != document.Symbols.Count)
        {
            document.Symbols.Clear();
            document.Symbols.AddRange(merged);
        }

        foreach (var info in document.Symbols)
        {
            MergeRelationships(info);
        }
    }

    private static void MergeRelationships(SymbolInformation info)
    {
        if (info.Relationships.Count < 2)
        {
            return;
        }

        var index = new Dictionary<string, Relationship>(StringComparer.Ordinal);
        var list = new List<Relationship>(info.Relationships.Count);
        foreach (var relationship in info.Relationships)
        {
            if (index.TryGetValue(relationship.Symbol, out var existing))
            {
                existing.IsReference |= relationship.IsReference;
                existing.IsImplementation |= relationship.IsImplementation;
                existing.IsTypeDefinition |= relationship.IsTypeDefinition;
                existing.IsDefinition |= relationship.IsDefinition;
            }
            else
            {
                index.Add(relationship.Symbol, relationship);
                list.Add(relationship);
            }
        }

        if (list.Count != info.Relationships.Count)
        {
            info.Relationships.Clear();
            info.Relationships.AddRange(list);
        }
    }
}
