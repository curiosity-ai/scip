# TODO — SCIP → C# port

Legend: `[x]` done · `[ ]` open. Upstream paths are relative to `.reference/`.

## 0. Setup
- [x] Move original Go repo to `.reference/scip`, disable its CI (`*.yaml.disabled`)
- [x] Clone per-language indexers into `.reference/` (java, typescript, rust-analyzer, clang, ruby, python, dotnet, dart, php) and disable their workflows
- [x] Solution layout, `Directory.Build.props`, central package management, `global.json` (.NET 11)
- [x] Protobuf generation from `scip.proto` (`tools/sync-proto.sh`, Grpc.Tools)
- [x] CLAUDE.md, README.md, TODO.md, docs/PORTING.md
- [x] Azure DevOps pipeline (`.azure-devops/build-nuget.yml`): build, test, pack, publish to NuGet from main
- [ ] Create the `nuget-curiosity-org` service connection / confirm NuGet package-id ownership (`ScipSharp`, `ScipSharp.Cli`, `ScipSharp.Indexer.*`; the plain `Scip` id was already taken)
- [ ] `scip --version` should report the packaged build version (currently the embedded upstream `version.txt`)
- [x] NuGet packaging: one package per library/indexer + `ScipSharp.Cli` tool (verified: pack + local tool install + run)
- [ ] Package icon; per-package READMEs
- [ ] Decide on AOT / trimming for the `scip` CLI

## 1. Core library `src/ScipSharp` (from `scip/bindings/go/scip`)
- [x] Protobuf types
- [x] `SymbolParser` (`symbol_parser.go`, `symbol.go`) + tests
- [x] `SymbolFormatter` (`symbol_formatter.go`) + tests
- [x] `IndexWriter` (streaming index output)
- [x] `IsLocalSymbol`/`IsGlobalSymbol` helpers on `Symbol`-string extension (currently on `SymbolParser`)
- [x] `Position` / `Range` / `OccurrenceRange` (`position.go`, `occurrence_range.go`) + tests
- [x] Position encodings (UTF-8/UTF-16/UTF-32) and `SourceFile` helpers (`source_file.go`)
- [x] Sorting & canonicalization (`sort.go`, `canonicalize.go`)
- [x] Sanitize (`sanitize.go`), flatten (`flatten.go`), symbol table (`symbol_table.go`)
- [x] `testutil`: `SnapshotFormatter`, `ScipSharp.Testing.TestRunner` (`test_runner.go`), `ScipSharp.Testing.SnapshotTesting` (`snapshot_testing.go`); `memtest` (Go memory test) intentionally not ported
- [ ] Invalid UTF-16 (lone surrogate) rejection in `SymbolParser` to match Go's UTF-8 validation
- [ ] Benchmarks (BenchmarkDotNet) for symbol parsing vs. Go `parseSymbolV2`
- [ ] Decide fate of upstream `bindings/{java,kotlin,rust,typescript,haskell,dotnet}` (likely: not ported; protobuf covers them)

## 2. CLI `src/ScipSharp.Cli` (from `scip/cmd/scip`, `scip/docs/CLI.md`)
- [x] Skeleton with `print` (JSON only) and `index <lang>`
- [x] `print` — text output (pp-style, ANSI `--color`), `--json` (proto field names), `-` for stdin
- [x] `lint` (`lint.go`) — `ScipSharp.Cli.Linting.IndexLinter`
- [x] `stats` (`stats.go`) — LOC counting is a simple stand-in for `gocloc` (extension-based comment rules)
- [x] `snapshot` (`snapshot.go`)
- [x] `test` (`test.go`) — runner in `ScipSharp.Testing.TestRunner` (`src/ScipSharp`); no reprolang dependency (works on any index + test files)
- [x] `expt-convert` (`convert.go`) — SCIP -> SQLite (Microsoft.Data.Sqlite + zstd), not LSIF; `--cpu-profile` is accepted but ignored
- [x] `--version` from `version.txt` (embedded); per-command `--from`, `--comment-syntax`, `--project-root` (as in Go; there are no truly global flags)
- [x] Port `main_test.go` (help/version; the `docs/CLI.md` byte-for-byte sync check is not ported: System.CommandLine help text differs from urfave/cli), `print_test.go`, `lint_test.go`, `convert_test.go`
- [ ] Regenerate `docs/CLI.md` from the C# help output
- [x] CLI assembly is `ScipSharp.Cli` (the tool command is still `scip`); projects, assemblies, namespaces and NuGet ids were all renamed from `Scip.*` to `ScipSharp.*`

