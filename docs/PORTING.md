# Porting notes

## Source map

| Upstream (`.reference/…`) | C# destination | Status |
|---|---|---|
| `scip/scip.proto` | `src/Scip/Protos/scip.proto` (via `tools/sync-proto.sh`) | done |
| `scip/bindings/go/scip` | `src/Scip` | symbols + index writer done |
| `scip/cmd/scip` | `src/Scip.Cli` | done |
| `scip/reprolang` | (planned) `src/Scip.Reprolang` | done |
| `scip-dotnet` | `src/Indexers/Scip.Indexer.DotNet` | ported (see TODO.md for gaps) |
| `scip-typescript` | `src/Indexers/Scip.Indexer.TypeScript` | ported (see TODO.md for gaps) |
| `scip-java` | `src/Indexers/Scip.Indexer.Java` | ported (see TODO.md for gaps) |
| `rust-analyzer` (SCIP emitter only) | `src/Indexers/Scip.Indexer.Rust` | ported (see TODO.md for gaps) |
| `scip-clang` | `src/Indexers/Scip.Indexer.Clang` | ported (see TODO.md for gaps) |
| `scip-ruby` | `src/Indexers/Scip.Indexer.Ruby` | ported (see TODO.md for gaps) |
| `scip-python` | `src/Indexers/Scip.Indexer.Python` | ported (see TODO.md for gaps) |
| `scip-dart` | `src/Indexers/Scip.Indexer.Dart` | ported (see TODO.md for gaps) |
| `scip-php` | `src/Indexers/Scip.Indexer.Php` | ported (see TODO.md for gaps) |

## Key differences from the Go implementation

- Go's `beaut.UTF8String` validation has no .NET counterpart; .NET strings are UTF-16. The symbol grammar is
  ASCII-delimited so parsing works on UTF-16 directly. Lone surrogates are not yet rejected (see TODO).
- The Go parser is written to avoid heap allocation (`RecordOutput == nil` = validate-only). The C# port exposes
  `SymbolParser.Validate` for the same use and otherwise allocates normally; benchmark before optimising.
- Errors are returned as strings from the internal reader and surfaced via `TryParse`/`Parse`
  (`SymbolParseException`), instead of Go error types.

## Per-language design decisions

_Record the semantic-analysis strategy for each indexer here as you decide it (Roslyn, tree-sitter, LSP, shelling out)._
