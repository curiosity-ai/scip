# Scip.Indexer.Php

Native C# port of [scip-php](https://github.com/davidrjenni/scip-php) (`.reference/scip-php`): a SCIP indexer for PHP
projects managed by Composer.

**Pure managed code.** The indexer never starts `php`, `composer` or any other process and needs neither installed. It
reads the project's source files and Composer metadata directly. (The test project may *optionally* call a locally
installed `php` to cross-check the lexer; those tests skip silently when it is absent.)

```
scip index php --project-root <dir> --output index.scip [--php-version=8.4.0]
```

The project must have a `composer.json` and (like upstream) a populated `vendor/` directory (`composer install`).

## Strategy

Upstream is a thin layer over three PHP libraries and the PHP runtime. Each one has a native replacement:

| Upstream | Native replacement here |
|---|---|
| `nikic/php-parser` (lexer, parser, `NameResolver`, `ParentConnectingVisitor`, pretty printer) | `Syntax/Lexer.cs`, `Syntax/PhpParser.cs`, `Syntax/Ast.cs`: a hand-written PHP 8.x lexer + recursive-descent / precedence-climbing parser. Names are resolved while parsing (namespaces, `use`, `use function/const`, group use, `namespace\X`, special `self/static/parent`), parents are connected after parsing. `DocGenerator.cs` contains the subset of the pretty printer that upstream's hover text needs. |
| `phpstan/phpdoc-parser` | `Syntax/PhpDoc.cs`: parses `@var`, `@return`, `@property[-read/-write]`, `@method` and the type grammar needed by upstream's `TypeParser` (unions, intersections, nullable, `T[]`, array/object shapes, `$this`, conditional types; generics/callables are parsed but ignored, as upstream ignores them). |
| `composer/class-map-generator` | `ComposerProject.ScanPaths`: scans `classmap` / `psr-4` / `psr-0` autoload paths with the lexer and applies PSR filtering. |
| `vendor/autoload.php` + `ClassLoader::findFile`, `class_exists`, `function_exists`, `ReflectionFunction`, `get_defined_constants` | `ComposerProject` reads `vendor/composer/{installed,autoload_classmap,autoload_psr4,autoload_namespaces,autoload_files}.php` by *evaluating the tiny PHP subset they use* (`Composer/PhpArrayEval.cs` on top of our own parser) and re-implements `ClassLoader::findFile` (classmap, PSR-4 longest-prefix, PSR-0, fallback dirs). Functions and constants (`function`, `const`, `define()`) are indexed by scanning the files of `autoload_files.php`, which is exactly the set composer loads at runtime. `composer.lock` `autoload.files` are handled like upstream. |
| `jetbrains/phpstorm-stubs` (`PhpStormStubsMap`) | `Composer/Builtins.cs`, a hand-written catalogue of built-in classes / functions / constants. **See limitations.** |
| `google/protobuf` PHP bindings | `Scip` library (`IndexWriter`, generated types). |

The semantic part is a faithful port, file for file:

| Upstream | Here |
|---|---|
| `Indexer.php` | `PhpProjectIndexer.cs` (+ `PhpIndexer.cs`, the `IIndexer` entry point; streams via `IndexWriter`) |
| `DocIndexer.php` | `DocIndexer.cs` |
| `SymbolNamer.php` | `SymbolNamer.cs` (symbol format `scip-php composer <pkg> <version> <descriptor>`) |
| `Types/Types.php`, `Types/Internal/*` | `Types.cs` (the light-weight type inference over `$this`, `new`, calls, property fetches, `??`, ternary, `match`, doc-comment types, supertypes/traits, lazily collected dependency files) |
| `DocGenerator.php` | `DocGenerator.cs` |
| `Composer/Composer.php` | `Composer/ComposerProject.cs` |
| `Parser/*` (`PosResolver`, `DocCommentParser`) | `DocIndexer.cs` (`LineIndex`), `Syntax/PhpDoc.cs` |

SCIP specifics kept from upstream: positions are UTF-8 byte offsets (`PositionEncoding.UTF8CodeUnitOffsetFromLineStart`;
sources are decoded as Latin-1 internally so a string index *is* a byte offset), `SymbolInformation` carries only
documentation, external symbols are emitted for references into dependencies/builtins, no enclosing ranges, no
relationships. One deliberate difference: occurrences use the typed `single_line_range`/`multi_line_range` fields of the
current proto instead of the deprecated `range` list.

