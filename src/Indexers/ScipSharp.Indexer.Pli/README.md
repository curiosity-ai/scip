# ScipSharp.Indexer.Pli

Native C# SCIP indexer for PL/I, run with `scip index pli`. It lexes, parses and resolves PL/I itself. It does not
need a PL/I compiler, preprocessor or language server. Supported dialects: IBM Enterprise PL/I (z/OS), PL/I for AIX
and Open PL/I, Iron Spring PL/I, and Multics PL/I. Where the dialects disagree, the indexer accepts both forms.

```bash
scip index pli --project-root path/to/src -- --include-dir=copylib --margins=2,72
scip index pli --project-root multics -- --margins=none --case-sensitive
scip lint index.scip
```

## What is indexed

- **Files**: sources (`.pli .pl1 .plx .pls`) are compilation units. `%INCLUDE` members are read as part of the
  unit that includes them. Their occurrences go into the include file's own document. Include files
  (`.inc .incl.pl1 .plinc .cpy`) that nothing includes are indexed on their own.
- **Blocks**: external procedures (`PROC`/`PROCEDURE`, `OPTIONS(MAIN)`), nested procedures, `BEGIN` blocks (named
  or anonymous), Enterprise `PACKAGE`s with `EXPORTS`, secondary `ENTRY` points, and extra labels on a `PROC`
  statement. Definitions get an enclosing range that runs from the label to `END`. `END label` closes several groups
  at once (multiple closure).
- **Declarations**: `DCL`/`DECLARE` with structure level numbers, factored declarations
  (`DCL (A, (B, C) FIXED) BIN`), dimensions, and the `LIKE`, `TYPE`/`HANDLE`, `DEFINED`, `BASED`, `INIT` and `REFER`
  attributes. Also `BUILTIN`, `ENTRY` (external entries), `GENERIC`, `CONDITION`, `FILE`, `EXTERNAL`/`INTERNAL`,
  `PARAMETER`, `DEFINE STRUCTURE`/`ALIAS`/`ORDINAL`, labels (including label arrays `L(3):`) and parameters.
  Declarations apply to their whole block, wherever they appear in it.
- **References**: names in assignments (marked `WriteAccess`), expressions, `CALL` targets, `GO TO`/`LEAVE`/`ITERATE`
  labels, `END label`, `DO` control variables, `ON` units, `SIGNAL`/`REVERT`, I/O statements, `EXEC SQL` host
  variables (`:name`), names in attributes such as `LIKE x`, `BASED(p)`, `DEFINED x` or `CHAR(n)`, and the members of
  `GENERIC`.
- **Resolution**: the search starts in the innermost block and works outwards, so inner declarations hide outer
  ones. Structure references may be fully qualified (`A.B.C`) or partially qualified (`A.C`, `C`). Partial
  qualification picks the match that skips the fewest levels. Members of a `LIKE` or `TYPE` template are found
  through the template. When a name is not declared, it is resolved in this order:
  1. A built-in function, pseudovariable or standard file: a reference to the `builtin` package, listed as an
     external symbol.
  2. A keyword (PL/I keywords are not reserved, so this is decided by context): skipped.
  3. A `CALL` target or a Multics `segment$entry` name: an external entry.
  4. Anything else: an implicit declaration, emitted as a local symbol and documented as "implicitly declared".
- **Cross-file linking**: external procedures and their `ENTRY` points are registered by name across all units, and
  also under `PROC$ENTRY` for Multics. A `DCL x ENTRY` or a `CALL x` in one file then refers to the definition in
  another file. `EXTERNAL` variables share one symbol in every file that declares them.
- **Preprocessor (best effort)**: `%INCLUDE`/`%XINCLUDE` (forms `name`, `ddname(member)` and `'path'`). `%DCL`
  variables and `%name: PROC ... %END` procedures, which act as macro symbols. References in `%IF`, `%DO`, `%`
  assignments, `%ACTIVATE` and `%REPLACE`, and to macro names used in ordinary code. Both branches of `%IF` and
  `%SELECT` are indexed, because nothing is evaluated.
- **SymbolInformation**: display name (as written), kind (Function, Variable, Field, Parameter, Constant for labels,
  Namespace, Package, Struct/Type, EnumMember, Event for conditions, Macro), and the declaration as documentation.
  The documentation shows the declared attributes, with factored attributes merged in (e.g. `DCL 3 RATE FIXED DEC(7, 2)`).

## Symbol scheme

```
scip-pli pli <package> <version> <descriptors>
```

`<package>` is `--package-name` and defaults to the name of the project directory. `<version>` is
`--package-version` and defaults to `.`. Names are upper-cased unless `--case-sensitive` is given.

