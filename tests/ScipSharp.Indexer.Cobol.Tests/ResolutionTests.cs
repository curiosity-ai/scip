namespace ScipSharp.Indexer.Cobol.Tests;

/// <summary>Name resolution end to end: qualification, scoping, procedures, CALL targets, copybook resolution.</summary>
public class ResolutionTests
{
    private const string Qualified = """
        IDENTIFICATION DIVISION.
        PROGRAM-ID. Q.
        DATA DIVISION.
        WORKING-STORAGE SECTION.
        01  REC-A.
            05 GRP.
               10 FLD PIC X.
        01  REC-B.
            05 GRP.
               10 FLD PIC X.
        PROCEDURE DIVISION.
            MOVE FLD OF REC-B TO FLD IN GRP IN REC-A
            MOVE FLD TO FLD OF GRP OF REC-B
            GOBACK.
        """;

    [Test]
    public async Task QualificationPicksTheRightItemAndReferencesQualifiers()
    {
        using var p = new TempProject(new() { ["Q.cbl"] = TempProject.Fixed(Qualified) });
        var doc = p.Index().Doc("Q.cbl");
        var line = IndexQueries.LineOf(Qualified, "MOVE FLD OF REC-B");
        await Assert.That(doc.SymbolsOnLine(line)).IsEquivalentTo(new[]
        {
            "Q/REC-B.GRP.FLD.", "Q/REC-B.", "Q/REC-A.GRP.FLD.", "Q/REC-A.GRP.", "Q/REC-A.",
        });
        // Unqualified (ambiguous) references fall back to the first declaration.
        await Assert.That(doc.SymbolsOnLine(line + 1)).IsEquivalentTo(new[]
        {
            "Q/REC-A.GRP.FLD.", "Q/REC-B.GRP.FLD.", "Q/REC-B.GRP.", "Q/REC-B.",
        });
    }

    private const string Nested = """
        IDENTIFICATION DIVISION.
        PROGRAM-ID. OUTER.
        DATA DIVISION.
        WORKING-STORAGE SECTION.
        01  G IS GLOBAL PIC X.
        01  L PIC X.
        PROCEDURE DIVISION.
            CALL 'INNER'
            GOBACK.
        IDENTIFICATION DIVISION.
        PROGRAM-ID. INNER.
        PROCEDURE DIVISION.
            MOVE G TO L
            CALL 'OTHER'
            GOBACK.
        END PROGRAM INNER.
        END PROGRAM OUTER.
        """;

    private const string Other = """
        IDENTIFICATION DIVISION.
        PROGRAM-ID. OTHER.
        PROCEDURE DIVISION.
            CALL 'INNER'
            GOBACK.
        """;

