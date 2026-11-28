using static ScipSharp.Indexer.Pli.Tests.TestHelpers;

namespace ScipSharp.Indexer.Pli.Tests;

/// <summary>Pathological inputs: must finish quickly and must not overflow the stack.</summary>
public class RobustnessTests
{
    [Test]
    public async Task DiamondOfIncludesIsNotExpandedExponentially()
    {
        // Each member includes the two members of the next level: 2^24 expansions without a cap.
        const int levels = 24;
        var files = new Dictionary<string, string>();
        for (var k = 0; k < levels; k++)
        {
            var body = k + 1 < levels ? $"%INCLUDE A{k + 1}; %INCLUDE B{k + 1};\n" : "DCL LEAF FIXED;\n";
            files[$"A{k}.inc"] = $"DCL X{k} FIXED;\n" + body;
            files[$"B{k}.inc"] = $"DCL Y{k} FIXED;\n" + body;
        }

        files["main.pli"] = "M: PROC;\n%INCLUDE A0;\nLEAF = 1;\nEND M;\n";
        var index = IndexFiles(files, "--margins=none", "--package-name=t");
        var main = index.Documents.Single(d => d.RelativePath == "main.pli");
        await Assert.That(main.Occurrences.Any(o => o.Symbol == "scip-pli pli t . M().LEAF.")).IsTrue();
    }

    [Test]
    public async Task DeeplyNestedParenthesesDoNotOverflowTheStack()
    {
        const int depth = 10000;
        var nested = new string('(', depth) + "A" + new string(')', depth);
        var src = $"P: PROC; DCL {nested} FIXED; DCL B FIXED; B = {nested}; CALL F({nested}); B = 1; END;";
        var index = IndexSource(src, "--package-name=t");
        await Assert.That(SymbolsAt(index, src, "B").Count).IsEqualTo(3);
    }
}
