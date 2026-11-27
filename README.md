# SCIP for .NET

A C# / .NET 11 port of [SCIP](https://github.com/scip-code/scip), the Code Intelligence Protocol — the protocol
library, the `scip` CLI, and the per-language indexers
(Java, TypeScript, Rust, C/C++, Ruby, Python, C#, Dart, PHP, ABAP), plus native indexers for languages without an upstream
SCIP indexer (COBOL, Visual Basic 6, PL/I, Fortran, Delphi, Pascal, SQL).

> **Status: working port, partial fidelity.** The core library, the `scip` CLI commands and all seventeen indexers are implemented
> and tested: nine pure-C# ports of the upstream `scip-*` indexers, an ABAP indexer built on a C# port of abaplint, and seven
> original native indexers, all of varying completeness. See [TODO.md](TODO.md) for the roadmap.

## Layout

```
src/ScipSharp              core library (protobuf types, symbols, index writer)
src/ScipSharp.Indexing     indexer abstractions
src/Indexers/*        one project per language indexer
src/ScipSharp.Cli          the `scip` dotnet tool
tests/ScipSharp.Tests      TUnit tests
.reference/           upstream Go SCIP and the language indexers being ported (read-only)
```

## Install

```bash
dotnet tool install -g ScipSharp.Cli  # the `scip` command
scip index python --project-root .   # also: dotnet, java, typescript, rust, clang, ruby, dart, php, abap,
                                     #       cobol, vb6, pli, fortran, delphi, pascal, sql
scip print index.scip
```

NuGet packages (all published from `main` by `.azure-devops/build-nuget.yml`):

| Package | Contents |
|---|---|
| `ScipSharp.Cli` | the `scip` .NET tool, bundling every indexer |
| `ScipSharp` | core library: protobuf types, symbols, positions, index writer (namespace `ScipSharp`) |
| `ScipSharp.Indexing` | indexer abstractions (`IIndexer`) |
| `ScipSharp.Indexer.DotNet` / `.Java` / `.TypeScript` / `.Python` / `.Ruby` / `.Rust` / `.Clang` / `.Dart` / `.Php` / `.Abap` | one package per language indexer |
| `ScipSharp.Indexer.Cobol` / `.Vb6` / `.Pli` / `.Fortran` / `.Delphi` / `.Pascal` / `.Sql` | one package per native language indexer |

## Build & test

Requires the .NET 11 SDK.

```bash
dotnet build
dotnet test
dotnet run --project src/ScipSharp.Cli -- --help
dotnet pack ScipSharp.slnx -c Release /p:Version=1.0.0   # packages land in artifacts/package/release
```

## Library example

```csharp
using ScipSharp;

Symbol symbol = SymbolParser.Parse("lsif-java maven package 1.0.0 java/io/File#Entry.method(+1).");
string text = SymbolFormatter.Verbose.Format(symbol);
```

## Upstream

The original Go implementation, schema and docs live in [`.reference/scip`](.reference/scip); the original README is
at [`.reference/scip/README.md`](.reference/scip/README.md). See [CLAUDE.md](CLAUDE.md) for contributor guidance and
[docs/PORTING.md](docs/PORTING.md) for the source map.

Licensed under Apache-2.0 (see [`.reference/scip/LICENSE`](.reference/scip/LICENSE); the root `LICENSE` file mirrors it).
