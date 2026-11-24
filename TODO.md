# TODO — SCIP → C# port

Legend: `[x]` done · `[ ]` open. Upstream paths are relative to `.reference/`.

## 0. Setup
- [x] Move original Go repo to `.reference/scip`, disable its CI (`*.yaml.disabled`)
- [x] Clone per-language indexers into `.reference/` (java, typescript, rust-analyzer, clang, ruby, python, dotnet, dart, php) and disable their workflows
- [x] Solution layout, `Directory.Build.props`, central package management, `global.json` (.NET 11)
- [x] Protobuf generation from `scip.proto` (`tools/sync-proto.sh`, Grpc.Tools)
- [x] CLAUDE.md, README.md, TODO.md, docs/PORTING.md
- [ ] New CI workflow for the C# solution (only when requested)
- [ ] NuGet packaging metadata + icon/readme for `Scip` and `Scip.Cli` tool
- [ ] Decide on AOT / trimming for the `scip` CLI

## 1. Core library `src/Scip` (from `scip/bindings/go/scip`)
- [x] Protobuf types
- [x] `SymbolParser` (`symbol_parser.go`, `symbol.go`) + tests
- [x] `SymbolFormatter` (`symbol_formatter.go`) + tests
- [x] `IndexWriter` (streaming index output)
- [ ] `IsLocalSymbol`/`IsGlobalSymbol` helpers on `Symbol`-string extension (currently on `SymbolParser`)
- [ ] `Position` / `Range` / `OccurrenceRange` (`position.go`, `occurrence_range.go`) + tests
- [ ] Position encodings (UTF-8/UTF-16/UTF-32) and `SourceFile` helpers (`source_file.go`)
- [ ] Sorting & canonicalization (`sort.go`, `canonicalize.go`)
- [ ] Sanitize (`sanitize.go`), flatten (`flatten.go`), symbol table (`symbol_table.go`)
- [ ] Port remaining Go tests, incl. `testutil` and `memtest`
- [ ] Invalid UTF-16 (lone surrogate) rejection in `SymbolParser` to match Go's UTF-8 validation
- [ ] Benchmarks (BenchmarkDotNet) for symbol parsing vs. Go `parseSymbolV2`
- [ ] Decide fate of upstream `bindings/{java,kotlin,rust,typescript,haskell,dotnet}` (likely: not ported; protobuf covers them)

## 2. CLI `src/Scip.Cli` (from `scip/cmd/scip`, `scip/docs/CLI.md`)
- [x] Skeleton with `print` (JSON only) and `index <lang>`
- [ ] `print` — text output, `--color`, `--json` flag parity
- [ ] `lint` (`lint.go`)
- [ ] `stats` (`stats.go`)
- [ ] `snapshot` (`snapshot.go`)
- [ ] `test` (`test.go`) + `reprolang` fixtures
- [ ] `expt-convert` (`convert.go`, LSIF → SCIP)
- [ ] `--version` from `version.txt`; global `--from`, `--comment-syntax`, `--project-root`
- [ ] Port `main_test.go`, `print_test.go`, `lint_test.go`, `convert_test.go`

## 3. `reprolang` (test language, `scip/reprolang`)
- [ ] Port grammar/parser (tree-sitter replacement or hand-written parser)
- [ ] Port `testdata` and snapshot harness

## 4. Language indexers
Each needs: a design note in `docs/PORTING.md`, implementation, ported tests/snapshots.

- [ ] **dotnet** ← `scip-dotnet` (C#/VB) — natural fit: Roslyn (`Microsoft.CodeAnalysis`); start here
- [ ] **typescript** ← `scip-typescript` (TS/JS; depends on the TypeScript compiler API)
- [ ] **java** ← `scip-java` (Java/Scala/Kotlin; Gradle/Maven integration, semanticdb)
- [ ] **rust** ← `rust-analyzer` (`crates/rust-analyzer/src/cli/scip.rs` only — SCIP emission)
- [ ] **clang** ← `scip-clang` (C/C++; clang tooling, compilation databases)
- [ ] **ruby** ← `scip-ruby` (Sorbet fork)
- [ ] **python** ← `scip-python` (Pyright fork)
- [ ] **dart** ← `scip-dart` (analyzer package)
- [ ] **php** ← `scip-php` (php-parser based)
- [ ] Shared indexing helpers in `Scip.Indexing` (document builder, symbol-table/occurrence helpers, package metadata)
- [ ] Decide per language: native port vs. orchestrating the upstream language toolchain

## 5. Quality
- [ ] Cross-check outputs against Go `scip` CLI on shared fixtures (golden files)
- [ ] Docs: port `docs/DESIGN.md`, `docs/CLI.md`, `docs/Development.md` to the C# project
