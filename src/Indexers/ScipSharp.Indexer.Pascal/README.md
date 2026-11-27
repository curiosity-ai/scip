# ScipSharp.Indexer.Pascal

Native C# SCIP indexer for **Pascal**: ISO 7185 (and Extended Pascal ISO 10206, best effort), Turbo/Borland Pascal,
and Free Pascal in its `fpc`, `objfpc`, `delphi`, `tp`, `iso` and `macpas` modes, including Lazarus projects
(`.lpi`), packages (`.lpk`), programs (`.lpr`) and forms (`.lfm`). Run with `scip index pascal`.

It is a pure managed implementation: no `fpc`, `lazbuild`, `pasls`, GNU Pascal or any other toolchain is started.

```bash
scip index pascal --project-root path/to/project [-- --search-path=../lib --define=DEBUG]
scip lint index.scip
```

## Options

| Option | Meaning |
|---|---|
| `--search-path=<dirs>` / `-Fu<dir>` | extra unit directories (`;`/`,` separated, repeatable). Units found there resolve but are not indexed as documents; their symbols become external symbols (with documentation). |
| `--include-path=<dirs>` / `-Fi<dir>` | extra directories for `{$I}` / `{$INCLUDE}` |
| `--define=<NAME[=value]>` / `-d<NAME>` | conditional symbol for `{$IFDEF}` / `{$IF}` (defaults: an FPC 3.2.2 x86_64 Linux set: `FPC`, `VER3_2`, `UNIX`, `LINUX`, `CPU64`, ... and `FPC_FULLVERSION=30202`) |
| `--undefine=<NAME>` | remove a default define |
| `--mode=<mode>` | default compiler mode (`objfpc` by default, `iso` for `.p` files); `.lpi`/`.lpk` `SyntaxMode`/`-M` and `{$mode}` take precedence |
| `--encoding=<name>` | code page of files that are not valid UTF-8 (default `windows-1252`; e.g. `cp437`, `cp850`, `cp1251`) |
| `--package-name=<name>`, `--package-version=<v>` | override the package of project symbols |
| `--no-forms` | do not index `.lfm` files |

## What is indexed

* **Files**: `.pas`, `.pp`, `.p`, `.lpr` under the project root (hidden dirs, `backup/`, `__history/` skipped);
  include files (`{$I}`, and C-preprocessor `#include`/`#if` lines as used by Pascal-P5) get their own documents;
  text `.lfm` forms. Every document sets `position_encoding` explicitly: UTF-8 byte offsets for UTF-8 files (BOM
  optional), UTF-16 offsets for UTF-16 files and for legacy 8-bit files (decoded with `--encoding`; one byte is one
  UTF-16 unit there, so offsets equal byte offsets).
* **Lexing**: `{ }`, `(* *)`, `//` comments (nested `{ }` when the FPC/OBJFPC mode is explicit, with a fallback when
  nesting is unbalanced), `&`-escaped identifiers, `$hex`, `&octal`, `%binary`, `#13#10` / `^M` character codes,
  `(. .)` digraphs, case-insensitive keywords per mode. Classic sources that use later reserved words (`string`,
  `object`, `operator`, ...) as identifiers are detected at the declaration and demoted to identifiers.
* **Preprocessor**: `{$mode}`, `{$IFDEF}/{$IFNDEF}/{$IFOPT}/{$IF}/{$ELSEIF}/{$ELSE}/{$ENDIF}/{$IFEND}` with an
  expression evaluator (`defined()`, comparisons, `and/or/not`), `{$DEFINE}/{$UNDEF}`, `{$I}`/`{$INCLUDE}`.
  Inactive code produces no occurrences.
* **Declarations**: programs (with ISO program parameters), units (interface/implementation/initialization/
  finalization), libraries, Extended Pascal modules (`import` = `uses`, `export` skipped), labels, const/typed
  const/resourcestring, types (enumerations, subranges, sets, pointers incl. forward `^T` within a type section,
  arrays incl. conformant array parameters, files, records with variant parts, TP `object`, classes, interfaces,
  class/record/type helpers, advanced records, procedural types, generics `generic`/`specialize` and Delphi-style
  `<T>`), variables (`absolute`, initialised), routines (nested to any depth, `forward`, `external`, overloads,
  operators `operator +` and `class operator`, methods incl. `TOuter.TInner.Method`), properties (indexed,
  `default`), fields, parameters, `Result`, `on E: Exception` variables.