| Construct | Descriptor |
|---|---|
| external procedure / its ENTRY points | `PAYROLL().`, `ALT_ENTRY().` (top level: linkage is by name) |
| internal procedure | `PAYROLL().REPORT().` |
| package and its procedures | `GEOMETRY/`, `GEOMETRY/AREA_OF().` |
| BEGIN block | `PAYROLL().BLOCK1/`, anonymous: `PAYROLL().$begin1/` |
| variable, label, condition, file | `PAYROLL().COUNT.` |
| structure member | `PAYROLL().EMPLOYEE.PAY.RATE.` |
| parameter | `PAYROLL().REPORT().(N)`, members of a structure parameter: `P().(S)F.` |
| EXTERNAL variable | `LOG_LEVEL.` |
| external entry with no definition in the project | `ioa_$nnl().` (documented as such) |
| `DEFINE STRUCTURE`/`ALIAS`/`ORDINAL` type, its members and values | `GEOMETRY/POINT#`, `GEOMETRY/POINT#X.`, `GEOMETRY/CIRCLE.` |
| preprocessor variable / procedure | `` `src/main.pli`/DEBUG! `` (scoped to the compilation unit) |
| built-in | `scip-pli pli builtin . SUBSTR().`, `scip-pli pli builtin . SYSPRINT.` |
| implicit declaration | `local N` |

Documents use `UTF16CodeUnitOffsetFromLineStart` positions. Files that are not valid UTF-8 (for example EBCDIC
transfers with `¬`) are decoded as Latin-1.

## Options

```
--margins=<l>,<r>|none|auto   source margins (default 2,72; `none` = free form; `auto` = per file)
--include-dir=<dir>           %INCLUDE search directory (repeatable, relative to the project root)
--extensions=<list>           source extensions (default .pli,.pl1,.plx,.pls)
--include-extensions=<list>   include extensions (default .inc,.incl.pl1,.plinc,.cpy)
--case-sensitive              case-sensitive names (Multics)
--package-name=<name>         package name of global symbols
--package-version=<v>         package version of global symbols
```

A `*PROCESS`/`%PROCESS` statement at the top of a file can set `MARGINS(l,r)` or `NOMARGINS`, and that setting
overrides the option for that file. Characters outside the margins, such as column 1 carriage control and sequence
numbers in columns 73–80, are ignored, but positions still refer to the original columns. `%INCLUDE` members are
searched for in this order:

1. The directory of the including file.
2. Each `--include-dir`.
3. The project root.
4. Any project file with that name, after stripping the extension or `.incl.pl1`.

The member name is tried as written and with each known extension, case-insensitively.

## Known gaps

- No preprocessor evaluation. Macro expansion, `%IF` conditions and generated names (`FIELD%;J`) are not computed,
  so code that exists only after expansion is not indexed, and inactive branches are indexed too.
- `DEFAULT` statements and implicit attribute rules are ignored. Implicit declarations carry no attributes.
- `GENERIC` references resolve to the generic name, not to the selected member. Overloaded procedure
  (`ENTRY`) descriptors are not matched against arguments.
- Ambiguous partially qualified references are resolved to the closest match instead of being reported.
- `LIKE` and `TYPE` members resolve to the template's members. No per-variable copies of the members are created.
- Fields of `BASED` storage reached through pointer qualification (`P -> S.F`) resolve by name only. Pointer types
  are not checked.
- `EXEC CICS` and `EXEC SQL` contents are only scanned for declared names and `:host` variables.
- Labels have the `Constant` kind, because SCIP has no `Label` kind.

## Research

- Grammars studied (ideas only, no code copied):
  [tree-sitter-pli](https://github.com/Simon-Peleska/tree-sitter-pli) (MIT): statement and attribute coverage,
  multiple dialects, EBCDIC operator glyphs. [antlr-grammar-pli](https://github.com/markbsigler/antlr-grammar-pli)
  (MIT): a two-phase preprocessor/parser design for Enterprise PL/I and its keyword lists.
  [pliparser](https://github.com/aleph-lang/pliparser) (QPL, not used). PL/I has no reserved words. Because of that,
  all three grammars rely on a contextual or GLR parse, and this indexer uses statement-level, context-driven
  parsing instead.
- No grammar for PL/I exists in ANTLR grammars-v4. Eclipse Che4z covers COBOL only. IBM Z Open Editor is closed source.
  pl1gcc is GPL and was not consulted for code.
- Corpora: [Multics](https://github.com/dancrossnyc/multics) (Multics licence) and `PLI/MACSAMP.pli` from [IBM/zopeneditor-sample](https://github.com/IBM/zopeneditor-sample) (Apache-2.0; the repository's other samples carry "Restricted Materials of IBM" banners and are not used). See `tests/ScipSharp.Indexer.Pli.Tests/testdata/SOURCES.md`.
