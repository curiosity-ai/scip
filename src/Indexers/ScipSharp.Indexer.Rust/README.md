# ScipSharp.Indexer.Rust

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
3. **Symbol scheme** (`RustMonikers`): follows what the *shipped* `rust-analyzer scip` binary emits (verified against
   `rust-analyzer 1.97` on ripgrep): `rust-analyzer cargo <package> <version|.> <module>/Type#member().` where the crate root
   module is `crate/` and every other symbol starts at its first module below the root (no `lib/`/`bin/<name>`
   prefix, so lib/bin/test targets of one package share a namespace and symbols link across indexes); std crates use the
   repository URL as version and the real defining crate (`core option/Option#`, `alloc vec/Vec#`, `std path/Path#`);
   impl descriptors `impl#[SelfType][Trait]` (trait shown by name only), locals as `local N` with `enclosing_symbol`.
4. **Emission** (`RustIndexAssembler`): UTF-8 byte column positions, single-line ranges as typed `SingleLineRange`,
   `enclosing_range` including leading doc comments/attributes but not plain comments, signature and documentation,
   streamed through `ScipSharp.IndexWriter`. Ports upstream's duplicate-symbol report and the "only the first inherent impl
   gets `SymbolInformation`" rule.

## Covered

Modules (inline and file), structs (named fields), unions, enums + variants (+ variant fields), traits (methods,
consts, assoc types), impl blocks (inherent and trait), functions/methods (kind Function/Method/StaticMethod/
TraitMethod), consts, statics, type aliases, `macro_rules!`, doc comments, function parameters and `let` bindings as
locals with lexical-scope references, items nested in function bodies (with upstream's known "no function name"
behaviour, FIXME #18771).

## Resolution layer (`RustWorld`, `RustResolver`, `RustStd`, `RustAnalysis`)

After all targets are scanned, `RustAnalysis.Analyze` builds a cross-crate item graph and resolves references:

- module tree (`mod x;`, `#[path]`, inline), `use` trees (nested braces, `self`, `as` aliases, globs, `crate`/`super`/`self`),
  paths through modules, enums (variants), types (inherent/trait impl items, trait items);
- workspace crates and `path = "..."` dependencies from `[dependencies]` (also the package's own lib from bin/test
  targets); path deps outside the project root are resolved and reported as `external_symbols`;
- a built-in model of `core`/`alloc`/`std` (`RustStd`): symbols such as
  `rust-analyzer cargo core https://github.com/rust-lang/rust/library/core core/option/Option#Some#`, std re-exports,
  the prelude, and the signatures of common methods (Option/Result/Vec/String/str/slice/HashMap/HashSet/BTreeMap/
  VecDeque/Rc/Arc/RefCell/Iterator ...). Referenced std items are emitted as `external_symbols`;
- simple type tracking: params, `let x: T`, `let x = Path::ctor(..)` / struct literal / method-call chains, `self`,
  fields (incl. tuple fields `.0`), `?`, indexing, `for`/`if let Some(x)` bindings, smart-pointer/String/Vec deref,
  generic substitution through impl self types; method calls and field accesses resolve to the impl item (trait
  default methods resolve to the trait item);
- struct-literal/pattern field references, operator overloading (`s += t` resolves to `impl[S][AddAssign<Self>]add_assign()`),
  tuple struct/variant fields (`Foo#0.`), generic/lifetime parameters (locals enclosed by the item), closure parameters,
  `for`/`if let`/`while let` bindings.

## NOT covered (honest gaps)

- No full type inference / trait solving: expression types come from the simple model above. Method calls on
  generic parameters (`T: Trait`), closures' return types, iterator adaptor item types, `match` arm bindings,
  `impl Trait` returns, associated-type projections and macro-expanded code are not resolved.
- Only a small, hand-written part of the standard library is modelled (and impl descriptors of std items are
  approximate, e.g. `Vec<T, A>`); other `std::*` paths produce no reference. Third-party registry crates
  (non-path dependencies) are not parsed.
- Impl type display is the source text of the type except for the defaulted `Rhs = Self` operator traits.
- `cfg` attributes are ignored; derives/attribute macros and `#[doc = ""]` are not interpreted; `macro_rules`
  bodies are skipped (invocations are resolved, expansions are not).
- Local numbering follows declaration order, not rust-analyzer's token order.
- Vendored-library handling and cargo features/build scripts (`OUT_DIR`) do not apply.

## Tests (`tests/ScipSharp.Indexer.Rust.Tests`)

Ports all `check_symbol` tests of `scip.rs` (`ScipSymbolTests` for single-file ones, `ResolutionTests` for the
multi-crate `deps:foo` fixtures expressed as path dependencies, `operator_overload`, `basic`, struct-literal field,
param/closure-param/local, `global_symbol_for_pub_struct[_reference]`), the documentation/enclosing-range tests, plus
resolution tests (imports, globs, method calls, generics, std) and end-to-end/smoke tests. Smoke run on
`.reference/rust-analyzer/crates/syntax`: ~13.8k definitions + ~36k references, 135 external symbols. No external
toolchain is used in tests.

Note: the project is not in `ScipSharp.slnx` (shared file); run with
`dotnet test --project tests/ScipSharp.Indexer.Rust.Tests`.
