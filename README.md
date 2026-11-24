# SCIP for .NET

A C# / .NET 11 port of [SCIP](https://github.com/scip-code/scip), the Code Intelligence Protocol — the protocol
library, the `scip` CLI, and the per-language indexers
(Java, TypeScript, Rust, C/C++, Ruby, Python, C#, Dart, PHP).

> **Status: working port, partial fidelity.** The core library, the `scip` CLI commands and all nine indexers are implemented
> and tested. All nine language indexers exist as pure-C# ports of varying completeness (see TODO.md). See [TODO.md](TODO.md) for the roadmap.

## Layout

```
src/Scip              core library (protobuf types, symbols, index writer)
src/Scip.Indexing     indexer abstractions
src/Indexers/*        one project per language indexer
src/Scip.Cli          the `scip` dotnet tool
tests/Scip.Tests      TUnit tests
.reference/           upstream Go SCIP and the language indexers being ported (read-only)
```

## Install

```bash
dotnet tool install -g Scip.Cli      # the `scip` command
scip index python --project-root .   # also: dotnet, java, typescript, rust, clang, ruby, dart, php
scip print index.scip
```

NuGet packages (all published from `main` by `.azure-devops/build-nuget.yml`):

| Package | Contents |
|---|---|
| `Scip.Cli` | the `scip` .NET tool, bundling every indexer |
| `Scip` | core library: protobuf types, symbols, positions, index writer |
| `Scip.Indexing` | indexer abstractions (`IIndexer`) |
| `Scip.Indexer.DotNet` / `.Java` / `.TypeScript` / `.Python` / `.Ruby` / `.Rust` / `.Clang` / `.Dart` / `.Php` | one package per language indexer |

## Build & test

Requires the .NET 11 SDK.

```bash
dotnet build
dotnet test
dotnet run --project src/Scip.Cli -- --help
dotnet pack Scip.slnx -c Release /p:Version=1.0.0   # packages land in artifacts/package/release
```

## Library example

```csharp
using Scip;

Symbol symbol = SymbolParser.Parse("lsif-java maven package 1.0.0 java/io/File#Entry.method(+1).");
string text = SymbolFormatter.Verbose.Format(symbol);
```

## Upstream

The original Go implementation, schema and docs live in [`.reference/scip`](.reference/scip); the original README is
at [`.reference/scip/README.md`](.reference/scip/README.md). See [CLAUDE.md](CLAUDE.md) for contributor guidance and
[docs/PORTING.md](docs/PORTING.md) for the source map.

Licensed under Apache-2.0 (see [`.reference/scip/LICENSE`](.reference/scip/LICENSE); the root `LICENSE` file mirrors it).
