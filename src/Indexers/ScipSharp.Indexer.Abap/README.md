# ScipSharp.Indexer.Abap

A SCIP indexer for ABAP. It reads a folder of abapGit sources and needs no SAP system and no external tool.

```
scip index abap --project-root <dir> --output index.scip [--release=v740sp08] [--config=abaplint.json]
                [--project-name=<name>] [--project-version=<v>]
```

The folder holds abapGit files: `zcl_foo.clas.abap`, `zcl_foo.clas.locals_imp.abap`, `zif_bar.intf.abap`,
`zprog.prog.abap`, `zfg.fugr.*.abap` and their `.xml` files, plus dictionary objects (`ztable.tabl.xml`, `zdtel.dtel.xml`, …).
If the root has an `abaplint.json`, its syntax settings (release, error namespace, …) are used.

## How it works

| Layer | Where |
|---|---|
| Lexer, statement parser (all 317 statements, macros), structures, file information | `Language/Lexer`, `Language/Statements`, `Language/Structures`, `Language/FileInformation` |
| Objects and their abapGit XML readers (CLAS, INTF, PROG, FUGR, TYPE, TABL, DTEL, DOMA, TTYP, VIEW, …), registry, DDIC | `Language/Objects`, `Language/Registry.cs`, `Language/DDIC.cs` |
| Syntax check: types, scopes, declarations and resolved references | `Language/Syntax`, `Language/Types` |
| SCIP emission | `AbapProjectIndexer.cs`, `AbapSymbols.cs`, `AbapIndexer.cs` |

The `Language/` folder is a C# port of the abaplint core (`packages/core` of https://github.com/abaplint/abaplint, MIT;
see `THIRD-PARTY-NOTICES.md`). It was verified to match `@abaplint/core` 2.120.80 in tokens, statements, structures and
syntax-check issues on 360 abapGit repositories. It stays close to the TypeScript original, so the strict analyzers of
this repository are turned off for it in `Language/.editorconfig`.

The indexer:

1. parses every file and runs the syntax check once per object (class, interface, program, function group, type pool);
2. records every declaration under its file, position and name: classes and interfaces (global or local), methods,
   their parameters, attributes, constants, types, events, aliases, forms with their parameters, function-module
   parameters, program-level data and types, and local variables;
3. maps each reference the syntax check resolved (method calls, data reads and writes, type and class usages,
   `PERFORM`) to the declaration it points to;
4. adds what the syntax check resolves without recording a reference: `CALL FUNCTION 'NAME'`, `INCLUDE`, `SUBMIT`,
   `CLASS … IMPLEMENTATION`, and dictionary types and tables (`TYPE ztable`, `FROM ztable`);
5. adds hover text (the declaring statement and its ABAP Doc `"!` comment) and relationships (super class,
   implemented interfaces, redefined methods).

## Symbols

`scip-abap abap <project-name> <project-version> <descriptors>`; names are upper-case because ABAP is case-insensitive.

| ABAP | Descriptors |
|---|---|
| global class / interface | `ZCL_FOO#` |
| method, attribute, type, event of a class | `ZCL_FOO#BAR().`, `ZCL_FOO#MV_X.`, `ZCL_FOO#TY_X#`, `ZCL_FOO#EV.` |
| method parameter | `ZCL_FOO#BAR().(IV_X)` |
| `METHODS zif_bar~meth REDEFINITION` | the interface's method `ZIF_BAR#METH().` |
| local class of an object | `ZPROG/LCL_FOO#` (`ZCL_FOO/LCL_HELPER#` for class-local types) |
| program or include, its global data, types, forms | `ZPROG/`, `ZPROG/GV_X.`, `ZPROG/TY_X#`, `ZPROG/MY_FORM().` |
| function group (its main program) | `SAPLZFG/`; global data `SAPLZFG/GT_X.` |
| function module and its parameters | `Z_FM().`, `Z_FM().(IV_X)` (FM names are unique in a system) |
| dictionary object | `ZTABLE#`, `ZDTEL#` |
| locals of methods, forms, function modules | `local N` |

Names that are not declared in the sources (SAP standard classes, tables, types, function modules) are emitted as
external symbols in the empty package: `scip-abap abap . . CL_SALV_TABLE#`, `scip-abap abap . . MARA#`.

## Known gaps

- Components of structures (`ls_row-field`) are not linked: the syntax check records only the first name of a chain.
- CDS views (DDLS) are not parsed (abaplint's CDS parser is not part of the port).
- Dynpros, GUI statuses, messages (`MESSAGE e001(zmsg)`) and text symbols are not linked.
- Function-module parameters have no definition occurrence (they are declared in the XML).
- Where the syntax check meets an error (an unknown `Z` object, say), it skips the rest of the enclosing block, as
  abaplint does; references after the error in that block are missing.
