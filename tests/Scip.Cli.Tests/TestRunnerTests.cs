using Scip.Testing;
using static Scip.Cli.Tests.TestHelpers;

namespace Scip.Cli.Tests;

[NotInParallel]
public class TestRunnerTests
{
    [Test]
    public async Task TestCasesForLine()
    {
        var (actual, linesUsed) = TestRunner.TestCasesForLine(
            2,
            [
                "void main() {",
                " //   ^ definition <skipped line>",
                "  print(foo)",
                "  //    ^^^ reference <with enforce length>",
                "  //     ^ reference <single line>",
                "        // <- reference <start of comment>",
                "  //    ^ diagnostic Warning",
                "  //    > with multiline",
                "  //    > additional data",
                "  final test = 4",
                "         ^ definition <skipped line>",
                "}",
            ],
            "//");

        await Assert.That(actual.Count).IsEqualTo(4);
        await Assert.That(linesUsed).IsEqualTo(6);

        await Assert.That(actual[0].EnforceLength).IsTrue();
        await Assert.That(actual[0].Attribute.Length).IsEqualTo(3);
        await Assert.That(actual[1].EnforceLength).IsFalse();
        await Assert.That(actual[2].EnforceLength).IsFalse();
        await Assert.That(actual[3].EnforceLength).IsFalse();

        await Assert.That(actual[0].Attribute.Start).IsEqualTo(8);
        await Assert.That(actual[1].Attribute.Start).IsEqualTo(9);
        await Assert.That(actual[2].Attribute.Start).IsEqualTo(8);
        await Assert.That(actual[3].Attribute.Start).IsEqualTo(8);

        await Assert.That(actual[0].Attribute.Kind).IsEqualTo(TestRunner.AttrKind.Reference);
        await Assert.That(actual[1].Attribute.Kind).IsEqualTo(TestRunner.AttrKind.Reference);
        await Assert.That(actual[2].Attribute.Kind).IsEqualTo(TestRunner.AttrKind.Reference);
        await Assert.That(actual[3].Attribute.Kind).IsEqualTo(TestRunner.AttrKind.Diagnostic);

        await Assert.That(actual[3].Attribute.AdditionalData).IsEquivalentTo(new[] { "with multiline", "additional data" });
    }

    [Test]
    public async Task ParseTestCaseSyntheticDefinition()
    {
        var ok = TestRunner.TryParseTestCase("//   ___ synthetic_definition local child", [], "//", out var tc);
        await Assert.That(ok).IsTrue();
        await Assert.That(tc.Attribute.Kind).IsEqualTo(TestRunner.AttrKind.SyntheticDefinition);
        await Assert.That(tc.Attribute.Data).IsEqualTo("local child");
        await Assert.That(tc.EnforceLength).IsTrue();
        // Only the contiguous run of 3 underscores counts, not the '_' inside the kind.
        await Assert.That(tc.Attribute.Length).IsEqualTo(3);
        await Assert.That(tc.Attribute.Start).IsEqualTo(5);
        await Assert.That(tc.Attribute.HasEndPosition).IsFalse();
    }

    [Test]
    public async Task ParseTestCaseMultilineSuffix()
    {
        var ok = TestRunner.TryParseTestCase("// ^^^^^ reference local bar 2:1", [], "//", out var tc);
        await Assert.That(ok).IsTrue();
        await Assert.That(tc.Attribute.Kind).IsEqualTo(TestRunner.AttrKind.Reference);
        await Assert.That(tc.Attribute.Data).IsEqualTo("local bar");
        await Assert.That(tc.Attribute.HasEndPosition).IsTrue();
        await Assert.That(tc.Attribute.EndLineDelta).IsEqualTo(2);
        await Assert.That(tc.Attribute.EndCharacter).IsEqualTo(1);
    }

    [Test]
    public async Task ParseTestCaseUnderscoreRejectsNonSynthetic()
    {
        await Assert.That(() => TestRunner.TryParseTestCase("// ___ reference local child", [], "//", out _))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("//            kind Namespace")]
    [Arguments("//            display_name foo")]
    [Arguments("//            enclosing_symbol local root")]
    [Arguments("//            relationship local foo definition")]
    [Arguments("//            > documentation line")]
    public async Task ParseTestCaseIgnoresMetadataLines(string line)
    {
        await Assert.That(TestRunner.TryParseTestCase(line, [], "//", out _)).IsFalse();
    }

