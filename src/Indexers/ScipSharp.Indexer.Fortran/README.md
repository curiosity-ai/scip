# ScipSharp.Indexer.Fortran

Native C# SCIP indexer for Fortran — FORTRAN 66/77 fixed form through Fortran 90/95/2003/2008/2018/2023 free form.
Run with `scip index fortran`. There is no upstream `scip-fortran`; this is an original implementation. It never runs a
compiler, preprocessor or language server: lexing, statement classification, scoping and resolution are all C#.

```bash
scip index fortran --project-root path/to/project --output index.scip
scip lint index.scip
```

## What is indexed

- **Source forms**: fixed form (`.f .for .ftn .f77 .fpp`; `C`/`c`/`*`/`!`/`D` comment lines, labels in columns 1-5,
  continuation in column 6, text after column 72 ignored — `--fixed-line-length` — DEC tab format, blank-insensitive
  glued keywords such as `CALLFOO(X)`, `GOTO10`, `DO10I=1,10`, `REALX`), free form (`.f90 .f95 .f03 .f08 .f18 .f23`,
  `!` comments, `&` continuation incl. leading `&` and continued character context), `;` separators, Hollerith
  constants, BOZ literals, kind suffixes (`1.0_dp` references `dp`). A "fixed" file whose statements start in columns
  1-5 is treated as free form (fortls-style detection). `--form` forces a form.
- **Preprocessing** (best effort): `#include` and Fortran `INCLUDE` are spliced in and parsed in the includer's scope;
  included files get their own documents. By default *every* `#if`/`#ifdef` branch is indexed; with `-D`/`--cpp=eval`
  conditions are evaluated (`defined`, `!`, `&&`, `||`, comparisons, arithmetic). `#define`d names are never resolved
  as Fortran names. No macro expansion.
