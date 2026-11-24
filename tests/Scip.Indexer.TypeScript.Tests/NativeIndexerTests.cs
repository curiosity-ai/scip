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
    [Arguments("a.ts", "const TabListView = ListView<Tab>;")]
    [Arguments("a.ts", "const L = ListView<{ message: string, time: string }>\nconst M = new Map<string, number>\nexport default Foo<string>;")]
    [Arguments("a.ts", "type P = { params: [target: Foo, method: string, commandParams?: object]; q: [a?: number, ...rest: string[]] }")]
    [Arguments("a.ts", "const T = new InjectionToken<<T>(loadFn: () => T) => T>('x');\ntype G = Foo<<T>() => T>")]
    [Arguments("a.ts", "const a = x satisfies Y; const b = <const T,>(x: T) => x; class C { accessor z = 1; static accessor w: number }")]
    [Arguments("a.ts", "using a = f(); await using b = g(); for (using c of xs) {}")]
    [Arguments("a.ts", "class D<in out T, const U extends readonly unknown[]> {}")]
    [Arguments("a.ts", "import a from \"./a.json\" with { type: \"json\" }; export { b } from \"./b\" with { type: \"json\" }; const m = await import(\"./c\", { with: { type: \"json\" } })")]
    [Arguments("a.ts", "@Component({ selector: \"x\" }) class E { @Input() a = 1; @HostListener(\"click\", [\"$event\"]) on(e) {} constructor(@Inject(T) private t: T) {} }")]
    [Arguments("a.ts", "abstract class F { abstract m(): void; abstract get x(): number; } type Ctor = abstract new (...a: any[]) => object")]
    [Arguments("a.ts", "const r = /[/]\\/(?<n>x)/dgimsuvy; const q = a?.b!.c?.[0]!.d?.(1)!")]
    [Arguments("a.ts", "enum G { A = \"a\", \"B-C\" = 1, D = A | 2 } declare const enum H { X } declare global { interface Window { w: 1 } } namespace A.B.C { export import X = Y.Z }")]
    [Arguments("a.ts", "function f(this: Foo, a?: number, ...r: string[]): asserts a is string {} function g(x): x is string { return true } function h(): asserts this {}")]
    [Arguments("a.ts", "let x: typeof import(\"./a\"); let y: import(\"./a\").B<string>; type U = unique symbol; type TT = `a${string}b`; type K = keyof typeof obj[number]")]
    [Arguments("a.ts", "type M = { readonly [K in keyof T as `get${Capitalize<K & string>}`]-?: T[K] } & { +readonly [K in X]+?: 1 }")]
    [Arguments("a.ts", "export = foo; import fs = require(\"fs\"); export as namespace Lib; export type { A } from \"./a\"; import { type B, type C as D } from \"./b\"")]
    [Arguments("a.ts", "label: { break label } if (a) b; else if (c) d; do x++; while (y) \nswitch (a) { case 1: default: }")]
    [Arguments("a.ts", "class G2 { static { init(); } #p = 1; static #q() {} get #r() { return 1 } has(o) { return #p in o } declare readonly x: number; override m() {} }")]
    [Arguments("a.ts", "const o = { async *gen() {}, get a() { return 1 }, set a(v) {}, [k]: 1, \"s\": 2, 3: 4, ...rest, async, get, set, static: 1 }")]
    [Arguments("a.ts", "a ||= b; a &&= c; a ??= d; x **= 2; const big = 10n ** 2n; const n = 1_000.5e-3; const h = 0xFFn")]
    [Arguments("a.ts", "for await (const x of y) {} for (var i = 0, j = 1; i < j; i++, j--) {} for (const k in o) {} out: while (1) { continue out }")]
    [Arguments("a.ts", "let f = async <T,>(x: T): Promise<T> => x; let g = async (x) => x; let h = async x => x; let i = function* () {}")]
    [Arguments("a.ts", "class A1 { constructor(public readonly a: number, protected b?: string) { super() } m?(): void; n!: number; [key: string]: any; static readonly s = 1 }")]
    [Arguments("a.ts", "interface I1 extends A, B<C> { new (x: number): I1; (y: string): void; readonly [i: number]: string; m?<T>(this: I1): T; get x(): number; set x(v: number) }")]
    [Arguments("a.ts", "type Fn = new () => void; type G3 = (a: number) => a is 1; type O = { a: 1 } | [1, 2] | (() => void)[] | readonly string[] | ((x: number) => void) | undefined")]
    [Arguments("a.ts", "const t = tag`a${b}`; const u = a<b>`x`; const v = new.target; const w = import.meta.url; if (a < b && c > d) {}")]
    [Arguments("a.ts", "y = a ? (b): number => c : d; z = (a, b) => ({}); v = a ? (b) => c : d")]
    [Arguments("a.ts", "declare function f(a: number): void; declare module \"m\" { export default x } declare namespace N { let a: number } export declare const z: number")]
    [Arguments("a.ts", "type A2<T> = T extends [infer H extends string, ...infer R] ? H : never; type B2 = T extends (infer U extends number)[] ? U : 0")]
    [Arguments("a.ts", "function f<const T extends readonly unknown[]>(x: T) {} const a = <T extends unknown>(x: T) => x")]
    [Arguments("a.ts", "var yield_ = 1; var async = 2; var of = 3; var type = 4; type = 5; let get = 1, set = 2, declare = 3, abstract = 4, module = 5, namespace = 6, as = 7, from = 8")]
    [Arguments("a.tsx", "const f = <T,>(x: T) => x; const g = <T extends unknown>(x: T) => x; const e = <C<string> prop={1} />; const h = async <T,>(x: T) => <div>{x}</div>")]
    [Arguments("a.ts", "export class UpdateUserDto extends PartialType(CreateUserDto) {}\nclass M extends Mixin(Base)<T> implements I {}\nclass N extends Foo<string> {}")]
    [Arguments("a.ts", "export type AssertEqual<T, U> = (<V>() => V extends T ? 1 : 2) extends <V>() => V extends U ? 1 : 2 ? true : false;")]
    [Arguments("a.ts", "type X<S> = `${core.output<S> extends infer T extends LiteralPart ? Empty<T> : never}`; type Y<T> = T extends infer U extends string ? U : never")]
    [Arguments("a.tsx", "const e = <path d=\"M 145 75\n C 110 35, 60 55\" fill=\'a\nb\' />;")]
    public async Task ParsesWithoutErrors(string fileName, string source)
    {
        var file = Parser.Parse(fileName, source);
        await Assert.That(file.Diagnostics).IsEmpty();
        await Assert.That(file.Root.Elements.Count).IsGreaterThan(0);
        await Assert.That(file.Root.End).IsEqualTo(source.Length);
    }

    [Test]
    public async Task RecoversFromSyntaxErrorsAndKeepsParsingTheRest()
    {
        const string source = "const a = 1;\nconst b = ) ;\nfunction f() {\n  let x = 1;\n  let y = ];\n  return x;\n}\nclass C { m() { @@@ } }\nexport const z = 3;\n";
        var file = Parser.Parse("a.ts", source);
        await Assert.That(file.Diagnostics.Count).IsGreaterThanOrEqualTo(2);
        var names = file.Root.Elements.Select(e => e.Kind.ToString()).ToList();
        await Assert.That(names).Contains("FunctionDeclaration");
        await Assert.That(names).Contains("ClassDeclaration");
        await Assert.That(file.Root.Elements.Last().Kind.ToString()).IsEqualTo("VariableStatement");
    }

    [Test]
    public async Task RecoveryStepsOverTemplateLiteralsWithBrokenSubstitutions()
    {
        const string source = "type A = `${ @@ ${`x`} }` extends ;\nconst after = `a${b}`;\nexport const z = 3;\n";
        var file = Parser.Parse("a.ts", source);
        await Assert.That(file.Diagnostics.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(file.Root.Elements.Last().Kind.ToString()).IsEqualTo("VariableStatement");
        await Assert.That(file.Root.Elements.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task CssImportsAreNotParsedAsSource()
    {
        var dir = NewTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "a.ts"), "import './x.css'; export const a = 1;\n");
            await File.WriteAllTextAsync(Path.Combine(dir, "x.css"), ".a { color: red }\n");
            var (exit, index, log) = await IndexAsync(dir);
            await Assert.That(exit).IsEqualTo(0).Because(log);
            await Assert.That(log).DoesNotContain("syntax error");
            await Assert.That(index.Documents.Select(d => d.RelativePath)).DoesNotContain("x.css");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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
