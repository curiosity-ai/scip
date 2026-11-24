# TODO — SCIP → C# port

Legend: `[x]` done · `[ ]` open. Upstream paths are relative to `.reference/`.

## 0. Setup
- [x] Move original Go repo to `.reference/scip`, disable its CI (`*.yaml.disabled`)
- [x] Clone per-language indexers into `.reference/` (java, typescript, rust-analyzer, clang, ruby, python, dotnet, dart, php) and disable their workflows
- [x] Solution layout, `Directory.Build.props`, central package management, `global.json` (.NET 11)
- [x] Protobuf generation from `scip.proto` (`tools/sync-proto.sh`, Grpc.Tools)
- [x] CLAUDE.md, README.md, TODO.md, docs/PORTING.md
- [x] Azure DevOps pipeline (`.azure-devops/build-nuget.yml`): build, test, pack, publish to NuGet from main
- [ ] Create the `nuget-curiosity-org` service connection / confirm NuGet package-id ownership (`Scip`, `Scip.Cli`, `Scip.Indexer.*`)
- [ ] `scip --version` should report the packaged build version (currently the embedded upstream `version.txt`)
- [x] NuGet packaging: one package per library/indexer + `Scip.Cli` tool (verified: pack + local tool install + run)
- [ ] Package icon; per-package READMEs
- [ ] Decide on AOT / trimming for the `scip` CLI

## 1. Core library `src/Scip` (from `scip/bindings/go/scip`)
- [x] Protobuf types
- [x] `SymbolParser` (`symbol_parser.go`, `symbol.go`) + tests
- [x] `SymbolFormatter` (`symbol_formatter.go`) + tests
- [x] `IndexWriter` (streaming index output)
- [x] `IsLocalSymbol`/`IsGlobalSymbol` helpers on `Symbol`-string extension (currently on `SymbolParser`)
- [x] `Position` / `Range` / `OccurrenceRange` (`position.go`, `occurrence_range.go`) + tests
- [x] Position encodings (UTF-8/UTF-16/UTF-32) and `SourceFile` helpers (`source_file.go`)
- [x] Sorting & canonicalization (`sort.go`, `canonicalize.go`)
- [x] Sanitize (`sanitize.go`), flatten (`flatten.go`), symbol table (`symbol_table.go`)
- [x] `testutil`: `SnapshotFormatter`, `Scip.Testing.TestRunner` (`test_runner.go`), `Scip.Testing.SnapshotTesting` (`snapshot_testing.go`); `memtest` (Go memory test) intentionally not ported
- [ ] Invalid UTF-16 (lone surrogate) rejection in `SymbolParser` to match Go's UTF-8 validation
- [ ] Benchmarks (BenchmarkDotNet) for symbol parsing vs. Go `parseSymbolV2`
- [ ] Decide fate of upstream `bindings/{java,kotlin,rust,typescript,haskell,dotnet}` (likely: not ported; protobuf covers them)

## 2. CLI `src/Scip.Cli` (from `scip/cmd/scip`, `scip/docs/CLI.md`)
- [x] Skeleton with `print` (JSON only) and `index <lang>`
- [x] `print` — text output (pp-style, ANSI `--color`), `--json` (proto field names), `-` for stdin
- [x] `lint` (`lint.go`) — `Scip.Cli.Linting.IndexLinter`
- [x] `stats` (`stats.go`) — LOC counting is a simple stand-in for `gocloc` (extension-based comment rules)
- [x] `snapshot` (`snapshot.go`)
- [x] `test` (`test.go`) — runner in `Scip.Testing.TestRunner` (`src/Scip`); no reprolang dependency (works on any index + test files)
- [x] `expt-convert` (`convert.go`) — SCIP -> SQLite (Microsoft.Data.Sqlite + zstd), not LSIF; `--cpu-profile` is accepted but ignored
- [x] `--version` from `version.txt` (embedded); per-command `--from`, `--comment-syntax`, `--project-root` (as in Go; there are no truly global flags)
- [x] Port `main_test.go` (help/version; the `docs/CLI.md` byte-for-byte sync check is not ported: System.CommandLine help text differs from urfave/cli), `print_test.go`, `lint_test.go`, `convert_test.go`
- [ ] Regenerate `docs/CLI.md` from the C# help output
- [ ] Assembly is `Scip.Cli` (not `scip`): assembly names are case-insensitive and would clash with `Scip.dll`; the tool command is still `scip`

## 3. `reprolang` (test language, `scip/reprolang`)
- [x] Port grammar/parser (hand-written parser in `src/Scip.Reprolang`, no native tree-sitter) + `ReproIndexer.Index(directory)`
- [x] Port `testdata` and snapshot tests (`tests/Scip.Reprolang.Tests`)
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
- [x] Forward indexer-specific CLI options from `scip index <lang>` to indexers (unmatched tokens pass through)
- [ ] Shared indexing helpers in `Scip.Indexing` (document builder, package metadata) — extract duplication across the nine indexers
- [x] Decision: pure native C# for all indexers (no runtime compilers/toolchains; see CLAUDE.md)

## 5. Quality
- [ ] Large-project fix pass (docs/VALIDATION.md): Ruby stack overflow (Rails); TypeScript stack overflow + parser gaps (Angular, Playwright); Dart lint (Flutter, Riverpod); PHP (Laravel anon classes, Symfony non-UTF-8 identifiers); Java duplicate relationships (Guava); .NET multi-TFM duplicates (Humanizer, Newtonsoft)
- [~] (lint errors fixed everywhere; open: relationship-ordering decision) Fix findings from real-project validation (`docs/VALIDATION.md`): dotnet project loading (Serilog) + non-zero exit on failure; SymbolInformation for locals (TS, Ruby); external SymbolInformation (Java); duplicate/dangling SymbolInformation & relationships (Python, Java); make every validation index pass `scip lint`
- [ ] Cross-check outputs against Go `scip` CLI on shared fixtures (golden files)
- [ ] Docs: port `docs/DESIGN.md`, `docs/CLI.md`, `docs/Development.md` to the C# project
