using System.Text;
using ScipSharp.Indexer.Delphi.Syntax;
using ScipSharp.Indexer.Delphi.Text;

namespace ScipSharp.Indexer.Delphi.Tests;

public class LexerTests
{
    private static List<Token> Lex(string s) => Lexer.Tokenize(s, 0);

    private static List<Token> Preprocess(string s, CompilerSettings? settings = null, Func<string, string, (int, string)?>? include = null) =>
        new Preprocessor(settings ?? CompilerSettings.Default, include ?? (static (_, _) => null)).Process(Lex(s), "x.pas", 0);

    private static string[] Texts(IEnumerable<Token> tokens) => tokens.Where(t => !t.IsEof).Select(t => t.Text).ToArray();

    [Test]
    public async Task SkipsAllCommentStyles()
    {
        var tokens = Lex("a { brace } b (* paren *) c // line\n d");
        await Assert.That(Texts(tokens)).IsEquivalentTo(["a", "b", "c", "d"]);
    }

    [Test]
    public async Task EscapedIdentifiersAreNotKeywords()
    {
        var tokens = Lex("&begin &Type end");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Identifier);
        await Assert.That(tokens[0].Text).IsEqualTo("begin");
        await Assert.That((tokens[0].Flags & TokenFlags.Escaped) != 0).IsTrue();
        await Assert.That(tokens[0].Column).IsEqualTo(0);
        await Assert.That(tokens[0].EndColumn).IsEqualTo(6);
        await Assert.That(tokens[2].Kind).IsEqualTo(TokenKind.Keyword);
    }

    [Test]
    public async Task LexesStringsWithCharCodesAndQuotes()
    {
        var tokens = Lex("'it''s'#13#10'x'#$41 y");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.String);
        await Assert.That(tokens[0].Text).IsEqualTo("it's\r\nxA");
        await Assert.That(tokens[1].Text).IsEqualTo("y");
    }

    [Test]
    public async Task LexesMultilineStrings()
    {
        var src = "X := '''\n  line 1\n  line 2\n  ''';\nY";
        var tokens = Lex(src);
        var str = tokens.Single(t => t.Kind == TokenKind.String);
        await Assert.That(str.Text).Contains("line 1");
        await Assert.That(str.Line).IsEqualTo(0);
        await Assert.That(str.EndLine).IsEqualTo(3);
        await Assert.That(tokens.Single(t => t.Text == "Y").Line).IsEqualTo(4);
    }

    [Test]
    public async Task LexesNumbersAndRanges()
    {
        var tokens = Lex("$FF %1010 1_000 1.5e10 1..10 &17");
        await Assert.That(Texts(tokens)).IsEquivalentTo(["$FF", "%1010", "1_000", "1.5e10", "1", "..", "10", "&17"]);
    }

    [Test]
    public async Task ColumnsAreUtf16CodeUnits()
    {
        var tokens = Lex("'😀' X");
        var x = tokens.Single(t => t.Text == "X");
        await Assert.That(x.Column).IsEqualTo(5); // quote + surrogate pair (2) + quote + space
    }

    [Test]
    public async Task AttachesXmlDocToNextToken()
    {
        var tokens = Lex("/// <summary>Hi</summary>\n/// more\nprocedure P;");
        await Assert.That(tokens[0].Doc).IsEqualTo(" <summary>Hi</summary>\n more");
        var plain = Lex("// not doc\nprocedure P;");
        await Assert.That(plain[0].Doc).IsNull();
    }

    [Test]
    public async Task DigraphBracketsAreNormalised()
    {
        await Assert.That(Texts(Lex("a(.1.)"))).IsEquivalentTo(["a", "[", "1", "]"]);
    }

    [Test]
    public async Task EvaluatesIfdefElseAndDefines()
    {
        var tokens = Preprocess("{$DEFINE A} {$IFDEF A} yes {$ELSE} no {$ENDIF} {$IFNDEF A} no2 {$ENDIF} {$UNDEF A} {$IFDEF A} no3 {$ENDIF}");
        await Assert.That(Texts(tokens)).IsEquivalentTo(["yes"]);
    }

    [Test]
    public async Task EvaluatesIfExpressions()
    {
        var src = """
            {$IF CompilerVersion >= 36} d12 {$ELSEIF CompilerVersion >= 15} d7 {$ELSE} old {$IFEND}
            {$IF Defined(MSWINDOWS) and not Defined(FPC)} win {$ENDIF}
            {$IF SizeOf(Pointer) = 8} x64 {$ELSE} x86 {$ENDIF}
            {$IF Declared(SomethingUnknown)} declared {$ENDIF}
            """;
        await Assert.That(Texts(Preprocess(src))).IsEquivalentTo(["d12", "win", "x86"]);
        var d7 = CompilerSettings.Create("Win64", 15, [], []);
        await Assert.That(Texts(Preprocess(src, d7))).IsEquivalentTo(["d7", "win", "x64"]);
    }

    [Test]
    public async Task NestedConditionalsInInactiveBranchesStayInactive()
    {
        var src = "{$IFDEF NOPE} {$IFDEF MSWINDOWS} a {$ELSE} b {$ENDIF} {$ELSE} c {$ENDIF}";
        await Assert.That(Texts(Preprocess(src))).IsEquivalentTo(["c"]);
    }

    [Test]
    public async Task ExtraDefinesAndUndefinesApply()
    {
        var settings = CompilerSettings.Create("Win32", 36, ["MYDEF"], ["MSWINDOWS"]);
        var tokens = Preprocess("{$IFDEF MYDEF} a {$ENDIF} {$IFDEF MSWINDOWS} b {$ENDIF}", settings);
        await Assert.That(Texts(tokens)).IsEquivalentTo(["a"]);
    }

    [Test]
    public async Task SplicesIncludeFilesWithTheirOwnFileId()
    {
        var tokens = Preprocess("a {$I inc.inc} c", include: (_, name) => name == "inc.inc" ? (7, "{$DEFINE X} b") : null);
        await Assert.That(Texts(tokens)).IsEquivalentTo(["a", "b", "c"]);
        await Assert.That(tokens.Single(t => t.Text == "b").File).IsEqualTo(7);
        var withDefine = Preprocess("{$INCLUDE 'inc.inc'} {$IFDEF X} x {$ENDIF}", include: (_, _) => (7, "{$DEFINE X}"));
        await Assert.That(Texts(withDefine)).IsEquivalentTo(["x"]);
    }

    [Test]
    public async Task IoCheckSwitchIsNotAnInclude()
    {
        var called = false;
        Preprocess("{$I-} a {$I+}", include: (_, _) =>
        {
            called = true;
            return null;
        });
        await Assert.That(called).IsFalse();
    }

    [Test]
    public async Task TracksScopedEnums()
    {
        var tokens = Preprocess("a {$SCOPEDENUMS ON} b {$SCOPEDENUMS OFF} c");
        await Assert.That(tokens.Where(t => (t.Flags & TokenFlags.ScopedEnums) != 0).Select(t => t.Text).ToArray()).IsEquivalentTo(["b"]);
    }

    [Test]
    public async Task DecodesBomsAndWindows1252()
    {
        await Assert.That(SourceDecoder.Decode([0xEF, 0xBB, 0xBF, (byte)'a'], out var e1)).IsEqualTo("a");
        await Assert.That(e1).IsEqualTo(SourceEncoding.Utf8Bom);
        await Assert.That(SourceDecoder.Decode([0xFF, 0xFE, (byte)'a', 0], out var e2)).IsEqualTo("a");
        await Assert.That(e2).IsEqualTo(SourceEncoding.Utf16LittleEndian);
        await Assert.That(SourceDecoder.Decode(Encoding.UTF8.GetBytes("Größe"), out var e3)).IsEqualTo("Größe");
        await Assert.That(e3).IsEqualTo(SourceEncoding.Utf8);
        await Assert.That(SourceDecoder.Decode([0x47, 0x72, 0xF6, 0xDF, 0x65, 0x80], out var e4)).IsEqualTo("Größe€");
        await Assert.That(e4).IsEqualTo(SourceEncoding.Windows1252);
    }
}
