using ScipSharp.Indexer.Fortran.Syntax;

namespace ScipSharp.Indexer.Fortran.Tests;

/// <summary>Source form handling: comments, continuation, labels, column limits, statement separators, doc comments, lexing.</summary>
public class ReaderTests
{
    private static List<Stmt> Read(string text, SourceForm form, int fixedLength = 72, bool evaluate = false, Dictionary<string, string>? defines = null)
    {
        var doc = new SourceDoc("/x", "x", text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'), form);
        return StatementReader.Read(doc, fixedLength, new Preprocessor(evaluate, defines ?? []));
    }

    private static string Join(Stmt st) => string.Join(' ', st.Tokens.Select(static t => t.Text));

    [Test]
    public async Task FixedFormCommentsLabelsAndContinuation()
    {
        var stmts = Read("""
            C comment
            * another
            ! bang comment
                  PROGRAM P
               10 X = 1 +
                 $    2
            c     interleaved comment
                 &    + 3
                  END
            """, SourceForm.Fixed);
        await Assert.That(stmts.Select(Join).ToList()).IsEquivalentTo(new List<string> { "PROGRAM P", "X = 1 + 2 + 3", "END" });
        var assign = stmts[1];
        await Assert.That(assign.Label!.Text).IsEqualTo("10");
        await Assert.That(assign.Label.Col).IsEqualTo(3);
        // the continued "2" keeps its own line/column
        var two = assign.Tokens.Single(static t => t.Text == "2");
        await Assert.That(two.Line).IsEqualTo(5);
        await Assert.That(two.Col).IsEqualTo(10);
    }

    [Test]
    public async Task FixedFormIgnoresColumnsAfter72()
    {
        var line = "      X = Y" + new string(' ', 61) + "SEQ00010";
        await Assert.That(line.IndexOf('S', StringComparison.Ordinal)).IsEqualTo(72);
        var stmts = Read(line, SourceForm.Fixed);
        await Assert.That(Join(stmts.Single())).IsEqualTo("X = Y");
        var extended = Read(line, SourceForm.Fixed, fixedLength: 0);
        await Assert.That(Join(extended.Single())).IsEqualTo("X = Y SEQ00010");
    }

    [Test]
    public async Task FixedFormTabFormatAndDebugLines()
    {
        var stmts = Read("10\tX = 1 +\n\t1 2\nD     PRINT *, X\n\tEND", SourceForm.Fixed);
        await Assert.That(stmts.Select(Join).ToList()).IsEquivalentTo(new List<string> { "X = 1 + 2", "END" });
        await Assert.That(stmts[0].Label!.Text).IsEqualTo("10");
    }

    [Test]
    public async Task FreeFormContinuationWithLeadingAmpersandAndStrings()
    {
        var stmts = Read("""
            call foo(a, &   ! trailing comment
                     ! comment line inside the continuation
                     b)
            s = 'abc&
                &def'  ! continued character context
            x = 1; y = 2 ; 100 continue
            """, SourceForm.Free);
        await Assert.That(stmts.Select(Join).ToList()).IsEquivalentTo(new List<string>
        {
            "call foo ( a , b )", "s = 'abcdef'", "x = 1", "y = 2", "continue",
        });
        await Assert.That(stmts[4].Label!.Text).IsEqualTo("100");
    }

    [Test]
    public async Task BangInsideStringIsNotAComment()
    {
        var stmts = Read("print *, 'hello ! world', \"it''s\" ! real comment", SourceForm.Free);
        await Assert.That(Join(stmts.Single())).IsEqualTo("print * , 'hello ! world' , \"it''s\"");
    }

    [Test]
    public async Task DocCommentsBeforeAndAfter()
    {
        var stmts = Read("""
            !> Leading doc
            !> second line
            subroutine s(x)
              !! trailing doc (FORD)
              real :: x !< inline doc
            end subroutine
            """, SourceForm.Free);
        await Assert.That(stmts[0].Docs).IsEquivalentTo(new List<string> { "Leading doc", "second line", "trailing doc (FORD)" });
        await Assert.That(stmts[1].Docs).IsEquivalentTo(new List<string> { "inline doc" });
    }

    [Test]
    public async Task FixedFormDoxygenDocsSkipPlainComments()
    {
        var stmts = Read("""
            *> \brief Doc
            *  ======
            *> more
                  SUBROUTINE S
                  END
            """, SourceForm.Fixed);
        await Assert.That(stmts[0].Docs).IsEquivalentTo(new List<string> { @"\brief Doc", "more" });
    }

    [Test]
    public async Task LexerHandlesNumbersOperatorsAndKinds()
    {
        var t = Read("x = 1.eq.2 .and. 3.d0 > .5e-3_dp .or. a%b .myop. c // 'z' ; z = b'0101' + 1.0_8", SourceForm.Free)
            .SelectMany(static s => s.Tokens).ToList();
        var kinds = t.Select(static x => $"{x.Kind}:{x.Text}").ToList();
        await Assert.That(kinds).Contains("Int:1");
        await Assert.That(kinds).Contains("DotOp:.eq.");
        await Assert.That(kinds).Contains("Real:3.d0");
        await Assert.That(kinds).Contains("Real:.5e-3");
        await Assert.That(kinds).Contains("DotOp:.myop.");
        await Assert.That(kinds).Contains("Op://");
        await Assert.That(kinds).Contains("Boz:b'0101'");
        await Assert.That(kinds).Contains("Real:1.0");
        var kind = t.Single(static x => x.Text == "dp");
        await Assert.That(kind.IsKindParam).IsTrue();
    }

    [Test]
    public async Task HollerithConstants()
    {
        var t = Read("   10 FORMAT (5HHELLO, 2X, 3HA,B)", SourceForm.Fixed).Single().Tokens;
        await Assert.That(t.Count(static x => x.Kind == TokenKind.String)).IsEqualTo(2);
        await Assert.That(t.Any(static x => x.Text == "5HHELLO")).IsTrue();
        await Assert.That(t.Any(static x => x.Text == "3HA,B")).IsTrue();
        // REAL*8H is a declaration of H in blank-insensitive fixed form, not a Hollerith constant
        var decl = Read("      REAL*8H", SourceForm.Fixed).Single().Tokens;
        await Assert.That(decl.Any(static x => x.Kind == TokenKind.String)).IsFalse();
    }

    [Test]
    public async Task PreprocessorAllBranchesOrEvaluated()
    {
        const string src = "#ifdef A\ninteger :: x\n#else\nreal :: x\n#endif\n#if defined(B) && B > 1\nlogical :: y\n#endif";
        await Assert.That(Read(src, SourceForm.Free).Count).IsEqualTo(3);
        var eval = Read(src, SourceForm.Free, evaluate: true, defines: new() { ["B"] = "2" });
        await Assert.That(eval.Select(Join).ToList()).IsEquivalentTo(new List<string> { "real :: x", "logical :: y" });
        var include = Read("#include \"defs.h\"\nx = 1", SourceForm.Free);
        await Assert.That(include[0].CppInclude).IsEqualTo("defs.h");
    }

    [Test]
    public async Task FormDetection()
    {
        await Assert.That(SourceForms.FromExtension("a.f")).IsEqualTo(SourceForm.Fixed);
        await Assert.That(SourceForms.FromExtension("a.FOR")).IsEqualTo(SourceForm.Fixed);
        await Assert.That(SourceForms.FromExtension("a.F90")).IsEqualTo(SourceForm.Free);
        await Assert.That(SourceForms.FromExtension("a.f08")).IsEqualTo(SourceForm.Free);
        await Assert.That(SourceForms.LooksLikeFreeForm(["C comment", "      X = 1"])).IsFalse();
        await Assert.That(SourceForms.LooksLikeFreeForm(["program p", "  x = 1"])).IsTrue();
    }
}
