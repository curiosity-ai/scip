# Porting notes

## Source map

| Upstream (`.reference/…`) | C# destination | Status |
|---|---|---|
| `scip/scip.proto` | `src/ScipSharp/Protos/scip.proto` (via `tools/sync-proto.sh`) | done |
| `scip/bindings/go/scip` | `src/ScipSharp` | symbols + index writer done |
| `scip/cmd/scip` | `src/ScipSharp.Cli` | done |
| `scip/reprolang` | (planned) `src/ScipSharp.Reprolang` | done |
| `scip-dotnet` | `src/Indexers/ScipSharp.Indexer.DotNet` | ported (see TODO.md for gaps) |
| `scip-typescript` | `src/Indexers/ScipSharp.Indexer.TypeScript` | ported (see TODO.md for gaps) |
| `scip-java` | `src/Indexers/ScipSharp.Indexer.Java` | ported (see TODO.md for gaps) |
| `rust-analyzer` (SCIP emitter only) | `src/Indexers/ScipSharp.Indexer.Rust` | ported (see TODO.md for gaps) |
| `scip-clang` | `src/Indexers/ScipSharp.Indexer.Clang` | ported (see TODO.md for gaps) |
| `scip-ruby` | `src/Indexers/ScipSharp.Indexer.Ruby` | ported (see TODO.md for gaps) |
| `scip-python` | `src/Indexers/ScipSharp.Indexer.Python` | ported (see TODO.md for gaps) |
| `scip-dart` | `src/Indexers/ScipSharp.Indexer.Dart` | ported (see TODO.md for gaps) |
| `scip-php` | `src/Indexers/ScipSharp.Indexer.Php` | ported (see TODO.md for gaps) |
| abaplint (`packages/core`, via an existing C# port; no upstream SCIP indexer) | `src/Indexers/ScipSharp.Indexer.Abap` | new indexer (see its README) |

## Key differences from the Go implementation

- Go's `beaut.UTF8String` validation has no .NET counterpart; .NET strings are UTF-16. The symbol grammar is
  ASCII-delimited so parsing works on UTF-16 directly. Lone surrogates are not yet rejected (see TODO).
- The Go parser is written to avoid heap allocation (`RecordOutput == nil` = validate-only). The C# port exposes
  `SymbolParser.Validate` for the same use and otherwise allocates normally; benchmark before optimising.
- Errors are returned as strings from the internal reader and surfaced via `TryParse`/`Parse`
  (`SymbolParseException`), instead of Go error types.

## Per-language design decisions

_Record the semantic-analysis strategy for each indexer here as you decide it (Roslyn, tree-sitter, LSP, shelling out)._

### ABAP (`ScipSharp.Indexer.Abap`)

There is no upstream SCIP indexer for ABAP. The indexer is built on a C# port of the abaplint parser and syntax check
(`packages/core` of https://github.com/abaplint/abaplint, MIT), copied into `src/Indexers/ScipSharp.Indexer.Abap/Language`
with its namespaces renamed to `ScipSharp.Indexer.Abap.*`. That port was verified token-for-token, statement-for-statement
and issue-for-issue against `@abaplint/core` 2.120.80 on a corpus of 360 abapGit repositories. Its upstream statement and
structure test suites run in `tests/ScipSharp.Indexer.Abap.Tests`.

- Input: a folder of abapGit files (`*.abap` + `*.xml`); an `abaplint.json` in the root is honoured (`--config`, `--release`).
- Semantics: abaplint's syntax check per object (spaghetti scopes with declarations and resolved references).
  Declarations are keyed by file + position + name; every abaplint reference is mapped through that table.
  Unresolved (void) names — SAP standard objects that are not in the sources — become external symbols.
- abaplint records no reference for `CALL FUNCTION 'X'`, `INCLUDE`, `SUBMIT`, `CLASS … IMPLEMENTATION` or dictionary types and
  tables; those are added from the statement trees.
- See `src/Indexers/ScipSharp.Indexer.Abap/README.md` for the symbol layout and the known gaps.

