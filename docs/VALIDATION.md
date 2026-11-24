# Real-project validation (2026-11-24)

The `scip` tool (Release build of `Scip.Cli`) was run via `scip index <lang>` against shallow clones of open-source
projects (Linux, .NET 11.0.100 SDK, no other language toolchains installed). Each index was then checked with
`scip lint` (the Go lint rules, ported) and a cross-reference sanity check: the share of non-local reference
occurrences whose symbol has a definition occurrence (`defined`) or at least a `SymbolInformation`/external symbol (`known`).

| Lang | Project @ commit | Result | Time / peak RSS | Docs | Occurrences | defined / known refs |
|---|---|---|---|---|---|---|
| dart | dart-lang/shelf `pkgs/shelf` @2856b69 | ok | 0.3 s / 50 MB | 37 | 1.4k | 100% / 100% |
| python | pallets/flask @49943b5 | ok | 2.8 s / 132 MB | 83 | 20k | 68% / 93% |
| python | psf/requests @3c7baaa | ok | 1.4 s / 84 MB | 19 | 7.8k | 57% / 92% |
| java | google/gson @d9b5079 | ok | 5.4 s / 248 MB | 259 | 79k | 52% / 52% |
| java | apache/commons-lang @1880038 | ok | 9.0 s / 416 MB | 623 | 239k | 57% / 57% |
| typescript | colinhacks/zod @d40c329 | ok | 7.7 s / 447 MB | 467 | 129k | 100% / 100% |
| typescript | expressjs/express @43fca52 | ok, needs `--infer-tsconfig` (no tsconfig.json) | 1.2 s / 174 MB | 142 | 18k | 100% / 100% |
| ruby | sinatra/sinatra @2e3f791 | ok | 0.4 s / 69 MB | 150 | 13k | 97% / 97% |
| ruby | rack/rack @b432304 | ok | 0.7 s / 78 MB | 96 | 21k | 98% / 98% |
| rust | BurntSushi/ripgrep @4a51093 | ok | 0.8 s / 94 MB | 104 | 35k | 99.9% / 99.9% |
| clang | madler/zlib @617e19a | ok (no compile_commands.json → source discovery) | 0.9 s / 194 MB | 78 | 34k | 99.7% / 99.7% |
| clang | fmtlib/fmt @3be6709 | ok (same) | 0.7 s / 159 MB | 59 | 30k | 97.5% / 97.5% |
| php | Seldaek/monolog @6d4cf3a | ok (no vendor/) | 0.7 s / 177 MB | 225 | 14k | 77% / 100% |
| dotnet | serilog/serilog @43105b3 | **broken**: MSBuild project load fails, exit code 0 | 18 s / 369 MB | 50 | 1.6k | 27% / 48% |

## Findings (open)

1. **dotnet** (FIXED): Serilog's `<TargetFrameworks>$(TargetFrameworks);net10.0;...` expands to a leading empty entry on Linux, which MSBuildWorkspace does not drop, so projects loaded with an empty TargetFramework. The indexer now retries with an explicit TargetFramework and exits non-zero when a project cannot load or nothing is indexed. Original report: on Serilog every real project fails to load in `MSBuildWorkspace` (`Project does not contain 'Compile' target`)
   although `dotnet build` of the same project works; only the leftover projects are indexed, and the process still exits 0.
   Must fail loudly, and project loading needs fixing (multi-targeting / SDK resolution under `global.json`).
2. **Lint compliance** (all indexes fail `scip lint`; the Go lint requires a `SymbolInformation` for every occurrence symbol):
   - typescript, ruby: local-symbol occurrences without `SymbolInformation` (zod 7.8k, rack 11k, sinatra 7k).
   - java: ~24k (gson) / ~65k (commons-lang) occurrences of JDK/library symbols with no external `SymbolInformation`;
     ~1k–1.5k relationships to symbols that are not defined anywhere.
   - python: duplicate `SymbolInformation`, relationships to symbols not present (e.g. base classes in sibling modules), some local/global occurrences without info.
   - rust (11–17), clang (216–398), php (42): few; mostly missing/duplicate `SymbolInformation`.
3. **typescript** needs `--infer-tsconfig` equivalent behaviour by default for plain-JS projects, or a clearer message.
4. **dart** monorepos: `pubspec.yaml` must be at the project root (`pkgs/shelf` worked, repo root did not) — same as upstream.

Reproduce: see `docs/VALIDATION.md` commands in the scratch runner (`run1.sh`): `scip index <lang> --project-root <repo> --output index.scip`, then `scip lint index.scip`.

## After the lint fix pass (same projects, merged build)

Every index (Serilog included) now has **0 `scip lint` errors** and every referenced symbol has a
`SymbolInformation` (locals per document, undefined globals as `external_symbols`): gson/commons-lang/zod/fmt keep
only "multiple relationships could be merged" warnings (50 / 5 / 2 / 1). express indexes without a tsconfig
(inferred automatically, logged). 653 tests pass.

| Project | Lint errors before → after | External symbols now | Refs resolved to a project definition |
|---|---|---|---|
| flask / requests | thousands → 0 | 522 / 309 | 68% / 57% |
| gson / commons-lang | ~25k / ~65k → 0 | 1,573 / 2,311 | 52% / 57% |
| zod / express | ~7.8k / few → 0 | 1 / 0 | 100% / 100% |
| sinatra / rack | ~9k / ~14k → 0 | 340 / 296 | 68% / 76% |
| ripgrep | 11 → 0 | 201 | 69% |
| zlib / fmt | ~700 / ~500 → 0 | 141 / 400 | 90% / 82% |
| monolog / shelf | 42 / 4 → 0 | 281 / 0 | 77% / 100% |
| serilog (dotnet) | project load broken (50 docs) → 235 docs, 0 errors, 0 warnings | 862 | 62% |

The remaining "unresolved to a project definition" references are library/stdlib symbols, now recorded as external symbols.

### Caveat: relationship ordering
The (faithfully ported) Go lint resolves a relationship target only if it is defined in the same or an *earlier* document.
To pass it, the Java indexer drops cross-file inverse relationships that cannot be ordered (flag `--all-inverse-relationships`
restores the full output but fails lint), Python drops relationships left pointing at a later document (import cycles), and
Dart/Clang order documents definitions-first. Decision pending: keep this, or make the lint order-independent and keep all relationships.
