using ScipSharp.Indexer.Cobol.Syntax;

namespace ScipSharp.Indexer.Cobol.Tests;

public class LexerTests
{
    private static List<Token> Lex(string text, SourceFormat format = SourceFormat.Auto, bool debug = false) =>
        Lexer.Lex(text, 0, format, debug);

    private static string[] Words(IEnumerable<Token> tokens) => tokens.Select(t => t.Text).ToArray();

    [Test]
    public async Task FixedFormatIgnoresSequenceAndIdentificationAreas()
    {
        var tokens = Lex("000100 MOVE A TO B.".PadRight(72) + "XYZ00010\n", SourceFormat.Fixed);
        await Assert.That(Words(tokens)).IsEquivalentTo(new[] { "MOVE", "A", "TO", "B", "." });
        await Assert.That(tokens[0].Location).IsEqualTo(new SourceLocation(0, 0, 7, 11));
        await Assert.That(tokens[0].AreaA).IsTrue();
        await Assert.That(tokens[1].AreaA).IsFalse();
    }

    [Test]
    public async Task CommentDebugAndBlankLines()
    {
        const string src = "      * comment MOVE\n      / page eject\n      D    DISPLAY X\n           DISPLAY Y\n";
        await Assert.That(Words(Lex(src, SourceFormat.Fixed))).IsEquivalentTo(new[] { "DISPLAY", "Y" });
        await Assert.That(Words(Lex(src, SourceFormat.Fixed, debug: true))).IsEquivalentTo(new[] { "DISPLAY", "X", "DISPLAY", "Y" });
    }

    [Test]
    public async Task FloatingCommentsEndTheLine()
    {
        var tokens = Lex("       MOVE A *> TO B\n       TO C.\n", SourceFormat.Fixed);
        await Assert.That(Words(tokens)).IsEquivalentTo(new[] { "MOVE", "A", "TO", "C", "." });
    }

    [Test]
    public async Task ContinuedAlphanumericLiteral()
    {
        var line1 = "       01 X PIC X(30) VALUE 'HELLO".PadRight(72) + "SEQ";
        const string line2 = "      -    ' WORLD'.";
        var tokens = Lex(line1 + "\n" + line2 + "\n", SourceFormat.Fixed);
        var literal = tokens.Single(t => t.Kind == TokenKind.Literal);
        await Assert.That(literal.Value).IsEqualTo("HELLO" + new string(' ', 72 - 34) + " WORLD");
        await Assert.That(literal.Location!.Value.Line).IsEqualTo(0);
        await Assert.That(tokens[^1].Kind).IsEqualTo(TokenKind.Period);
    }

    [Test]
    public async Task ContinuedWord()
    {
        var tokens = Lex("       MOVE WS-LONG-\n      -    NAME TO B.\n", SourceFormat.Fixed);
        await Assert.That(Words(tokens)).IsEquivalentTo(new[] { "MOVE", "WS-LONG-NAME", "TO", "B", "." });
    }

    [Test]
    public async Task DoubledQuotesAndPrefixedLiterals()
    {
        var tokens = Lex("MOVE 'IT''S' TO A. MOVE X'FF00' TO B. MOVE N\"NAT\" TO C.", SourceFormat.Free);
        var literals = tokens.Where(t => t.Kind == TokenKind.Literal).ToList();
        await Assert.That(literals.Select(l => l.Value).ToArray()).IsEquivalentTo(new[] { "IT'S", "FF00", "NAT" });
        await Assert.That(literals[1].Text).IsEqualTo("X'FF00'");
    }

    [Test]
    public async Task PictureStringsAreSingleTokens()
    {
        var tokens = Lex("05 A PIC S9(7)V99. 05 B PICTURE IS $$,$$9.99. 05 C PIC X(10)", SourceFormat.Free);
        var pictures = tokens.Where(t => t.Kind == TokenKind.Picture).Select(t => t.Text).ToArray();
        await Assert.That(pictures).IsEquivalentTo(new[] { "S9(7)V99", "$$,$$9.99", "X(10)" });
        await Assert.That(tokens.Count(t => t.Kind == TokenKind.Period)).IsEqualTo(2);
    }

