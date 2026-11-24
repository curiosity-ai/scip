# Comparison against the upstream indexers (sanity check, 2026-11-24)

Our indexer output (merged build, 707 tests) vs. the real upstream tools run on the same pristine checkouts
(scip-typescript, scip-python, scip-ruby 0.5.1, rust-analyzer scip, scip-php 0.0.2, scip-dotnet 0.2.16, scip-java 0.12.3).
Comparison script: occurrences are matched by (document, range); symbols compared exactly and "descriptor-only"
(ignoring scheme/package/version); roles = SymbolRole bitmask. This is a sanity check, not a conformance suite.
Nothing was fixed as part of this exercise.

| Project (indexer) | Definitions P / R | References P / R | Descriptor-identical at same range | Roles overlap | Hover docs ours / upstream |
|---|---|---|---|---|---|
| serilog (dotnet) | 100 / 100 | 100 / 99.7 | 99.0% (exact 99.0%) | 100% | 99% / 100% |
| monolog (php) | 98.4 / 100 | 99.9 / 95.1 | 100% | 100% | 98% / 100% |
| gson (java) | 99.8 / 99.7 | 99.9 / 79.8 | 99.1% | 100% | 7% / 16% |
| express (typescript) | 99.7 / 99.8 | 100 / 89.7 | 94.2% | 99.9% | **1% / 100%** |
| zod (typescript) | 90.6 / 98.8 | 99.5 / 68.7 | 87.1% | 97.1% | **1% / 100%** |
| requests (python) | 99.1 / 99.9 | 99.6 / 92.8 | 42.8% | 92.1% | 31% / 71% |
| flask (python) | 99.8 / 100 | 92.4 / 89.6 | 64.7% | 93.8% | 33% / 68% |
| rack (ruby) | 97.7 / 87.5 | 97.2 / 88.5 | 93.5% | **54.4%** | **0% / 96%** |
| sinatra (ruby) | 98.0 / 89.1 | 96.0 / 77.5 | 93.6% | **60.7%** | **0% / 95%** |
| ripgrep (rust) | 95.0 / 83.5 | 99.5 / 63.3 | **39.7%** (0 common defined symbols) | 99.9% | 13% / 18% |
| shelf (dart) | — upstream scip_dart emitted an empty index (0 documents) even outside the scratch path | | | | |
| zlib, fmt (clang) | — scip-clang binaries are not downloadable here (GitHub releases blocked); not compared | | | | |

## Findings (not yet addressed)

**Close to upstream:** .NET and PHP are essentially equal on the documents upstream could index; Java definitions and symbol
descriptors match at ~99%. Ranges are good everywhere (definition precision ≥ 95% except zod 90.6%).
On Serilog upstream scip-dotnet hits the same MSBuild "Project does not contain 'Compile' target" failure we had before our fix
and indexes only 50 of 235 documents; ours matches it on those 50 and covers the rest.

**Differences that matter for interoperability**
1. **Hover documentation is missing in TypeScript (1% vs 100%) and Ruby (0% vs ~95%)**; lower in Python (31–33% vs ~70%) and Java (7% vs 16%).
2. **Ruby read-access role**: upstream marks references `ReadAccess` (8); ours emits 0 on ~6k (sinatra) / ~11k (rack) references.
3. **Python import roles**: ours `Import|ReadAccess` (10) vs upstream `ReadAccess` (8) on ~600–1,200 occurrences.
4. **Rust root-module naming**: ours `lib/…`, `bin/rg/…`, `std/…` vs upstream `crate/…`; symbols therefore don't link with upstream-produced indexes (0 common defined symbols on ripgrep). Also 17% fewer definitions and 37% fewer references (no type-aware resolution).
5. **Python module naming for `src/` layouts**: upstream `src.flask.app`, ours `flask.app` (arguably better, but not identical); version string upstream = git commit, ours = `pyproject` version.
6. **Java**: overload disambiguator numbering differs (`toJson(+5)` vs `toJson()`), so some references to overloaded methods bind to a different overload; upstream emits ~3.5× more relationships (5,898 vs 1,672); we emit 1,573 external symbols, upstream 0 (lint accepts both); upstream scheme/package prefix is `semanticdb maven …`, ours `scip-java maven …`; 40 `package-info.java` documents are not indexed by us.
7. **TypeScript**: we use typed ranges (`single_line_range`), upstream the deprecated `range` array — consumers must support the typed form (all our indexers do this; PHP/Java/Ruby/Rust/.NET also emit typed ranges). `.cts/.mts` files are skipped (zod: 470 vs 472 docs); zod shows 3.6k occurrences we mark as definitions that upstream treats as references (and 448 the other way); reference recall 68.7% on zod, 89.7% on express (no lib.d.ts/type inference, upstream resolves through the checker); module-level `const x = require()` is a global symbol for us, a local for upstream.
8. **Ruby**: definition recall 87–89%, reference recall 77–88%; no relationships (0 vs 62/46); synthetic test-DSL symbols differ.

