namespace ScipSharp.Indexer.Sql.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new SqlIndexer().Name).IsEqualTo("sql");
    }
}
