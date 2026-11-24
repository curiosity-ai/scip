# Scip.Indexer.Clang

C and C++ SCIP indexer, ported (by behaviour, not structure) from `.reference/scip-clang`.

## Design decision: a pure C# front end

Upstream is a Clang tool: it runs the real Clang front end and walks the typed AST. A .NET program cannot embed
Clang, and this port has a hard requirement of **no runtime dependency on clang, libclang/ClangSharp or any other
child process**. So the front end is reimplemented natively:

| Stage | File | What it does |
|---|---|---|
| compile_commands.json | `CompilationDatabase.cs`, `CommandLineCleaner.cs` | `command` and `arguments` forms, POSIX-shell splitting, stripping of `-c/-o/-M*`, `-march/-mcpu/-mtune`, `-m(no-)fix-*` (port of upstream `CommandLineCleaner`); `-I/-isystem/-iquote/-D/-U/-include/-std` are honoured. Without a database, `*.c/.cc/.cpp/.cxx/.cu` files under the root are indexed with default flags. |
| Lexer | `Lexer.cs` | C/C++ tokens over UTF-8 bytes, so columns are already SCIP `UTF8CodeUnitOffsetFromLineStart`. Comments, line continuations, raw strings. |
| Preprocessor | `Preprocessor.cs` | `#if/#ifdef/#elif/#else/#endif` with full integer expression evaluation (`defined`, `__has_include`), object- and function-like macros (`#`, `##`, variadics, hide-sets), `#include` resolution (including file dir, then `-I`), `#pragma once`. Emits include and macro definition/use events. Includes that cannot be found (system headers) are skipped. |
| Parser + name resolution | `CppParser.cs`, `SymbolTable.cs` | Tolerant declaration-level C++ parser building a scope tree (namespaces, classes, enums, functions, blocks, templates), then approximate resolution of identifiers in types, initialisers and bodies (qualified names, `using namespace`, base classes, receiver types of `.`/`->` from declared variable types, overload choice by argument count). Same trade-off as upstream's `ApproximateNameResolver`, applied to *all* code since there is no type checker. |
| Glue | `TranslationUnitIndexer.cs` | One TU -> occurrences per project file; symbol construction with `Scip.SymbolFormatter` under upstream's `cxx . . $ ` prefix. |
| Driver | `ClangIndexer.cs` | Shards translation units across `--jobs N` workers (default: processors), merges occurrences per header (set semantics, so a header included by many TUs yields one document), writes with `Scip.IndexWriter`. |

Symbol naming follows upstream `SymbolFormatter.cc`: namespaces `a/`, types `T#`, fields/variables/enumerators `x.`,
functions `f(<disambiguator>).`, per-document numbered `local N` for parameters and block variables,
`<file>/path` symbols for documents and `#include` targets, `$anonymous_namespace_<file>` and
`$anonymous_type_<hash>_<n>`, macros as `path:line:col!`. Positions are single-line ranges in UTF-8 byte offsets.

CLI arguments (after the project root): `--compdb-path FILE`, `--jobs N`.

## Tests (`tests/Scip.Indexer.Clang.Tests`)

* compdb parsing (`simple.json`) and command cleaning.
* Preprocessor/lexer unit tests.
* **Upstream snapshots** (`test/index/*`): the snapshot comment annotations are parsed and compared with our output;
  each case asserts a minimum coverage (regression floor). No external compiler is used; nothing is skipped.

Measured coverage of the upstream definition/reference annotations (hashes normalised): functions 100%, vars 100%,
methods 98%, function templates 100%, ctors/dtors 86%, vars/templates 88%, vars/members 86%, namespaces 82%,
types 87%, inheritance 95%, types/templates 76%, operators 53%, body 12%.

## Known gaps (honest list)

* **No type checker.** Overloads are picked by argument count only; `auto`, return-type chaining, ADL, operator
  overload resolution (`a + b`, `x()` on objects, `[]`, `->`), implicit conversions and template argument deduction are
  not modelled. Member access on expressions of unknown type falls back to a unique-member-by-name heuristic.
* **Disambiguator hashes differ** from upstream (SHA-256 prefix of the parameter-type token text, not a hash of
  Clang's canonical type). Symbols are stable and unique per signature but not byte-identical to scip-clang, and
  cross-index linking against indexes produced by scip-clang will not match for functions.
* System headers (`<vector>`, libc) are not found, so std/libc symbols are neither resolved nor emitted as external symbols.
* No templates semantics: instantiations, explicit/partial specialisations (merged into the primary), dependent names.
* `using Base::f;` declarations, namespace-qualified references inside macro bodies, local classes inside functions
  (emitted as locals rather than `fn().C#`), anonymous-struct members, `extern "C"` function pointer typedef corner cases.
* Macro-expanded tokens produce no occurrences (only the macro use itself); macros defined/used across `#include`
  boundaries are tracked, but `#if` uses inside inactive branches are not.
* Not ported: relationships (`is_implementation` etc.), documentation comments (`showDocs`), package map / cross-repo
  symbols, `.cu`/CUDA specifics, the multi-process driver/IPC, `--emit-ipc`/robustness (crash/timeout) machinery,
  `compile_commands` response files, Windows/MSVC flags.
* A parse failure in one TU is reported and the exit code is 1, but other TUs are still emitted.
