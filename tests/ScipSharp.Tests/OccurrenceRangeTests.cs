#pragma warning disable CS0612 // deprecated range fields are exercised on purpose

namespace ScipSharp.Tests;

public class OccurrenceRangeTests
{
    private static Occurrence Legacy(string symbol, params int[] range)
    {
        var occ = new Occurrence { Symbol = symbol };
        occ.Range.AddRange(range);
        return occ;
    }

    [Test]
    public async Task SourceRangeTypedSingleLine()
    {
        var occ = new Occurrence { SingleLineRange = new SingleLineRange { Line = 5, StartCharacter = 2, EndCharacter = 7 } };
        await Assert.That(occ.TryGetSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(5, 2, 5, 7));
    }

    [Test]
    public async Task SourceRangeTypedMultiLine()
    {
        var occ = new Occurrence { MultiLineRange = new MultiLineRange { StartLine = 1, StartCharacter = 2, EndLine = 3, EndCharacter = 4 } };
        await Assert.That(occ.TryGetSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(1, 2, 3, 4));
    }

    [Test]
    public async Task SourceRangeDeprecatedFallback()
    {
        await Assert.That(Legacy("", 2, 3, 5).TryGetSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(2, 3, 2, 5));
    }

    [Test]
    public async Task SourceRangeTypedTakesPrecedenceOverDeprecated()
    {
        var occ = Legacy("", 100, 100, 100);
        occ.SingleLineRange = new SingleLineRange { Line = 5, StartCharacter = 2, EndCharacter = 7 };
        await Assert.That(occ.TryGetSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(5, 2, 5, 7));
    }

    [Test]
    public async Task SourceRangeMissing()
    {
        await Assert.That(new Occurrence().TryGetSourceRange(out var r)).IsFalse();
        await Assert.That(r).IsEqualTo(default(Range));
        await Assert.That(new Occurrence().GetSourceRange()).IsNull();
    }

    [Test]
    public async Task SourceRangeDeprecatedMalformed()
    {
        await Assert.That(Legacy("", 1, 2).TryGetSourceRange(out var r)).IsFalse();
        await Assert.That(r).IsEqualTo(default(Range));
    }

    [Test]
    public async Task EnclosingSourceRangeTypedSingleLine()
    {
        var occ = new Occurrence { SingleLineEnclosingRange = new SingleLineRange { Line = 5, StartCharacter = 0, EndCharacter = 10 } };
        await Assert.That(occ.TryGetEnclosingSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(5, 0, 5, 10));
    }

    [Test]
    public async Task EnclosingSourceRangeTypedMultiLine()
    {
        var occ = new Occurrence { MultiLineEnclosingRange = new MultiLineRange { StartLine = 1, StartCharacter = 0, EndLine = 9, EndCharacter = 1 } };
        await Assert.That(occ.TryGetEnclosingSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(1, 0, 9, 1));
    }

    [Test]
    public async Task EnclosingSourceRangeDeprecatedFallback()
    {
        var occ = new Occurrence();
        occ.EnclosingRange.AddRange([2, 0, 5, 1]);
        await Assert.That(occ.TryGetEnclosingSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(2, 0, 5, 1));
    }

    [Test]
    public async Task EnclosingSourceRangeTypedTakesPrecedence()
    {
        var occ = new Occurrence { SingleLineEnclosingRange = new SingleLineRange { Line = 5, StartCharacter = 0, EndCharacter = 10 } };
        occ.EnclosingRange.AddRange([100, 100, 100]);
        await Assert.That(occ.TryGetEnclosingSourceRange(out var r)).IsTrue();
        await Assert.That(r).IsEqualTo(new Range(5, 0, 5, 10));
    }

    [Test]
    public async Task EnclosingSourceRangeMissing()
    {
        await Assert.That(new Occurrence().TryGetEnclosingSourceRange(out var r)).IsFalse();
        await Assert.That(r).IsEqualTo(default(Range));
    }

    [Test]
    public async Task SetSourceRangeSingleLine()
    {
        var occ = Legacy("", 99, 99, 99);
        occ.SetSourceRange(new Range(3, 1, 3, 8));
        await Assert.That(occ.Range.Count).IsEqualTo(0);
        await Assert.That(occ.TypedRangeCase).IsEqualTo(Occurrence.TypedRangeOneofCase.SingleLineRange);
        await Assert.That(occ.SingleLineRange).IsEqualTo(new SingleLineRange { Line = 3, StartCharacter = 1, EndCharacter = 8 });
        await Assert.That(occ.GetSourceRange()).IsEqualTo(new Range(3, 1, 3, 8));
    }

