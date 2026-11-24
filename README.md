# SCIP for .NET

A C# / .NET 11 port of [SCIP](https://github.com/scip-code/scip), the Code Intelligence Protocol — the protocol
library, the `scip` CLI, and the per-language indexers
(Java, TypeScript, Rust, C/C++, Ruby, Python, C#, Dart, PHP).

> **Status: early port.** The core symbol parser/formatter, streaming index writer and a CLI skeleton are working
> and tested. Language indexers are stubs. See [TODO.md](TODO.md) for the roadmap.

## Layout

```
src/Scip              core library (protobuf types, symbols, index writer)
src/Scip.Indexing     indexer abstractions
src/Indexers/*        one project per language indexer
src/Scip.Cli          the `scip` dotnet tool
tests/Scip.Tests      TUnit tests
.reference/           upstream Go SCIP and the language indexers being ported (read-only)
```

## Build & test

Requires the .NET 11 SDK.

```bash
dotnet build
dotnet test
dotnet run --project src/Scip.Cli -- --help
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
