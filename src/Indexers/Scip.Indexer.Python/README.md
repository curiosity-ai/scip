# Scip.Indexer.Python

A **pure native C#** port of [scip-python](https://github.com/sourcegraph/scip-python) (`.reference/scip-python`, SCIP logic in
`packages/pyright-scip`). It has no runtime dependency on Python, pip, pyright, node or any other external process: it reads
`.py`/`.pyi` files and `*.dist-info` metadata directly from disk.

```
scip index python --project-root <dir> [--output index.scip]
```

Python-specific options are read from the arguments the CLI leaves unmatched, in scip-python's spelling:
`--project-name`, `--project-version`, `--project-namespace`, `--target-only`, `--environment <json>`, `--cwd`, `--quiet`, plus
`--python-version 3.x` (the `python-stdlib` package version; default: taken from a venv's `pyvenv.cfg`/`lib/pythonX.Y`, else 3.13).
Name and version otherwise come from `pyproject.toml` (`[project]`, then `[tool.poetry]`), and the version finally from the git
`HEAD` commit (read from `.git`, no `git` process). Without a version the run fails like upstream.

## Strategy

Pyright's type evaluator cannot be ported wholesale, but scip-python only needs a small part of it: *which declaration does this
name refer to*, plus a few facts about declarations. So the port is layered:

| Layer | Upstream | Here |
|---|---|---|
| Tokenizer + parser | pyright `tokenizer.ts`/`parser.ts` | `Syntax/` – hand written Python 3.x parser (indentation, f-strings with nested fields, walrus, `match`, async, decorators, comprehensions, lambdas). Produces pyright-shaped nodes (`Ast.cs`) because symbol naming is defined over pyright's parent chains. Recovers from syntax errors per statement. |
| Binder | pyright `binder.ts` | `Semantics/Binder.cs` – module/class/function/lambda/comprehension scopes, `global`/`nonlocal`, deferred function-body binding order, `self.x = ...` member-access declarations, walrus hoisting, wildcard imports. |
| Import resolution / environment | `importResolver.ts`, `virtualenv/*` (which run `pip`/`python`) | `Semantics/ModuleSystem.cs` – project root + `extraPaths` (+ auto `src`), directory of the importing file, site-packages; namespace packages; relative imports. Installed distributions come from `*.dist-info/METADATA` + `RECORD` (same data as `importlib.metadata`), or from `--environment` JSON. Standard library = name list (`Hardcoded.cs`, ported from `hardcoded.ts`). |
| "Type evaluator" | `typeEvaluator.ts` | `Semantics/Evaluator.cs` – scope lookup, alias resolution through import chains, and an *entity* evaluator (module / class / instance / `self` / function / external) that handles names, attribute access, calls of classes, annotated parameters and variables, `Optional[X]`, `X \| None`, simple return-type inference and base-class member lookup. |
| Symbol naming + emission | `symbols.ts`, `ScipSymbol.ts`, `treeVisitor.ts`, `indexer.ts` | `Emit/` – a faithful port of `TreeVisitor` (same node-kind switch, same quirks: lambda parameters are `local N(x)`, nested functions in methods hang off the class, the `moduleName`-vs-import-text split, counter allocation order). Symbols are built with `Scip.SymbolFormatter`; output is streamed with `Scip.IndexWriter`. |
| Config / files | `config.ts`, `FileMatcher.ts` | `Project/ProjectConfig.cs` – small TOML subset reader, `[tool.scip]`/`[tool.pyright]` `include`/`exclude`/`extraPaths`, default excludes, venv auto-exclusion, project files reached via imports are indexed too. |

Positions are UTF-16 code units (`position_encoding = UTF16CodeUnitOffsetFromLineStart`, which is what pyright's offsets are).
Like upstream, ranges are written to the (deprecated) `range`/`enclosing_range` fields.

## Tests (`tests/Scip.Indexer.Python.Tests`)

* The **upstream snapshot suite** (`snapshots/input` + `snapshots/output`, all 22 projects, plus a port of `formatSnapshot`)
  runs against the indexer. Every document matches the pyright-backed expected output except the ones listed in
  `SnapshotTests.Divergent`, each with its reason. Snapshots rendered with `showDocs` are compared without documentation
  lines (see below).
* Ports of upstream unit tests (`pyproject.toml` name/version inference, annotated variables), parser/tokenizer tests,
  relationship/override tests, environment (`dist-info`) reading, UTF-16 positions, symbol escaping.
* Smoke tests that parse every `.py` of an installed CPython standard library (591 files, 0 syntax errors on the machine this was
  written on) and index a subset; they pass trivially when no Python installation exists. No Python process is ever started.

## What is covered

* Definitions, references, imports (roles `Import|ReadAccess`), module `__init__:` definition at 0:0, enclosing ranges for
  classes/functions, `SymbolInformation` for modules/classes/functions/parameters, overrides and base classes as
  `is_implementation` relationships, external symbols for stdlib/builtins/dependencies.
* Locals vs globals exactly as upstream (function/lambda/comprehension scope => `local N`), project-package naming, namespaces,
  empty/space-containing package names, module names relative to the import root with the shortest-name rule.
* Cross-file resolution inside the project and into site-packages sources: `from m import x`, `import a.b.c` (+ submodule
  range extension), relative imports, re-exports, `__all__`/wildcard imports, `self.attr`, class attributes through
  inheritance, instances created by calling a class, keyword arguments to resolvable calls.

## What is NOT covered (honest gaps)

* **No typeshed / no hover provider.** Upstream documentation comes from pyright's hover (inferred types such as
  `(variable) x: Unknown`, `-> None`, typeshed docstrings, docstring-to-markdown conversion). We emit module docs, a source-derived
  signature/class stub plus docstrings, and `(variable) x: <annotation>` for annotated variables. Types are never inferred for display.
* **Standard-library members have no declaration kind.** Without stubs, `from typing import X`/`re.search` are classified by a
  heuristic (CamelCase => class `#`, typing special forms and `UPPER_CASE` => term `.`, otherwise function `().`). Upstream is exact.
  `os.path`-style submodules are recognised from a small list only.
* **Builtins** are a built-in table: classes/exceptions `builtins/Name#`, overloaded functions (`print`, `max`, ...)
  `builtins/name().`; other builtin functions get a fresh `local N` per use, as upstream does.
* **The evaluator is intentionally small:** no generics (`Sequence[Foo]` iteration), no `isinstance` narrowing, no flow analysis
  (all declarations of a symbol are considered, the first one is the definition), no overload/union/TypeVar solving, no
  decorators that change a function's type, no `__getattr__`, no stub-only/`py.typed` package nuances, no PEP 695 type parameters
  (skipped by the parser), no `typings/` stub folder, no `.pth` files. Member accesses that cannot be resolved produce no
  occurrence (upstream behaves the same when pyright cannot resolve them, but it resolves far more).
* Unresolved keyword-argument names produce no occurrence (upstream emits a `local`).
* `match` statements are parsed and captures are bound, but patterns are only approximated by the expression grammar.
* The environment is discovered from `VIRTUAL_ENV`, project venvs and common system `site-packages` locations, not from a running
  interpreter, so exotic setups (conda, `.pth`-based layouts, user-site) need `--environment`.
* The shared `scip` CLI currently does not forward Python-specific options (`--project-name`, ...) because its `index` subcommand
  does not accept unmatched tokens; use `pyproject.toml` or call `PythonIndexer.Run(...)`/`IIndexer` directly until it does.
