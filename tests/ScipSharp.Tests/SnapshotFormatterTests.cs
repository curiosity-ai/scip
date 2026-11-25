namespace ScipSharp.Tests;

public class SnapshotFormatterTests
{
    /// <summary>Port of Go <c>TestFormatSnapshotFeatures</c>: enclosing_symbol, multiline and synthetic definitions, zero-width ranges.</summary>
    [Test]
    public async Task FormatSnapshotFeatures()
    {
        const string source = "package foo\nfunc bar() {\n  return\n}\n";
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var sourcePath = Path.Combine(dir.FullName, "test.go");
            File.WriteAllText(sourcePath, source);

            Occurrence Def(string symbol, Range r)
            {
                var o = new Occurrence { Symbol = symbol, SymbolRoles = (int)SymbolRole.Definition };
                o.SetSourceRange(r);
                return o;
            }

            var reference = new Occurrence { Symbol = "local bar" };
            reference.SetSourceRange(new Range(1, 5, 3, 1));

            var document = new Document { RelativePath = "test.go" };
            document.Occurrences.AddRange([Def("local foo", new Range(0, 8, 0, 11)), reference, Def("local zero", new Range(2, 2, 2, 2))]);
            document.Symbols.Add(new SymbolInformation
            {
                Symbol = "local foo",
                Kind = SymbolInformation.Types.Kind.Namespace,
                DisplayName = "foo",
                EnclosingSymbol = "local root",
            });
            document.Symbols.Add(new SymbolInformation
            {
                Symbol = "local child",
                Kind = SymbolInformation.Types.Kind.Field,
                DisplayName = "child",
                Relationships = { new Relationship { Symbol = "local foo", IsDefinition = true } },
            });

            var result = SnapshotFormatter.FormatSnapshot(document, "//", SymbolFormatter.DescriptorOnly, sourcePath);

            await Assert.That(result.Errors.Count).IsEqualTo(0);
            var snapshot = result.Text;
            await Assert.That(snapshot).Contains("//        ^^^ definition local foo");
            await Assert.That(snapshot).Contains("enclosing_symbol local root");
            await Assert.That(snapshot).Contains("kind Namespace");
            await Assert.That(snapshot).Contains("//        ___ synthetic_definition local child");
            await Assert.That(snapshot).Contains("relationship local foo definition");
            await Assert.That(snapshot).Contains("//     ^^^^^^^ reference local bar 2:1");
            await Assert.That(snapshot).Contains("//  ^ definition local zero");
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task UnparseableSymbolIsEmittedVerbatimWithError()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var sourcePath = Path.Combine(dir.FullName, "t.txt");
            File.WriteAllText(sourcePath, "abc");
            var document = new Document { RelativePath = "t.txt" };
            var occ = new Occurrence { Symbol = "not a symbol" };
            occ.SetSourceRange(new Range(0, 0, 0, 3));
            document.Occurrences.Add(occ);

            var result = SnapshotFormatter.FormatSnapshot(document, "#", SymbolFormatter.Verbose, sourcePath);

            await Assert.That(result.Errors.Count).IsEqualTo(1);
            await Assert.That(result.Text).Contains("^^^ reference not a symbol");
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
