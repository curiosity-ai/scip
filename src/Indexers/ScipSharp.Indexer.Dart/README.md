# ScipSharp.Indexer.Dart

C# port of [`scip-dart`](https://github.com/Workiva/scip-dart) (`.reference/scip-dart`). Usage: `scip index dart [root] [--index-pubspec] [--verbose]`.

## Strategy: pure native C#, no Dart toolchain

Upstream gets all semantics from Dart's `analyzer` package (fully resolved ASTs and element models). Nothing equivalent
exists for .NET, and this port must not depend on `dart`, `pub` or the analyzer at runtime. So the indexer is a
self-contained, **syntactic** indexer:

| Piece | Implementation |
|---|---|
| `DartLexer` | Tolerant tokenizer: comments, `///` docs, raw/triple-quoted strings, `${}`/`$id` interpolation (interpolated identifiers become tokens). UTF-16 offsets. |
| `DartParser` | Declaration-level parser: classes (all Dart 3 modifiers), mixins, enums (constants, members), extensions (named and unnamed), typedefs, functions, local functions, variables/fields, getters/setters, operators, constructors (unnamed, named, factory, redirecting), parameters (named / positional / `this.x` / `super.x` / function-typed), type parameters, import prefixes, directives. Bodies and initialisers are skipped, not parsed. |
| `ProjectIndexer` | Whole-package resolution **by name**: top-level declarations of every file, `Type.member` / `this.member` / unqualified member references, constructor calls, supertypes (`extends`/`with`/`implements`/`on`) with transitive relationships, `prefix.Name`, type-parameter references, import/export/part file references. Shadowing by parameters and local variables is approximated heuristically. |
| `Pubspec` / `PubspecIndexer` | Native, minimal YAML reader for `pubspec.yaml` and `pubspec.lock`; `--index-pubspec` ports upstream's pubspec document. `.dart_tool/package_config.json` is not needed: package versions come from `pubspec.lock`. |
| Output | `ScipSharp.SymbolFormatter` builds every symbol; `ScipSharp.IndexWriter` streams metadata, documents and external symbols. |

## Symbol format (identical to upstream)

`scip-dart pub <package> <version> <file-namespace>/<descriptors>` e.g.
``scip-dart pub demo 1.0.0 lib/`a.dart`/Thing#`<get>label`.``; constructors are ``Thing#`<constructor>`().``, named
parameters `Thing#run().(times)`, type parameters `Thing#[T]`, private names and positional parameters are `local N`.
Unnamed extensions reproduce upstream's `null#` quirk.

Differences from upstream output: ranges use the typed `SingleLineRange`/`MultiLineRange` fields, the document
declares `PositionEncoding = UTF16`, `text_document_encoding` is UTF-16 (upstream declares UTF-8 while emitting UTF-16
columns), and version `.` is used for a pubspec without `version`.

## Tests (`tests/ScipSharp.Indexer.Dart.Tests`)

* Every Dart file of upstream's `basic-project` and `dart3-features` snapshots: **definitions of non-local symbols (name,
  position, full symbol) match the analyzer-generated snapshots exactly** (no missing, no extra).
* Reference precision against the same snapshots (>90% of our own-package references are present upstream; recall >50%).
* Unit tests: symbol shapes, relationships, cross-file references and directives, local shadowing, nested packages,
  UTF-16 positions, end-to-end `IndexAsync`, pubspec parsing/indexing, lexer.
* No external toolchain is required by tests (the upstream snapshot files are read as fixtures).

## Known gaps (honest list)

No type resolution, so these are **not** indexed:

* References to SDK / third-party symbols (`int`, `List`, `print`, `package:` types) and therefore no external symbols
  except `package:` file references; `dart:` directives are skipped (upstream uses the SDK version).
* Instance-member access on expressions (`obj.method()`, `a.b.c`), cascades, named-argument labels (`Foo(x: 1)`),
  extension-member calls, `super`/inherited calls through other receivers, implicit `.call`.
* Local variables and their references (`final x = ...`), pattern variables, closure parameters; only local *functions*
  are defined. Because of that, `local N` numbering differs from upstream (positional parameter and private-name
  locals only), and references are approximated by name (a local shadowing a top-level name in an unusual way can still
  produce a wrong reference).
* Diagnostics and `Unused`/`Deprecated` tags; `displayString` signature documentation is the source text of the
  declaration header, not the analyzer's rendering.
* Documentation `///` only (not `/** */`); no `part of library.name;` handling.
* Constructor references through redirecting factories and `Foo.new`; tear-offs of constructors.
* Resolution is by name across the whole package and ignores `import ... show/hide` and library boundaries.
* `.dart` files under `build/` and other non-hidden dirs are indexed like upstream; generated `part` semantics are not modelled.
