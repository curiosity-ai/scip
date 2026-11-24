namespace Scip.Indexer.Ruby.Tests;

public sealed record SnapshotResult(int ExpectedDefinitions, int MissingDefinitions, int ExpectedReferences, int MissingReferences, List<string> Problems);

public static class SnapshotRunner
{
    public static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshots");

    public static IEnumerable<string> FixtureNames() =>
        Directory.EnumerateFiles(FixtureDirectory, "*.snapshot.rb")
            .Select(f => Path.GetFileName(f)[..^".snapshot.rb".Length])
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// Indexes the fixture and compares against its upstream snapshot. Only definitions, and references to symbols
    /// defined in the same fixture, are compared: everything else upstream emits relies on Sorbet's type inference
    /// or its built-in RBI payload (<c>Kernel#puts()</c>, <c>Class#new()</c>, <c>T#Boolean.</c>, ...).
    /// </summary>
    public static SnapshotResult Run(string name)
    {
        var snapshot = SnapshotFile.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, name + ".snapshot.rb")));
        var docs = RubyIndexer.IndexSources([(name + ".rb", snapshot.Source)], new GemMetadata("snapshot", "0.0.1"));
        var actual = new HashSet<(int Line, int Col, int Len, bool Def, string Sym)>();
        foreach (var o in docs[0].Occurrences)
        {
            if (o.Symbol.StartsWith("local ", StringComparison.Ordinal) || o.SingleLineRange is null)
            {
                continue;
            }

            var r = o.SingleLineRange;
            actual.Add((r.Line, r.StartCharacter, r.EndCharacter - r.StartCharacter, (o.SymbolRoles & 1) != 0, SymbolFormatter.DescriptorOnly.Format(o.Symbol)));
        }

        var definedSymbols = snapshot.Expected.Where(e => e.IsDefinition).Select(e => e.Symbol).ToHashSet();
        List<string> problems = [];
        int defs = 0, missDefs = 0, refs = 0, missRefs = 0;
        foreach (var e in snapshot.Expected)
        {
            if (e.IsDefinition)
            {
                defs++;
                if (!actual.Contains((e.Line, e.Col, e.Length, true, e.Symbol)))
                {
                    missDefs++;
                    problems.Add($"missing definition {e.Symbol} at {e.Line + 1}:{e.Col + 1}");
                }
            }
            else if (definedSymbols.Contains(e.Symbol))
            {
                refs++;
                if (!actual.Contains((e.Line, e.Col, e.Length, false, e.Symbol)))
                {
                    missRefs++;
                    problems.Add($"missing reference {e.Symbol} at {e.Line + 1}:{e.Col + 1}");
                }
            }
        }

        return new SnapshotResult(defs, missDefs, refs, missRefs, problems);
    }
}
