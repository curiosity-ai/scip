using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using ScipSharp.Cli.Linting;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Pascal.Tests;

/// <summary>Project discovery: Lazarus packages/projects, search paths, include files, encodings, CLI options.</summary>
public class ProjectTests
{
    [Test]
    public async Task LazarusPackageNamesAndVersionsSymbols()
    {
        using var p = new TestProject()
            .Add("pkg/mylib.lpk", """
                <?xml version="1.0" encoding="UTF-8"?>
                <CONFIG>
                  <Package Version="5">
                    <Name Value="MyLib"/>
                    <Version Major="1" Minor="4" Release="2"/>
                    <Files>
                      <Item><Filename Value="../src/mylib.pas"/><UnitName Value="MyLib"/></Item>
                    </Files>
                    <CompilerOptions><SearchPaths><OtherUnitFiles Value="../src"/></SearchPaths></CompilerOptions>
                  </Package>
                </CONFIG>
                """)
            .Add("src/mylib.pas", "unit MyLib;\ninterface\nconst Answer = 42;\nimplementation\nend.\n");
        var index = p.Index();
        var doc = index.Documents.Single();
        await Assert.That(doc.RelativePath).IsEqualTo("src/mylib.pas");
        await Assert.That(doc.Occurrences.Select(static o => o.Symbol)).Contains("scip-pascal lazarus MyLib 1.4.2 MyLib/Answer.");
    }

