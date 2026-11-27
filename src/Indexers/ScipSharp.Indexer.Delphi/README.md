# ScipSharp.Indexer.Delphi

Native C# SCIP indexer for **Delphi / Object Pascal** (Delphi 7 through Delphi 12 Athens). Run with
`scip index delphi`. It needs no Delphi compiler (`dcc32`/`dcc64`), MSBuild, RAD Studio, FPC or language server: the
lexer, preprocessor, parser and two-pass resolver are implemented in C#.

```bash
scip index delphi --project-root path/to/project --output index.scip
scip index delphi -- --help        # indexer options
scip lint index.scip
```

## What is indexed

| Input | Handling |
|---|---|
| `.pas` units, `.dpr` programs/libraries, `.dpk` packages | parsed; one SCIP document each |
| `{$I file}` / `{$INCLUDE file}` | spliced into the including unit; the include file becomes its own document (its occurrences point into it) |
| `.dfm` / `.fmx` / `.lfm` text forms | paired with the same-named unit; `object Button1: TButton` → references to the form field and the component type; `OnClick = Button1Click` → reference to the handler method; property names (`Caption`, `Font.Name`) → references to the component class's properties. Binary (`TPF0`) forms are skipped |
| `.dproj` | `DCC_UnitSearchPath`, `DCC_Namespace` (unit scope names), `DCC_Define`, `DCC_UnitAlias`, `MainSource`, `VerInfo_Keys` FileVersion |

Every document sets `position_encoding = UTF16CodeUnitOffsetFromLineStart`. Sources are decoded as UTF-8 (with or
without BOM), UTF-16 (BOM) or — when a file is not valid UTF-8 — Windows-1252 ("ANSI", e.g. Delphi 7 sources), and
columns are UTF-16 code units of the decoded text.

Language coverage:

- Lexer: `{ }`, `(* *)`, `//` comments; `///` XMLDoc (attached to the next declaration and rendered as Markdown-ish
  documentation); `&`-escaped identifiers; `#13`/`#$0D` character codes; Delphi 12 multi-line `'''` strings; `$hex`,
  `%binary`, digit separators; Unicode identifiers.
- Preprocessor: `{$DEFINE}`/`{$UNDEF}`, `{$IFDEF}`/`{$IFNDEF}`/`{$IFOPT}`, `{$IF expr}`/`{$ELSEIF}`/`{$ELSE}`/`{$ENDIF}`/
  `{$IFEND}` with `Defined()`, `Declared()` (conservative), `SizeOf()`, `CompilerVersion`/`RTLVersion` and arithmetic/
  comparison/boolean operators; predefined symbols for the chosen platform and compiler version (`VER360`, `MSWINDOWS`,
  `WIN32`/`WIN64`, `CPUX64`, `UNICODE`, …); `{$SCOPEDENUMS}`; inactive branches are skipped.
- Declarations: units (interface/implementation, initialization/finalization), programs, libraries, packages
  (`requires`/`contains`), `uses … in '…'`; classes (abstract/sealed, `class of`), records (with methods, operators,
  variant parts), interfaces/dispinterfaces (GUIDs), old-style `object`s, class and record helpers, generics with
  constraints (types and methods), nested types, enums (scoped and unscoped), sets, arrays, pointers, procedural types
  (`of object`, `reference to`), subranges, strong aliases (`type X`); fields, `class var`, constants, resource
  strings, `var`/`threadvar`, labels; methods (class/static methods, constructors/destructors, `class operator`,
  overloads, `message`, `external`, `forward`, method resolution clauses `procedure IFoo.Bar = Baz;`); properties
  (indexed, `index n`, `default`, class properties, read/write/stored/implements specifiers resolved); attributes
  (`[Foo]` resolves to `FooAttribute` first).