PHP-compatible token numbering: anonymous classes / closures are named `anon-class-<N>` / `anon-func-<N>` where `N` is the
index of the start token in PHP's token stream (whitespace and comments count). The lexer reproduces PHP's tokenization
(open/close tags, interpolated strings, heredoc, casts, ...) so the names equal upstream's; this is verified against the
upstream golden snapshot and (optionally) against `PhpToken::tokenize`.

## Tests (`tests/Scip.Indexer.Php.Tests`, TUnit)

Ported from upstream: the `IndexerTest` golden snapshots (all 10 files, rendered with a port of `scip snapshot`'s
formatter), `ComposerTest`, `PosResolverTest` (positions and `posInDoc` cases), `ParserTest` (empty file), the
`CompositeType` semantics (exercised through the indexer). Extra tests: every PHP fixture parses, upstream's own sources
parse (when `.reference` is reachable), name resolution, modern syntax, token numbering vs. PHP (skipped without `php`),
end-to-end temporary projects.

Run: `dotnet test tests/Scip.Indexer.Php.Tests` (the project is not in `Scip.slnx`, which is shared and not edited here).
Fixtures are copied verbatim from upstream except that the dependency symlink in the indexer fixture's `vendor/` is a
real directory copy.

## Known gaps and differences (honest list)

* **Built-in symbols (largest gap).** Upstream resolves PHP built-ins through phpstorm-stubs (~thousands of symbols and
  full member information, parsed on demand). `Builtins.cs` is a curated subset: ~170 classes, ~470 functions, ~145
  constants. A built-in not in the list yields no occurrence. Member information for built-ins exists only for the
  `Exception`/`Error` families (so `$this->getCode()` etc. resolve); other built-in classes resolve as types but their
  methods/properties do not. Extending the lists is a data change.
* **PHP version of built-in symbols.** Upstream uses `PHP_VERSION` of the interpreter that runs it. A native indexer has
  no interpreter; the version is `--php-version=` (default `8.4.0`).
* **Runtime-only knowledge.** Upstream can ask the loaded PHP runtime about anything composer's autoloader loads. Here
  functions/constants come from scanning `autoload_files.php` files only; classes that composer would only know via
  dynamic means, `Composer\Autoload\*` reflection special cases, and extensions' symbols are not covered. A missing
  `vendor/composer/*.php` degrades to project-only knowledge with a warning.
* **ClassMapGenerator** is approximated: `.php/.inc/.hh` files, PSR-4/PSR-0 class-vs-path filtering, `exclude-from-classmap`
  regex; edge cases of composer's implementation (e.g. `.git` handling, duplicate classes, ambiguity warnings) are simplified.
* **Parser coverage.** The whole upstream PHP source tree, the Composer fixtures, and ordinary modern PHP (8.0-8.4:
  enums, readonly, attributes, match, first-class callables, named args, nullsafe, DNF types, `new` in initializers)
  parse. There is no error recovery: a file with a syntax error is reported (`stderr`) and skipped, while upstream would
  abort. Property hooks (8.4) are skipped, not indexed. Heredoc indentation removal is not modelled (positions are
  unaffected). Alternative-syntax templates and `__halt_compiler` data are handled minimally.
* **Pretty printing** (hover signatures) covers common expressions; anything exotic falls back to the original source text,
  which can differ from php-parser's normalised output (e.g. long-form vs short arrays are preserved, string
  escapes are not re-encoded).
* **Quirks kept on purpose** (upstream behaviour): group `use A\{B}` names are not resolved; references through variables
  other than `$this` are untyped; `static`/`self` return types do not propagate; `@property` without a type is ignored;
  top-level functions/classes in files that are not autoloadable are invisible to dependents.
* Doc comment `@method`/`@property` definition positions are computed from the tag token (upstream greps the first
  occurrence of the name inside the tag text); identical for well-formed tags.
* Not ported: `bin/scip-php` argument handling beyond `--help`, `--php-version` (and an ignored `--memory-limit`), the
  `Reader`/`Types`-internal unit tests whose subject is PHP-specific plumbing (`ReaderTest`, `TypesTest` assertions are
  covered through the indexer snapshot instead).
* The shared CLI project currently has assembly name `scip` while the core library is `Scip`; on case-sensitive file
  systems `scip index php` via `dotnet run` fails with a `TypeLoadException` regardless of this project (the indexer is
  verified through `PhpIndexer.IndexAsync` in the tests). Not fixed here because `src/Scip.Cli` is shared.
