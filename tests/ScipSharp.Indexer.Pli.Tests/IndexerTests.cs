namespace ScipSharp.Indexer.Pli.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new PliIndexer().Name).IsEqualTo("pli");
    }
}
