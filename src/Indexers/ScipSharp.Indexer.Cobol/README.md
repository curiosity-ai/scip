# ScipSharp.Indexer.Cobol

Native C# SCIP indexer for COBOL (`scip index cobol`). There is no upstream `scip-cobol`; this is an original
implementation. **Pure managed code**: no COBOL compiler (GnuCOBOL, IBM, Micro Focus), preprocessor or language
server is needed or started.

```
scip index cobol --project-root <dir> --output index.scip [options]
```

| Option | Meaning |
|---|---|
| `--source-format=auto\|fixed\|free\|variable` | Reference format (default `auto`: fixed unless the layout looks free; `>>SOURCE` and `$SET SOURCEFORMAT` switch it per file anyway). |
| `--copybook-dir=<dir>` | Copybook search directory, relative to the root or absolute; repeatable, or a `,`/path-separator list. |
| `--program-extensions=<list>` | Program files (default `cbl,cob,cobol,pco,sqb,ccp`, case-insensitive). |
| `--copybook-extensions=<list>` | Copybook files (default `cpy,copy,cpb,cbk,inc,cblcpy`). |
| `--debug-lines` | Treat `D` indicator lines as code (`WITH DEBUGGING MODE`); default: comments. |
| `--no-unused-copybooks` | Skip copybooks that no program copies (default: index them on their own). |
| `--exclude=<dir>` | Skip a directory (repeatable). Hidden directories, `node_modules`, `bin`, `obj`, `target` are always skipped. |
| `--project-name=<n>`, `--project-version=<v>` | Package of the symbols (default: root directory name, `0.0.0`). |
| `--quiet` | No warnings (missing copybooks, recursive COPY). |

## Pipeline

1. **Lexer** (`Syntax/Lexer.cs`): reference-format aware. Fixed format: columns 1–6 sequence, 7 indicator
   (`*` `/` comment, `D` debug, `-` continuation, `$` Micro Focus directive), 8–72 program text, 73–80 ignored. Free
   and Micro Focus variable format. `*>` floating comments, `>>SOURCE [FORMAT] [IS] FREE|FIXED`, `$SET SOURCEFORMAT"…"`,
   `CBL`/`PROCESS` option lines, continued alphanumeric literals and words, `X'…'`/`N"…"`/`Z'…'` literals, doubled
   quotes, picture strings as single tokens, pseudo-text delimiters. Columns are UTF-16 offsets into the original line
   (documents set `PositionEncoding.UTF16CodeUnitOffsetFromLineStart`); sources are read as UTF-8 with a Latin-1 fallback.
2. **Preprocessor** (`Syntax/Preprocessor.cs`): `COPY name|'name' [OF|IN lib] [SUPPRESS] [REPLACING …]` (recursive, with
   cycle/depth protection), `EXEC SQL INCLUDE … END-EXEC`, `++INCLUDE`, `REPLACE … / REPLACE OFF`, dropping `EJECT`,
   `SKIPn`, `TITLE`. REPLACING supports whole text-word sequences, IBM-style partial words (`==:TAG:==`, `==(PFX)==`,
   also inside words such as `FLG-(PFX)-OK`) and COBOL 2002 `LEADING`/`TRAILING`. Every token keeps its own file and
   position, so replaced names still point at the copybook text they came from (partial replacements are mapped back
   to the changed columns of the original word).
3. **Copybook resolution** (`Project/CopybookResolver.cs`): path-like literals relative to the including file; a
   directory named like the `OF`/`IN` library; the including file's directory; each `--copybook-dir`; finally any project
   file whose stem matches (preferring copybook extensions, conventional directories such as `copy`, `copybook`, `cpy`,
   `copylib`, `include`, then the shortest path). Case-insensitive, extensions tried in order.
