# Scip.Indexer.Clang

C and C++ SCIP indexer, ported (by behaviour, not structure) from `.reference/scip-clang`.

## Design decision: a pure C# front end

Upstream is a Clang tool: it runs the real Clang front end and walks the typed AST. A .NET program cannot embed
Clang, and this port has a hard requirement of **no runtime dependency on clang, libclang/ClangSharp or any other
child process**. So the front end is reimplemented natively:

| Stage | File | What it does |
|---|---|---|
| compile_commands.json | `CompilationDatabase.cs`, `CommandLineCleaner.cs` | `command` and `arguments` forms, POSIX-shell splitting, stripping of `-c/-o/-M*`, `-march/-mcpu/-mtune`, `-m(no-)fix-*` (port of upstream `CommandLineCleaner`); `-I/-isystem/-iquote/-D/-U/-include/-std` are honoured. Without a database, `*.c/.cc/.cpp/.cxx/.cu` files under the root are indexed with default flags. |
| Lexer | `Lexer.cs` | C/C++ tokens over UTF-8 bytes, so columns are already SCIP `UTF8CodeUnitOffsetFromLineStart`. Comments, line continuations, raw strings. |
| Preprocessor | `Preprocessor.cs` | `#if/#ifdef/#elif/#else/#endif` with full integer expression evaluation (`defined`, `__has_include`), object- and function-like macros (`#`, `##`, variadics, hide-sets), `#include` resolution (including file dir, then `-I`), `#pragma once`. Emits include and macro definition/use events. Includes that cannot be found (system headers) are skipped. |
| Parser + name resolution | `CppParser.cs`, `SymbolTable.cs` | Tolerant declaration-level C++ parser building a scope tree (namespaces, classes, enums, functions, blocks, templates), then approximate resolution of identifiers in types, initialisers and bodies (qualified names, `using namespace`, base classes, receiver types of `.`/`->` from declared variable types, overload choice by argument count). Same trade-off as upstream's `ApproximateNameResolver`, applied to *all* code since there is no type checker. |
| Types | `TypeCanon.cs`, `WyHash.cs` | Native model of Clang's canonical type printing (C-style `PrintingPolicy`: `_Bool`, `struct S`, `void (void)`, `type-parameter-D-I`, `> >`) and wyhash, so function disambiguators equal upstream's byte for byte. |
| System headers | `BuiltinHeaders.cs` | Built-in libc/libstdc++-shaped declarations (`<vector>`, `<string>`, `<map>`, `<memory>`, `<cstdio>`, ...) used when a header is not on the include path; they live outside the project so uses become references to external symbols (`std/vector#push_back(...).`). |
| Docs | `CommentIndex.cs` | Raw comments above / trailing a definition become `SymbolInformation.documentation`; every file symbol gets `File: <path>`. |
| Glue | `TranslationUnitIndexer.cs` | One TU -> occurrences per project file; symbol construction with `Scip.SymbolFormatter` under upstream's `cxx . . $ ` prefix. |
| Driver | `ClangIndexer.cs` | Shards translation units across `--jobs N` workers (default: processors), merges occurrences per header (set semantics, so a header included by many TUs yields one document), writes with `Scip.IndexWriter`. |

Symbol naming follows upstream `SymbolFormatter.cc`: namespaces `a/`, types `T#`, fields/variables/enumerators `x.`,
functions `f(<disambiguator>).`, per-document numbered `local N` for parameters and block variables,
`<file>/path` symbols for documents and `#include` targets, `$anonymous_namespace_<file>` and
`$anonymous_type_<hash>_<n>`, macros as `path:line:col!`. Positions are single-line ranges in UTF-8 byte offsets.

CLI arguments (after the project root): `--compdb-path FILE`, `--jobs N`.

## Tests (`tests/Scip.Indexer.Clang.Tests`)

* compdb parsing (`simple.json`) and command cleaning.
* Preprocessor/lexer unit tests.
* **Upstream snapshots** (`test/index/*`): the snapshot comment annotations are parsed and compared with our output;
  each case asserts a minimum coverage (regression floor). No external compiler is used; nothing is skipped.

Measured coverage of the upstream definition/reference annotations (`UpstreamSnapshotCoverageExactHashes` compares
function disambiguators **exactly**): functions 100%, methods 100%, ctors/dtors 100%, operators 97%, function templates
100%, types/templates 100%, inheritance 100%, vars/templates 100%, vars/members 97%, types/types 91%, body 100%,
vars 100%; namespaces 89% (hashes normalised).

## What is modelled now

* **Disambiguators**: `f(<wyhash(canonical type)>)` with Clang's canonical type string (typedef expansion, tag keywords,
  qualified names, `type-parameter-0-1` / `template-parameter-0-2`, pack `...`, template-id arguments, implicit
  `noexcept` of defaulted special members, destructors and `operator delete`, `operator new`/conversion/literal names).
* **Operators**: declarations (`operator<<`, `operator()`, `operator[]`, `operator->`, conversions, `new/delete[]`, `""_x`)
  and uses typed from declared types (binary, postfix, call, subscript, `->`, user-defined literals); overloads are chosen
  by arity and literal/class argument kinds.
* **Relationships**: classes -> all transitive bases, virtual methods -> nearest overridden declaration per base path
  (`is_implementation` + `is_reference`), exactly as in upstream snapshots.
* **Using-declarations**: `using Base::f;` references every overload and defines a shadow symbol per function; uses still
  resolve to the underlying declaration (dependent ones in templates produce no shadow).
* **Templates**: template parameters visible in class bodies, `template<>` specialisations (own hash / reference to the
  primary), partial specialisation names, template-template parameters, dependent member typedefs.
* **Constructors**: `D d;`, `D d{..}`, `D d = {..}`, `D d = D()`, `D d = move(x)` and base initialisers reference the
  selected user-declared constructor (default / copy / move by argument value category).
* **Type tracking**: `auto` from initialisers, member/call chains (`a.b().c`), `this`, pointer vs value for `->`.
* **Local classes** are named `f(hash).C#`; namespace aliases resolve silently; docs from comments.

## Known gaps (honest list)

* **No type checker.** Overloads are picked by arity and simple argument kinds; ADL beyond the argument class's
  namespace, implicit conversions, template argument deduction, `auto` from lambdas/templates and range-for element
  types are not modelled.
* The built-in header model is small and only approximates libstdc++ (default template arguments are not filled in for
  canonical names, so hashes of functions taking e.g. `std::vector<int>` may differ; `std::string` is special-cased).
* Non-type template arguments that are expressions (`int(E::E0)`) are printed as written, not evaluated.
* Template instantiation *references* resolve to the primary template's symbols; implicit instantiations have no own symbols.
* Macro-expanded tokens produce no occurrences; `#include` of system headers emits no `<file>` reference; lambda
  `operator()` symbols; anonymous struct members of function-local types.
* Not ported: package map / cross-repo symbols, `.cu`/CUDA specifics, the multi-process driver/IPC, crash/timeout
  robustness machinery, response files, Windows/MSVC flags, concepts/requires semantics.
* A parse failure in one TU is reported and the exit code is 1, but other TUs are still emitted.