    [Test]
    public async Task GlobalItemsAreVisibleToNestedProgramsAndCallsResolveAcrossFiles()
    {
        using var p = new TempProject(new() { ["OUTER.cbl"] = TempProject.Fixed(Nested), ["OTHER.cob"] = TempProject.Fixed(Other) });
        var index = p.Index();
        var outer = index.Doc("OUTER.cbl");
        await Assert.That(outer.SymbolsOnLine(IndexQueries.LineOf(Nested, "MOVE G TO L"))).IsEquivalentTo(new[] { "OUTER/G." });
        await Assert.That(outer.SymbolsOnLine(IndexQueries.LineOf(Nested, "CALL 'INNER'"))).IsEquivalentTo(new[] { "OUTER/INNER/" });
        await Assert.That(outer.SymbolsOnLine(IndexQueries.LineOf(Nested, "CALL 'OTHER'"))).IsEquivalentTo(new[] { "OTHER/" });
        // A nested program is not callable from another compilation unit: the call stays external.
        var other = index.Doc("OTHER.cob");
        await Assert.That(other.SymbolsOnLine(IndexQueries.LineOf(Other, "CALL 'INNER'"))).IsEquivalentTo(new[] { "INNER/" });
        await Assert.That(index.ExternalSymbols.Select(s => s.Symbol).ToArray()).IsEquivalentTo(new[] { IndexQueries.Pkg + "INNER/" });
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    private const string Procedures = """
        IDENTIFICATION DIVISION.
        PROGRAM-ID. P.
        DATA DIVISION.
        WORKING-STORAGE SECTION.
        01  N PIC 9.
        PROCEDURE DIVISION.
        S1 SECTION.
        PARA.
            PERFORM PARA OF S2
            PERFORM N TIMES
               CONTINUE
            END-PERFORM
            PERFORM S2
            GO TO PARA.
        S2 SECTION.
        PARA.
            EXIT.
        """;

    [Test]
    public async Task ParagraphsAreQualifiedBySectionAndPreferTheCurrentSection()
    {
        using var p = new TempProject(new() { ["P.cbl"] = TempProject.Fixed(Procedures) });
        var doc = p.Index().Doc("P.cbl");
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(Procedures, "PERFORM PARA OF S2"))).IsEquivalentTo(new[] { "P/S2().PARA().", "P/S2()." });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(Procedures, "PERFORM N TIMES"))).IsEquivalentTo(new[] { "P/N." });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(Procedures, "PERFORM S2"))).IsEquivalentTo(new[] { "P/S2()." });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(Procedures, "GO TO PARA"))).IsEquivalentTo(new[] { "P/S1().PARA()." });
    }

    [Test]
    public async Task CopybookDirectoriesAndOutsideRootCopybooks()
    {
        var outside = Path.Combine(Path.GetTempPath(), "scip-cobol-lib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "EXTBOOK.cpy"), TempProject.Fixed("01  EXT-REC PIC X."));
        try
        {
            const string main = """
                IDENTIFICATION DIVISION.
                PROGRAM-ID. M.
                DATA DIVISION.
                WORKING-STORAGE SECTION.
                    COPY INBOOK.
                    COPY EXTBOOK.
                PROCEDURE DIVISION.
                    MOVE IN-REC TO EXT-REC.
                """;
            using var p = new TempProject(new()
            {
                ["src/M.cbl"] = TempProject.Fixed(main),
                ["books/x/INBOOK"] = TempProject.Fixed("01  IN-REC PIC X."),
            });
            var index = p.Index(new CobolIndexerOptions { PackageName = "t", CopybookDirectories = ["books/x", outside] });
            var doc = index.Doc("src/M.cbl");
            // In-root copybooks own their symbols; a copybook outside the root has no document, so the program owns them.
            await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(main, "MOVE IN-REC"))).IsEquivalentTo(new[] { "`books/x/INBOOK`/IN-REC.", "M/EXT-REC." });
            await Assert.That(index.Documents.Select(d => d.RelativePath).Order().ToArray()).IsEquivalentTo(new[] { "books/x/INBOOK", "src/M.cbl" });
            await Assert.That(TestSupport.Lint(index)).IsEmpty();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Test]
    public async Task CopybookResolutionIsCaseInsensitiveAndPrefersTheIncludersDirectory()
    {
        const string main = """
            IDENTIFICATION DIVISION.
            PROGRAM-ID. M.
            DATA DIVISION.
            WORKING-STORAGE SECTION.
                COPY book1.
            PROCEDURE DIVISION.
                MOVE SPACES TO NEAR-REC.
            """;
        using var p = new TempProject(new()
        {
            ["a/M.cbl"] = TempProject.Fixed(main),
            ["a/BOOK1.CPY"] = TempProject.Fixed("01  NEAR-REC PIC X."),
            ["b/copy/BOOK1.cpy"] = TempProject.Fixed("01  FAR-REC PIC X."),
        });
        var doc = p.Index().Doc("a/M.cbl");
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(main, "MOVE SPACES"))).IsEquivalentTo(new[] { "`a/BOOK1.CPY`/NEAR-REC." });
    }

    [Test]
    public async Task FileNamesRecordsAndFileQualifiers()
    {
        const string src = """
            IDENTIFICATION DIVISION.
            PROGRAM-ID. F.
            ENVIRONMENT DIVISION.
            INPUT-OUTPUT SECTION.
            FILE-CONTROL.
                SELECT OPTIONAL MASTER ASSIGN TO MASTERDD
                    FILE STATUS IS MS-STATUS.
            DATA DIVISION.
            FILE SECTION.
            FD  MASTER.
            01  M-REC.
                05 M-KEY PIC X(8).
            WORKING-STORAGE SECTION.
            01  MS-STATUS PIC XX.
            PROCEDURE DIVISION.
                OPEN I-O MASTER
                MOVE SPACES TO M-KEY OF MASTER
                READ MASTER INTO MS-STATUS.
            """;
        using var p = new TempProject(new() { ["F.cbl"] = TempProject.Fixed(src) });
        var index = p.Index();
        var doc = index.Doc("F.cbl");
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(src, "SELECT OPTIONAL"), definitions: true)).IsEquivalentTo(new[] { "F/MASTER#" });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(src, "FILE STATUS"))).IsEquivalentTo(new[] { "F/MS-STATUS." });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(src, "FD  MASTER"))).IsEquivalentTo(new[] { "F/MASTER#" });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(src, "M-KEY OF MASTER"))).IsEquivalentTo(new[] { "F/M-REC.M-KEY.", "F/MASTER#" });
        var record = doc.Symbols.Single(s => s.Symbol.EndsWith("F/M-REC.", StringComparison.Ordinal));
        await Assert.That(record.Documentation).Contains("Record of file `MASTER`");
        await Assert.That(record.Kind).IsEqualTo(SymbolInformation.Types.Kind.Struct);
    }

    [Test]
    public async Task DataItemDocumentationAndKinds()
    {
        const string src = """
            IDENTIFICATION DIVISION.
            PROGRAM-ID. D.
            DATA DIVISION.
            WORKING-STORAGE SECTION.
            01  W-GROUP.
                05 W-AMOUNT PIC S9(7)V99 COMP-3 VALUE +0.
                05 W-FLAG   PIC X.
                   88 W-YES VALUE 'Y'.
            77  W-COUNT PIC 9(4) COMP.
            """;
        using var p = new TempProject(new() { ["D.cbl"] = TempProject.Fixed(src) });
        var symbols = p.Index().Doc("D.cbl").Symbols.ToDictionary(s => s.Symbol[IndexQueries.Pkg.Length..]);
        await Assert.That(symbols["D/W-GROUP.W-AMOUNT."].Documentation[0]).IsEqualTo("```cobol\n05 W-AMOUNT PIC S9(7)V99 COMP-3 VALUE +0\n```");
        await Assert.That(symbols["D/W-GROUP.W-AMOUNT."].Kind).IsEqualTo(SymbolInformation.Types.Kind.Field);
        await Assert.That(symbols["D/W-GROUP.W-FLAG.W-YES."].Kind).IsEqualTo(SymbolInformation.Types.Kind.Constant);
        await Assert.That(symbols["D/W-GROUP."].Kind).IsEqualTo(SymbolInformation.Types.Kind.Struct);
        await Assert.That(symbols["D/W-COUNT."].Kind).IsEqualTo(SymbolInformation.Types.Kind.Variable);
        await Assert.That(symbols["D/"].Kind).IsEqualTo(SymbolInformation.Types.Kind.Module);
        await Assert.That(symbols["D/W-GROUP.W-AMOUNT."].DisplayName).IsEqualTo("W-AMOUNT");
    }

    [Test]
    public async Task MissingPeriodsAndGarbageDoNotAbortIndexing()
    {
        const string src = """
            IDENTIFICATION DIVISION.
            PROGRAM-ID. BROKEN.
            DATA DIVISION.
            WORKING-STORAGE SECTION.
            01  A PIC X
            01  B PIC X.
            01  ( ) ) . 05 .
            PROCEDURE DIVISION.
            P1.
                MOVE A TO B
                IF ( ( ( END-IF
                PERFORM P2 THRU.
            P2.
                MOVE 'unterminated
            """;
        using var p = new TempProject(new() { ["BROKEN.cbl"] = TempProject.Fixed(src), ["EMPTY.cbl"] = "", ["JUNK.cpy"] = "\u0000\u0001 )))) ====" });
        var index = p.Index();
        var doc = index.Doc("BROKEN.cbl");
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(src, "MOVE A TO B"))).IsEquivalentTo(new[] { "BROKEN/A.", "BROKEN/B." });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(src, "PERFORM P2"))).IsEquivalentTo(new[] { "BROKEN/P2()." });
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task LowerCaseSourcesShareUpperCaseSymbols()
    {
        const string src = """
            identification division.
            program-id. mixed.
            data division.
            working-storage section.
            01 Total-Amount pic 9(5).
            procedure division.
                add 1 to TOTAL-amount.
            """;
        using var p = new TempProject(new() { ["mixed.cbl"] = TempProject.Fixed(src) });
        var doc = p.Index().Doc("mixed.cbl");
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(src, "add 1"))).IsEquivalentTo(new[] { "MIXED/TOTAL-AMOUNT." });
        await Assert.That(doc.Symbols.Single(s => s.Symbol.EndsWith("TOTAL-AMOUNT.", StringComparison.Ordinal)).DisplayName).IsEqualTo("Total-Amount");
    }

    private const string ClauseOperands = """
        IDENTIFICATION DIVISION.
        PROGRAM-ID. R.
        DATA DIVISION.
        WORKING-STORAGE SECTION.
        01  BASE PIC X(4).
        01  OTHER-REC.
            05 BASE PIC X(4).
            05 ALT-1 REDEFINES BASE PIC 9(4).
        01  ALT-2 REDEFINES BASE PIC 9(4).
        66  RN RENAMES ALT-1.
        PROCEDURE DIVISION.
            GOBACK.
        """;

    [Test]
    public async Task RedefinesAndRenamesResolveToTheNearestPrecedingCandidate()
    {
        using var p = new TempProject(new() { ["R.cbl"] = TempProject.Fixed(ClauseOperands) });
        var doc = p.Index().Doc("R.cbl");
        // Inside a group: the sibling; at level 01: the preceding 01 record (not the field of OTHER-REC).
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(ClauseOperands, "ALT-1 REDEFINES"))).IsEquivalentTo(new[] { "R/OTHER-REC.BASE." });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(ClauseOperands, "ALT-2 REDEFINES"))).IsEquivalentTo(new[] { "R/BASE." });
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(ClauseOperands, "RENAMES"))).IsEquivalentTo(new[] { "R/OTHER-REC.ALT-1." });
    }

    [Test]
    public async Task ThousandsOfRedefinesAndUnterminatedEntriesStayLinear()
    {
        // Each REDEFINES used to rescan every item of the program, and each SELECT / FD missing its period rescanned
        // to the end of the file: ~25 s for these 16k entries.
        const int n = 16_000;
        var text = new System.Text.StringBuilder();
        text.Append("IDENTIFICATION DIVISION.\nPROGRAM-ID. BIG.\nENVIRONMENT DIVISION.\nINPUT-OUTPUT SECTION.\nFILE-CONTROL.\n");
        for (var i = 0; i < n; i++)
        {
            text.Append($"    SELECT F{i} ASSIGN TO X{i}\n");
        }

        text.Append("DATA DIVISION.\nWORKING-STORAGE SECTION.\n01 BASE PIC X(10).\n");
        for (var i = 0; i < n; i++)
        {
            text.Append($"01 R{i} REDEFINES BASE PIC X(10).\n");
        }

        text.Append("PROCEDURE DIVISION.\n    GOBACK.\n");
        var source = text.ToString();
        using var p = new TempProject(new() { ["BIG.cbl"] = TempProject.Fixed(source) });
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var doc = p.Index().Doc("BIG.cbl");
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
        await Assert.That(doc.SymbolsOnLine(IndexQueries.LineOf(source, $"R{n - 1} REDEFINES"))).IsEquivalentTo(new[] { "BIG/BASE." });
    }
}