    [Test]
    public async Task SearchPathUnitsResolveToExternalSymbols()
    {
        using var lib = new TestProject().Add("vendor/util.pas", """
            unit Util;
            interface
            { Adds one. }
            function Inc1(x: integer): integer;
            implementation
            function Inc1(x: integer): integer; begin Inc1 := x + 1 end;
            end.
            """);
        using var p = new TestProject().Add("main.pas", "program Main;\nuses Util;\nbegin\n  writeln(Inc1(1))\nend.\n");
        var index = p.Index($"--search-path={Path.Combine(lib.Root, "vendor")}");
        await Assert.That(index.Documents.Select(static d => d.RelativePath)).IsEquivalentTo(["main.pas"]);
        var inc = index.ExternalSymbols.Single(static s => s.Symbol.EndsWith("Util/Inc1().", StringComparison.Ordinal));
        await Assert.That(inc.Symbol).IsEqualTo("scip-pascal . vendor . Util/Inc1().");
        await Assert.That(inc.Documentation.Single()).IsEqualTo("Adds one.");
        await Assert.That(IndexLinter.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task IncludeFilesGetTheirOwnDocuments()
    {
        using var p = new TestProject()
            .Add("u.pas", "unit U;\ninterface\n{$I decls.inc}\nimplementation\n{$I inc/body.inc}\nend.\n")
            .Add("decls.inc", "procedure Hello;\n")
            .Add("inc/body.inc", "procedure Hello;\nbegin\nend;\n");
        var index = p.Index();
        await Assert.That(index.Documents.Select(static d => d.RelativePath)).IsEquivalentTo(["decls.inc", "inc/body.inc", "u.pas"]);
        var decl = index.Documents.Single(static d => d.RelativePath == "decls.inc").Occurrences.Single();
        var body = index.Documents.Single(static d => d.RelativePath == "inc/body.inc").Occurrences.Single();
        await Assert.That(decl.Symbol).IsEqualTo(body.Symbol);
        await Assert.That(decl.HasRole(SymbolRole.ForwardDefinition)).IsTrue();
        await Assert.That(body.HasRole(SymbolRole.Definition)).IsTrue();
        await Assert.That(IndexLinter.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task DefinesSelectConditionalCode()
    {
        const string src = "program P;\nvar a, b: integer;\nbegin\n{$IFDEF FAST} a := 1 {$ELSE} b := 1 {$ENDIF}\nend.\n";
        using var p = new TestProject().Add("p.pas", src);
        var plain = p.Index().Documents.Single();
        await Assert.That(plain.SymbolsAt(src, "b")).Count().IsEqualTo(2);
        await Assert.That(plain.SymbolsAt(src, "a")).Count().IsEqualTo(1);
        var fast = p.Index("--define=FAST").Documents.Single();
        await Assert.That(fast.SymbolsAt(src, "a")).Count().IsEqualTo(2);
        await Assert.That(fast.SymbolsAt(src, "b")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task LegacyEncodedFilesUseUtf16Positions()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        const string src = "program P;\n{ Grüße }\nvar s: string;\nbegin s := 'été'; writeln(s) end.\n";
        using var p = new TestProject().Add("p.pas", Encoding.GetEncoding(850).GetBytes(src));
        var doc = p.Index("--encoding=cp850").Documents.Single();
        await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
        await Assert.That(doc.SymbolsAt(src, "s")).Count().IsEqualTo(3);
    }

    [Test]
    public async Task Utf8WithBomUsesByteOffsets()
    {
        const string src = "program P; var äö: integer; x: integer; begin x := 1 end.\n";
        using var p = new TestProject().Add("p.pas", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(src)]);
        var doc = p.Index().Documents.Single();
        await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf8CodeUnitOffsetFromLineStart);
        var x = doc.Occurrences.First(static o => o.Symbol.EndsWith("P/x.", StringComparison.Ordinal));
        await Assert.That(x.Column()).IsEqualTo(Encoding.UTF8.GetByteCount(src[..src.IndexOf("x:", StringComparison.Ordinal)]));
    }

    [Test]
    public async Task ParsesArguments()
    {
        var o = PascalIndexer.ParseArguments("/tmp", ["--search-path=a;b", "-Fuc", "--define=X", "-dY", "--mode", "tp", "--package-name=n", "--package-version=1", "--no-forms"])!;
        await Assert.That(o.SearchPaths.Select(static s => Path.GetFileName(s)!)).IsEquivalentTo(["a", "b", "c"]);
        await Assert.That(o.Defines).IsEquivalentTo(["X", "Y"]);
        await Assert.That(o.Mode).IsEqualTo(Syntax.PascalMode.Tp);
        await Assert.That(o.PackageName).IsEqualTo("n");
        await Assert.That(o.IndexForms).IsFalse();
        await Assert.That(PascalIndexer.ParseArguments("/tmp", ["--help"])).IsNull();
        await Assert.That(() => PascalIndexer.ParseArguments("/tmp", ["--bogus"])).Throws<ArgumentException>();
        await Assert.That(() => PascalIndexer.ParseArguments("/tmp", ["--mode=cobol"])).Throws<ArgumentException>();
    }

    [Test]
    public async Task IndexAsyncWritesAParsableIndex()
    {
        using var p = new TestProject().Add("hello.pas", "program Hello;\nbegin\n  writeln('hi')\nend.\n");
        var output = Path.Combine(p.Root, "index.scip");
        var code = await new PascalIndexer().IndexAsync(new IndexerOptions(p.Root, output, []), CancellationToken.None);
        await Assert.That(code).IsEqualTo(0);
        var index = Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output));
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-pascal");
        await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
        await Assert.That(index.Documents.Single().Language).IsEqualTo("pascal");
        await Assert.That(index.ExternalSymbols.Select(static s => s.Symbol)).Contains("scip-pascal fpc rtl . System/WriteLn().");
        await Assert.That(await new PascalIndexer().IndexAsync(new IndexerOptions(p.Root, output, ["--help"]), CancellationToken.None)).IsEqualTo(0);
        await Assert.That(await new PascalIndexer().IndexAsync(new IndexerOptions(p.Root, output, ["--nope"]), CancellationToken.None)).IsEqualTo(1);
    }

    /// <summary>
    /// Cross-check: the portable objfpc fixture is real Free Pascal (compiled by fpc when it is installed). The ISO and
    /// Turbo fixtures use 16-bit/ISO-only features (segment:offset absolute, Intel asm, conformant arrays) on purpose.
    /// </summary>
    [Test]
    [Arguments("lazarus/collections.pas", "-Mobjfpc")]
    public async Task FixturesCompileWithFreePascal(string file, string mode)
    {
        var fpc = FindOnPath("fpc");
        if (fpc is null)
        {
            Skip.Test("fpc is not installed");
            return;
        }

        var dir = Path.Combine(SnapshotDir(), Path.GetDirectoryName(file)!);
        var outDir = Directory.CreateTempSubdirectory("scip-pascal-fpc-");
        try
        {
            var psi = new ProcessStartInfo(fpc, ["-s", mode, $"-FU{outDir.FullName}", $"-Fu{dir}", $"-Fi{dir}", Path.Combine(SnapshotDir(), file)])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi)!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            await Assert.That(proc.ExitCode).IsEqualTo(0).Because(output);
        }
        finally
        {
            outDir.Delete(recursive: true);
        }
    }

    private static string SnapshotDir([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path)!, "testdata", "snapshots", "input");

    private static string? FindOnPath(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(d => Path.Combine(d, exe))
            .FirstOrDefault(File.Exists);
}
