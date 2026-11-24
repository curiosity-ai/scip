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