- Bodies: designator chains `A.B(x)[i]^.C` with type flow through fields/properties/functions, constructors, casts
  `TFoo(x)` and `(x as TFoo)`, default array properties and generic substitution (`TList<TFoo>[i].Member`,
  `TDictionary<K,V>`), `for … in` element types (`GetEnumerator`/`Current`), `with` (best effort), `inherited`
  (with and without a name), `Self`, `Result`, inline `var`/`const` (with type inference from a designator
  initialiser), `for var`, `on E: Exception do`, anonymous methods, nested routines, `goto`/labels, `asm` blocks
  (skipped). Assignment targets get the `WriteAccess` role.
- Relationships: class/interface ancestors (`is_implementation`), overridden methods (`is_implementation` +
  `is_reference`), interface methods implemented by name or through a method resolution clause.
- Forward declarations (`TFoo = class;`, `procedure P; forward;`) are the same symbol as the full declaration; the
  forward one has the `ForwardDefinition` role. Implementation-section bodies (`procedure TFoo.Bar`, interface
  routines) reference the declaration (overloads matched by parameter types) and carry the body as enclosing range;
  their parameter names reference the declared parameters.

## Symbol scheme

```
scip-delphi delphi <package> <version> <descriptors>
```

- `<package>`: `--project-name`, else the single `.dproj` (or `.dpr`/`.dpk`) in the root, else the directory name.
  `<version>`: `--project-version`, else the `.dproj` FileVersion, else `.`. Built-in stub units use the packages
  `rtl` (System.*, Winapi.*, Data.*), `vcl` and `fmx` with version `.`; units found in out-of-root search paths use the
  directory name; units that cannot be found at all use `unknown`.
- A unit `System.Generics.Collections` contributes one namespace per segment: `System/Generics/Collections/`
  (programs and packages likewise: `Features/`).
- Types: `Unit/TFoo#`, nested `Unit/TOuter#TInner#`. A generic type that shares its name with another type in the same
  scope (`TProc` / `TProc<T>`) gets an arity suffix: `TProc-1#`.
- Routines and methods: `Unit/Foo().`, `Unit/TFoo#Bar().`; overloads are disambiguated in declaration order:
  `Bar().`, `Bar(+1).`, `Bar(+2).` (interface-section declarations first).
- Fields, properties, constants, variables: `Unit/TFoo#FName.`, `Unit/MaxItems.`. Enum members live under their enum
  type (`Unit/TColor#clRed.`) even when unscoped. Inline anonymous types take the descriptor of their variable
  (`Unit/Point.X.` for `var Point: record X: Integer end`).
- Type parameters `Unit/TList#[T]`, `Unit/TFoo#Bar().[U]`; parameters `Unit/TFoo#Bar().(AValue)`; indexed-property
  parameters `Unit/TFoo#Items.(Index)`.
- Everything declared inside a routine body (locals, nested routines and their parameters, inline variables,
  anonymous method parameters, labels, `on E:` variables) is a `local N` symbol.

