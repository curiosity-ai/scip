// Cases from abaplint packages/core/test/abap/lexer/*.ts (MIT, see THIRD-PARTY-NOTICES.md of the indexer)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using static ScipSharp.Indexer.Abap.Tests.TestUtils;

namespace ScipSharp.Indexer.Abap.Tests;

public class LexerTests
{
    [Test]
    public async Task ArrowWhitespace()
    {
        var tokens = GetTokens(") ->text");
        await Assert.That(tokens.Count).IsEqualTo(3);
        await Assert.That(tokens[1]).IsTypeOf<WInstanceArrow>();
    }

    [Test]
    [Arguments(".", 1)]
    [Arguments("`foo` \"#EC NOTEXT", 2)]
    [Arguments("`foo`\"#EC NOTEXT", 2)]
    public async Task TokenCount(string abap, int expected) => await Assert.That(GetTokens(abap).Count).IsEqualTo(expected);

    [Test]
    [Arguments("|{ sdf }|")]
    [Arguments("|{ sdf }sdf|")]
    public async Task StringTemplate(string abap)
    {
        var tokens = GetTokens(abap);
        await Assert.That(tokens.Count).IsEqualTo(3);
        await Assert.That(tokens[0]).IsTypeOf<StringTemplateBegin>();
        await Assert.That(tokens[2]).IsTypeOf<StringTemplateEnd>();
    }

    [Test]
    public async Task TwoCommentsSameColumn()
    {
        var tokens = GetTokens("\n \" hello\n \" world");
        await Assert.That(tokens.Count).IsEqualTo(2);
        await Assert.That(tokens[0].GetCol()).IsEqualTo(tokens[1].GetCol());
    }

    [Test]
    public async Task PositionsAreOneBasedRowsAndColumns()
    {
        var tokens = GetTokens("DATA foo TYPE i.\n  WRITE foo.");
        var write = tokens.First(t => t.GetStr() == "WRITE");
        await Assert.That(write.GetRow()).IsEqualTo(2);
        await Assert.That(write.GetCol()).IsEqualTo(3);
    }
}
