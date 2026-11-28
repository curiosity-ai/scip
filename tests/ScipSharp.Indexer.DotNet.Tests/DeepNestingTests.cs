using Index = ScipSharp.Index;

namespace ScipSharp.Indexer.DotNet.Tests;

/// <summary>Pathologically nested sources must not take the whole process down.</summary>
[NotInParallel]
public class DeepNestingTests
{
    [Test]
    public async Task DeeplyNestedFileIsSkippedAndTheRestIndexed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scip-dotnet-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Deep.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{FixtureIndexer.Framework}</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            // Roslyn's parser overflowed the stack (uncatchable) on 10000 nested parentheses.
            const int depth = 10000;
            File.WriteAllText(Path.Combine(directory, "Deep.cs"),
                $"class Deep {{ int M(int a) => {new string('(', depth)}a{new string(')', depth)}; }}");
            File.WriteAllText(Path.Combine(directory, "Ok.cs"), "class Ok { int M(int a) => (a + a); }");

            Index index = await FixtureIndexer.IndexAsync(directory);

            var paths = index.Documents.Select(d => d.RelativePath).ToList();
            await Assert.That(paths).Contains("Ok.cs");
            await Assert.That(paths).DoesNotContain("Deep.cs");
        }
        finally
        {
            FixtureIndexer.Cleanup(directory);
        }
    }
}
