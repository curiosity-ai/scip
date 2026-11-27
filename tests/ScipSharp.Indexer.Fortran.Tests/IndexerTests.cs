namespace ScipSharp.Indexer.Fortran.Tests;

public class IndexerTests
{
    [Test]
    public async Task HasCliName()
    {
        await Assert.That(new FortranIndexer().Name).IsEqualTo("fortran");
    }
}
