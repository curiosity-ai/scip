namespace Scip.Tests;

public class SourceFileTests
{
    private const string TestFileContents = "Mary had a little lamb,\nIts fleece was white as snow.\nAnd everywhere that Mary went,\nThe lamb was sure to go.\n";

    [Test]
    [Arguments(0, 0, 0, 0, "")]
    [Arguments(0, 0, 0, 8, "Mary had")]
    [Arguments(0, 0, 1, 0, "Mary had a little lamb,\n")]
    [Arguments(0, 0, 1, 10, "Mary had a little lamb,\nIts fleece")]
    [Arguments(0, 0, 4, 0, TestFileContents)]
    public async Task RangeText(int sl, int sc, int el, int ec, string expected)
    {
        var file = new SourceFile("", "", TestFileContents);
        await Assert.That(file.RangeText(new Range(sl, sc, el, ec))).IsEqualTo(expected);
    }

    [Test]
    public async Task RangeTextHonoursPositionEncoding()
    {
        // "a🚀b": UTF-8 offsets a=0 🚀=1 b=5; UTF-16: a=0 🚀=1 b=3; UTF-32: a=0 🚀=1 b=2
        var file = new SourceFile("", "", "a\U0001F680b");
        await Assert.That(file.RangeText(new Range(0, 5, 0, 6))).IsEqualTo("b");
        await Assert.That(file.RangeText(new Range(0, 1, 0, 5), PositionEncoding.Utf8CodeUnitOffsetFromLineStart)).IsEqualTo("\U0001F680");
        await Assert.That(file.RangeText(new Range(0, 1, 0, 3), PositionEncoding.Utf16CodeUnitOffsetFromLineStart)).IsEqualTo("\U0001F680");
        await Assert.That(file.RangeText(new Range(0, 1, 0, 2), PositionEncoding.Utf32CodeUnitOffsetFromLineStart)).IsEqualTo("\U0001F680");
    }

    [Test]
    public async Task RangeTextRejectsBadRanges()
    {
        var file = new SourceFile("", "", "abc");
        await Assert.That(() => file.RangeText(new Range(0, 0, 0, 4))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => file.RangeText(new Range(0, 0, 3, 0))).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FromDirectoryReadsRecursively()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "sub"));
            File.WriteAllText(Path.Combine(dir.FullName, "a.txt"), "x\ny");
            File.WriteAllText(Path.Combine(dir.FullName, "sub", "b.txt"), "z");

            var files = SourceFile.FromDirectory(dir.FullName).OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToList();

            await Assert.That(files.Select(f => f.RelativePath).ToArray()).IsEquivalentTo(new[] { "a.txt", Path.Combine("sub", "b.txt") });
            await Assert.That(files[0].Lines.ToArray()).IsEquivalentTo(new[] { "x", "y" });
            await Assert.That(files[0].ToString()).Contains("\"Lines\":[\"x\",\"y\"]");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task ConvertsBetweenEncodings()
    {
        const string line = "a\U0001F680bé";
        var utf8 = PositionEncoding.Utf8CodeUnitOffsetFromLineStart;
        var utf16 = PositionEncoding.Utf16CodeUnitOffsetFromLineStart;
        var utf32 = PositionEncoding.Utf32CodeUnitOffsetFromLineStart;

        await Assert.That(PositionEncodings.Convert(line, 5, utf8, utf16)).IsEqualTo(3);
        await Assert.That(PositionEncodings.Convert(line, 5, utf8, utf32)).IsEqualTo(2);
        await Assert.That(PositionEncodings.Convert(line, 3, utf16, utf8)).IsEqualTo(5);
        await Assert.That(PositionEncodings.Convert(line, 2, utf32, utf16)).IsEqualTo(3);
        await Assert.That(PositionEncodings.Convert(line, 0, utf8, utf16)).IsEqualTo(0);
        await Assert.That(PositionEncodings.LineLength(line, utf8)).IsEqualTo(8);
        await Assert.That(PositionEncodings.LineLength(line, utf16)).IsEqualTo(5);
        await Assert.That(PositionEncodings.LineLength(line, utf32)).IsEqualTo(4);

        // End of line is valid; inside a character, past the end, and negative are not.
        await Assert.That(PositionEncodings.TryConvert(line, 8, utf8, utf16, out var end)).IsTrue();
        await Assert.That(end).IsEqualTo(5);
        await Assert.That(PositionEncodings.TryConvert(line, 3, utf8, utf16, out _)).IsFalse();
        await Assert.That(PositionEncodings.TryConvert(line, 9, utf8, utf16, out _)).IsFalse();
        await Assert.That(PositionEncodings.TryConvert(line, -1, utf8, utf16, out _)).IsFalse();
        await Assert.That(() => PositionEncodings.Convert(line, 2, PositionEncoding.UnspecifiedPositionEncoding, utf16))
            .Throws<ArgumentException>();
    }
}