- **Program units and scopes**: `PROGRAM` (also the unnamed main program), `MODULE`, `SUBMODULE` (ancestor/parent
  chain), `BLOCK DATA`, external / module / internal (`CONTAINS`) procedures, `ENTRY`, statement functions, interface
  blocks (generic names, `OPERATOR(...)`, `ASSIGNMENT(=)`, defined I/O), abstract interfaces, separate module
  procedures (`module subroutine` in an interface ↔ `module procedure` in a submodule), derived types with components,
  type parameters, type-bound procedures, generic bindings and `FINAL`, `ENUM`/`ENUMERATOR`, `BLOCK`, `ASSOCIATE`,
  `SELECT TYPE` (the associate name takes the guard's type), construct names, labels.
- **Declarations**: type declarations (F77 `REAL*8`, `CHARACTER*(*)`, `CHARACTER A*10`, F90 attributes), `DIMENSION`,
  `PARAMETER`, `COMMON`, `NAMELIST`, `EXTERNAL`, `INTRINSIC`, `SAVE`, `IMPLICIT` (letter ranges) / `IMPLICIT NONE`,
  `PUBLIC`/`PRIVATE` (statement, attribute and default), `USE` (`ONLY`, renames `a => b`, intrinsic modules), `IMPORT`,
  procedure pointers and dummy procedures, `DATA`, `EQUIVALENCE` (references).
- **Resolution** (two passes: every file is collected before anything is resolved, so file order never matters):
  local names → use association (ONLY lists, renames, re-exports through other modules, PRIVATE) → host association
  (internal and module procedures, submodules, BLOCK and other constructs; interface bodies only see IMPORTed names) →
  global external procedures. Undeclared names under implicit typing become implicitly typed locals (definition at the
  first appearance); `IMPLICIT NONE` disables that. Undeclared `name(...)` is an external function (unless intrinsic),
  `CALL name` an external subroutine; a scalar F77-typed name used with an argument list is an external function.
  `%` chains are typed through declared `TYPE(t)`/`CLASS(t)`, function results, bindings, `EXTENDS` parents (incl. the
  parent component) and `ASSOCIATE`/`SELECT TYPE` names. Keyword arguments, I/O specifiers (`unit=`, `stat=`), and
  type-parameter keywords are not treated as variables; label specifiers (`ERR=`, `END=`, `FMT=`, a label as format) are
  label references. User-defined operators (`.cross.`) reference their generic interface.
- **SymbolInformation**: kind, display name (as written), signature (header + dummy declarations for procedures,
  `type, attrs :: name(dims) = init` for data), documentation from FORD (`!>` before, `!!` after) and Doxygen
  (`!>`, `*>`, `!<`; LAPACK's `\verbatim`/`\ingroup` markers are dropped), enclosing ranges for program units, types and
  procedures, `enclosing_symbol` for locals, and relationships: `EXTENDS` (implementation), specific → generic,
  implementation procedure → type-bound binding, overriding binding → parent binding, binding → generic binding.
- Interface bodies of external procedures are `forward_definition`s of the global procedure; `EXTERNAL f` and typed
  external names are references. Positions are UTF-16 code units (`position_encoding` is set on every document).

## Symbol scheme

`scip-fortran <manager> <package> <version> <descriptors>` — manager `fpm` and name/version from `fpm.toml` when present
(otherwise `.`, the project directory name and `.`; override with `--package-*`). Fortran names are case-insensitive and
are lower-cased in symbols.

| Entity | Descriptor |
|---|---|
| module / program / block data | `m/`, `prog/`, `blk/` (unnamed main program: `main/`) |
| submodule | `ancestor/sub/` |
| external procedure, ENTRY | `name().` (package root) |
| module procedure, abstract interface | `m/name().` |
| internal procedure | `<host>name().`, e.g. `m/outer().inner().` |
| separate module procedure | the interface's symbol, `m/name().` |
| generic interface / operator | `m/name().` (`m/name(generic).` when a specific has the same name), ``m/`operator(.x.)`().``, ``m/`assignment(=)`().`` |
| derived type, component, type parameter | `m/t#`, `m/t#c.`, `m/t#[k]` |
| type-bound procedure / generic binding | `m/t#b().` |
| module variable, named constant, enumerator, namelist | `m/x.` |
| COMMON block and its members | `` `/blk/`# `` and `` `/blk/`#x. `` (blank COMMON: `` `//`# ``) — members are linked by name |
| entity declared by an INCLUDEd file inside a subprogram | `` `path/to/file.inc`/x. `` (shared by every includer) |
| module not in the project / its ONLY-listed names | `scip-fortran . . . mpi/`, `scip-fortran . . . mpi/name.` |
| locals, dummy arguments, labels, construct names, statement functions, types/variables of programs and subprograms | `local N` |

Undefined external procedures and unknown modules are emitted as external symbols.

## Options

```
-h --help                    display help
   --form=<auto|fixed|free>  force the source form (default auto)
   --fixed-line-length=<n>   last statement column in fixed form (default 72, 0 = unlimited)
-I <dir>, --include-dir=<dir>  extra INCLUDE / #include search directory (repeatable)
-D <name[=value]>, --define=...  preprocessor macro; switches #if evaluation on (repeatable)
   --cpp=<all|eval>          index every #if branch (default) or evaluate conditions
   --package-name/--package-version/--package-manager=<v>
   --exclude=<path>          directory name or relative path to skip (hidden directories and `build/` are always skipped)
```

## Known gaps

- No macro expansion; with the default "all branches" mode a construct split across `#if`/`#else` can confuse scoping
  (error tolerant: the rest of the file still indexes).
- Fixed-form blank insensitivity is handled for keywords at statement start and for `DO10I=`; names with embedded blanks
  (`MAX ITER = 1`) or numbers with blanks (`1. D0`) are not joined.
- Generic resolution does not pick the specific procedure for a call (references go to the generic); intrinsic operator
  overloads (`operator(+)`) are not linked from expressions; type-bound user operators are not resolved.
- Keyword arguments (`call f(n=1)`) do not reference the dummy argument; dummy arguments are local symbols.
- `%` typing is best effort: no type inference for expressions other than designators and function results.
- COMMON members are matched by name, not by storage position; EQUIVALENCE only records references.
- Includes outside the project root are not read; intrinsic module members get no symbols.
- LAPACK-style `\param` blocks are kept in the procedure documentation, not split onto the dummy arguments.

## Research / references

Designed from the Fortran standards; ideas (no code) taken from
[fortls](https://github.com/fortran-lang/fortls) (MIT — studied: line-based parser and scope model, fixed/free
detection, intrinsic module list, doc-comment conventions; its `test/test_source` tree is used as an ad-hoc robustness
input) and [tree-sitter-fortran](https://github.com/stadelmanma/tree-sitter-fortran) (MIT — statement coverage; its
examples are an ad-hoc robustness input). Background references for semantics: LFortran (BSD-3, ASR scoping),
flang/f18 (Apache-2.0, prescanner / parse tree), fortran-src (Apache-2.0, FORTRAN 66/77 forms), FORD (doc markers).
Committed test corpora: Reference-LAPACK, fortran-lang/minpack and fortran-lang/fpm (see
`tests/ScipSharp.Indexer.Fortran.Tests/testdata/SOURCES.md`); the full LAPACK tree (3,700 files, 1.6M lines),
fortran-lang/stdlib and fpm index lint-clean.
