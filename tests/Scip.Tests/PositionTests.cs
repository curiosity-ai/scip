namespace Scip.Tests;

public class PositionTests
{
    // Exhaustive stand-in for the Go rapid property tests: every well-formed range over a small grid.
    private static readonly Position[] Grid =
        [.. from l in Enumerable.Range(0, 4) from c in Enumerable.Range(0, 4) select new Position(l, c)];

    private static IEnumerable<Range> AllRanges() =>
        from a in Grid from b in Grid where a.CompareTo(b) <= 0 select new Range(a, b);

    [Test]
    public async Task ComparePositionIsTotalOrder()
    {
        Position[] ordered = [new(0, 0), new(0, 1), new(1, 0), new(1, 1), new(1, 2), new(2, 1), new(2, 2)];
        for (var i = 0; i < ordered.Length; i++)
        {
            for (var j = 0; j < ordered.Length; j++)
            {
                await Assert.That(ordered[i].CompareTo(ordered[j])).IsEqualTo(Math.Sign(i.CompareTo(j)));
            }
        }
    }

    [Test]
    public async Task ComparePositionIsTransitive()
    {
        foreach (var p1 in Grid)
        {
            foreach (var p2 in Grid)
            {
                foreach (var p3 in Grid)
                {
                    if (p1.CompareTo(p2) < 0 && p2.CompareTo(p3) < 0)
                    {
                        await Assert.That(p1.CompareTo(p3)).IsLessThan(0);
                    }
                }
            }
        }
    }

    [Test]
    public async Task IntersectUnit()
    {
        var r1 = new Range(0, 0, 0, 10);
        var r2 = new Range(0, 5, 1, 5);
        var r3 = new Range(1, 0, 1, 10);
        await Assert.That(r1.Intersects(r2)).IsTrue();
        await Assert.That(r2.Intersects(r3)).IsTrue();
        await Assert.That(r1.Intersects(r3)).IsFalse();
    }

    [Test]
    public async Task IntersectsImpliesContainsOtherStart()
    {
        var all = AllRanges().ToList();
        foreach (var r1 in all)
        {
            foreach (var r2 in all.Where(r1.Intersects))
            {
                await Assert.That(r1.Contains(r2.Start) || r2.Contains(r1.Start)).IsTrue();
            }
        }
    }

    [Test]
    public async Task LessIsConsistentWithCompare()
    {
        var all = AllRanges().ToList();
        foreach (var r1 in all)
        {
            foreach (var r2 in all)
            {
                await Assert.That(r1.Less(r2)).IsEqualTo(r1.Compare(r2) < 0);
                await Assert.That(r1.LessStrict(r2)).IsEqualTo(r1.CompareStrict(r2) < 0);
                await Assert.That(r1.Compare(r2)).IsEqualTo(-r2.Compare(r1));
            }
        }
    }

    [Test]
    [Arguments(new int[] { }, null, "incorrect length")]
    [Arguments(new[] { 3 }, null, "incorrect length")]
    [Arguments(new[] { 3, 3 }, null, "incorrect length")]
    [Arguments(new[] { 12, 0, 14 }, "12:0-12:14", null)]
    [Arguments(new[] { 12, 14, 0 }, null, "end before start")]
    [Arguments(new[] { 12, 15, 14, 0 }, "12:15-14:0", null)]
    [Arguments(new[] { 12, 0, 12, 0 }, "12:0-12:0", null)]
    [Arguments(new[] { 12, 0, 13, 1, 4 }, null, "incorrect length")]
    [Arguments(new[] { 3, -1, 4 }, null, "negative offsets")]
    public async Task NewRange(int[] input, string? expectedRange, string? expectedError)
    {
        var ok = Range.TryCreate(input, out var range, out var error);
        await Assert.That(ok).IsEqualTo(expectedError is null);
        if (ok)
        {
            await Assert.That(range.ToString()).IsEqualTo(expectedRange);
        }
        else
        {
            await Assert.That(range).IsEqualTo(default(Range));
            await Assert.That(Range.Describe(error)).IsEqualTo(expectedError);
            await Assert.That(() => Range.Create(input)).Throws<RangeException>();
        }
    }

    [Test]
    public async Task NewRangeAgreesWithUnchecked()
    {
        // All slices of length 0..4 over -2..4 (the Go test draws random ones from -2..10).
        var values = Enumerable.Range(-2, 7).ToArray();
        IEnumerable<int[]> Slices(int length) => length == 0
            ? [[]]
            : Slices(length - 1).SelectMany(prefix => values.Select(v => (int[])[.. prefix, v]));

        for (var length = 0; length <= 4; length++)
        {
            foreach (var input in Slices(length))
            {
                if (Range.TryCreate(input, out var r1, out _))
                {
                    await Assert.That(r1.CompareStrict(Range.CreateUnchecked(input))).IsEqualTo(0);
                }
            }
        }
    }

    [Test]
    public async Task ValidateAcceptsWellFormed()
    {
        await Assert.That(new Range(0, 0, 0, 0).Validate()).IsNull();
        await Assert.That(new Range(1, 2, 1, 2).Validate()).IsNull();
        await Assert.That(new Range(1, 2, 3, 4).Validate()).IsNull();
    }

    [Test]
    public async Task ValidateRejectsMalformed()
    {
        await Assert.That(new Range(-1, 0, 0, 0).Validate()).IsEqualTo(RangeError.NegativeOffsets);
        await Assert.That(new Range(0, 0, 0, -1).Validate()).IsEqualTo(RangeError.NegativeOffsets);
        await Assert.That(new Range(3, 0, 1, 0).Validate()).IsEqualTo(RangeError.EndBeforeStart);
        await Assert.That(new Range(1, 5, 1, 2).Validate()).IsEqualTo(RangeError.EndBeforeStart);
    }

    [Test]
    public async Task ScipRangeRoundTrips()
    {
        await Assert.That(new Range(1, 2, 1, 5).ToScipRange()).IsEquivalentTo(new[] { 1, 2, 5 });
        await Assert.That(new Range(1, 2, 3, 5).ToScipRange()).IsEquivalentTo(new[] { 1, 2, 3, 5 });
        await Assert.That(new Range(1, 2, 3, 5).IsSingleLine).IsFalse();
    }
}
