using Index = Scip.Index;

namespace Scip.Indexer.DotNet.Tests;

/// <summary>Checks the dependency hover metadata emitted in <c>Index.external_symbols</c>.</summary>
[NotInParallel]
public class ExternalSymbolsTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GenericHoverDescribesDeclarationRegardlessOfReferenceOrder(bool stringsFirst)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scip-dotnet-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Example.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{FixtureIndexer.Framework}</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            const string intCalls = """
                ints.Add(1);
                _ = ints.First();
                _ = Enumerable.First(ints);
                _ = ints.First(x => x > 0);
                _ = Enumerable.First(ints, x => x > 0);
                """;
            const string stringCalls = """
                strings.Add("x");
                _ = strings.First(x => x.Length > 0);
                _ = Enumerable.First(strings, x => x.Length > 0);
                _ = strings.First();
                _ = Enumerable.First(strings);
                """;
            File.WriteAllText(Path.Combine(directory, "Example.cs"), $$"""
                using System.Collections.Generic;
                using System.Linq;

                public class Example
                {
                    public void Run(List<int> ints, List<string> strings)
                    {
                        {{(stringsFirst ? stringCalls : intCalls)}}
                        {{(stringsFirst ? intCalls : stringCalls)}}
                        Helper.Later();
                    }
                }
                {{(stringsFirst ? "public record Text(string Value);" : "public record Number(int Value);")}}
                {{(stringsFirst ? "public record Number(int Value);" : "public record Text(string Value);")}}
                public static class Helper
                {
                    public static void Later() { }
                }
                """);

            Index index = await FixtureIndexer.IndexAsync(directory, "--allow-global-symbol-definitions");
            var document = index.Documents.Single(doc => doc.RelativePath == "Example.cs");
            var addReferences = document.Occurrences.Where(o => o.Symbol.Contains("/List#Add(")).ToList();
            await Assert.That(addReferences.Count).IsEqualTo(2);
            await Assert.That(addReferences.Select(o => o.Symbol).Distinct().Count()).IsEqualTo(1);
            var add = index.ExternalSymbols.Single(info => info.Symbol == addReferences[0].Symbol);
            await Assert.That(add.Documentation[0]).Contains("List<T>.Add(T item)");

            var sourceLines = File.ReadAllLines(Path.Combine(directory, "Example.cs"));
            var firstReferences = document.Occurrences.Where(o => o.Symbol.Contains("/Enumerable#First(")).ToList();
            await Assert.That(firstReferences.Count).IsEqualTo(8);
            await Assert.That(firstReferences.Select(o => o.Symbol).Distinct().Count()).IsEqualTo(2);
            foreach (var hasPredicate in new[] { false, true })
            {
                var references = firstReferences
                    .Where(o => sourceLines[Range.FromOccurrence(o).Start.Line].Contains("=>") == hasPredicate)
                    .ToList();
                await Assert.That(references.Count).IsEqualTo(4);
                await Assert.That(references.Select(o => o.Symbol).Distinct().Count()).IsEqualTo(1);
                var external = index.ExternalSymbols.Single(info => info.Symbol == references[0].Symbol);
                await Assert.That(external.Documentation[0]).Contains("TSource");
                await Assert.That(external.Documentation[0]).DoesNotContain("int");
                await Assert.That(external.Documentation[0]).DoesNotContain("string");
                await Assert.That(external.Documentation[0].Contains("predicate")).IsEqualTo(hasPredicate);
            }

            var equatableTargets = document.Symbols.SelectMany(info => info.Relationships)
                .Where(r => r.Symbol.EndsWith("/IEquatable#", StringComparison.Ordinal)).ToList();
            await Assert.That(equatableTargets.Count).IsEqualTo(2);
            await Assert.That(equatableTargets.Select(r => r.Symbol).Distinct().Count()).IsEqualTo(1);
            var equatable = index.ExternalSymbols.Single(info => info.Symbol == equatableTargets[0].Symbol);
            await Assert.That(equatable.Documentation[0]).Contains("IEquatable<T>");

            var later = document.Occurrences
                .Where(o => o.Symbol.EndsWith("/Helper#Later().", StringComparison.Ordinal)).ToList();
            await Assert.That(later.Count).IsEqualTo(2);
            await Assert.That(index.ExternalSymbols.Select(info => info.Symbol)).DoesNotContain(later[0].Symbol);
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }

    [Test]
    public async Task EveryGenuineExternalOccurrenceAndRelationshipTargetHasSymbolInformation()
    {
        var directory = FixtureIndexer.CopyFixture("syntax");
        try
        {
            var index = await FixtureIndexer.IndexAsync(directory);
            var declared = new HashSet<string>(index.Documents.SelectMany(d => d.Symbols).Select(i => i.Symbol));
            declared.UnionWith(index.ExternalSymbols.Select(i => i.Symbol));

            var referenced = index.Documents.SelectMany(d => d.Occurrences).Select(o => o.Symbol)
                .Concat(index.Documents.SelectMany(d => d.Symbols).SelectMany(i => i.Relationships).Select(r => r.Symbol));
            var dangling = new SortedSet<string>(
                referenced.Where(s => IsGenuineExternal(s) && !declared.Contains(s)), StringComparer.Ordinal);

            await Assert.That(dangling).IsEmpty();
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }

    // A genuine external symbol resolves to a real NuGet/BCL package, not the index-local placeholder.
    private static bool IsGenuineExternal(string symbol) =>
        symbol.StartsWith("scip-dotnet nuget ", StringComparison.Ordinal)
        && !symbol.StartsWith("scip-dotnet nuget . . ", StringComparison.Ordinal);
}
