# Scip.Indexer.TypeScript

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
| `FileIndexer.ts`, `Packages.ts`, `ScipSymbol.ts`, `Descriptor.ts` | `Semantics/FileIndexer.cs`, `Semantics/ScipSymbols.cs` (symbols are built with `Scip.SymbolFormatter`) |
| `main.ts`, `ProjectIndexer.ts`, `inferTsconfig.ts` | `TypeScriptIndexer.cs`, `Project/TsConfig.cs` (JSONC, `extends`, `include`/`exclude`/`files`, `references`, `allowJs`, inference), `Project/Workspaces.cs` |

Output is streamed with `Scip.IndexWriter` (metadata, then one document at a time; typed ranges; UTF-16 positions).
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

## Tests (`tests/Scip.Indexer.TypeScript.Tests`)

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

* **No real type inference.** Members reached through inferred types (generic instantiation such as `.map<T>(...)`,
  callback parameter inference, `await`/`Promise<T>` results, union narrowing, control flow, mapped/conditional types)
  produce no occurrence. Object literals whose contextual type comes from such inference get `name0:` meta symbols
  instead of references to the interface property (9 snapshot documents differ for this reason).
* **No `lib.d.ts` / global scripts.** `console`, `Array#map`, `Math`, `Promise`, DOM etc. are not resolved, so upstream's
  references into `scip-typescript npm typescript <ver> lib/...` are absent. Cross-file global declarations of non-module
  scripts are not shared.
* **Signature documentation text is approximate** (`signature_documentation`): types come from annotations or simple
  literal inference, not the checker (`any` otherwise). Escaping of unusual literal types differs.
* **JSX**: parsed, tag names of components resolve, but intrinsic elements (`<p>`) and attribute names do not (upstream
  maps them to `JSX.IntrinsicElements` / props types). The `react` snapshot therefore does not match.
* **Svelte** (`svelte2tsx`) is not supported; **JS prototype-assignment members** (`C.prototype.x = ...`,
  `Object.defineProperty(C.prototype, ...)`) and CommonJS `module.exports` patterns are not modelled.
* `pnpm`/`yarn` workspace discovery is a native approximation of `pnpm ls -r` / `yarn workspaces info`.
* Syntax errors abort a file (it is reported on stderr and skipped); the parser is not error-tolerant.
* Parser is lenient about ASI and does not validate semantics; very exotic syntax (decorator metadata, `using`, import
  attributes beyond skipping, some JSDoc-typed JS) may fail to parse.
