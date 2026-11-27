namespace ScipSharp.Indexer.Delphi.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new DelphiIndexer().Name).IsEqualTo("delphi");
    }
}