4. **Structure parser** (`Analysis/StructureParser.cs`, pass 1): program units (`PROGRAM-ID`, `CLASS-ID`, `METHOD-ID`,
   `FUNCTION-ID`; nesting via `END PROGRAM`, several programs per file with or without `END PROGRAM`, `COMMON`),
   identification-division comment entries, `EXEC … END-EXEC` blocks, `SELECT` file names, `SPECIAL-NAMES` mnemonic/class
   names, `FD`/`SD`, data description entries (01–49 hierarchy, 66 `RENAMES`, 77, 78, 88; `FILLER`/unnamed; `GLOBAL`;
   `INDEXED BY`), sections, paragraphs and `ENTRY` points. Tolerant of missing periods and garbage.
5. **Resolver** (`Analysis/Resolver.cs`, pass 2): data references with `OF`/`IN` qualification (ancestors in order,
   file-name qualifiers; ambiguous unqualified names fall back to the first declaration), `GLOBAL` items and files of
   containing programs, class data inside methods; procedure names after `PERFORM`, `THRU`, `GO TO` (incl.
   `DEPENDING ON` lists), `ALTER … TO PROCEED TO`, `INPUT/OUTPUT PROCEDURE` (paragraph `OF` section, current section
   preferred); `REDEFINES`/`RENAMES` operands among their siblings; file, index and special names; `CALL`/`CANCEL 'x'`,
   `SET … TO ENTRY 'x'`, `EXEC CICS LINK/XCTL … PROGRAM('x')` to nested, sibling or project programs and `ENTRY` points
   (otherwise an external symbol); `INVOKE obj 'method'` when the method name is unique; user `FUNCTION`s; host
   variables (`:name`) in `EXEC SQL`; operands in parentheses in `EXEC CICS`/`EXEC DLI` (data first, then paragraphs).
6. **Emission** (`CobolProjectIndexer.cs`): pass 1 over all programs collects program-ids/entries and the set of copied
   files; pass 2 streams one document per program through `IndexWriter`; copybook documents (with occurrences from every
   program that copies them, de-duplicated) follow, then external symbols.

## Symbols

`scip-cobol cobol <package> <version> <descriptors>`, names upper-cased:

| Construct | Descriptor | Kind |
|---|---|---|
| program / nested program | `PROG/`, `OUTER/INNER/` | Module |
| class, method, user function | `CLS#`, `CLS#METH().`, `FN().` | Class, Method, Function |
| data item (named ancestors) | `PROG/REC.GROUP.FIELD.` | Struct (group), Field, Variable (01/77 elementary) |
| condition name (88), constant (78) | `PROG/REC.FLAG.IS-ON.` | Constant |
| index name | `PROG/TABLE.ENTRY.IDX.` | Variable |
| file (SELECT/FD/SD) | `PROG/CUSTFILE#` | File |
| section, paragraph | `PROG/SECT().`, `PROG/SECT().PARA().` (or `PROG/PARA().`) | Namespace, Function |
| ENTRY point | `PROG/NAME(entry).` | Function |
| special (mnemonic/class) name | `PROG/TOP-OF-PAGE.` | Constant |
| copybook | `` `path/BOOK.cpy`/ `` (zero-width definition at the top of its document) | File |
| unresolved CALL target | `NAME/` (external symbol) | Module |

**Copybook ownership.** Anything defined by text that comes from a copybook (data items, paragraphs, files, …) is named
under the copybook, e.g. `` `app/cpy/CVACT01Y.cpy`/ACCOUNT-RECORD.ACCT-ID. ``; the chain starts at the first
ancestor defined in the same copybook. A copybook field is therefore *one* symbol across every program that copies it,
which is what impact analysis wants. REPLACING that changes a name yields a different symbol at the same location.
Copybooks outside the project root have no document; their definitions belong to the including program instead.

`SymbolInformation` carries kind, display name (as written) and documentation: the normalised entry text in a
`cobol` code block (e.g. `05 WS-AMOUNT PIC S9(7)V99 COMP-3 VALUE +0`), "Record of file X" for FD/SD records, `COMMON`.
Definitions carry enclosing ranges (program, section, paragraph, data entry, SELECT) when they lie in one document.

## Tests (`tests/ScipSharp.Indexer.Cobol.Tests`, TUnit)

