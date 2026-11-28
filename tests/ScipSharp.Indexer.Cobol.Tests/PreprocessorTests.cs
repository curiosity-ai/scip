using ScipSharp.Indexer.Cobol.Syntax;

namespace ScipSharp.Indexer.Cobol.Tests;

/// <summary>COPY / REPLACING / REPLACE on token streams (no files: copybooks come from an in-memory map).</summary>
public class PreprocessorTests
{
    private static PreprocessedSource Run(string main, Dictionary<string, string> books, List<string>? warnings = null, int maxExpandedTokens = Preprocessor.MaxExpandedTokens)
    {
        var names = books.Keys.ToList();
        IReadOnlyList<Token> LexBook(int id) => Lexer.Lex(books[names[id - 1]], id, SourceFormat.Free);
        int Resolve(string name, string? library, int includer) => names.FindIndex(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)) is >= 0 and var i ? i + 1 : -1;
        var pre = new Preprocessor(LexBook, Resolve, warnings is null ? null : warnings.Add, maxExpandedTokens);
        return pre.Process(0, Lexer.Lex(main, 0, SourceFormat.Free));
    }

    private static string Text(PreprocessedSource p) => string.Join(" ", p.Tokens.Select(t => t.Text));

    [Test]
    public async Task CopyExpandsInPlaceAndRecordsOrigin()
    {
        var result = Run("01 A. COPY BOOK. 01 B.", new() { ["BOOK"] = "05 X PIC 9." });
        await Assert.That(Text(result)).IsEqualTo("01 A . 05 X PIC 9 . 01 B .");
        await Assert.That(result.Tokens.Single(t => t.Text == "X").Origin).IsEqualTo(1);
        await Assert.That(result.Tokens.Single(t => t.Text == "A").Origin).IsEqualTo(-1);
        await Assert.That(result.Copies.Single().TargetFileId).IsEqualTo(1);
        await Assert.That(result.IncludedFiles.Contains(1)).IsTrue();
    }

    [Test]
    public async Task CopyOfLibraryLiteralNameAndSuppress()
    {
        var result = Run("COPY 'BOOK' OF MYLIB SUPPRESS. X.", new() { ["BOOK"] = "Y" });
        await Assert.That(Text(result)).IsEqualTo("Y X .");
    }

    [Test]
    public async Task ReplacingWholeWordsKeepsTheCopybookLocation()
    {
        var result = Run("COPY BOOK REPLACING ==OLD-NAME== BY ==NEW-NAME== LEADING ==PRE== BY ==XY==.", new() { ["BOOK"] = "05 OLD-NAME PIC X. 05 OLD-NAME-2 PIC X. 05 PRE-A PIC X." });
        await Assert.That(Text(result)).IsEqualTo("05 NEW-NAME PIC X . 05 OLD-NAME-2 PIC X . 05 XY-A PIC X .");
        var replaced = result.Tokens.Single(t => t.Text == "NEW-NAME");
        await Assert.That(replaced.Location).IsEqualTo(new SourceLocation(1, 0, 3, 11));
    }

    [Test]
    public async Task PartialWordReplacementWithColonTags()
    {
        var result = Run("COPY BOOK REPLACING ==:TAG:== BY ==CUST==.", new() { ["BOOK"] = "01 :TAG:-REC. 05 :TAG:-ID PIC 9." });
        await Assert.That(Text(result)).IsEqualTo("01 CUST-REC . 05 CUST-ID PIC 9 .");
        await Assert.That(result.Tokens.Single(t => t.Text == "CUST-REC").Location).IsEqualTo(new SourceLocation(1, 0, 3, 12));
    }

    [Test]
    public async Task PartialWordReplacementWithParenthesesInsideAWord()
    {
        var result = Run("COPY BOOK REPLACING ==(V)== BY ==ACCT==.", new() { ["BOOK"] = "IF (FLG-(V)-OK OR X) MOVE 1 TO (V)C" });
        await Assert.That(Text(result)).IsEqualTo("IF ( FLG-ACCT-OK OR X ) MOVE 1 TO ACCTC");
        // The leading '(' keeps its column; the word maps to the changed part of the cluster.
        var word = result.Tokens.Single(t => t.Text == "FLG-ACCT-OK");
        await Assert.That(word.Location).IsEqualTo(new SourceLocation(1, 0, 4, 14));
        await Assert.That(result.Tokens.Single(t => t.Text == "ACCTC").Location).IsEqualTo(new SourceLocation(1, 0, 31, 35));
    }

    [Test]
    public async Task MultiTokenReplacement()
    {
        var result = Run("COPY BOOK REPLACING ==PIC X(10)== BY ==PIC X(20)== X BY Y.", new() { ["BOOK"] = "05 A PIC X(10). 05 X PIC 9." });
        await Assert.That(Text(result)).IsEqualTo("05 A PIC X(20) . 05 Y PIC 9 .");
    }

    [Test]
    public async Task ReplacingByNothingDeletesText()
    {
        var result = Run("COPY BOOK REPLACING ==VALUE ZERO== BY ====.", new() { ["BOOK"] = "05 A PIC 9 VALUE ZERO." });
        await Assert.That(Text(result)).IsEqualTo("05 A PIC 9 .");
    }

    [Test]
    public async Task NestedCopyAndCycleDetection()
    {
        var warnings = new List<string>();
        var result = Run("COPY A.", new() { ["A"] = "X COPY B.", ["B"] = "Y COPY A." }, warnings);
        await Assert.That(Text(result)).IsEqualTo("X Y");
        await Assert.That(warnings.Any(w => w.Contains("recursive", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MissingCopybookIsReported()
    {
        var warnings = new List<string>();
        var result = Run("COPY NOPE. MOVE A TO B. EXEC SQL INCLUDE SQLCA END-EXEC.", [], warnings);
        await Assert.That(Text(result)).IsEqualTo("MOVE A TO B . .");
        await Assert.That(warnings).IsEquivalentTo(new[] { "copybook not found: NOPE" });
        await Assert.That(result.Copies.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SqlIncludeAndPanvaletInclude()
    {
        var result = Run("EXEC SQL INCLUDE DCL END-EXEC. ++INCLUDE PAN\nZ.", new() { ["DCL"] = "01 H.", ["PAN"] = "01 P." });
        await Assert.That(Text(result)).IsEqualTo("01 H . . 01 P . Z .");
    }

    [Test]
    public async Task ReplaceStatementAppliesUntilReplaceOff()
    {
        var result = Run("REPLACE ==A== BY ==B== ==C D== BY ==E==. MOVE A TO C D. REPLACE OFF. MOVE A TO X.", []);
        await Assert.That(Text(result)).IsEqualTo("MOVE B TO E . MOVE A TO X .");
    }

    [Test]
    public async Task ReplaceAppliesToCopiedText()
    {
        var result = Run("REPLACE ==FOO== BY ==BAR==. COPY BOOK.", new() { ["BOOK"] = "MOVE FOO TO X." });
        await Assert.That(Text(result)).IsEqualTo("MOVE BAR TO X .");
    }

    [Test]
    public async Task CompilerDirectingStatementsAreDropped()
    {
        var result = Run("EJECT\nMOVE A TO B.\nSKIP2\nTITLE 'X'.\nDISPLAY B.", []);
        await Assert.That(Text(result)).IsEqualTo("MOVE A TO B . DISPLAY B .");
    }

    [Test]
    public async Task DiamondCopyGraphIsBoundedInsteadOfExpandingExponentially()
    {
        // Each copybook copies the next one twice: 2^40 copies without the expansion budget.
        var books = new Dictionary<string, string>();
        for (var d = 0; d < 40; d++)
        {
            books[$"C{d}"] = d < 39 ? $"01 V{d} PIC X. COPY C{d + 1}. COPY C{d + 1}." : $"01 V{d} PIC X.";
        }

        var warnings = new List<string>();
        var result = Run("COPY C0. 01 LAST-ONE PIC X.", books, warnings, maxExpandedTokens: 20_000);
        await Assert.That(result.Tokens.Count).IsLessThanOrEqualTo(20_000 + 10);
        await Assert.That(result.Tokens[^4].Text).IsEqualTo("LAST-ONE");
        await Assert.That(warnings.Any(w => w.Contains("copybook tokens expanded", StringComparison.Ordinal))).IsTrue();
    }
}
