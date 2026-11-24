namespace Scip;

/// <summary>Merges duplicate documents, symbols, occurrences and relationships (port of <c>flatten.go</c>). Results keep first-seen order.</summary>
public static class Flattener
{
    /// <summary>Merges documents with the same relative path. Merged documents are mutated; a new list is returned.</summary>
    public static List<Document> FlattenDocuments(IEnumerable<Document> documents)
    {
        var byPath = new Dictionary<string, Document>(StringComparer.Ordinal);
        var result = new List<Document>();
        foreach (var document in documents)
        {
            if (!byPath.TryGetValue(document.RelativePath, out var existing))
            {
                byPath[document.RelativePath] = document;
                result.Add(document);
                continue;
            }

            existing.Symbols.AddRange(document.Symbols);
            existing.Occurrences.AddRange(document.Occurrences);
        }

        return result;
    }

    /// <summary>Merges symbol information with the same symbol name; returns a new list.</summary>
    public static List<SymbolInformation> FlattenSymbols(IEnumerable<SymbolInformation> symbols)
    {
        var byName = new Dictionary<string, SymbolInformation>(StringComparer.Ordinal);
        var result = new List<SymbolInformation>();
        foreach (var symbol in symbols)
        {
            if (!byName.TryGetValue(symbol.Symbol, out var existing))
            {
                byName[symbol.Symbol] = symbol;
                result.Add(symbol);
                continue;
            }

            CombineDocumentation(existing.Documentation, symbol.Documentation);
            existing.Relationships.AddRange(symbol.Relationships);
        }

        return result;
    }

    /// <summary>
    /// Sorts (in place) and merges occurrences with equal ranges and symbols; returns a new list.
    /// </summary>
    public static List<Occurrence> FlattenOccurrences(IList<Occurrence> occurrences)
    {
        var flattened = new List<Occurrence>(occurrences.Count);
        if (occurrences.Count == 0)
        {
            return flattened;
        }

        Sorting.SortOccurrences(occurrences);
        flattened.Add(occurrences[0]);
        for (var i = 1; i < occurrences.Count; i++)
        {
            var occurrence = occurrences[i];
            var top = flattened[^1];
            if (top.CompareOccurrence(occurrence) != 0)
            {
                flattened.Add(occurrence);
                continue;
            }

            if (top.SyntaxKind == SyntaxKind.UnspecifiedSyntaxKind)
            {
                top.SyntaxKind = occurrence.SyntaxKind;
            }

            top.SymbolRoles |= occurrence.SymbolRoles;
            top.OverrideDocumentation.AddRange(occurrence.OverrideDocumentation);
            top.Diagnostics.AddRange(occurrence.Diagnostics);
        }

        return flattened;
    }

    /// <summary>Merges relationships with the same symbol name (OR-ing the flags); returns a new list.</summary>
    public static List<Relationship> FlattenRelationships(IEnumerable<Relationship> relationships)
    {
        var bySymbol = new Dictionary<string, Relationship>(StringComparer.Ordinal);
        var result = new List<Relationship>();
        foreach (var relationship in relationships)
        {
            if (!bySymbol.TryGetValue(relationship.Symbol, out var combined))
            {
                bySymbol[relationship.Symbol] = relationship;
                result.Add(relationship);
                continue;
            }

            combined.IsReference |= relationship.IsReference;
            combined.IsImplementation |= relationship.IsImplementation;
            combined.IsTypeDefinition |= relationship.IsTypeDefinition;
            combined.IsDefinition |= relationship.IsDefinition;
        }

        return result;
    }

    private static void CombineDocumentation(Google.Protobuf.Collections.RepeatedField<string> existing, IEnumerable<string> additional)
    {
        // Snapshot semantics: only strings absent from the original list are appended (duplicates within `additional` are kept).
        var original = existing.ToHashSet(StringComparer.Ordinal);
        foreach (var s in additional.ToList())
        {
            if (!original.Contains(s))
            {
                existing.Add(s);
            }
        }
    }
}
