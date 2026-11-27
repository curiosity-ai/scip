using ScipSharp.Indexer.Vb6.Syntax;
using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6.Tests;

public class LexerTests
{
    private static List<Token> Lex(string text) => Lexer.Lex(SourceText.FromString(text).Lines).Tokens;

    private static string Kinds(string text) =>
        string.Join(" ", Lex(text).Select(t => t.Kind == TokenKind.NewLine ? "NL" : $"{t.Kind}:{t.Text}{(t.Suffix != '\0' ? t.Suffix.ToString() : "")}"));

    [Test]
    public async Task JoinsLineContinuationsIntoOneLogicalLine()
    {
        var tokens = Lex("x = a + _\r\n    b\r\ny = 1");
        await Assert.That(tokens.Count(t => t.Kind == TokenKind.NewLine)).IsEqualTo(2);
        var b = tokens.Single(t => t.Text == "b");
        await Assert.That(b.Line).IsEqualTo(1);
        await Assert.That(b.Col).IsEqualTo(4);
    }

    [Test]
    public async Task SkipsCommentsAndRem()
    {
        await Assert.That(Kinds("x = 1 ' note\r\nRem whole line\r\ny = 2: Rem after colon"))
            .IsEqualTo("Identifier:x Punct:= Number:1 NL Identifier:y Punct:= Number:2 Punct:: NL");
    }

    [Test]
    public async Task CommentEndingInContinuationSwallowsNextLine()
    {
        var result = Lexer.Lex(SourceText.FromString("' first _\r\n  still comment\r\nx = 1").Lines);
        await Assert.That(result.Tokens.Count(t => t.Kind == TokenKind.Identifier)).IsEqualTo(1);
        await Assert.That(result.CommentLines.Keys.Order().ToArray()).IsEquivalentTo([0, 1]);
    }

    [Test]
    public async Task RecognisesTypeSuffixes()
    {
        await Assert.That(Kinds("s$ = Left$(a, 1): n% = 1: l& = 2: d# = 3: c@ = 4: f! = 5"))
            .IsEqualTo("Identifier:s$ Punct:= Identifier:Left$ Punct:( Identifier:a Punct:, Number:1 Punct:) Punct:: Identifier:n% Punct:= Number:1 Punct:: "
                + "Identifier:l& Punct:= Number:2 Punct:: Identifier:d# Punct:= Number:3 Punct:: Identifier:c@ Punct:= Number:4 Punct:: Identifier:f! Punct:= Number:5 NL");
        var s = Lex("s$ = 1")[0];
        await Assert.That(s.EndCol).IsEqualTo(1); // the range covers the name only
    }

    [Test]
    public async Task DistinguishesBangFromSingleSuffix()
    {
        await Assert.That(Kinds("x = rs!Name")).IsEqualTo("Identifier:x Punct:= Identifier:rs Punct:! Identifier:Name NL");
    }

    [Test]
    public async Task LexesLiterals()
    {
        await Assert.That(Kinds("x = &HFF& + &O17 + 1.5E+3 + .25 + 10#"))
            .IsEqualTo("Identifier:x Punct:= Number:&HFF& Punct:+ Number:&O17 Punct:+ Number:1.5E+3 Punct:+ Number:.25 Punct:+ Number:10# NL");
        await Assert.That(Kinds("d = #1/2/2003# + #12:30:00 PM#")).IsEqualTo("Identifier:d Punct:= Date:#1/2/2003# Punct:+ Date:#12:30:00 PM# NL");
        await Assert.That(Kinds("s = \"say \"\"hi\"\"\"")).IsEqualTo("Identifier:s Punct:= String:say \"hi\" NL");
    }

    [Test]
    public async Task FileNumbersAreNotDates()
    {
        await Assert.That(Kinds("Print #1, a: Write #f, b: Close #1"))
            .IsEqualTo("Identifier:Print Punct:# Number:1 Punct:, Identifier:a Punct:: Identifier:Write Punct:# Identifier:f Punct:, Identifier:b Punct:: Identifier:Close Punct:# Number:1 NL");
    }

    [Test]
    public async Task BracketedIdentifiersAreNeverKeywords()
    {
        var tokens = Lex("[_NewEnum] = [Type]");
        await Assert.That(tokens[0].Text).IsEqualTo("_NewEnum");
        await Assert.That(tokens[0].Col).IsEqualTo(1);
        await Assert.That(tokens[2].IsName).IsTrue();
        await Assert.That(Lex("Type")[0].IsName).IsFalse();
    }

    [Test]
    public async Task KeywordsAreCaseInsensitive()
    {
        var tokens = Lex("PRIVATE sub foo()");
        await Assert.That(tokens[0].Is("Private")).IsTrue();
        await Assert.That(tokens[1].IsKeyword).IsTrue();
        await Assert.That(tokens[2].IsName).IsTrue();
    }

    [Test]
    public async Task DecodesAnsiAndUtf8Sources()
    {
        var ansi = SourceText.Windows1252.GetBytes("Dim café As String ' €");
        var decoded = SourceText.Decode(ansi, SourceText.Windows1252);
        await Assert.That(decoded.Lines[0]).IsEqualTo("Dim café As String ' €");
        var utf8 = System.Text.Encoding.UTF8.GetBytes("Dim café As String");
        await Assert.That(SourceText.Decode(utf8, SourceText.Windows1252).Lines[0]).IsEqualTo("Dim café As String");
        var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(utf8).ToArray();
        await Assert.That(SourceText.Decode(bom, SourceText.Windows1252).Lines[0]).IsEqualTo("Dim café As String");
        var cyrillic = SourceText.ResolveEncoding("1251");
        await Assert.That(SourceText.Decode(cyrillic.GetBytes("Dim имя"), cyrillic).Lines[0]).IsEqualTo("Dim имя");
    }

    [Test]
    public async Task ColumnsAreUtf16Offsets()
    {
        var tokens = Lexer.Lex(SourceText.Decode(SourceText.Windows1252.GetBytes("é = café"), SourceText.Windows1252).Lines).Tokens;
        await Assert.That(tokens[2].Text).IsEqualTo("café");
        await Assert.That(tokens[2].Col).IsEqualTo(4);
        await Assert.That(tokens[2].EndCol).IsEqualTo(8);
    }
}
