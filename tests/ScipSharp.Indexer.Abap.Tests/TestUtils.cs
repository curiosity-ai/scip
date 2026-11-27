using ScipSharp.Indexer.Abap.Files;
using ScipSharp.Indexer.Abap.Lexer;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements;

namespace ScipSharp.Indexer.Abap.Tests;

/// <summary>Port of the helpers in abaplint test/abap/_utils.ts.</summary>
internal static class TestUtils
{
    public static IReadOnlyList<AbstractToken> GetTokens(string abap) =>
        new Lexer.Lexer().Run(new MemoryFile("cl_foo.clas.abap", abap)).Tokens;

    public static IReadOnlyList<StatementNode> GetStatements(string abap, ABAPRelease? release = null, LanguageVersion languageVersion = LanguageVersion.Normal)
    {
        var lexerResult = new Lexer.Lexer().Run(new MemoryFile("cl_foo.clas.abap", abap));
        return new StatementParser(release ?? Versions.DefaultRelease, null, languageVersion).Run([lexerResult], [])[0].Statements;
    }
}