    [Test]
    public async Task SetSourceRangeMultiLine()
    {
        var occ = new Occurrence();
        occ.SetSourceRange(new Range(1, 0, 4, 2));
        await Assert.That(occ.TypedRangeCase).IsEqualTo(Occurrence.TypedRangeOneofCase.MultiLineRange);
        await Assert.That(occ.MultiLineRange).IsEqualTo(new MultiLineRange { StartLine = 1, StartCharacter = 0, EndLine = 4, EndCharacter = 2 });
    }

    [Test]
    public async Task SetEnclosingSourceRange()
    {
        var occ = new Occurrence();
        occ.EnclosingRange.AddRange([99, 99, 99]);
        occ.SetEnclosingSourceRange(new Range(1, 0, 5, 1));
        await Assert.That(occ.EnclosingRange.Count).IsEqualTo(0);
        await Assert.That(occ.TypedEnclosingRangeCase).IsEqualTo(Occurrence.TypedEnclosingRangeOneofCase.MultiLineEnclosingRange);
        await Assert.That(occ.MultiLineEnclosingRange).IsEqualTo(new MultiLineRange { StartLine = 1, StartCharacter = 0, EndLine = 5, EndCharacter = 1 });
    }

    [Test]
    [Arguments(1, 0, 5, "x", 1, 0, 5, "x", 0)]
    [Arguments(0, 0, 5, "x", 1, 0, 5, "x", -1)]
    [Arguments(2, 0, 5, "x", 1, 0, 5, "x", 1)]
    [Arguments(1, 0, 5, "a", 1, 0, 5, "b", -1)]
    [Arguments(1, 0, 5, "b", 1, 0, 5, "a", 1)]
    public async Task Compare(int l1, int s1, int e1, string sym1, int l2, int s2, int e2, string sym2, int want)
    {
        var a = Legacy(sym1, l1, s1, e1);
        var b = Legacy(sym2, l2, s2, e2);
        await Assert.That(a.CompareOccurrence(b)).IsEqualTo(want);
    }

    [Test]
    public async Task Contains()
    {
        var occ = Legacy("", 2, 5, 10);
        await Assert.That(occ.Contains(new Position(2, 5))).IsTrue();
        await Assert.That(occ.Contains(new Position(2, 9))).IsTrue();
        await Assert.That(occ.Contains(new Position(2, 4))).IsFalse();
        await Assert.That(occ.Contains(new Position(2, 10))).IsFalse();
        await Assert.That(occ.Contains(new Position(3, 0))).IsFalse();
        await Assert.That(new Occurrence().Contains(new Position(0, 0))).IsFalse();
    }

    [Test]
    public async Task SymbolRoleMatches()
    {
        var occ = new Occurrence { SymbolRoles = (int)SymbolRole.Definition | (int)SymbolRole.Test };
        await Assert.That(SymbolRole.Definition.Matches(occ)).IsTrue();
        await Assert.That(SymbolRole.Test.Matches(occ)).IsTrue();
        await Assert.That(SymbolRole.Import.Matches(occ)).IsFalse();
    }

    [Test]
    public async Task SymbolTableKeysBySymbol()
    {
        var doc = new Document();
        doc.Symbols.Add(new SymbolInformation { Symbol = "a", DisplayName = "A" });
        doc.Symbols.Add(new SymbolInformation { Symbol = "b" });
        var table = doc.SymbolTable();
        await Assert.That(table.Count).IsEqualTo(2);
        await Assert.That(table["a"].DisplayName).IsEqualTo("A");
    }

    [Test]
    public async Task LocalGlobalExtensions()
    {
        await Assert.That("local 1".IsLocalSymbol()).IsTrue();
        await Assert.That("local 1".IsGlobalSymbol()).IsFalse();
        await Assert.That("scheme . . . a#".IsGlobalSymbol()).IsTrue();
    }

    [Test]
    public async Task SymbolOrderMatchesUtf8ByteOrder()
    {
        // U+FF5E (BMP, 3 UTF-8 bytes) sorts before U+1F680 (surrogate pair in UTF-16, 4 UTF-8 bytes).
        await Assert.That(SymbolOrder.Compare("～", "\U0001F680")).IsLessThan(0);
        await Assert.That(string.CompareOrdinal("～", "\U0001F680")).IsGreaterThan(0);
        await Assert.That(SymbolOrder.Compare("ab", "abc")).IsLessThan(0);
        await Assert.That(SymbolOrder.Compare("b", "a")).IsGreaterThan(0);
        await Assert.That(SymbolOrder.Compare("a", "a")).IsEqualTo(0);
    }
}
