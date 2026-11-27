using ScipSharp.Indexer.Pli.Syntax;

namespace ScipSharp.Indexer.Pli.Tests;

public class LexerTests
{
    private static List<Token> Lex(string text, Margins? margins = null, MarginMode mode = MarginMode.Fixed) =>
        Lexer.Tokenize(SourceText.Create(text, margins ?? Margins.None, mode).Masked, 0);

    private static List<string> Texts(string text, Margins? margins = null, MarginMode mode = MarginMode.Fixed) =>
        Lex(text, margins, mode).ConvertAll(static t => t.Text);

    [Test]
    public async Task DefaultMarginsDropColumnOneAndSequenceNumbers()
    {
        var line = "X" + " A = B;".PadRight(71) + "00010000";
        await Assert.That(Texts(line, Margins.Default)).IsEquivalentTo(new[] { "A", "=", "B", ";" });
    }

    [Test]
    public async Task MarginsKeepOriginalColumns()
    {
        var tokens = Lex("1  DCL X;", new Margins(2, 72));
        await Assert.That(tokens[0].Text).IsEqualTo("DCL");
        await Assert.That(tokens[0].Column).IsEqualTo(3);
        await Assert.That(tokens[1].Column).IsEqualTo(7);
    }

    [Test]
    public async Task CommentsAcrossLinesAndMarginsAreIgnored()
    {
        var text = " A = 1; /* first\n   still comment */ B = 2;";
        await Assert.That(Texts(text, Margins.Default)).IsEquivalentTo(new[] { "A", "=", "1", ";", "B", "=", "2", ";" });
        var tokens = Lex(text, Margins.Default);
        await Assert.That(tokens[4].Line).IsEqualTo(1);
    }

    [Test]
    public async Task ProcessStatementSetsMargins()
    {
        var text = "*PROCESS MARGINS(1,10) SOURCE;\nA = 1;    IGNORED\n";
        var source = SourceText.Create(text, Margins.Default);
        await Assert.That(source.Margins).IsEqualTo(new Margins(1, 10));
        await Assert.That(source.ProcessOptions.Count).IsEqualTo(1);
        await Assert.That(Lexer.Tokenize(source.Masked, 0).ConvertAll(static t => t.Text)).IsEquivalentTo(new[] { "A", "=", "1", ";" });
    }

    [Test]
    public async Task NoMarginsProcessOptionMeansFreeForm()
    {
        var source = SourceText.Create("%PROCESS NOMARGINS;\nA = 1;\n", Margins.Default);
        await Assert.That(source.Margins.IsNone).IsTrue();
        await Assert.That(Lexer.Tokenize(source.Masked, 0)[0].Text).IsEqualTo("A");
    }

    [Test]
    public async Task AutoMarginsDetectFreeAndFixedForm()
    {
        await Assert.That(SourceText.Create("proc: procedure;\nend;\n", Margins.Default, MarginMode.Auto).Margins.IsNone).IsTrue();
        await Assert.That(SourceText.Create(" P: PROC;\n END;\n", Margins.Default, MarginMode.Auto).Margins).IsEqualTo(Margins.Default);
    }

    [Test]
    public async Task NationalCharactersAndDollarNames()
    {
        await Assert.That(Texts("#REC = @PTR -> $X; call ioa_$nnl (x);"))
            .IsEquivalentTo(new[] { "#REC", "=", "@PTR", "->", "$X", ";", "call", "ioa_$nnl", "(", "x", ")", ";" });
    }

    [Test]
    public async Task StringsWithSuffixesAndDoubledQuotes()
    {
        var tokens = Lex("X = 'IT''S'; Y = '1010'B; Z = 'FF'X; W = \"multics\"\"q\"");
        var strings = tokens.Where(static t => t.Kind == TokenKind.String).Select(static t => t.Text).ToList();
        await Assert.That(strings).IsEquivalentTo(new[] { "'IT''S'", "'1010'B", "'FF'X", "\"multics\"\"q\"" });
    }

    [Test]
    public async Task NumbersOperatorsAndQualification()
    {
        await Assert.That(Texts("A(1).B = 1.5E+3 ** 2; C ¬= D; E ||= F; G -> H;"))
            .IsEquivalentTo(new[] { "A", "(", "1", ")", ".", "B", "=", "1.5E+3", "**", "2", ";", "C", "¬=", "D", ";", "E", "||=", "F", ";", "G", "->", "H", ";" });
    }

    [Test]
    public async Task PreprocessorConcatenationIsOneToken()
    {
        await Assert.That(Texts("DCL FIELD%;J FIXED;")).IsEquivalentTo(new[] { "DCL", "FIELD", "%;", "J", "FIXED", ";" });
    }

    [Test]
    public async Task Latin1FallbackDecoding()
    {
        byte[] bytes = [(byte)'A', 0xAC, (byte)'=', (byte)'B']; // 0xAC is '¬' in Latin-1, invalid as UTF-8
        await Assert.That(SourceText.Decode(bytes)).IsEqualTo("A¬=B");
    }
}