## Reproduce
`npm i @sourcegraph/scip-typescript @sourcegraph/scip-python`; `gem install scip-ruby`; `rustup component add rust-analyzer`;
`composer require davidrjenni/scip-php` (+ `composer install --no-dev` inside the package and in the target project);
`dotnet tool install scip-dotnet` (`DOTNET_ROLL_FORWARD=Major`); scip-java from Maven Central (`com.sourcegraph:scip-java_2.13:0.12.3`,
run `com.sourcegraph.scip_java.ScipJava index` with `JAVA_TOOL_OPTIONS` unset and `-Werror` relaxed for gson on JDK 21).


## After the alignment pass (merged build, 791 tests passing, all 36 validation projects lint-clean)

Re-measured with the same script on the same upstream indexes. "descr" = symbol descriptor identical at the same range;
"exact" = whole symbol string identical (differs where the package/version part intentionally differs, e.g. Python/TypeScript/Ruby package metadata).

| Project | Defs P / R | Refs P / R | descr % | exact % | roles overlap % | docs % ours/upstream | relationships ours/upstream |
|---|---|---|---|---|---|---|---|
| serilog (dotnet) | 100 / 100 | 100 / 99.7 | 99.0 | 99.0 | 100 | 99 / 100 | 635 / 70 |
| monolog (php) | 98.4 / 100 | 99.9 / 95.1 | 100 | n/a (package prefix) | 100 | 98 / 100 | 0 / 0 |
| gson (java) | 100 / 100 | 100 / 99.1 | 99.9 | 97.5 | 100 | 7 / 16 (generated sources absent) | 1,666 / 5,898 (same on shared docs) |
| express (ts) | 100 / 99.8 | 99.9 / 96.4 | 98.8 | n/a | 100 | 97 / 100 | 0 / 0 |
| zod (ts) | 98.8 / 99.5 | 99.8 / 98.8 | 98.9 | 93.1 | 99.6 | 95 / 100 | 100 / 100 |
| requests (python) | 99.1 / 99.9 | 99.6 / 94.2 | 98.6 | n/a | 99.8 | 50 / 71 | 58 / 64 |
| flask (python) | 99.8 / 100 | 100 / 95.8 | 99.2 | n/a | 99.9 | 56 / 68 | 116 / 119 |
| rack (ruby) | 97.5 / 99.5 | 97.4 / 89.6 | 97.9 | n/a | 99.9 | 100 / 96 | 29 / 46 |
| sinatra (ruby) | 98.6 / 99.5 | 96.3 / 86.4 | 93.5 | n/a | 99.9 | 99 / 95 | 58 / 62 |
| ripgrep (rust) | 99.2 / 99.4 | 99.6 / 84.7 | 99.7 | 85.4 | 100 | 17 / 18 | 0 / 0 |

Dart (upstream `scip_dart ./` skips its own package; `scip_dart .` works — shelf was an artifact of that): collection / bloc / http —
definition recall 99 / 100 / 100 %, reference recall 92 / 89 / 90 %, relationships 645 vs 702 / 446 vs 449 / 104 vs 107.
C/C++ remains uncompared (scip-clang binaries cannot be downloaded in this environment).

### Remaining known differences
- Reference recall: Ruby 86–90 % and Rust 84.7 % (type inference), Python 94–96 %, Java external-module classpaths (~217 guava occurrences), TypeScript JS expando members (`this.x =`, `exports.x =`) and union narrowing.
- Python: docstring→markdown conversion; return-type inference beyond None/literals; external symbols 583 vs 273 (our lint needs entries upstream omits); version falls back to the git hash when pyproject parsing fails, as upstream's 0.6.8 TOML parser does.
- Java: upstream's class-signature format in 0.12.3 differs from the goldens in `.reference` (we follow the goldens); snapshot test normalises scheme/docs/package refs/enclosing_symbol.
- Rust: naming mirrors the installed rust-analyzer 1.97 binary rather than `moniker.rs`; references into external crates, `*` deref and `..` range refs missing.
- Ruby: gem version `latest` unless `--gem-metadata` is given (as upstream).