    [Test]
    public async Task CheckSymbolPartCountMismatch()
    {
        var tc = new TestRunner.TestCase(
            new TestRunner.SymbolAttribute
            {
                Kind = TestRunner.AttrKind.Definition,
                Start = 0,
                Length = 3,
                Data = "scheme manager name 1.0.0 Foo#",
            },
            enforceLength: true);
        var attr = new TestRunner.SymbolAttribute { Kind = TestRunner.AttrKind.Definition, Start = 0, Length = 3, Data = "local 0" };
        // Must not throw, and must not match (different number of symbol parts).
        await Assert.That(tc.Check(attr)).IsFalse();
    }

    [Test]
    public async Task RunTestsSyntheticAndMultiline()
    {
        var index = new Index();
        var doc = new Document { RelativePath = "test.txt" };
        doc.Occurrences.Add(Occ("local foo", new Range(0, 3, 0, 6), SymbolRole.Definition));
        doc.Occurrences.Add(Occ("local bar", new Range(3, 3, 5, 1)));
        doc.Symbols.Add(new SymbolInformation { Symbol = "local foo" });
        doc.Symbols.Add(new SymbolInformation
        {
            Symbol = "local child",
            Relationships = { new Relationship { Symbol = "local foo", IsDefinition = true } },
        });
        index.Documents.Add(doc);

        var passContent = string.Join('\n',
        [
            "   foo",
            "// ^^^ definition local foo",
            "// ___ synthetic_definition local child",
            "   barbar",
            "// ^^^^^^ reference local bar 2:1",
            "mid",
            "x",
        ]);

        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var testFile = Path.Combine(dir.FullName, "test.txt");
            await File.WriteAllTextAsync(testFile, passContent);

            var passOut = new StringWriter();
            TestRunner.Run(dir.FullName, [], false, index, "//", passOut);
            await Assert.That(passOut.ToString()).Contains("✓ test.txt (3 assertions)");

            // A wrong multiline end position must fail and report the actual suffix.
            await File.WriteAllTextAsync(testFile, passContent.Replace("reference local bar 2:1", "reference local bar 2:2", StringComparison.Ordinal));
            var failOut = new StringWriter();
            await Assert.That(() => TestRunner.Run(dir.FullName, [], false, index, "//", failOut))
                .Throws<TestRunner.TestFailedException>();
            await Assert.That(failOut.ToString()).Contains("reference local bar 2:2").And.Contains("reference local bar 2:1");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task CheckDocumentsReportsMissingFiles()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "a.txt"), "x\n");
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "extra.txt"), "y\n");
            var index = new Index { Documents = { new Document { RelativePath = "a.txt" } } };

            var output = new StringWriter();
            TestRunner.Run(dir.FullName, [], false, index, "//", output); // not checking documents: passes
            var strict = new StringWriter();
            await Assert.That(() => TestRunner.Run(dir.FullName, [], true, index, "//", strict))
                .Throws<TestRunner.TestFailedException>();
            await Assert.That(strict.ToString()).Contains("Missing documents in SCIP index").And.Contains("extra.txt");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task TestCommandEndToEnd()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "t.txt"), "foo\n// <- definition local foo\n");
            var doc = new Document { RelativePath = "t.txt" };
            doc.Occurrences.Add(Occ("local foo", new Range(0, 0, 0, 3), SymbolRole.Definition));
            var indexPath = WriteIndex(dir, new Index { Documents = { doc } });

            var (code, output, _) = await RunCli("test", "--from", indexPath, dir.FullName);
            await Assert.That(code).IsEqualTo(0);
            await Assert.That(output).Contains("t.txt (1 assertions)");

            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "t.txt"), "foo\n// <- reference local foo\n");
            var (failCode, failOutput, _) = await RunCli("test", "--from", indexPath, dir.FullName);
            await Assert.That(failCode).IsEqualTo(1);
            await Assert.That(failOutput).Contains("Failure [Ln: 1, Col: 0]");
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
