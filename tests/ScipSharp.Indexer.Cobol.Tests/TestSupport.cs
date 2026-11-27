namespace ScipSharp.Indexer.Cobol.Tests;

internal static class TestSupport
{
    public static string TestData => Path.Combine(AppContext.BaseDirectory, "testdata");

    /// <summary>The source tree's testdata directory (snapshot updates are written there, not into bin/).</summary>
    public static string SourceTestData
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ScipSharp.slnx")))
            {
                dir = dir.Parent;
            }

            var candidate = dir is null ? null : Path.Combine(dir.FullName, "tests", "ScipSharp.Indexer.Cobol.Tests", "testdata");
            return candidate is not null && Directory.Exists(candidate) ? candidate : TestData;
        }
    }

    public static Index Index(string root, CobolIndexerOptions? options = null, List<string>? warnings = null)
    {
        var indexer = new CobolProjectIndexer(root, options ?? new CobolIndexerOptions { PackageName = "fixture" }, warnings is null ? null : warnings.Add);
        var index = indexer.Build();
        if (indexer.Errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", indexer.Errors));
        }

        return index;
    }

    /// <summary>The invariants checked by <c>scip lint</c>: canonical symbols, one SymbolInformation per symbol and document, information for every occurrence.</summary>
    public static List<string> Lint(Index index)
    {
        var errors = new List<string>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var info in index.ExternalSymbols)
        {
            if (!known.Add(info.Symbol))
            {
                errors.Add("duplicate external " + info.Symbol);
            }
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in index.Documents)
        {
            if (!paths.Add(doc.RelativePath))
            {
                errors.Add("duplicate document " + doc.RelativePath);
            }

            var local = new HashSet<string>(StringComparer.Ordinal);
            foreach (var info in doc.Symbols)
            {
                if (!local.Add(info.Symbol))
                {
                    errors.Add($"duplicate info {info.Symbol} in {doc.RelativePath}");
                }

                if (index.ExternalSymbols.Any(e => e.Symbol == info.Symbol))
                {
                    errors.Add("both local and external " + info.Symbol);
                }

                known.Add(info.Symbol);
            }
        }

        foreach (var doc in index.Documents)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var occ in doc.Occurrences)
            {
                if (!SymbolParser.TryParse(occ.Symbol, out var parsed, out var error))
                {
                    errors.Add($"invalid symbol {occ.Symbol}: {error}");
                    continue;
                }

                if (SymbolFormatter.Verbose.Format(parsed) != occ.Symbol)
                {
                    errors.Add("non-canonical " + occ.Symbol);
                }

                if (!known.Contains(occ.Symbol))
                {
                    errors.Add($"no info for {occ.Symbol} in {doc.RelativePath}");
                }

                if (!occ.TryGetSourceRange(out var range) || range.Validate() is not null)
                {
                    errors.Add($"bad range for {occ.Symbol} in {doc.RelativePath}");
                    continue;
                }

                if (!seen.Add($"{range}|{occ.SymbolRoles}|{occ.Symbol}"))
                {
                    errors.Add($"duplicate occurrence {occ.Symbol} at {range} in {doc.RelativePath}");
                }
            }
        }

        return errors;
    }
}
