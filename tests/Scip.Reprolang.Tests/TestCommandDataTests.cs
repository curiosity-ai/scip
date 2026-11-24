using Scip.Reprolang;

namespace Scip.Reprolang.Tests;

/// <summary>
/// The <c>testdata/test_cmd</c> fixtures are consumed by the CLI <c>test</c> command (assertion runner, not yet ported);
/// here we check that each directory indexes and that fixtures follow the naming rule the Go test relies on.
/// </summary>
public class TestCommandDataTests
{
    private static readonly string TestData = Path.Combine(AppContext.BaseDirectory, "testdata", "test_cmd");

    public static IEnumerable<string> Cases() =>
        Directory.EnumerateDirectories(TestData).Select(d => Path.GetFileName(d)!).Order(StringComparer.Ordinal);

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task FixturesIndex(string name)
    {
        var directory = Path.Combine(TestData, name);
        var index = ReproIndexer.Index(directory);
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("reprolang");
        await Assert.That(index.Documents.Count).IsEqualTo(Directory.GetFiles(directory).Length);
        foreach (var file in Directory.GetFiles(directory))
        {
            var fileName = Path.GetFileName(file);
            await Assert.That(fileName.StartsWith("passes", StringComparison.Ordinal) || fileName.StartsWith("fails", StringComparison.Ordinal)).IsTrue();
        }
    }

    [Test]
    public async Task RolesFixtureSymbols()
    {
        var index = ReproIndexer.Index(Path.Combine(TestData, "roles"));
        var doc = index.Documents.Single(d => d.RelativePath == "passes.repro");
        await Assert.That(doc.Occurrences.Select(o => o.Symbol))
            .Contains("reprolang repro_manager roles 1.0.0 passes.repro/hello().");
        await Assert.That(doc.Occurrences.Select(o => o.SymbolRoles))
            .IsEquivalentTo(new[] { (int)SymbolRole.Definition, 0, (int)SymbolRole.Definition, (int)SymbolRole.ForwardDefinition });
    }
}
