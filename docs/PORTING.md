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
| (none — native) | `src/Indexers/ScipSharp.Indexer.Cobol` | new indexer (see TODO.md for gaps) |
| (none — native) | `src/Indexers/ScipSharp.Indexer.Vb6` | new indexer (see TODO.md for gaps) |
| (none — native) | `src/Indexers/ScipSharp.Indexer.Pli` | new indexer (see TODO.md for gaps) |
| (none — native) | `src/Indexers/ScipSharp.Indexer.Fortran` | new indexer (see TODO.md for gaps) |
| (none — native) | `src/Indexers/ScipSharp.Indexer.Delphi` | new indexer (see TODO.md for gaps) |
| (none — native) | `src/Indexers/ScipSharp.Indexer.Pascal` | new indexer (see TODO.md for gaps) |

## Key differences from the Go implementation

- Go's `beaut.UTF8String` validation has no .NET counterpart; .NET strings are UTF-16. The symbol grammar is
  ASCII-delimited so parsing works on UTF-16 directly. Lone surrogates are not yet rejected (see TODO).
- The Go parser is written to avoid heap allocation (`RecordOutput == nil` = validate-only). The C# port exposes
  `SymbolParser.Validate` for the same use and otherwise allocates normally; benchmark before optimising.
- Errors are returned as strings from the internal reader and surfaced via `TryParse`/`Parse`
  (`SymbolParseException`), instead of Go error types.

## Per-language design decisions

_Record the semantic-analysis strategy for each indexer here as you decide it (Roslyn, tree-sitter, LSP, shelling out)._

### Indexers without an upstream SCIP project

COBOL, Visual Basic 6, PL/I, Fortran, Delphi, Pascal and SQL have no `scip-*` indexer to port, so these are original
implementations. Each one reports `UpstreamProject` as `(none: native implementation)`, follows the same pure-native rule
(no compiler or toolchain at runtime), and documents its symbol scheme, options, research sources and gaps in its own
`README.md`. Test corpora are permissively licensed copies recorded in each test project's `testdata/SOURCES.md`.

- **COBOL** (`cobol`): a reference-format-aware lexer (fixed/free/variable, indicator column, continuation,
  `>>SOURCE`/`$SET`), a text-manipulation phase (recursive `COPY … REPLACING` incl. IBM partial-word `:TAG:`/`(PFX)`
  replacement, `REPLACE`, `EXEC SQL INCLUDE`) that keeps each token's copybook position, a tolerant structural parser
  (programs/nesting, data hierarchy 01–49/66/77/78/88, files, sections/paragraphs) and a name-based resolver (OF/IN
  qualification, GLOBAL scoping, PERFORM/GO TO/ALTER targets, CALL/CICS program targets across the project). There is no
  full statement grammar. Definitions written in a copybook are named under the copybook (`` `cpy/X.cpy`/REC.FIELD. ``)
  so copybook fields are one symbol across programs.
- **Visual Basic 6** (`vb6`): a hand-written lexer/preprocessor/declaration parser plus a scoped, best-effort typed
  resolver; no VB6/VBA/COM dependency and no type-library reading. `#If` is evaluated with VB6 compiler constants, `.vbp`
  `CondComp` and `--define`; designer blocks give form/control symbols; event handlers and `Implements` methods are bound
  by VB's `X_Y` naming rule, with relationships. Sources decode as UTF-8 when valid, else as the ANSI code page (default
  1252). Symbols are `scip-vb6 vbp <vbp Name> <Major.Minor.Revision> Module#Member…`; a file listed by several projects is
  indexed once, under the first.
- **PL/I** (`pli`): PL/I reserves no keywords, so instead of a full grammar the token stream is split into statements and
  each is classified by context (assignment detection first, then leading and option keywords at paren depth 0). Source
  margins (default 2,72, or `*PROCESS MARGINS`) are applied by blanking, so positions keep original columns. A first pass
  builds the block tree and declarations (which apply to their whole block); a second resolves names, including partially
  qualified structure references and LIKE/TYPE templates. External procedures/entries are top-level descriptors linked
  across files; `%INCLUDE` members are separate documents; unresolved names fall back to built-ins, external entries, then
  implicit locals.
- **Fortran** (`fortran`): a source reader for fixed and free form plus best-effort cpp (all `#if` branches by default,
  evaluated with `-D`) that splices `INCLUDE`/`#include` into the includer's scope. A keyword-agnostic statement
  classifier feeds two passes: pass 1 collects scopes and declarations for every file (modules, submodules, external
  procedures, COMMON blocks program-wide); pass 2 resolves through use/host association, `IMPORT`, implicit typing and `%`
  component chains. Symbols are `scip-fortran <fpm|.> <pkg> <ver>` with lower-cased names; metadata from `fpm.toml`.
- **Delphi** (`delphi`): lexer plus `{$…}` preprocessor (defines, `{$IF}` expressions, includes with their own file id),
  an error-tolerant recursive-descent parser keeping statement blocks as token ranges, and a two-pass binder/walker with
  lightweight type flow (aliases, generic substitution, default array properties, helpers, `with`, `inherited`). RTL/VCL/
  FMX symbols come from embedded declaration-only stub units parsed by the same parser. Unprefixed `uses` are mapped
  through `DCC_Namespace` and the default unit scope names so Delphi 7 and Delphi 12 code share symbols; dotted unit names
  are one namespace descriptor per segment (`System/SysUtils/`).
- **Pascal** (`pascal`, ISO/Extended, Turbo, Free Pascal, Lazarus): a mode-aware lexer and preprocessor (modes, `{$IF}`
  evaluation, includes, cpp lines) feeding a one-pass parser that binds as it goes, like Wirth-style compilers. Units
  compile in two phases on demand so circular implementation `uses` work; expression types are tracked along designators
  for members, `with`, ancestors, helpers and generics. External units resolve against a hand-written RTL/LCL summary
  table. Lazarus `.lpi`/`.lpk` give package names, versions, search paths and modes; `.lfm` forms are indexed. Kept
  separate from the Delphi indexer because indexer projects may only reference `ScipSharp.Indexing`.
