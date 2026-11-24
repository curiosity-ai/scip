# Scip.Indexer.Java

Pure native C# port of [`scip-java`](../../../.reference/scip-java) for **Java**. At run time it needs **no javac, no
compiler-plugin jar and no build tool**: it parses sources, resolves symbols and reads dependencies itself, then streams the
SCIP index through `Scip.IndexWriter`. (Scala and Kotlin are not supported.)

## How upstream works, and what replaced what

Upstream scip-java is a javac plugin that emits one SCIP shard per file (there is no SemanticDB any more), a separate
aggregator that merges shards and rewrites symbols with package coordinates, and Gradle/Maven/`scip-java.json` build-tool
glue that attaches the plugin. This port replaces each part natively:

| Upstream | Native replacement here | Files |
|---|---|---|
| javac front end (`ScipVisitor`, `trees.getElement`) | Java 21 lexer + recursive-descent parser + own symbol resolver / expression typer | `Syntax/`, `Semantics/` |
| javac symbol table for dependencies and the JDK | class-file reader (constant pool, generic `Signature`s, `InnerClasses`), jar/directory reader, **jimage (`lib/modules`) reader** (JDK 9+; `rt.jar` for JDK 8) | `Classpath/` |
| `GlobalSymbolsCache`, `ScipJavaSignatureFormatter` | `SymbolNamer`, `DocumentIndexer.Signatures.cs` | `Semantics/` |
| Gradle / Maven / `scip-java.json` build tools | parsers for `pom.xml` (parent, properties, modules, dependencyManagement, transitive deps from the local repo), `build.gradle[.kts]` + `settings.gradle` (regex based, best effort), `scip-java.json`; jars located in `~/.m2` and the Gradle module cache | `Project/` |
| `scip-aggregator` (package attribution, inverse relationships) | symbols are attributed to Maven/JDK packages while indexing; inverse `is_implementation` relationships are added after all documents are indexed. The shard aggregator itself is kept as an extra command | `NativeProjectIndexer.cs`, `ShardAggregator.cs` |

## Pipeline

1. `ProjectDiscovery` finds source roots + dependency jars (no process is run).
2. `Workspace` parses every file and declares its types (members are completed lazily); `ClassPath` loads everything else
   (jars, project output dirs, the JDK image) on demand.
3. `DocumentIndexer` walks each file with lexical scopes, resolving names, field/method access (including inherited
   members, generic substitution, overload selection after JLS 15.12.2 (strict / loose / varargs phases, then most specific; method type-variable bounds), lambda parameter inference from the
   target functional interface, enum switch labels, imports/static imports, anonymous/local classes, type parameters and
   annotations) and emits definitions, references, enclosing ranges, kinds, signatures, javadoc and relationships.
4. Documents are written with `IndexWriter` (UTF-16 position encoding), after inverse relationships are computed.

Symbols follow the scip-java 0.12.3 binary: `semanticdb maven <maven/group/artifact|jdk|.> <version|.> descriptors` (the scheme
the release writes, as seen in its output for gson), locals are `local N` numbered per document in declaration order.
Named local classes (and their members) are global descriptors under the declaring method (`T#test().Dummy#`); only
anonymous classes, locals and parameters are `local N`. Resource and pattern variables are global terms (`T#m().r.`).
Method disambiguators follow semanticdb-javac: the index among same-named members in the owner's member order, which is
declaration order for sources and class-file order for jars. For JDK classes compiled with `--release N` (read from the
`maven.compiler.release` / `testRelease` properties, compiler-plugin configuration and JDK-activated profiles) the API is read
from the JDK's `lib/ct.sym`, exactly as javac does, which fixes both member order and which members exist. Project sources get their module's Maven (or Gradle `group`/name/`version`) coordinates,
else `maven . .`.

## Usage

`scip index java --project-root DIR --output index.scip`. The indexer also understands extra arguments when called through
`IndexerOptions.Arguments`: `--config FILE`, `--jdk-home DIR`, `--jdk-version N`, `--classpath a.jar:b.jar`; and two optional
compatibility commands that are **not** needed for normal use: `aggregate --targetroot DIR` (merge shards produced by the upstream
javac plugin) and `javac-plugin --plugin-jar JAR` (run `javac -Xplugin:scip` on the sources, then aggregate).

## Verification

* `tests/Scip.Indexer.Java.Tests/SnapshotTests.cs` indexes upstream's `scip-snapshots/cases/java/common` and compares the
  rendered snapshot of every file with the upstream golden file: **25 of 26 files are byte-identical** (Methods, Fields, Enums,
  InnerClasses, Overrides, TypeVariables, TypeAnnotations, AnonymousClasses, OverloadResolution, Docstrings, ...). The one
  mismatch, `LombokBuilder`, needs Lombok annotation processing.
* The parser parses all 133 upstream Java files; the indexer indexed Apache commons-lang3 3.14.0 (228 files, ~63k
  occurrences) in under 2 s without failures (manual stress run, not in CI).
* `DependencyTests` builds a library with javac (test-only), puts it in a fake local Maven repo and checks references into the
  jar (generic substitution, overloads, nested classes, package attribution).

## Known gaps (honest list)

* **Scala and Kotlin** are not implemented (upstream uses the Kotlin Analysis API for Kotlin).
* **No annotation processing / generated sources** (Lombok, AutoValue, protobuf, Dagger...). Code that depends on them
  loses references; `LombokBuilder` snapshot differs.
* **Type inference is partial**: no full Java inference. Generic method type arguments are bound from arguments and lambda
  results; chained generic calls through inferred wildcard/intersection types, `var` with lambdas, switch-expression and
  conditional result typing, and overloads that depend on lambda shapes may resolve to the wrong overload or to nothing
  (the reference is then simply omitted, never invented). Overload selection follows the JLS phases and most-specific rule but with the partial type information above.
* **Gradle** support is regex based (no Groovy/Kotlin DSL evaluation, no version catalogs, no BOMs, no transitive
  resolution); dependencies must already be in `~/.m2` or the Gradle cache. **Maven** reads local `.pom`s only (no download, no remote parents, only `<jdk>`-activated profiles, no `import` BOMs); all modules share one classpath (upstream resolves per module, so dependency versions can differ).
* JDK classes come from `JAVA_HOME`/`java` on `PATH` (compressed jimage resources are not supported); `--jdk-version` only
  relabels symbols. Multi-release jars use the base classes only. `module-info.java` is skipped (`package-info.java` is indexed: package annotations, package references).
* Java 25 compact source files / unnamed classes are not parsed; `import module` is accepted but not resolved.
* All documents are kept in memory until the end (needed for inverse relationships); very large projects need RAM.
* `enclosing_symbol` is emitted for local symbols only (as 0.12.3 does), `text` is not emitted. Class signatures keep the
  newer `extends`/`implements` form of the golden snapshots, which 0.12.3 omits; javadoc is the raw javac text (leading space, trailing newline).
* The CLI project (`src/Scip.Cli`) has an assembly named `scip` that collides case-insensitively with `Scip.dll`;
  running the built CLI currently fails with a `TypeLoadException` for every indexer that uses protobuf types
  (not caused by, and not fixable from, this project).
