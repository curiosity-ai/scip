namespace ScipSharp.Indexer.Pascal.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new PascalIndexer().Name).IsEqualTo("pascal");
    }
}
