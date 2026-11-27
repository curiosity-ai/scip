namespace ScipSharp.Indexer.Vb6.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new Vb6Indexer().Name).IsEqualTo("vb6");
    }
}
