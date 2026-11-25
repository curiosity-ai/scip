# ScipSharp.Indexer.TypeScript

Pure C# port of [`scip-typescript`](https://github.com/sourcegraph/scip-typescript) (`.reference/scip-typescript`,
commit `7f51391`). **No Node.js, `tsc` or any external toolchain is needed at run time.**

## Design decision

There is no TypeScript compiler API in .NET, and spawning `node`/`tsc` is not allowed, so the semantic layer upstream
gets from the TypeScript checker is re-implemented natively:

| Upstream | Port |
|---|---|
| `ts.createProgram` / parser | `Syntax/` — scanner + recursive-descent parser for TS, TSX, JS, JSX (same node shapes as the TS compiler, so upstream logic ports 1:1) |
| binder (symbol tables) | `Semantics/Binder.cs` — lexical scopes, hoisting, export tables |
| `TypeChecker.getSymbolAtLocation` | `Semantics/Checker.cs` — name resolution, imports/exports/re-exports, node module resolution, and a *declaration-based* mini type model (see below) |
| `FileIndexer.ts`, `Packages.ts`, `ScipSymbol.ts`, `Descriptor.ts` | `Semantics/FileIndexer.cs`, `Semantics/ScipSymbols.cs` (symbols are built with `ScipSharp.SymbolFormatter`) |
| `main.ts`, `ProjectIndexer.ts`, `inferTsconfig.ts` | `TypeScriptIndexer.cs`, `Project/TsConfig.cs` (JSONC, `extends`, `include`/`exclude`/`files`, `references`, `allowJs`, inference), `Project/Workspaces.cs` |

Output is streamed with `ScipSharp.IndexWriter` (metadata, then one document at a time; typed ranges; UTF-16 positions).
Symbols use upstream's scheme: `scip-typescript npm <package> <version> <path>/<descriptors>`, `local N` counters
reproduced by porting `scipSymbol` call-for-call.

The "type model" does not infer types. For an expression it computes the *containers* (class / interface / type literal /
enum / namespace / module / object literal) whose members apply, from: declared type annotations, `new X()`, `this`,
`super`, imports/aliases, property chains, calls to functions with declared (or single `return`) result types, and
contextual typing of object literals (variable annotations, returns, arguments, arrays, nested properties). Static vs.
instance sides of classes are distinguished.

CLI flags accepted (`scip index typescript -- ...`): `--cwd`, `--output`, `--infer-tsconfig`, `--max-file-byte-size 1mb`,
`--npm-workspaces`, `--yarn-workspaces`, `--pnpm-workspaces`, project paths. Workspaces are read from `package.json` /
`pnpm-workspace.yaml` directly (upstream shells out to yarn/pnpm).

## Tests (`tests/ScipSharp.Indexer.TypeScript.Tests`)

* `NativeIndexerTests` — parser coverage (every upstream snapshot input must parse), end-to-end index of small projects.
  No external tools.
* `SnapshotTests` — port of upstream `main.test.ts` + `SnapshotTesting.ts`: indexes each directory of
  `.reference/scip-typescript/snapshots/input` and compares with `snapshots/output`. These need the npm packages the
  upstream fixtures import (`@types/node`, `@types/react`, `@sourcegraph/tsconfig`); the fixture fetches them with `npm`
  into a temp dir **as test data only** and skips with a clear reason when `npm`/network is unavailable. Documents listed
  in `known-good.txt` (42 of ~60 documents) must match upstream byte-for-byte (after dropping references into `lib.d.ts`
  and npm packages from the expectation); regenerate with `SCIP_TS_BASELINE_OUT=file dotnet run`. `SCIP_TS_DIFF_DIR=dir`
  dumps expected/obtained for non-matching documents.

## Coverage and honest gaps

Covered: definitions/references for everything lexically or declaratively resolvable in TS/JS; imports, re-exports,
`export *`, namespace imports, `export =`, relative and `node_modules` (incl. `@types`, workspace symlinks) module
resolution; module-specifier references; enclosing ranges; symbol kinds; relationships (implementations); `@deprecated`
diagnostics; JSDoc documentation; constructor symbols for `new`; object-literal property symbols (`name0:`); shorthand
properties; object binding patterns; `.d.ts` files; tsconfig `extends`; project references; workspaces.

Not covered / differs from upstream:

* **A partial type model, not the checker.** `Checker` resolves members through *containers*: a declaration (class,
  interface, type literal, object literal, namespace, mapped type, function) plus an environment binding its type
  parameters and `this`. That covers type arguments and inheritance (`class A extends Base<X>`), call and `new` results
  (construct/call signatures, simple generic inference from arguments and from the contextual return type), `await`,
  array/index/element types, JSDoc `@type`/`@param` in JavaScript, contextual typing of callbacks and object literals
  (incl. discriminated unions), homomorphic mapped types (`Partial`, `Pick`, `Omit`, ...), `instanceof`/`typeof`/
  `Array.isArray` narrowing, CommonJS `require`/`module.exports` aliases, `package.json` `exports` (incl. self reference
  and `customConditions`), `paths`/`baseUrl` and module augmentations. It does not model control flow beyond those
  guards, overload resolution, conditional-type evaluation (both branches are kept) or full generic inference.
* **Built-in globals** come from the embedded `lib.*.d.ts` files (copied from `typescript` 6.0.3, see `Lib/NOTICE.txt`),
  selected from `lib`/`target`/`noLib` exactly like the compiler's default library, and are named
  `scip-typescript npm typescript 6.0.3 lib/`lib.es5.d.ts`/Array#`. Cross-file globals of non-module *project* scripts
  are still not shared.
* **Hover documentation** is `documentation = ["```ts\n<signature>\n```", <jsdoc>]` like the published scip-typescript
  (the checked-in reference additionally fills `signature_documentation`; both are written). Type text is derived from
  annotations, literals and the container model; it is `any` when the type cannot be named (about half of the
  signature blocks equal the checker's text on zod).
* Files are indexed in `ts.createProgram` order (roots, dependencies first) so that anonymous type literals and
  object-literal property counters (`typeLiteral12`, `name3:`) are numbered like upstream; expando members of JS
  (`this.x = ...`, `exports.x = ...`) are not declared, so references to them are missing.
* **JSX**: parsed, tag names of components resolve, but intrinsic elements (`<p>`) and attribute names do not (upstream
  maps them to `JSX.IntrinsicElements` / props types). The `react` snapshot therefore does not match.
* **Svelte** (`svelte2tsx`) is not supported; **JS prototype-assignment members** (`C.prototype.x = ...`,
  `Object.defineProperty(C.prototype, ...)`) and CommonJS `module.exports` patterns are not modelled.
* `pnpm`/`yarn` workspace discovery is a native approximation of `pnpm ls -r` / `yarn workspaces info`.
* Syntax errors abort a file (it is reported on stderr and skipped); the parser is not error-tolerant.
* Parser is lenient about ASI and does not validate semantics; very exotic syntax (decorator metadata, `using`, import
  attributes beyond skipping, some JSDoc-typed JS) may fail to parse.