    [Test]
    public async Task PseudoTextAndOperators()
    {
        var tokens = Lex("COPY X REPLACING ==:TAG:== BY ==A-B==. IF A >= B OR A <> C", SourceFormat.Free);
        await Assert.That(tokens.Count(t => t.Kind == TokenKind.PseudoDelimiter)).IsEqualTo(4);
        await Assert.That(tokens.Where(t => t.Kind == TokenKind.Operator).Select(t => t.Text).ToArray()).IsEquivalentTo(new[] { ">=", "<>" });
    }

    [Test]
    public async Task NumbersKeepDecimalPoints()
    {
        var tokens = Lex("COMPUTE X = 1.5 * Y.", SourceFormat.Free);
        await Assert.That(Words(tokens)).IsEquivalentTo(new[] { "COMPUTE", "X", "=", "1.5", "*", "Y", "." });
    }

    [Test]
    public async Task CaseInsensitiveUpper()
    {
        var token = Lex("move", SourceFormat.Free).Single();
        await Assert.That(token.Upper).IsEqualTo("MOVE");
        await Assert.That(token.Is("MOVE")).IsTrue();
    }

    [Test]
    public async Task SourceDirectivesSwitchFormat()
    {
        const string src = "       MOVE A TO B.\n       >>SOURCE FORMAT IS FREE\nMOVE C TO D. *> free now\n>>SOURCE FIXED\n000100 MOVE E TO F.\n";
        await Assert.That(Words(Lex(src, SourceFormat.Fixed)).Where(w => w.Length == 1 && char.IsLetter(w[0])).ToArray())
            .IsEquivalentTo(new[] { "A", "B", "C", "D", "E", "F" });
    }

    [Test]
    public async Task MicroFocusSetDirective()
    {
        const string src = "      $SET SOURCEFORMAT\"FREE\"\nMOVE A TO B.\n";
        await Assert.That(Words(Lex(src, SourceFormat.Fixed))).IsEquivalentTo(new[] { "MOVE", "A", "TO", "B", "." });
    }

    [Test]
    public async Task CompilerOptionLinesAreSkipped()
    {
        const string src = "CBL APOST,NODYNAM\n       IDENTIFICATION DIVISION.\n";
        await Assert.That(Words(Lex(src, SourceFormat.Fixed))).IsEquivalentTo(new[] { "IDENTIFICATION", "DIVISION", "." });
    }

    [Test]
    public async Task DetectsFreeAndFixed()
    {
        await Assert.That(Lexer.Detect(["identification division.", "program-id. x.", "procedure division.", "    display 'x'."]))
            .IsEqualTo(SourceFormat.Free);
        await Assert.That(Lexer.Detect(["000100 IDENTIFICATION DIVISION.", "000200 PROGRAM-ID. X.", "      * comment"]))
            .IsEqualTo(SourceFormat.Fixed);
        await Assert.That(Lexer.Detect(["       IDENTIFICATION DIVISION.", "       PROGRAM-ID. X."]))
            .IsEqualTo(SourceFormat.Fixed);
        await Assert.That(Lexer.Detect(["AB1234 IDENTIFICATION DIVISION.", "AB1235 PROGRAM-ID. X."]))
            .IsEqualTo(SourceFormat.Fixed);
    }

    [Test]
    public async Task VariableFormatReadsPastColumn72()
    {
        var line = "       MOVE A TO " + new string(' ', 60) + "B.";
        await Assert.That(Words(Lex(line, SourceFormat.Variable))).Contains("B");
        await Assert.That(Words(Lex(line, SourceFormat.Fixed))).DoesNotContain("B");
    }

    [Test]
    public async Task ColumnsAreUtf16Offsets()
    {
        var tokens = Lex("MOVE 'ÄÖ€' TO NAME-É.", SourceFormat.Free);
        var name = tokens.Single(t => t.Text == "NAME-É");
        await Assert.That(name.Location).IsEqualTo(new SourceLocation(0, 0, 14, 20));
    }
}