Lexer, preprocessor (COPY/REPLACING/REPLACE/partial words/cycles), resolution (qualification, scoping, sections,
copybook search, files, kinds/documentation, error tolerance), option parsing and CLI entry point, golden snapshots of
the fixtures in `testdata/snapshots` (`scip snapshot` format; `SCIP_UPDATE_SNAPSHOTS=1` regenerates them), a lint check
equivalent to `scip lint` on every fixture, and robustness tests over AWS CardDemo and 22 NIST COBOL85 programs (see
`testdata/SOURCES.md`). Run: `dotnet test --project tests/ScipSharp.Indexer.Cobol.Tests`.

## Known gaps

- No full grammar: statements are not parsed, so there are no read/write access roles (references are `ReadAccess`),
  no type/usage inference and no diagnostics. Resolution is name-based: a word that matches no definition is ignored
  (special registers, intrinsic functions, CICS/DB2 system copybooks that are not in the project).
- Ambiguous unqualified references (illegal COBOL, but common when a copybook is copied twice) pick the first declaration.
- Conditional compilation (`>>IF`/`>>DEFINE`, `$IF`) is not evaluated: all branches are indexed. `>>D`, `DECIMAL-POINT IS
  COMMA` numeric literals, and IBM `REPLACING` with non-delimited partial words are not handled.
- Report Writer (`RD`, report groups), screen section and communication section names are indexed as data items only.
- OO COBOL: classes/methods/`INVOKE 'literal'` only; no `REPOSITORY` aliases, `INTERFACE` semantics or object references.
- `CALL identifier` is a data reference only (the called program is not inferred from `VALUE` clauses); dynamic CICS
  `PROGRAM(var)` likewise. JCL, BMS maps, DB2 DDL/DCLGEN tables and IMS PSBs are not indexed.
- Copybooks that no program copies are indexed standalone; they may then lack context (e.g. a procedure copybook whose
  data lives in the program).

## Research

Investigated while designing this (no code copied; EPL/GPL sources were consulted only for language semantics):

- [eclipse-che4z/che-che4z-lsp-for-cobol](https://github.com/eclipse-che4z/che-che4z-lsp-for-cobol) (EPL-2.0) — the most
  complete open COBOL language server (Broadcom); surveyed for scope (copybook folders/settings, dialects, CICS/DB2
  preprocessing). Its test sources served as an uncommitted robustness corpus.
- [uwol/proleap-cobol-parser](https://github.com/uwol/proleap-cobol-parser) (MIT) — ANTLR4 COBOL 85 grammar and
  preprocessor; its preprocessor test cases (fixed/variable/tandem formats, literal continuation, `EXEC` blocks, comment
  entries, COPY variants) were used to cross-check lexer behaviour, and its test resources as an uncommitted corpus.
- [antlr/grammars-v4 `cobol85`](https://github.com/antlr/grammars-v4/tree/master/cobol85) — the grammar ProLeap builds on
  (reserved words, clause structure); not used directly.
- [yutaro-sakamoto/tree-sitter-cobol](https://github.com/yutaro-sakamoto/tree-sitter-cobol) (MIT) — tree-sitter grammar for
  opensource COBOL; source of the NIST COBOL85 copy used in the tests. A tree-sitter based approach was rejected: it needs
  a native parser library and does not handle COPY/REPLACING, which drives most cross-file navigation.
- IBM Enterprise COBOL Language Reference (reference format, COPY/REPLACE, partial-word replacement), the COBOL 2014
  standard (free format, `>>SOURCE`, `LEADING`/`TRAILING`), Micro Focus directives (`$SET`, variable format).
- Corpora: [aws-samples/aws-mainframe-modernization-carddemo](https://github.com/aws-samples/aws-mainframe-modernization-carddemo)
  (Apache-2.0, committed), NIST CCVS85 (public domain, partly committed),
  [openmainframeproject/cobol-programming-course](https://github.com/openmainframeproject/cobol-programming-course)
  (CC-BY-4.0) and [IBM/zopeneditor-sample](https://github.com/IBM/zopeneditor-sample) (checked locally only).