## 3. `reprolang` (test language, `scip/reprolang`)
- [x] Port grammar/parser (hand-written parser in `src/ScipSharp.Reprolang`, no native tree-sitter) + `ReproIndexer.Index(directory)`
- [x] Port `testdata` and snapshot tests (`tests/ScipSharp.Reprolang.Tests`)
- [ ] Port `test_cmd` pass/fail output expectations once the CLI `test` runner (`test_runner.go`) exists; wire `snapshot`/`test` to `ReproIndexer`

## 4. Language indexers
Each needs: a design note in `docs/PORTING.md`, implementation, ported tests/snapshots.

- [x] **dotnet** ← `scip-dotnet` (Roslyn, MSBuildWorkspace; needs .NET SDK at runtime to load projects). Open: WPF fixture, case-distinct paths test, logging-env test; consider native project-file loading
- [~] **typescript** ← `scip-typescript`: native TS/TSX/JS/JSX parser+binder+resolver. 42/~54 snapshot docs match. Open: type inference (generics/callbacks/Promise), lib.d.ts/globals, JSX intrinsics, Svelte, CommonJS, parser error recovery
- [~] **java** ← `scip-java`: native Java 21 parser/resolver, class/jar/JDK-modules readers, pom/gradle/scip-java.json. 25/26 snapshots match. Open: Kotlin, Scala, annotation processing (Lombok), full inference, real Gradle/Maven resolution
- [~] **rust** ← `rust-analyzer` SCIP emission: native Cargo.toml + token-level scanner. Open: real name resolution/type inference, cross-crate refs, external symbols, generics/closure params
- [~] **clang** ← `scip-clang`: native preprocessor + tolerant C++ parser. Open: type checker, upstream-compatible function disambiguator hashes, system headers, templates, relationships, docs, using-decls
- [~] **ruby** ← `scip-ruby`: native lexer/resolver. 37/113 snapshot fixtures exact (~76% defs, ~50% refs). Open: type inference, core/RBI payload, DSL rewriters, keyword params, relationships/docs, --gem-map-path
- [~] **python** ← `scip-python`: native tokenizer/parser/binder/import resolver. 22 upstream snapshots (6 divergent, documented). Open: typeshed/hover, generics/narrowing/flow analysis, match patterns, CLI option forwarding (--project-name etc.)
- [~] **dart** ← `scip-dart`: native syntactic indexer. Defs match upstream snapshots; refs precision >90%/recall >50%. Open: type resolution, SDK/3rd-party symbols, instance members, locals
- [x] **php** ← `scip-php`: native lexer/parser/resolver, Composer. 10/10 upstream goldens exact. Open: phpstorm-stubs for built-ins, parse error recovery, property hooks
- [~] **cobol** ← native (no upstream): lexer (fixed/free/variable), COPY/REPLACING/REPLACE preprocessor, copybook resolution, programs/nested/OO basics, data hierarchy, files, paragraphs/sections, qualified refs, PERFORM/GO TO/CALL/CICS/SQL refs, copybook-shared symbols; snapshots + CardDemo/NIST CCVS85 corpora. Open: >>IF/$IF conditional compilation, read/write roles, Report Writer/screen semantics, OO beyond CLASS-ID/METHOD-ID/INVOKE literal, CALL identifier target inference, JCL/BMS/DCLGEN
- [~] **vb6** ← native (no upstream): .vbp/.vbg + .bas/.cls/.frm/.ctl/VBA exports; #If evaluation, designer controls, scoped/typed resolution, event-handler/Implements relationships; snapshots + PolyWorks/VbAsyncSocket corpora. Open: type-library/OCX members, implicit variables (no Option Explicit), named arguments, VB_GlobalNameSpace, .frx UserForm controls
- [~] **pli** ← native (no upstream): lexer (margins/*PROCESS, #@$), contextual statement parser, block-scoped resolver (qualified/partial structure refs, LIKE/TYPE, implicit decls), PACKAGE/ENTRY/BEGIN, %INCLUDE + %DCL/%PROC, cross-file external linkage; snapshots + Multics corpus. Open: preprocessor evaluation/macro expansion, DEFAULT statements, GENERIC/overload selection, ambiguity diagnostics, CICS, a larger permissively licensed Enterprise PL/I corpus (only zopeneditor's MACSAMP.pli is unrestricted)
- [~] **fortran** ← native (no upstream): fixed/free form, cpp + INCLUDE, modules/submodules, use/host association, implicit typing, generics/operators, derived types + type-bound procedures, COMMON/BLOCK DATA/ENTRY/labels; LAPACK/minpack/fpm lint-clean. Open: macro expansion, generic→specific call resolution, keyword-arg→dummy refs, COMMON storage-position matching, \param docs on dummies
- [~] **delphi** ← native (no upstream): lexer/preprocessor/parser/cross-unit resolver, .dpr/.dpk/.dproj/{$I}/.dfm/.fmx, RTL/VCL/FMX stubs, forward/impl linking, overloads, generics, helpers, relationships; lint-clean on Delphi-Mocks/DUnitX/JsonDataObjects. Open: full overload/operator typing, Declared() in {$IF}, broader RTL stubs, binary DFMs, helper scoping, duplicate symbols for units copied in several folders
- [~] **pascal** ← native (ISO/Extended, Turbo, FPC fpc/objfpc/tp/iso/macpas, Lazarus lpi/lpk/lpr/lfm): lexer+preprocessor, one-pass binder (nested scopes, with, variant records, objects/classes/helpers/generics, overloads, operators, units two-phase), RTL/LCL table; snapshots + Pascal-P5/ThreadSafeCollections-FP corpora. Open: full FPC overload rules, constrained generic members, Delphi-mode generic method calls, EP schemata/export renaming, binary .lfm
- [~] **sql** ← native (no upstream): ANSI/PostgreSQL/T-SQL/MySQL/SQLite/PL/SQL + dbt; DDL/DML/procedural, two-pass catalog, migrations ordering, FK/view-lineage relationships; snapshots + sqlfluff/Postgres-regress corpora (~1,080 files) lint-clean. Open: expression typing for overloads, composite field access, dynamic SQL, sp_rename/synonyms/TYPE BODY, Jinja macros, search_path/USE across files, per-statement dialect switching
- [~] **abap** ← abaplint (`packages/core`, via an existing C# port copied into the indexer; no upstream SCIP indexer): abapGit folders, syntax-check based definitions/references, DDIC objects, void references as externals. Lint-clean on open-abap and the 360-repo abapGit corpus. Open: port the abaplint syntax/object replay suites (8 MB of data, left out), CDS (DDLS) parser, structure components (`ls-field`), dynpros/screens, messages (MSAG/T100), enhancements, `--release` per object
- [x] Forward indexer-specific CLI options from `scip index <lang>` to indexers (unmatched tokens pass through)
- [ ] Shared indexing helpers in `ScipSharp.Indexing` (document builder, package metadata) — extract duplication across the indexers
- [x] Decision: pure native C# for all indexers (no runtime compilers/toolchains; see CLAUDE.md)

## 5. Quality
- [x] Large-project fix pass (Ruby, TypeScript, Dart, PHP, Java, .NET) — see docs/VALIDATION.md
- [ ] Python: invalid local symbols on home-assistant/core (364 lint errors) — in progress
- [x] Diff indexer output against upstream indexers on shared projects (sanity check: docs/UPSTREAM-COMPARISON.md)
- [x] Upstream-comparison findings addressed (docs/UPSTREAM-COMPARISON.md, 'After the alignment pass')
- [ ] Remaining upstream gaps: reference recall (Ruby/Rust type inference, TS expando members), Python docstring markdown, Java per-module classpaths, Rust external-crate refs; clang still uncompared
- [x] Robustness pass over all indexers: exponential include/import/COPY walks, quadratic lookups, non-advancing lexer loops, regex backtracking and stack overflows fixed; `scip index` runs indexers on a 256 MB stack (`LargeStackThread`), recursion guarded with `TryEnsureSufficientExecutionStack` so too-deep input degrades per statement/file instead of aborting; regression tests (`RobustnessTests`/`PathologicalInputTests`/`DeepNestingTests` per indexer, small-stack threads)
- [ ] Remaining (pathological sizes only): ABAP method-call chains quadratic in length (every prefix is wrapped into a node), Rust shared `visited` set in `LookupIn` can miss references, .NET 5000-link call chains slow inside Roslyn's binder; indexers check cancellation only between objects/files
- [ ] Reduce memory/time on very large Python/Java projects (ha-core 4.9 GB / 117 s)
- [~] (lint errors fixed everywhere; open: relationship-ordering decision) Fix findings from real-project validation (`docs/VALIDATION.md`): dotnet project loading (Serilog) + non-zero exit on failure; SymbolInformation for locals (TS, Ruby); external SymbolInformation (Java); duplicate/dangling SymbolInformation & relationships (Python, Java); make every validation index pass `scip lint`
- [ ] Cross-check outputs against Go `scip` CLI on shared fixtures (golden files)
- [ ] Docs: port `docs/DESIGN.md`, `docs/CLI.md`, `docs/Development.md` to the C# project
