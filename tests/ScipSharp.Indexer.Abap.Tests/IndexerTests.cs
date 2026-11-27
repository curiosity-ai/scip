using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Abap.Tests;

/// <summary>Indexes the abapGit fixture under testdata/basic and checks the output against golden snapshots.</summary>
public class IndexerTests
{
    private const string Package = "scip-abap abap basic . ";

    private static string Project => Path.Combine(AppContext.BaseDirectory, "testdata", "basic");

    /// <summary>The goldens live in the source tree; set UPDATE_SNAPSHOTS=1 to rewrite them there.</summary>
    private static string SourceGoldens([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.Combine(Path.GetDirectoryName(path)!, "testdata", "basic-snapshots");

    private static readonly Lazy<Index> Built = new(() => Build(Project));

    internal static Index Build(string root, AbapIndexerSettings? settings = null)
    {
        var indexer = new AbapProjectIndexer(root, settings ?? new AbapIndexerSettings(), _ => { });
        var index = new Index { Metadata = indexer.CreateMetadata() };
        index.ExternalSymbols.AddRange(indexer.Run(index.Documents.Add));
        return index;
    }

    /// <summary>The documents with a golden snapshot (XML files without occurrences are not emitted).</summary>
    public static IEnumerable<string> Documents() =>
        Directory.EnumerateFiles(Path.Combine(Project + "-snapshots", "src")).Select(f => "src/" + Path.GetFileName(f)).Order(StringComparer.Ordinal);

    [Test]
    [MethodDataSource(nameof(Documents))]
    public async Task MatchesGoldenSnapshot(string relativePath)
    {
        var doc = Built.Value.Documents.Single(d => d.RelativePath == relativePath).Clone();
        var actual = SnapshotFormatter.FormatSnapshot(doc, "*", SymbolFormatter.Verbose, Path.Combine(Project, relativePath));
        await Assert.That(actual.Errors).IsEmpty();
        var golden = Path.Combine(SourceGoldens(), relativePath);
        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(golden)!);
            await File.WriteAllTextAsync(golden, actual.Text);
        }

        var expected = await File.ReadAllTextAsync(Path.Combine(Project + "-snapshots", relativePath));
        await Assert.That(actual.Text).IsEqualTo(expected);
    }

    [Test]
    public async Task ResolvesReferencesAcrossObjects()
    {
        var occurrences = Built.Value.Documents.SelectMany(d => d.Occurrences.Select(o => (d.RelativePath, o.Symbol, o.SymbolRoles))).ToList();

        // a form defined in an include, called from the report
        await Assert.That(occurrences).Contains(("src/zshapes.prog.abap", Package + "ZSHAPES_FORMS/PRINT().", 0));

        // an inherited attribute, written in the subclass
        await Assert.That(occurrences.Any(o => o.RelativePath == "src/zcl_circle.clas.abap" && o.Symbol == Package + "ZCL_BASE#MV_NAME."
            && (o.SymbolRoles & (int)SymbolRole.WriteAccess) != 0)).IsTrue();

        // a function module called by name, and its parameter used in its body
        await Assert.That(occurrences).Contains(("src/zshapes.prog.abap", Package + "Z_SHAPE_LOG().", 0));
        await Assert.That(occurrences.Any(o => o.Symbol == Package + "Z_SHAPE_LOG().(IV_TEXT)")).IsTrue();

        // the function group's global data belongs to its main program SAPLZSHAPES, not to the report ZSHAPES
        await Assert.That(occurrences).Contains(("src/zshapes.fugr.z_shape_log.abap", Package + "SAPLZSHAPES/GT_LOG.", (int)SymbolRole.WriteAccess));

        // a dictionary structure used as a type
        await Assert.That(occurrences).Contains(("src/zshapes.prog.abap", Package + "ZSHAPE_ROW#", 0));
    }

    [Test]
    public async Task DescribesInheritanceAndRedefinitions()
    {
        var symbols = Built.Value.Documents.SelectMany(d => d.Symbols).ToDictionary(s => s.Symbol, StringComparer.Ordinal);
        var circle = symbols[Package + "ZCL_CIRCLE#"];
        await Assert.That(circle.Kind).IsEqualTo(SymbolInformation.Types.Kind.Class);
        await Assert.That(circle.Relationships.Select(r => r.Symbol)).Contains(Package + "ZCL_BASE#");
        await Assert.That(symbols[Package + "ZCL_BASE#"].Relationships.Select(r => r.Symbol)).Contains(Package + "ZIF_SHAPE#");
        await Assert.That(symbols[Package + "ZCL_CIRCLE#CONSTRUCTOR()."].Documentation[0]).Contains("\"! Creates a circle");
    }

    [Test]
    public async Task VoidReferencesBecomeExternalSymbols()
    {
        var external = Built.Value.ExternalSymbols.Select(s => s.Symbol).ToList();
        await Assert.That(external).Contains("scip-abap abap . . CL_SALV_TABLE#");
        await Assert.That(external.All(s => s.StartsWith("scip-abap abap . . ", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MetadataAndDocuments()
    {
        var index = Built.Value;
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-abap");
        await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
        foreach (var d in index.Documents)
        {
            await Assert.That(d.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
            await Assert.That(d.Language).IsEqualTo(d.RelativePath.EndsWith(".xml", StringComparison.Ordinal) ? "xml" : "abap");
        }
    }

    [Test]
    public async Task PackageNameAndVersionCanBeSet()
    {
        var index = Build(Project, new AbapIndexerSettings(PackageName: "shapes", PackageVersion: "1.0"));
        await Assert.That(index.Documents.SelectMany(d => d.Symbols).Any(s => s.Symbol == "scip-abap abap shapes 1.0 ZCL_CIRCLE#")).IsTrue();
    }

    [Test]
    public async Task UnknownReleaseIsAnError()
    {
        await Assert.That(() => Build(Project, new AbapIndexerSettings(Release: "v999"))).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task IndexerWritesAParsableScipFile()
    {
        var output = Path.Combine(Path.GetTempPath(), $"scip-abap-{Guid.NewGuid():N}.scip");
        try
        {
            var code = await new AbapIndexer().IndexAsync(new IndexerOptions(Project, output, ["--release=v740sp08"]), CancellationToken.None);
            await Assert.That(code).IsEqualTo(0);
            var parsed = Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output));
            await Assert.That(parsed.Documents.Count).IsEqualTo(Built.Value.Documents.Count);
            await Assert.That(parsed.Metadata.ToolInfo.Arguments).Contains("--release=v740sp08");
        }
        finally
        {
            File.Delete(output);
        }
    }
}
