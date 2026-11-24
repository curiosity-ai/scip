# Scip.Indexer.Ruby

Pure native C# port of the SCIP emission of [`scip-ruby`](../../../.reference/scip-ruby) (a Sorbet fork).
**No runtime dependency on Ruby, Sorbet, `scip-ruby`, Bundler or any child process.** External Ruby tooling is
never needed; the test suite only optionally reads an installed Ruby stdlib as sample input (skipped when absent).

## Strategy and why

Sorbet's real pipeline is parser -> desugar -> namer -> resolver -> CFG -> type inference (`infer`), with a
serialized payload of the Ruby core/stdlib RBIs. `scip_indexer/` hooks into the typechecker's extension API and uses
inferred types to resolve receivers (`x.foo`, `Class#new()`, `Kernel#puts()`...). That cannot be ported wholesale.
Instead this project reimplements, natively:

| Upstream (`scip_indexer/`) | C# | Notes |
|---|---|---|
| `SCIPUtils.cc` descriptor/symbol emission, `SCIPSymbolRef.cc` naming | `RubyProject.cs` (`TypeDescriptors`, `MethodSymbol`, ...) via `Scip.SymbolFormatter` | Same scheme (`scip-ruby gem <name> <version> ...`), classes/modules as `Type` (`A#B#`), methods `m().`, constants and fields as `Term`, singleton classes `` `<Class:Foo>`# `` as *siblings* of `Foo`, top-level methods on `Object#`, globals under the `_global_ latest` placeholder gem |
| `SCIPGemMetadata.cc` | `GemMetadata.cs` | `Gemfile.lock` PATH spec, `.gemspec`, directory-name fallbacks, `--gem-metadata name@version`; unit tests ported |
| Sorbet lexer/parser/namer/resolver (the parts SCIP needs) | `RubyLexer.cs`, `RubyWalker.cs`, `RubyProject.cs` | Hand-written tokenizer + structural walker (keyword/`end` matching, modifier-vs-block disambiguation, `class << self`, endless defs, heredocs, `#{}` interpolation, percent literals) and a two-pass resolver (project-wide tables first, then lexical constant lookup, ancestors via `include`/`prepend`/`extend`/superclass, method lookup) |
| `SCIPIndexer.cc` document/occurrence emission | `RubyIndexer.cs` | `Scip.IndexWriter`; UTF-8 byte-offset typed ranges, enclosing ranges for classes/modules/methods, `SymbolInformation.kind` for definitions |

There is **no type inference**: only references that are resolvable from syntax and the project's own definitions are
emitted. Precision is favoured over recall; an unresolvable name produces no occurrence rather than a guess.

## Usage

`scip index ruby [--gem-metadata name@version]` (CLI wiring is outside this project). Indexes `*.rb`, `*.rbi`,
`*.rake`, `*.gemspec` under the project root, skipping `.git`, `vendor`, `node_modules`, `tmp`, `coverage`, `log`,
`.bundle`, and `sorbet/rbi/{gems,annotations,dsl}`.

## What is covered

- Definitions (with enclosing ranges): classes, modules (incl. `class A::B`, reopening across files), instance and
  singleton methods (`def self.x`, `def Const.x`, `class << self`, operator and setter names, endless defs),
  `attr_reader/writer/accessor`, constants, instance/class/global variables, locals, block/lambda parameters,
  method parameters, `for` variables, multiple assignment, `rescue => e`.
- References: constants (lexical scope, ancestors, `A::B::C` chains, `::Abs`), locals, implicit-self and `self.`
  method calls resolved through the project's inheritance/mixins, `Const.method` singleton calls (including
  `extend`ed modules), `Const.new` -> `initialize`, instance/class/global variable uses.
- `.rbi` files are indexed as ordinary Ruby (their `def`/`class` are syntax); `sig { ... }` blocks are walked as
  normal blocks.

## Honest gaps versus upstream

Measured against the upstream fixtures in `tests/Scip.Indexer.Ruby.Tests/Fixtures/snapshots` (113 fixtures; only
definitions and references to symbols defined in the same fixture are compared, everything else upstream emits needs
Sorbet's payload or inference): 37 fixtures match exactly; overall 1086/1423 (~76%) of expected definitions and
711/1427 (~50%) of those in-file references are reproduced.

- No type inference: `x.foo` on a non-constant, non-`self` receiver is not resolved; no flow typing, no
  union/intersection receivers, no generics/type members/`T.let`/`T.attached_class` handling.
- No Ruby core/stdlib or Sorbet RBI payload: no references to `Kernel#puts()`, `Class#new()`, `T#Boolean.` etc.
- No DSL rewriters: `T::Struct` `prop`/`const`, `T::Enum` `enums do`, `delegate`, `def_delegator`, `cattr_*`,
  `mattr_*`, RSpec/Minitest `describe`/`it`, `module_function`, `alias`/`alias_method`, `Struct.new`,
  `define_method`-style metaprogramming produce no definitions.
- Keyword parameters are emitted as locals, not as `Method().(param)` global parameter symbols; no `relationships`
  (inheritance/override edges), no hover documentation/signatures, no write-access references around assignments.
- `sorbet/rbi/{gems,annotations,dsl}` are skipped (upstream maps them to external gems via per-file gem metadata;
  `--gem-map-path` is not implemented). Heredoc bodies are not scanned for `#{}` references.
- The lexer is heuristic (value-vs-operator decisions for `/`, `%`, `<<`, `?`, `{`); pathological input may
  desynchronise block matching for the remainder of a file (documents are still emitted).

## Tests

`tests/Scip.Indexer.Ruby.Tests` (TUnit): ported `GemMetadataInference` cases; 37 upstream snapshot fixtures asserted
exactly plus an overall-recall floor over all fixtures; targeted unit tests for naming, resolution, ranges and
encodings; an end-to-end index write/parse; a stdlib robustness smoke test (skips when no Ruby is installed).
Run: `dotnet test --project tests/Scip.Indexer.Ruby.Tests`. The project is not in `Scip.slnx` (shared file).
