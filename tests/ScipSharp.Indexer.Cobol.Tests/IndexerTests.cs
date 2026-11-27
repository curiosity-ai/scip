namespace ScipSharp.Indexer.Cobol.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new CobolIndexer().Name).IsEqualTo("cobol");
    }
}
