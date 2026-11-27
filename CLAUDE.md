# CLAUDE.md

Guidance for working in this repository.

## What this is

A port of [SCIP](https://github.com/scip-code/scip) (Code Intelligence Protocol) — core library, CLI and the
per-language indexers — to **C# on .NET 11**, using modern C# (`LangVersion latest`, nullable, file-scoped
namespaces, primary constructors, collection expressions, `Span`-based parsing where it matters).

## Repository layout

| Path | Purpose |
|---|---|
| `src/ScipSharp` | Core library: protobuf types (generated), `SymbolParser`, `SymbolFormatter`, `IndexWriter`. Port of `bindings/go/scip`. |
| `src/ScipSharp.Indexing` | `IIndexer` abstraction + shared helpers used by every language indexer. |
| `src/Indexers/ScipSharp.Indexer.*` | One project per language indexer (Java, TypeScript, Rust, Clang, Ruby, Python, DotNet, Dart, Php, Abap; native, no upstream: Cobol, Vb6, Pli, Fortran, Delphi, Pascal, Sql). |
| `src/ScipSharp.Cli` | The `scip` dotnet tool (port of `cmd/scip`) + `scip index <lang>` entry points. |
| `tests/ScipSharp.Tests` | TUnit tests (Microsoft.Testing.Platform). |
| `tools/sync-proto.sh` | Regenerates `src/ScipSharp/Protos/scip.proto` from `.reference/scip/scip.proto`. |
| `.azure-devops/` | Build/test/pack/publish pipeline (NuGet packages + the `scip` tool). |
| `.reference/` | **Read-only** upstream sources (Go SCIP + the per-language indexers). See below. |
| `TODO.md` | The port backlog. Keep it current: tick items as you finish them, add what you discover. |

## `.reference/` — upstream code, do not edit

Shallow clones (no `.git`) of the upstream projects; pinned commits are listed in `.reference/SOURCES.md`.
- All upstream `.github/workflows/*.y(a)ml` files were renamed `*.disabled`. Do not re-enable them.
- Never build, format or modify anything in `.reference/`. It is only for reading while porting.
- `.reference/scip` is the original Go repo (`scip.proto`, `bindings/go/scip`, `cmd/scip`, `docs/`).

## Commands

```bash
export PATH=/opt/dotnet:$PATH        # if the SDK was installed with dotnet-install into /opt/dotnet
dotnet build                          # whole solution (warnings are errors)
dotnet test                           # runs TUnit tests via Microsoft.Testing.Platform (see global.json)
dotnet run --project src/ScipSharp.Cli -- print index.scip
tools/sync-proto.sh                   # after upstream scip.proto changes
```

Build output goes to `artifacts/` (via `ArtifactsPath`), not next to the projects.

## Conventions

- Target `net11.0`; shared settings live in `Directory.Build.props`, package versions in `Directory.Packages.props`
  (central package management — never put a `Version` on a `PackageReference`).
- Port **behaviour**, not Go structure. Prefer idiomatic C# (records, pattern matching, `IAsyncEnumerable`,
  `System.CommandLine`) over transliterating Go. Port the upstream tests alongside the code and keep their cases.
- Protobuf: the message `Descriptor` is generated as `SymbolDescriptor` (C# forbids a member named like its type).
  Wire format is unchanged. The enum is `SymbolDescriptor.Types.Suffix`.
- UTF-16 strings are fine for SCIP symbol parsing: every delimiter is ASCII. SCIP *positions*, however, are defined
  in terms of the document's `position_encoding` — never assume UTF-16 offsets are the wire encoding.
- Indexer projects reference only `ScipSharp.Indexing` (and via it `ScipSharp`); the CLI is the only project that references them all.
- Not-yet-ported indexers derive from `NotImplementedIndexer` and exit with code 2. Replace the base class when
  you implement one, and update `TODO.md`.
- **Pure native port.** Indexers must not depend at runtime on external compilers/toolchains (node/tsc, javac, clang,
  rustc, ruby, python, dart, php, language servers, plugin jars). Implement lexers/parsers/resolution in C#. External
  toolchains are allowed only in tests (cross-checks), which must skip gracefully when the tool is unavailable.
- CI/CD is the Azure DevOps pipeline `.azure-devops/build-nuget.yml` (modelled on `.reference/tesserae` and hnsw-sharp): a single Windows job that
  builds/packs `ScipSharp.Packages.slnx` (packable `src` projects only) and pushes from `main`; it does **not** run tests (run `dotnet test` locally). Packages:
  `ScipSharp`, `ScipSharp.Indexing`, `ScipSharp.Indexer.<Lang>` (one per language), `ScipSharp.Cli` (dotnet tool `scip`); projects, assemblies, namespaces
  and NuGet ids all use the `ScipSharp` prefix (the plain `Scip` NuGet id was taken). Package metadata lives
  in `Directory.Build.props`; version comes from the pipeline (`/p:Version=yy.M.<buildId mod 65535>`, since assembly version parts must fit in 16 bits). `ScipSharp.Reprolang` and tests
  are not packable. Don't add GitHub workflows without being asked.

## Porting workflow for an indexer

1. Read the upstream project in `.reference/<project>` — start with its README, entry point and tests.
2. Decide how semantics are obtained in .NET (Roslyn for C#; tree-sitter / language servers / spawning the
   upstream language toolchain for others — record the decision in `docs/PORTING.md`).
3. Implement against `IIndexer`; stream output with `ScipSharp.IndexWriter`.
4. Port the upstream snapshot tests; compare output against `.reference` expectations.
5. Tick the item in `TODO.md`.