* **Resolution** (a one-pass binder that follows the compiler's own rules): static nested scopes, `with`
  statements (fields of records/objects/classes, also through pointers), class members through ancestors (and the
  implicit `TObject`), helpers, generic type-argument substitution (`specialize TList<TItem>` → `Items[i].Name`),
  `inherited`, typed record constants `(X: 0; Y: 0)`, TP `New(P, Init(..))`, unit-qualified names (`SysUtils.Trim`,
  dotted unit names), overload choice by argument count and types, uses of global operator overloads.
  Units are compiled in two phases (interface, then implementation) on demand, so circular implementation-section
  `uses` work; `uses ... in 'file'`, `.lpi`/`.lpk` `OtherUnitFiles`/`IncludeFiles` and custom `-d`/`-Fu`/`-Fi`
  options are honoured.
* **Output**: definitions (routine bodies are the definition, interface/class/forward declarations become
  `forward_definition`), references (with `WriteAccess` on assignment targets and `for` variables, `Import` on
  `uses` entries), `SymbolInformation` (kind, display name, signature from the declaration, the comment block above
  or the trailing `//` comment as documentation), inheritance/override/interface-implementation relationships,
  enclosing ranges of routines and type declarations. `.lfm` forms reference the form class/variable, component
  fields and types, and `OnXxx = Handler` methods.
* **External symbols**: units that are not part of the project still resolve: a built-in table describes the
  commonly used parts of the FPC RTL/FCL (System, SysUtils, Classes, Math, StrUtils, fgl, Generics.Collections, Crt,
  Dos, Objects, fpcunit, ...) and the LCL (Forms, Controls, StdCtrls, ...). Names from a unit unknown to the table
  are attributed to it when it is the only candidate in `uses` (otherwise left unresolved rather than guessed).

## Symbol scheme

```
scip-pascal <manager> <package> <version> <descriptors>
```

| Package | Used for |
|---|---|
| `lazarus <Name> <Version>` | files listed in / below a `.lpk` (name and `Major.Minor.Release` version) or `.lpi` (title, version `.`) |
| `. <root-dir-name> .` | other project files (override with `--package-name/--package-version`) |
| `fpc rtl .` / `lazarus lcl .` | built-in RTL/FCL and LCL units |
| `. . .` | units that are neither in the project nor in the built-in table |
| `. <dir-name> .` | units found through `--search-path` outside the project |

Descriptors (names keep their declared spelling; Pascal is case-insensitive, lookups are too):

| Entity | Example |
|---|---|
| unit / program / library / module | `` `ThreadSafeCollections.List`/ `` |
| type | `Shapes/TCircle#` |
| field, variable, constant, property, enum member | `Shapes/TCircle#Radius.`, `Bank/accounts.`, `Bank/Kind#savings.` |
| routine / method / operator (overloads: `(+1)`, `(+2)` in declaration order) | `Collections/Max(+1).`, `Collections/+().` |
| nested routine | `Bank/Total().Add().Check().` |
| generic type parameter | `Collections/TTypedList#[T]` |
| parameters, locals, local types/consts, labels, `Result`, exception variables | `local N` |

## Design notes

* **One-pass, compiler-shaped binder.** Pascal is declare-before-use, so the parser enters declarations into scopes
  as it goes and resolves every identifier at its use, like Wirth's compilers do; the only deferred lookups are
  pointer targets (`PNode = ^TNode`) at the end of a `type` section and forward classes. Expression types are
  tracked along designators (`a.b[i]^.c`) to resolve members; arithmetic results are approximated.
* **Error tolerance.** The parser never throws on malformed input: every loop makes progress, statements resync on
  `;`/keywords, and section keywords always end statement lists. Each unit is guarded so an internal failure only
  stops that file (reported on stderr). Indexing runs on a thread with a large stack for deeply nested sources.
* No code was taken from GPL/LGPL projects (FPC compiler, fcl-passrc, Lazarus CodeTools, pascal-language-server);
  they were consulted for behaviour only. The RTL table is a hand-written summary.

## Known gaps

* Overload resolution uses argument counts and coarse type families, not FPC's full conversion rules.
* Members of generic parameters with constraints (`T: TFoo`), `class of` dispatch, and anonymous-method captures
  are not resolved; generic substitution is one level deep (direct `T` fields, properties and function results).
* Delphi-mode `Foo<T>(...)` generic *method calls* and inline `var` declarations are not parsed (the `delphi`
  indexer covers Delphi); Objective-Pascal (`objcclass`) and `macpas` specifics are only parsed loosely.
* `{$IF}` only knows the predefined/`--define` symbols (no `declared()`, `sizeof` of user types); `{$I %DATE%}`-style
  macros and `$(LazarusDir)` macros in Lazarus search paths are ignored.
* Extended Pascal schemata, `export` renaming and `protected`/`bindable` are not modelled. Binary `.lfm`/`.dfm` forms are
  skipped. Assembler blocks are skipped.
* External units outside the built-in table produce inferred symbols whose kind is guessed from use
  (`Foo().` when called, `Foo#` in type position, otherwise `Foo.`).

## Tests and corpora

`tests/ScipSharp.Indexer.Pascal.Tests`: lexer/preprocessor tests, semantic tests (nesting, forward merge, `with`,
variant records, units, overloads, generics, helpers, operators, keyword demotion, encodings), golden snapshots of
ISO, Turbo Pascal and Lazarus fixtures, lint checks, fuzzed/truncated-input robustness, and a coverage check over
two real-world corpora (see `testdata/SOURCES.md`): Pascal-P5 (public domain / BSD-2) and
ThreadSafeCollections-FP (MIT). An optional cross-check compiles the objfpc fixture with `fpc` when it is installed.

## Research references

* [Pascal-P5](https://github.com/samiam95124/Pascal-P5) (public domain / BSD): ISO 7185 compiler and test programs, used
  as corpus; its sources showed the C-preprocessor convention and classic identifiers (`operator`).
* [tree-sitter-pascal](https://github.com/Isopod/tree-sitter-pascal) (MIT): its test corpus was a checklist of FPC
  and Delphi syntax (generics, helpers, attributes, operators, modules).
* [ThreadSafeCollections-FP](https://github.com/ikelaiah/threadsafecollections-fp) (MIT) and
  [ObjPasUtils](https://github.com/Warfley/ObjPasUtils) (BSD-2): modern objfpc code with heavy generics, advanced
  records and class operators, used to measure resolution coverage.
* FPC documentation (reference guide: modes, directives, reserved words) and the behaviour of FPC's `fcl-passrc`,
  Lazarus CodeTools and [pascal-language-server](https://github.com/genericptr/pascal-language-server)
  (GPL/LGPL: ideas only, no code).
