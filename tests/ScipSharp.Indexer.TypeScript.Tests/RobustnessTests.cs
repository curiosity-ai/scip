using System.Diagnostics;
using ScipSharp.Indexer.TypeScript.Syntax;

namespace ScipSharp.Indexer.TypeScript.Tests;

/// <summary>Pathological inputs that used to hang the indexer, take exponential time or overflow the stack.</summary>
public class RobustnessTests
{
    private static string Repeat(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    private static async Task<(int Exit, Index Index, string Log)> IndexAsync(string dir)
    {
        var output = Path.Combine(dir, "index.scip");
        var options = new TypeScriptOptions { Cwd = dir, Output = output, InferTsconfig = true };
        var log = new StringWriter();
        var exit = await new TypeScriptIndexer().IndexAsync(options, log, log, CancellationToken.None);
        var index = exit == 0 ? Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output)) : new Index();
        return (exit, index, log.ToString());
    }

    private static async Task<(int Exit, Index Index, string Log)> IndexSourceAsync(string source)
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-ts-robust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "main.ts"), source + "\nexport const after = 1;\n");
            return await IndexAsync(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task AssertAfterIsDefined((int Exit, Index Index, string Log) result)
    {
        await Assert.That(result.Exit).IsEqualTo(0).Because(result.Log);
        await Assert.That(result.Log).DoesNotContain("unexpected error").Because(result.Log);
        var doc = result.Index.Documents.Single();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("/after.", StringComparison.Ordinal) && (o.SymbolRoles & 1) != 0)).IsTrue();
    }

    [Test]
    [Arguments("plus")]
    [Arguments("calls")]
    [Arguments("promise")]
    public async Task LongLeftAssociativeChainsAreIndexed(string kind)
    {
        // The parser builds these with loops, but the tree is as deep as the chain: tree walks must not recurse.
        var source = kind switch
        {
            "plus" => "export const x = " + string.Join(" + ", Enumerable.Range(0, 20000).Select(i => "a" + i)) + ";",
            "calls" => "declare const b: any;\nexport const x = b" + Repeat(".m()", 10000) + ";",
            _ => "declare const p: Promise<number>;\nexport const x = p" + Repeat(".then(v => v)", 5000) + ";",
        };
        await AssertAfterIsDefined(await IndexSourceAsync(source));
    }

    [Test]
    [Arguments("parens")]
    [Arguments("arrays")]
    [Arguments("callbacks")]
    [Arguments("ternary")]
    [Arguments("templates")]
    [Arguments("types")]
    public async Task DeepNestingSkipsTheStatementInsteadOfOverflowing(string kind)
    {
        const int n = 100_000;
        var source = kind switch
        {
            "parens" => "export const x = " + Repeat("(", n) + "1" + Repeat(")", n) + ";",
            "arrays" => "export const x = " + Repeat("[", n) + "1" + Repeat("]", n) + ";",
            "callbacks" => "export const x = " + Repeat("f(() => ", n) + "1" + Repeat(")", n) + ";",
            "ternary" => "export const x = " + Repeat("c ? 1 : ", n) + "0;",
            "templates" => "export const x = " + Repeat("`${", n) + "1" + Repeat("}`", n) + ";",
            _ => "export type X = " + Repeat("Array<", n) + "string" + Repeat(">", n) + ";",
        };
        await AssertAfterIsDefined(await IndexSourceAsync(source));
    }

    [Test]
    public async Task UnterminatedNestedTemplatesAreSkipped()
    {
        // Error recovery skips the raw template text; the skipper used to recurse once per nesting level.
        var (exit, index, log) = await IndexSourceAsync("export const x = " + Repeat("`${", 100_000) + ";");
        await Assert.That(exit).IsEqualTo(0).Because(log);
        await Assert.That(log).DoesNotContain("unexpected error");
        await Assert.That(index.Documents).HasSingleItem();
    }

    [Test]
    [Arguments("export const x = {0};", "(a = ", "1", ")")]
    [Arguments("export type X = {0};", "(a: ", "string", ") => void")]
    [Arguments("export const x = {0};", "a < ", "b", "")]
    public async Task SpeculativeParsingIsNotExponential(string template, string open, string inner, string close)
    {
        // Each level used to re-parse everything inside it (2^depth for the first two, quadratic for `<`).
        var depth = open.StartsWith('a') ? 3000 : 40;
        var source = string.Format(template, Repeat(open, depth) + inner + Repeat(close, depth));
        var sw = Stopwatch.StartNew();
        var file = Parser.Parse("a.ts", source);
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
        if (depth < 100) { await Assert.That(file.Diagnostics).IsEmpty(); }
    }

    [Test]
    [Arguments("diamond")]
    [Arguments("aliases")]
    public async Task TypeResolutionIsNotExponential(string kind)
    {
        // Generic interface diamonds create a fresh environment per path (a failed member lookup walked 2^depth
        // containers); aliases repeating an operand were re-expanded once per path (3^depth).
        var source = kind == "diamond"
            ? "export interface I0<T> { z(): T; }\n" + string.Concat(Enumerable.Range(1, 40).Select(i => $"export interface J{i - 1}<T> extends I{i - 1}<T> {{}}\nexport interface I{i}<T> extends I{i - 1}<T>, J{i - 1}<T> {{}}\n"))
              + "declare const x: I40<string>;\nexport const r = x.z().length + x.nope;"
            : "export type T0 = { v: number };\n" + string.Concat(Enumerable.Range(1, 13).Select(i => $"export type T{i} = T{i - 1} & T{i - 1} | T{i - 1};\n"))
              + "declare const x: T13;\nexport const r = x.v;";
        var sw = Stopwatch.StartNew();
        await AssertAfterIsDefined(await IndexSourceAsync(source));
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task LoneSurrogateCodePointEscapeDoesNotThrow()
    {
        var file = Parser.Parse("a.ts", "export const a = \"\\u{D800}\";");
        await Assert.That(file.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task SymlinkCyclesDoNotHangFileDiscovery()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-ts-symlink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "src", "main.ts"), "export const a = 1;\n");
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(dir, "src", "up"), "..");
                Directory.CreateSymbolicLink(Path.Combine(dir, "src", "self"), ".");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return; // symbolic links not available here
            }

            var (exit, index, log) = await IndexAsync(dir);
            await Assert.That(exit).IsEqualTo(0).Because(log);
            await Assert.That(index.Documents.Select(d => d.RelativePath)).IsEquivalentTo(["src/main.ts"]);
        }
        finally
        {
            foreach (var link in new[] { "up", "self" })
            {
                var path = Path.Combine(dir, "src", link);
                if (Directory.Exists(path)) { Directory.Delete(path); }
            }

            Directory.Delete(dir, recursive: true);
        }
    }
}
