using Index = Scip.Index;

namespace Scip.Indexer.DotNet.Tests;

/// <summary>A source file compiled by several projects/configurations is emitted as one merged document.</summary>
[NotInParallel]
public class MultiProjectMergeTests
{
    [Test]
    public async Task SharedSourceFileIsIndexedOnceWithUnionOfConditionalCode()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scip-dotnet-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(directory, "A"));
        Directory.CreateDirectory(Path.Combine(directory, "B"));
        Directory.CreateDirectory(Path.Combine(directory, "Shared"));
        try
        {
            foreach (var (name, constant) in new[] { ("A", "FLAVOR_A"), ("B", "FLAVOR_B") })
            {
                File.WriteAllText(Path.Combine(directory, name, $"{name}.csproj"), $"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>{FixtureIndexer.Framework}</TargetFramework>
                        <DefineConstants>$(DefineConstants);{constant}</DefineConstants>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include="../Shared/Shared.cs" />
                      </ItemGroup>
                    </Project>
                    """);
            }

            File.WriteAllText(Path.Combine(directory, "Shared", "Shared.cs"), """
                public interface IThing { void Run(); }

                public class Shared : IThing
                {
                #if FLAVOR_A
                    public int OnlyInA() => 1;
                #endif
                #if FLAVOR_B
                    public int OnlyInB() => 2;
                #endif
                    public void Run() { }
                }
                """);

            Index index = await FixtureIndexer.IndexAsync(
                directory, Path.Combine(directory, "A", "A.csproj"), Path.Combine(directory, "B", "B.csproj"));

            var documents = index.Documents.Where(d => d.RelativePath.EndsWith("Shared.cs", StringComparison.Ordinal)).ToList();
            await Assert.That(documents.Count).IsEqualTo(1);
            var document = documents[0];
            await Assert.That(index.Documents.Select(d => d.RelativePath).Distinct().Count()).IsEqualTo(index.Documents.Count);
            await Assert.That(document.Symbols.Any(s => s.Symbol.Contains("OnlyInA"))).IsTrue();
            await Assert.That(document.Symbols.Any(s => s.Symbol.Contains("OnlyInB"))).IsTrue();

            var occurrenceKeys = document.Occurrences
                .Select(o => $"{o.SingleLineRange}|{o.MultiLineRange}|{o.Symbol}|{o.SymbolRoles}").ToList();
            await Assert.That(occurrenceKeys.Distinct().Count()).IsEqualTo(occurrenceKeys.Count);
            await Assert.That(document.Symbols.Select(s => s.Symbol).Distinct().Count()).IsEqualTo(document.Symbols.Count);
            foreach (var info in document.Symbols)
            {
                await Assert.That(info.Relationships.Select(r => r.Symbol).Distinct().Count())
                    .IsEqualTo(info.Relationships.Count);
            }
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }
}
