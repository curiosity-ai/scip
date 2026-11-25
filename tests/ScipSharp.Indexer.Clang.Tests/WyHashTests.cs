using ScipSharp.Indexer.Clang;

namespace ScipSharp.Indexer.Clang.Tests;

public class WyHashTests
{
    [Test]
    [Arguments("void (void)", "49f6e7a06ebc5aa8")]
    [Arguments("void (int)", "d4f767463ce0a6b3")]
    [Arguments("void (const char *)", "85c52e162fed56f9")]
    public async Task MatchesUpstream(string type, string hash) => await Assert.That(WyHash.HexOfText(type)).IsEqualTo(hash);
}
