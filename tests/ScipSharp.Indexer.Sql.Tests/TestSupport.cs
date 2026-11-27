using ScipSharp.Indexer.Sql.Syntax;

namespace ScipSharp.Indexer.Sql.Tests;

/// <summary>Helpers to index SQL snippets in memory.</summary>
internal static class TestSupport
{
    public static string TestData => Path.Combine(AppContext.BaseDirectory, "testdata");

    /// <summary>Indexes the given files (relative path → text) in a temporary directory.</summary>
    public static Index IndexFiles(IReadOnlyDictionary<string, string> files, SqlDialect? dialect = null, string? defaultSchema = null)
    {
        var dir = Directory.CreateTempSubdirectory("scip-sql-test").FullName;
        try
        {
            foreach (var (path, text) in files)
            {
                var full = Path.Combine(dir, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
            }

            return new SqlProjectIndexer(dir, new SqlIndexerOptions { Dialect = dialect, DefaultSchema = defaultSchema, PackageName = "test" }).Build();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    public static Index IndexSql(string sql, SqlDialect? dialect = null, string? defaultSchema = null) =>
        IndexFiles(new Dictionary<string, string> { ["test.sql"] = sql }, dialect, defaultSchema);

    /// <summary>The symbols referenced (or defined) at each occurrence of <paramref name="text"/> in the single document, in order.</summary>
    public static List<string> SymbolsAt(Index index, string sql, string text, bool definitions = false)
    {
        var doc = index.Documents.Single();
        var source = new Lexing.SourceText(sql);
        var result = new List<string>();
        var from = 0;
        while ((from = sql.IndexOf(text, from, StringComparison.Ordinal)) >= 0)
        {
            var (line, col) = source.Position(from);
            foreach (var occ in doc.Occurrences)
            {
                var isDef = (occ.SymbolRoles & (int)SymbolRole.Definition) != 0;
                if (isDef != definitions)
                {
                    continue;
                }

                if (occ.SingleLineRange is { } r && r.Line == line && r.StartCharacter == col && r.EndCharacter == col + text.Length)
                {
                    result.Add(Short(occ.Symbol));
                }
            }

            from += text.Length;
        }

        return result;
    }

    /// <summary>Strips the scheme and package from a symbol (<c>public/t#c.</c>); locals stay <c>local N</c>.</summary>
    public static string Short(string symbol) =>
        symbol.StartsWith("local ", StringComparison.Ordinal) ? "local" : symbol.Split(' ', 5)[^1];

    /// <summary>A lint check equivalent to <c>scip lint</c> for the properties indexers must guarantee.</summary>
    public static List<string> Lint(Index index)
    {
        var problems = new List<string>();
        var external = index.ExternalSymbols.Select(s => s.Symbol).ToHashSet();
        problems.AddRange(index.ExternalSymbols.Where(s => !SymbolParser.TryParse(s.Symbol, out _, out _)).Select(s => "invalid external symbol: " + s.Symbol));
        problems.AddRange(index.Documents.SelectMany(d => d.Symbols).Where(s => !SymbolParser.TryParse(s.Symbol, out _, out _)).Select(s => "invalid symbol information: " + s.Symbol));
        var global = new HashSet<string>();
        foreach (var doc in index.Documents)
        {
            foreach (var s in doc.Symbols)
            {
                if (!s.Symbol.StartsWith("local ", StringComparison.Ordinal))
                {
                    global.Add(s.Symbol);
                }
            }
        }

        foreach (var s in global.Where(external.Contains))
        {
            problems.Add("both local and external: " + s);
        }

        var allRelationships = new HashSet<(string, string)>();
        foreach (var doc in index.Documents)
        {
            if (doc.PositionEncoding == PositionEncoding.UnspecifiedPositionEncoding)
            {
                problems.Add("no position encoding: " + doc.RelativePath);
            }

            var infos = new HashSet<string>();
            foreach (var s in doc.Symbols)
            {
                if (!infos.Add(s.Symbol))
                {
                    problems.Add($"duplicate symbol info {s.Symbol} in {doc.RelativePath}");
                }

                foreach (var r in s.Relationships)
                {
                    if (!allRelationships.Add((s.Symbol.StartsWith("local ", StringComparison.Ordinal) ? doc.RelativePath + s.Symbol : s.Symbol, r.Symbol)))
                    {
                        problems.Add($"duplicate relationship {s.Symbol} -> {r.Symbol}");
                    }

                    if (!global.Contains(r.Symbol) && !external.Contains(r.Symbol))
                    {
                        problems.Add($"relationship target without info: {r.Symbol}");
                    }
                }
            }

            var seen = new HashSet<string>();
            foreach (var occ in doc.Occurrences)
            {
                var key = $"{occ.SingleLineRange}{occ.MultiLineRange}{occ.Symbol}{occ.SymbolRoles}";
                if (!seen.Add(key))
                {
                    problems.Add($"duplicate occurrence {occ.Symbol} in {doc.RelativePath}");
                }

                if (!SymbolParser.TryParse(occ.Symbol, out var parsed, out var error))
                {
                    problems.Add($"invalid symbol {occ.Symbol}: {error}");
                    continue;
                }

                if (SymbolFormatter.Verbose.Format(parsed) != occ.Symbol)
                {
                    problems.Add($"non-canonical symbol {occ.Symbol}");
                }

                if (occ.Symbol.StartsWith("local ", StringComparison.Ordinal) ? !infos.Contains(occ.Symbol) : !global.Contains(occ.Symbol) && !external.Contains(occ.Symbol))
                {
                    problems.Add($"occurrence without symbol information: {occ.Symbol} in {doc.RelativePath}");
                }
            }
        }

        return problems;
    }

    public static ParsedScript Parse(string sql, SqlDialect dialect) => SqlParser.ParseScript(sql, dialect);
}
