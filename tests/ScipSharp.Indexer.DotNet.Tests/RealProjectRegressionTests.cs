using ScipSharp.Indexing;
using Index = ScipSharp.Index;

namespace ScipSharp.Indexer.DotNet.Tests;

/// <summary>Regressions found by indexing real projects (Serilog, MediatR).</summary>
[NotInParallel]
public class RealProjectRegressionTests
{
    private const string Sources = """
        public static class Guard
        {
            public static void AgainstNull(object? value) { if (value is null) throw new System.ArgumentNullException(); }
        }

        public partial class Widget : System.IDisposable, System.IComparable
        {
            public void Dispose() => Guard.AgainstNull(this);
            public int CompareTo(object? other) => 0;
        }

        public partial class Widget
        {
            public dynamic Value { get; set; } = 1;
            public global::System.Text.StringBuilder Builder = new();
        }
        """;

    private static string CreateProject(string csproj)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scip-dotnet-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "App.csproj"), csproj);
        File.WriteAllText(Path.Combine(directory, "Widget.cs"), Sources);
        File.WriteAllText(Path.Combine(directory, "Widget2.cs"), "public partial class Widget : System.IDisposable { }");
        return directory;
    }

    // Mirrors Serilog: `$(TargetFrameworks);...` appended to a property that is empty on this OS yields ";net10.0;...",
    // whose leading empty entry used to make every project fail with "Project does not contain 'Compile' target".
    private static string MultiTargetProject() => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks Condition=" '$(OS)' == 'Windows_NT'">net462</TargetFrameworks>
            <TargetFrameworks>$(TargetFrameworks);{FixtureIndexer.Framework};net8.0</TargetFrameworks>
            <Nullable>enable</Nullable>
            <OutputType>Library</OutputType>
          </PropertyGroup>
        </Project>
        """;

    [Test]
    public async Task EmptyLeadingTargetFrameworkEntryStillLoads()
    {
        var directory = CreateProject(MultiTargetProject());
        try
        {
            var index = await FixtureIndexer.IndexAsync(directory);
            var widget = index.Documents.Single(document => document.RelativePath == "Widget.cs");
            await Assert.That(widget.Occurrences.Count).IsGreaterThan(10);
            await Assert.That(widget.Symbols.Any(info => info.Symbol.EndsWith("Widget#", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }

    [Test]
    public async Task IndexSatisfiesLintInvariants()
    {
        var directory = CreateProject(MultiTargetProject());
        try
        {
            var index = await FixtureIndexer.IndexAsync(directory);
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var external in index.ExternalSymbols)
            {
                known.Add(external.Symbol);
            }

            foreach (var document in index.Documents)
            {
                foreach (var info in document.Symbols.Select(info => info.Symbol).Distinct())
                {
                    // A symbol may be defined in several documents (partial types) but never also be external.
                    await Assert.That(index.ExternalSymbols.Any(external => external.Symbol == info)).IsFalse();
                    known.Add(info);
                }
            }

            var related = index.Documents.SelectMany(d => d.Symbols).SelectMany(i => i.Relationships).ToList();
            await Assert.That(related.Count).IsGreaterThan(0);
            foreach (var relationship in related)
            {
                await Assert.That(known.Contains(relationship.Symbol)).IsTrue();
            }

            // A partial type repeats its base types in each part but relates to each of them from one document only.
            var owners = index.Documents
                .SelectMany(d => d.Symbols.SelectMany(i => i.Relationships.Select(r => (i.Symbol, r.Symbol, d.RelativePath))))
                .Distinct().GroupBy(t => (t.Item1, t.Item2));
            await Assert.That(owners.Any(group => group.Count() > 1)).IsFalse();

            foreach (var document in index.Documents)
            {
                var locals = document.Symbols.Where(i => i.Symbol.StartsWith("local ", StringComparison.Ordinal))
                    .Select(i => i.Symbol).ToHashSet();
                foreach (var occurrence in document.Occurrences)
                {
                    var symbol = occurrence.Symbol;
                    // No bare packages ("... . . ") or descriptor-less symbols such as `dynamic#`.
                    await Assert.That(symbol.Length > 0 && !symbol.EndsWith(' ')).IsTrue();
                    await Assert.That(symbol.StartsWith("scip-dotnet ", StringComparison.Ordinal)
                                      || symbol.StartsWith("local ", StringComparison.Ordinal)).IsTrue();
                    await Assert.That(symbol.StartsWith("local ", StringComparison.Ordinal)
                        ? locals.Contains(symbol)
                        : known.Contains(symbol)).IsTrue();
                }
            }
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }

    [Test]
    public async Task UnloadableProjectFailsTheRun()
    {
        var directory = CreateProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework>
            </Project>
            """);
        try
        {
            var exit = await new DotNetIndexer().IndexAsync(
                new IndexerOptions(directory, "index.scip", ["--skip-dotnet-restore"]), CancellationToken.None);
            await Assert.That(exit).IsNotEqualTo(0);
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }

    [Test]
    public async Task NothingToIndexFailsTheRun()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scip-dotnet-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            var exit = await new DotNetIndexer().IndexAsync(
                new IndexerOptions(directory, "index.scip", []), CancellationToken.None);
            await Assert.That(exit).IsNotEqualTo(0);
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }
}