Identifiers are escaped with backticks when they are not simple SCIP identifiers (`` Ansi/`TGröße`# ``).

## External symbols (RTL/VCL/FMX)

Embedded declaration-only stubs (written for this indexer, `Stubs/*.pas`) describe the commonly used public surface of
`System`, `System.SysUtils`, `System.Classes`, `System.Generics.Collections`/`Defaults`, `System.Types`,
`System.UITypes`, `System.Math`, `System.StrUtils`, `System.DateUtils`, `System.IOUtils`, `System.IniFiles`,
`System.SyncObjs`, `System.Variants`, `System.TypInfo`, `System.Rtti`, `Winapi.Windows`, `Winapi.Messages`,
`Vcl.Controls`/`Forms`/`StdCtrls`/`ExtCtrls`/`Dialogs`/`Graphics`/`ComCtrls`/`Menus`/`ActnList`, `Data.DB` and
`FMX.Types`/`Controls`/`Forms`/`StdCtrls`, including helpers such as `TStringHelper`. They are parsed with the same
parser, so references to them resolve with type flow (e.g. `TStringList.Create.Add`). Unqualified unit names
(`uses SysUtils, Forms`) are resolved through the project's `DCC_Namespace` list and then the RAD Studio defaults
(`System`, `Winapi`, `Vcl`, `FMX`, `Data`, …), so Delphi 7 and Delphi 12 code produce the same symbols.

Members the stubs do not describe are synthesised on the most-derived external class (`Vcl/Forms/TForm#Foo.`), and
unresolved names in a unit that uses exactly one unknown unit are attributed to that unit. Without parentheses the kind
of a synthesised member is unknown, so it is emitted as a term (`Foo.`); with a call it is a method (`Foo().`).

## Options

```
--project-name=<name>      package name of the project's symbols
--project-version=<v>      package version
--search-path=<dirs>       extra unit search paths, ';'-separated, relative to the root (repeatable)
--namespace=<names>        unit scope names for unqualified uses (repeatable)
-D<sym>, --define=<sym>    define conditional symbols (repeatable, ';'-separated)
--undefine=<sym>           undefine predefined symbols
--platform=<p>             Win32 (default), Win64, Linux64, OSX64, OSXARM64, Android, Android64, iOSDevice64
--compiler-version=<n>     CompilerVersion for {$IF} (default 36 = Delphi 12; 15 = Delphi 7)
--exclude=<dir>            skip a directory (repeatable); .git, __history and __recovery are always skipped
--no-forms                 do not index .dfm/.fmx files
--no-dproj                 ignore .dproj files
```

## Known gaps

- No full type checker: overloads are chosen by argument count (implementations by parameter types); operator
  overloads, implicit conversions and generic type inference for calls (`TArray.Sort(X)`) are not resolved;
  `Declared()` in `{$IF}` is false except for a few RTL version constants.
- `with` statements and inline `var` scopes are approximate (inline variables stay visible until the end of the
  enclosing routine); unresolved designators stop type flow for the rest of the chain.
- Only one branch of conditional code is indexed (the one active for the chosen defines/platform).
- RTL/VCL/FMX coverage is limited to the stubs; other Embarcadero and third-party units that are not part of the
  project resolve to `unknown` units. Synthesised members of external classes are attached to the nearest external
  class, not to the class that really declares them.
- Units declared by several files (copies in tests and examples) produce the same symbols in each document;
  resolution prefers the copy in the same directory, then the search path.
- Class helpers are not scoped (every helper in the program applies), binary DFMs and `.lfm` specifics are not
  handled, `{$I}` with macros (`%DATE%`) is ignored, and Free Pascal modes (`{$mode objfpc}`) are not interpreted —
  FPC/Lazarus code is the domain of the separate `pascal` indexer.

## Research and references

Studied for ideas (no code copied):

- **DelphiAST** (RomanYankovsky, MPL-2.0) — the `SimpleParser` lexer/parser that handles Delphi's context-sensitive
  directives and conditional compilation; confirmed the approach of a hand-written, directive-aware recursive-descent
  parser.
- **SonarDelphi** (integrated-application-development, LGPL-3.0) — its ANTLR grammar and symbol table (unit scopes,
  `System`/`SysInit` scopes, unit-import resolution through namespace prefixes, helper and type scopes) informed the
  scope chain and the unit-scope-name lookup used here.
- **tree-sitter-pascal** (Isopod, MIT) — feature checklist for Delphi/FPC syntax (anonymous methods, attributes,
  generics, helpers, preprocessor-wrapped declarations).
- **pascal-language-server** (genericptr) / Castle Engine `pasls` — LSP servers built on FPC's CodeTools; useful as a
  reference for what navigation users expect (declaration ↔ implementation jumps), not usable here (needs FPC).
- **ANTLR grammars-v4 `delphi`** and Embarcadero's DocWiki (Delphi language guide, conditional compilation, unit scope
  names) for syntax details.
