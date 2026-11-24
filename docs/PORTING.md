# Porting notes

## Source map

| Upstream (`.reference/…`) | C# destination | Status |
|---|---|---|
| `scip/scip.proto` | `src/Scip/Protos/scip.proto` (via `tools/sync-proto.sh`) | done |
| `scip/bindings/go/scip` | `src/Scip` | symbols + index writer done |
| `scip/cmd/scip` | `src/Scip.Cli` | skeleton |
| `scip/reprolang` | (planned) `src/Scip.Reprolang` | not started |
| `scip-dotnet` | `src/Indexers/Scip.Indexer.DotNet` | stub |
| `scip-typescript` | `src/Indexers/Scip.Indexer.TypeScript` | stub |
| `scip-java` | `src/Indexers/Scip.Indexer.Java` | stub |
| `rust-analyzer` (SCIP emitter only) | `src/Indexers/Scip.Indexer.Rust` | stub |
| `scip-clang` | `src/Indexers/Scip.Indexer.Clang` | stub |
| `scip-ruby` | `src/Indexers/Scip.Indexer.Ruby` | stub |
| `scip-python` | `src/Indexers/Scip.Indexer.Python` | stub |
| `scip-dart` | `src/Indexers/Scip.Indexer.Dart` | stub |
| `scip-php` | `src/Indexers/Scip.Indexer.Php` | stub |

## Key differences from the Go implementation

- Go's `beaut.UTF8String` validation has no .NET counterpart; .NET strings are UTF-16. The symbol grammar is
  ASCII-delimited so parsing works on UTF-16 directly. Lone surrogates are not yet rejected (see TODO).
- The Go parser is written to avoid heap allocation (`RecordOutput == nil` = validate-only). The C# port exposes
  `SymbolParser.Validate` for the same use and otherwise allocates normally; benchmark before optimising.
- Errors are returned as strings from the internal reader and surfaced via `TryParse`/`Parse`
  (`SymbolParseException`), instead of Go error types.

## Per-language design decisions

_Record the semantic-analysis strategy for each indexer here as you decide it (Roslyn, tree-sitter, LSP, shelling out)._
