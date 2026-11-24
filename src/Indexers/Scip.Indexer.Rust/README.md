# Scip.Indexer.Rust

C# port of the SCIP emission of `rust-analyzer scip` (`crates/rust-analyzer/src/cli/scip.rs`, plus the symbol-scheme
part of `crates/ide/src/moniker.rs`).

## Strategy: pure native, no toolchain

The indexer never starts `cargo`, `rustc` or `rust-analyzer`. A full Rust semantic analyzer (macro expansion, trait
solving, type inference) is out of scope, so the port is two native layers:

1. **Cargo package metadata** (`CargoWorkspace`): a minimal `Cargo.toml` reader (package name/version, workspace
   `members` incl. `dir/*`, `[lib]` path/name/proc-macro, `[[bin]]`) plus cargo's target auto-discovery
   (`src/lib.rs`, `src/main.rs`, `src/bin/*`, `examples/`, `tests/`, `benches/`, `build.rs`). `Cargo.lock` is not
   needed because only workspace packages are indexed.
2. **Token-level item scanner** (`RustLexer`, `RustCrateScanner`): walks each target's module tree (`mod x;` ->
   `x.rs` / `x/mod.rs` / `#[path]`, inline modules) and emits definitions and references.
3. **Symbol scheme** (`RustMonikers`): faithful port of `moniker.rs`/`scip.rs` naming:
   `rust-analyzer cargo <package> <version|.> <target namespace>/<module>/Type#member().`, crate root namespaces
   (`lib`, `proc-macro`, `custom-build`, `bin/<name>`, `example/<name>`, `test/<name>`, `bench/<name>`, `<name>`),
   impl descriptors (`impl#[SelfType][Trait]`), locals as `local N` with `enclosing_symbol`, and the descriptor-kind
   mapping.
4. **Emission** (`RustIndexAssembler`): UTF-8 byte column positions, single-line ranges as typed `SingleLineRange`,
   `enclosing_range` including leading doc comments/attributes but not plain comments, signature and documentation,
   streamed through `Scip.IndexWriter`. Ports upstream's duplicate-symbol report and the "only the first inherent impl
   gets `SymbolInformation`" rule.

## Covered

Modules (inline and file), structs (named fields), unions, enums + variants (+ variant fields), traits (methods,
consts, assoc types), impl blocks (inherent and trait), functions/methods (kind Function/Method/StaticMethod/
TraitMethod), consts, statics, type aliases, `macro_rules!`, doc comments, function parameters and `let` bindings as
locals with lexical-scope references, items nested in function bodies (with upstream's known "no function name"
behaviour, FIXME #18771).

## NOT covered (honest gaps)

- No name resolution or type inference. References are heuristic: an identifier resolves to a local in scope, or to
  an item when that name is defined **exactly once** in the same cargo target. Method calls, field accesses, paths
  through re-exports/`use` aliases, trait method dispatch, generics, and macro-expanded code are not resolved.
- No references across crates and no `external_symbols` (dependencies are not parsed; no std/core symbols).
- Impl type display is the source text of the type, not rust-analyzer's resolved display (upstream gives
  `AddAssign<Self>`; we give `AddAssign`). Tuple-struct fields, closure parameters, `for`/`match` bindings, generic
  parameters and lifetimes produce no symbols.
- The definition occurrence emitted for an `impl` block (at the `impl` keyword) is a guess at upstream's placement.
- `cfg` attributes are ignored (all items are indexed); `#[doc = ""]` attributes are not read.
- Vendored-library handling and cargo features/build scripts (`OUT_DIR`) do not apply.

## Tests (`tests/Scip.Indexer.Rust.Tests`)

Ports the `check_symbol` tests of `scip.rs` that need no cross-crate resolution (trait items, fields, params,
locals, all target kinds, nested-in-function misbehaviour), the documentation/enclosing-range tests, plus end-to-end
and smoke tests. Upstream tests needing `deps:foo` use-site resolution or `AddAssign<Self>` display are not ported.
No external toolchain is used in tests.

Note: the project is not in `Scip.slnx` (shared file); run with
`dotnet test --project tests/Scip.Indexer.Rust.Tests`.
