using Scip.Indexer.TypeScript.Syntax;

namespace Scip.Indexer.TypeScript.Tests;

/// <summary>Tests that need neither Node.js nor npm: the indexer is pure C#.</summary>
public class NativeIndexerTests
{
    private static string NewTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-ts-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<(int Exit, Index Index, string Output)> IndexAsync(string dir, Action<TypeScriptOptions>? _ = null)
    {
        var output = Path.Combine(dir, "index.scip");
        var options = new TypeScriptOptions { Cwd = dir, Output = output, InferTsconfig = true };
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await new TypeScriptIndexer().IndexAsync(options, stdout, stderr, CancellationToken.None);
        var index = exit == 0 ? Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output)) : new Index();
        return (exit, index, stdout + stderr.ToString());
    }

    [Test]
    public async Task IndexesClassMembersAcrossFiles()
    {
        var dir = NewTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "package.json"), """{"name":"demo","version":"1.2.3"}""");
            await File.WriteAllTextAsync(Path.Combine(dir, "a.ts"), """
                export class Greeter {
                  constructor(private readonly name: string) {}
                  greet(): string { return 'hi ' + this.name }
                }
                export function make(): Greeter { return new Greeter('x') }
                """);
            await File.WriteAllTextAsync(Path.Combine(dir, "b.ts"), """
                import { make, Greeter } from './a'
                const g: Greeter = make()
                g.greet()
                """);
            var (exit, index, log) = await IndexAsync(dir);
            await Assert.That(exit).IsEqualTo(0).Because(log);
            await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-typescript");
            await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");

            var b = index.Documents.Single(d => d.RelativePath == "b.ts");
            var symbols = b.Occurrences.Select(o => o.Symbol).ToList();
            await Assert.That(symbols).Contains("scip-typescript npm demo 1.2.3 `a.ts`/make().");
            await Assert.That(symbols).Contains("scip-typescript npm demo 1.2.3 `a.ts`/Greeter#");
            await Assert.That(symbols).Contains("scip-typescript npm demo 1.2.3 `a.ts`/Greeter#greet().");
            await Assert.That(symbols).Contains("scip-typescript npm demo 1.2.3 `a.ts`/");

            var a = index.Documents.Single(d => d.RelativePath == "a.ts");
            var definitions = a.Occurrences.Where(o => (o.SymbolRoles & 1) != 0).Select(o => o.Symbol).ToList();
            await Assert.That(definitions).Contains("scip-typescript npm demo 1.2.3 `a.ts`/Greeter#`<constructor>`().(name)");
            var greeter = a.Symbols.Single(s => s.Symbol.EndsWith("`a.ts`/Greeter#", StringComparison.Ordinal));
            await Assert.That(greeter.Kind).IsEqualTo(SymbolInformation.Types.Kind.Class);
            await Assert.That(a.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task EmptyProjectFailsWithExitCodeOne()
    {
        var dir = NewTempDirectory();
        try
        {
            var (exit, _, log) = await IndexAsync(dir);
            await Assert.That(exit).IsEqualTo(1);
            await Assert.That(log).Contains("no files got indexed");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task InfersJavaScriptConfig()
    {
        var dir = NewTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "x.js"), "export function f(a) { return a }\nf(1)\n");
            var (exit, index, log) = await IndexAsync(dir);
            await Assert.That(exit).IsEqualTo(0).Because(log);
            var doc = index.Documents.Single();
            await Assert.That(doc.Language).IsEqualTo("JavaScript");
            await Assert.That(doc.Occurrences.Count(o => o.Symbol.EndsWith("x.js`/f().", StringComparison.Ordinal))).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    [Arguments("a.ts", "const x = a < b > (c)")]
    [Arguments("a.ts", "const f = <T,>(x: T) => x")]
    [Arguments("a.ts", "type A = { [K in keyof T]?: T[K] } & Record<string, () => void>")]
    [Arguments("a.ts", "type C<T> = T extends (infer U)[] ? U : never")]
    [Arguments("a.ts", "class C<T extends object = {}> extends B<T> implements I { static #x = 1; get a() { return 1 } set a(v) {} declare y: number; [k: string]: any }")]
    [Arguments("a.ts", "enum E { A = 1, B, C = 'x'.length }\ndeclare module 'm' { export const v: number }\nnamespace N.M { export const z = 1 }")]
    [Arguments("a.ts", "for (const [a, { b }] of xs) { label: for (;;) { break label } }\nasync function* g() { yield* h(); await x }")]
    [Arguments("a.ts", "const s = `a${b}c${`d${e}`}`; const r = /ab+c/gi.test(s) ? a >> 2 >>> 3 : b >= c")]
    [Arguments("a.tsx", "const e = <div id=\"x\" {...p} onClick={() => f(<b>t</b>)}>text {y} <C.D /></div>; const f = <></>")]
    [Arguments("a.ts", "import type { A } from './a'; import d, * as ns from 'x'; export * as q from 'y'; export { a as b, type C }; export default class {}")]
    public async Task ParsesWithoutErrors(string fileName, string source)
    {
        var file = Parser.Parse(fileName, source);
        await Assert.That(file.Root.Elements.Count).IsGreaterThan(0);
        await Assert.That(file.Root.End).IsEqualTo(source.Length);
    }

    /// <summary>Every input file shipped with upstream's snapshot tests must parse.</summary>
    [Test]
    public async Task ParsesAllUpstreamSnapshotInputs()
    {
        var snapshots = Fixture.FindUpstreamSnapshots();
        if (snapshots is null)
        {
            Skip.Test(".reference/scip-typescript/snapshots not found");
            return;
        }

        var failures = new List<string>();
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(snapshots, "input"), "*", SearchOption.AllDirectories))
        {
            if (file.Contains("node_modules", StringComparison.Ordinal) || Path.GetExtension(file) is not (".ts" or ".tsx" or ".js" or ".jsx" or ".mjs" or ".cjs")) { continue; }
            count++;
            try
            {
                Parser.Parse(file, await File.ReadAllTextAsync(file));
            }
            catch (SyntaxException e)
            {
                failures.Add($"{file}: {e.Message} at {e.Position}");
            }
        }

        await Assert.That(count).IsGreaterThan(20);
        await Assert.That(failures).IsEmpty().Because(string.Join("\n", failures));
    }
}
