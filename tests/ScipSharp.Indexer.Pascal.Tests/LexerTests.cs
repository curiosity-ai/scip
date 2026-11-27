using System.Text;
using ScipSharp.Indexer.Pascal.Syntax;

namespace ScipSharp.Indexer.Pascal.Tests;

public class LexerTests
{
    private static List<Token> Lex(string text, bool nested = false)
    {
        var lexer = new Lexer(new SourceText("/x.pas", text, SourceEncoding.Utf8)) { NestedComments = nested };
        var result = new List<Token>();
        for (var t = lexer.Next(); t.Kind != TokenKind.Eof; t = lexer.Next())
        {
            result.Add(t);
        }

        return result;
    }

    private static List<Token> Preprocess(string text, PascalMode mode = PascalMode.ObjFpc, IncludeResolver? include = null, params string[] defines)
    {
        var (tokens, _) = Preprocessor.Run(new SourceText("/x.pas", text, SourceEncoding.Utf8), mode, defines, include ?? ((_, _) => null));
        return tokens.Where(static t => t.Kind != TokenKind.Eof).ToList();
    }

    [Test]
    public async Task SkipsAllCommentStylesAndRecordsThem()
    {
        var src = new SourceText("/x.pas", "a { one } b (* two *) c // three\nd", SourceEncoding.Utf8);
        var lexer = new Lexer(src);
        var words = new List<string>();
        for (var t = lexer.Next(); t.Kind != TokenKind.Eof; t = lexer.Next())
        {
            words.Add(t.Text);
        }

        await Assert.That(words).IsEquivalentTo(["a", "b", "c", "d"]);
        await Assert.That(src.Comments.Select(static c => c.Text)).IsEquivalentTo(["{ one }", "(* two *)", "// three"]);
    }

    [Test]
    public async Task NestedBraceCommentsOnlyWhenEnabled()
    {
        await Assert.That(Lex("{ a { b } c } x", nested: true).Select(static t => t.Text)).IsEquivalentTo(["x"]);
        await Assert.That(Lex("{ a { b } c } x").Select(static t => t.Text)).IsEquivalentTo(["c", "}", "x"]);

        // Unbalanced nesting falls back to a flat comment instead of swallowing the file.
        await Assert.That(Lex("{ stray { brace } x", nested: true).Select(static t => t.Text)).IsEquivalentTo(["x"]);
    }

    [Test]
    public async Task LexesLiterals()
    {
        var tokens = Lex("$FF &17 %101 1.5e3 1..2 'it''s'#13#10'x' #$41 &begin (. .) := <> ..");
        await Assert.That(tokens.Select(static t => t.Text)).IsEquivalentTo(
            ["$FF", "&17", "%101", "1.5e3", "1", "..", "2", "'it''s'#13#10'x'", "#$41", "begin", "[", "]", ":=", "<>", ".."]);
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Number);
        await Assert.That(tokens[7].Kind).IsEqualTo(TokenKind.String);
        await Assert.That(tokens[9].Escaped).IsTrue();
    }

    [Test]
    public async Task DirectivesAreTokens()
    {
        var tokens = Lex("{$mode objfpc}(*$H+*) x");
        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Directive);
        await Assert.That(tokens[0].Text).IsEqualTo("mode objfpc");
        await Assert.That(tokens[1].Text).IsEqualTo("H+");
    }

    [Test]
    public async Task CPreprocessorLinesBecomeDirectives()
    {
        await Assert.That(Lexer.CppDirective("include \"mpb32.inc\"")).IsEqualTo("I mpb32.inc");
        await Assert.That(Lexer.CppDirective("if !defined(A) && defined(B)")).IsEqualTo("IF  not defined(A)  and  defined(B)");
        var tokens = Preprocess("#define FOO 1\n#ifdef FOO\na\n#else\nb\n#endif\n");
        await Assert.That(tokens.Select(static t => t.Text)).IsEquivalentTo(["a"]);
    }

    [Test]
    public async Task KeywordsDependOnMode()
    {
        var objfpc = Preprocess("class string begin");
        await Assert.That(objfpc.All(static t => t.Kind == TokenKind.Keyword)).IsTrue();
        var iso = Preprocess("class string begin", PascalMode.Iso);
        await Assert.That(iso.Select(static t => t.Kind)).IsEquivalentTo([TokenKind.Identifier, TokenKind.Identifier, TokenKind.Keyword]);
        var switched = Preprocess("{$mode iso} class");
        await Assert.That(switched.Single().Kind).IsEqualTo(TokenKind.Identifier);
    }

    [Test]
    public async Task ConditionalCompilation()
    {
        const string src = """
            {$DEFINE A}
            {$IFDEF A} a1 {$ELSE} a2 {$ENDIF}
            {$IFNDEF B} b1 {$ENDIF}
            {$IF defined(A) and (FPC_FULLVERSION >= 30000)} c1 {$ELSEIF defined(A)} c2 {$ELSE} c3 {$IFEND}
            {$UNDEF A}
            {$IFDEF A} d1 {$ELSE} {$IFDEF FPC} d2 {$ENDIF} {$ENDIF}
            {$IFDEF CUSTOM} e1 {$ENDIF}
            """;
        var tokens = Preprocess(src, PascalMode.ObjFpc, null, [.. Preprocessor.DefaultDefines, "CUSTOM"]);
        await Assert.That(tokens.Select(static t => t.Text)).IsEquivalentTo(["a1", "b1", "c1", "d2", "e1"]);
    }

    [Test]
    public async Task IncludesAreSpliced()
    {
        var inc = new SourceText("/inc/defs.inc", "y z", SourceEncoding.Utf8);
        var seen = new List<string>();
        var tokens = Preprocess("x {$I defs.inc} {$I+} {$INCLUDE 'defs.inc'} w", PascalMode.ObjFpc, (name, _) =>
        {
            seen.Add(name);
            return inc;
        });
        await Assert.That(tokens.Select(static t => t.Text)).IsEquivalentTo(["x", "y", "z", "y", "z", "w"]);
        await Assert.That(seen).IsEquivalentTo(["defs.inc", "defs.inc"]);
        await Assert.That(tokens[1].Source).IsSameReferenceAs(inc);
    }

    [Test]
    public async Task DecodesUtf8AndLegacyEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var utf8 = SourceText.Decode("/a.pas", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("x := 'é'; y")], Encoding.Latin1);
        await Assert.That(utf8.Encoding).IsEqualTo(SourceEncoding.Utf8);
        await Assert.That(utf8.PositionEncoding).IsEqualTo(PositionEncoding.Utf8CodeUnitOffsetFromLineStart);
        await Assert.That(utf8.PositionOf(utf8.Text.IndexOf('y')).Character).IsEqualTo(11); // 'é' is two bytes

        var cp1252 = SourceText.Decode("/b.pas", Encoding.GetEncoding(1252).GetBytes("x := '€'; y"), Encoding.GetEncoding(1252));
        await Assert.That(cp1252.Encoding).IsEqualTo(SourceEncoding.Legacy);
        await Assert.That(cp1252.Text).IsEqualTo("x := '€'; y");
        await Assert.That(cp1252.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
        await Assert.That(cp1252.PositionOf(cp1252.Text.IndexOf('y')).Character).IsEqualTo(10);
    }
}
