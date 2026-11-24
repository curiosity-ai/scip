# Scip.Indexer.Java

C# port of [`scip-java`](../../../.reference/scip-java) (Java; Scala/Kotlin are not ported).

## Upstream no longer uses SemanticDB

The pinned upstream does **not** go through SemanticDB. Its architecture is:

1. A javac plugin (`scip-javac`, `-Xplugin:scip`) emits one SCIP *shard* (a singleton `Index`) per source file under
   `<targetroot>/META-INF/scip/<relative path>.scip`. Symbols in shards are *bare descriptors* (`com/ex/Foo#bar().`).
2. An aggregator (`scip-aggregator`) merges the shards, rewrites bare symbols into full symbols
   (`scip-java maven maven/<group>/<artifact> <version> com/ex/Foo#bar().`) by looking up which classpath jar (or the JDK)
   defines the top-level class, adds inverse `is_implementation` relationships, and writes `index.scip`.
3. Build-tool integration (Gradle init script / Maven plugin / plain `javac` via `scip-java.json`) attaches the plugin.

So there is no SemanticDB to convert. The portable, testable core is step 2, which is what is implemented natively here.

## Strategy

| Piece | Where | Status |
|---|---|---|
| Symbol model + reverse parser (`ScipSymbols`, `SymbolDescriptor`) | `JavaSymbols.cs` | ported, tests ported |
| Package attribution (`PackageTable`, `SymbolRewriter`, Maven/JDK packages) | `Packages.cs` | ported (JDK detection is a heuristic, see gaps) |
| Shard aggregation + inverse relationships, streamed via `Scip.IndexWriter` | `ShardAggregator.cs` | ported |
| Shard layout, `dependencies.txt`/`javacopts.txt`/POM classpath entries | `ShardPaths.cs` | ported |
| `scip-java.json` build tool (javac + plugin) | `JavacBuildTool.cs` | ported (Java only) |
| The javac plugin itself (javac AST -> shards) | n/a | **not ported**: it needs javac's in-process API; the upstream plugin jar is used |

The javac plugin cannot be reimplemented in C# without reimplementing a Java type checker, so it is consumed as the
upstream `scip-plugin.jar` (build `:scip-javac:shadowJar` in `.reference/scip-java` and pass `--plugin-jar` or set
`SCIP_JAVA_PLUGIN_JAR`).

## Usage (`scip index java ...`)

```
[aggregate] [--plugin-jar JAR] [--targetroot DIR] [--config FILE] [--strict]
            [--no-inverse-relationships] [--allow-empty-index] [--jdk-version N]
```

* Default: read `scip-java.json`/`lsif-java.json` if present (fields `classpath`, `sourceFiles`, `javacOptions`,
  `javaHome`), compile all `.java` files with `javac -Xplugin:scip` into `target/scip`, then aggregate.
* `aggregate`: skip compilation and merge shards already present in `--targetroot` (use with Gradle/Maven builds or your
  own `javac -Xplugin:scip -targetroot:DIR -sourceroot:DIR` invocation).

## Known gaps

* No Gradle / Maven / Bazel / sbt orchestration (the init script, Maven plugin and gradle plugin jars are not ported);
  use `aggregate` after building with the plugin attached.
* No Kotlin (`scip-kotlin-analysis`) or Scala support; `scip-snapshots` snapshot tests are not ported (they need the plugin).
* JDK classes are recognised by package prefix (`java/`, `javax/`, `jdk/`, `sun/`, `com/sun/`, ...) because .NET cannot
  read the `jrt:/` file system; upstream asks the running JVM. The JDK version comes from `--jdk-version`,
  `$JAVA_HOME/release`, or `java -version`.
* Aggregation is sequential (deterministic output); upstream's `--parallel` is not implemented.
* `--cleanup`, `--build-command`, custom javac wrappers and the `snapshot`/`aggregate` upstream sub-commands are not ported.
* Not verified end to end against a real plugin jar (none available in the sandbox); aggregator and orchestration are
  covered by unit tests in `tests/Scip.Indexer.Java.Tests`.
