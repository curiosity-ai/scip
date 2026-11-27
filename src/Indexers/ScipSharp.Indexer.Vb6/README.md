# ScipSharp.Indexer.Vb6

Native C# SCIP indexer for **Visual Basic 6** projects and **VBA** module exports. Run with `scip index vb6`.
There is no upstream `scip-vb6`; this is an original implementation. It never uses VB6, VBA, Office or COM: the lexer,
parser, preprocessor, project-file readers and the name resolver are plain managed code.

## Scope

- **Projects**: every `.vbp` below the root (or the ones named with `--project`). `Module=Name; file`, `Class=Name; file`,
  `Form=`, `UserControl=`, `PropertyPage=`, `UserDocument=`, `Designer=` items; `Name`, `MajorVer/MinorVer/RevisionVer`,
  `Startup`, `CondComp`, `Reference=*\A...vbp` project references. Paths are resolved with `\` separators and
  case-insensitively, so Windows projects index on Linux. `.vbg` groups are indexed too.
- **Loose files**: `.bas/.cls/.frm/.ctl/.pag/.dob/.dsr` files that no `.vbp` lists (VBA exports, snippets) form one
  synthetic project per directory, named after the directory.
- **Files shared by several projects** are indexed once, as part of the first project (by path) that lists them; the
  other projects resolve to the same symbols. Files a `.vbp` pulls in from **outside the root** are parsed for resolution
  only; the symbols they define are emitted as external symbols.
- **Designer blocks** (`.frm/.ctl/...`): the form itself, every control (`Begin VB.CommandButton Command1`), control
  arrays (`Index =`), menus; nested `BeginProperty` blocks are skipped. Class headers (`VERSION 1.0 CLASS / BEGIN ... END`).
- **Declarations**: `Attribute VB_Name/VB_Description/VB_Exposed`, `Sub`/`Function`/`Property Get|Let|Set` (the three
  property procedures share one symbol), `Declare [PtrSafe] Sub|Function ... Lib ... [Alias ...]`, `Type`/`Enum` and their
  members, `Const`, module variables (`Dim/Private/Public/Global`, `WithEvents`, arrays, fixed-length strings),
  `Event`, `Implements`, parameters, locals (`Dim`, `Static`, `Const`, `ReDim`), line labels.
- **Lexing**: line continuations (` _`), `'` and `Rem` comments (including comments continued with ` _`), bracketed
  identifiers (`[_NewEnum]`), type-suffix characters (`$ % & ! # @`), `&H`/`&O` literals, `#date#` literals (told apart from
  `#1` file numbers), doubled quotes in strings; case-insensitive keywords and names.
- **Conditional compilation** is evaluated like the compiler does: `#If/#ElseIf/#Else/#End If`, `#Const`, `.vbp`
  `CondComp`, `--define`, and the compiler constants `Win16=0 Win32=-1 Win64=0 VBA6=-1 Mac=0` (`VBA7=0` for `.vbp`
  projects, `-1` for loose VBA files). Inactive branches produce no occurrences, so `#If VBA7` / `#Else` pairs of
  `Declare` statements do not produce duplicates.
- **Resolution** (best effort, never fatal): procedure locals and parameters → the module (members, controls, enum
  members, private members) → the project (Public members of standard modules, Public enum members, Public UDTs/enums,
  module names) → referenced projects (classes with `VB_Exposed = True`). Member access is typed through declared types
  (`As`), function/property return types and array element types, `Me`, `New`, `TypeOf ... Is`, `With` blocks (nested,
  leading-dot members), qualified names (`modMain.Area`, `Form1.Command1.Caption`, `Project.Class`, `Module.Type`).
  Assignment targets, `For` variables and `ReDim` targets carry the `WriteAccess` role. `GoTo/GoSub/Resume/On Error GoTo`
  resolve labels. `RaiseEvent` resolves the event.
- **Event handlers and interfaces** are bound by name, as VB does: `Command1_Click` (control), `m_oSocket_OnReceive`
  (`WithEvents` variable), `Form_Load`/`UserControl_Resize` (the designer object), `IShape_Area` (`Implements IShape`).
  The prefix gets a reference occurrence to the control/variable/interface; the procedure gets a relationship:
  `is_implementation` to the interface member, `is_reference` to the event (a project `Event`, or an external
  `scip-vb6 vbp VB . CommandButton#Click().`). The class gets `is_implementation` to each implemented interface.
- **SymbolInformation**: kind, display name, signature documentation (the declaration as written, continuations joined),
  documentation from `VB_Description` attributes, the comment block directly above a declaration (separator lines such as
  `'=====` are dropped) or a trailing comment; control captions; enclosing ranges for modules, procedures, UDTs, enums,
  variables.
- **Encoding**: sources are read as UTF-8 when they carry a BOM or are valid UTF-8, otherwise in the ANSI code page given
  by `--encoding` (default Windows-1252; any Windows code page works, e.g. `1251`, `932`). Positions are UTF-16 code unit
  offsets of the decoded text (`position_encoding = UTF16CodeUnitOffsetFromLineStart` on every document); for
  single-byte code pages these equal byte offsets. `--emit-text` embeds the decoded text in each document, which helps
  consumers that would otherwise decode ANSI files as UTF-8.

## Symbol scheme

```
scip-vb6 vbp <project Name> <MajorVer.MinorVer.RevisionVer> <descriptors>
```

| Entity | Example |
|---|---|
| project (`.vbp` `Name=`) | `scip-vb6 vbp Features 1.2.3 Features/` |
| module, class, form, user control | `scip-vb6 vbp Features 1.2.3 frmMain#` |
| Sub, Function, Declare, Event | `... modMain#Area().`, `... cCircle#Changed().` |
| Property (Get/Let/Set), variable, constant, control | `... cCircle#Radius.`, `... frmMain#cmdGo.` |
| UDT / enum and their members | `... modMain#POINTAPI#X.`, `... modMain#ShapeKind#skCircle.` |
| parameters, locals, labels | `local N` |
| type-library classes (from designer blocks, `As Lib.Class`) and their events | `scip-vb6 vbp VB . CommandButton#`, `scip-vb6 vbp MSComctlLib . ListView#` |

Names keep their declared spelling (VB is case-insensitive; the IDE normalises spelling). Loose files use the
directory name as package and `.` as version. When two projects share a name and version (`Project1` is common), the
later ones (by path) get `Name(<vbp path>)` as package name so their symbols stay distinct.

## Options

```
scip index vb6 [--project=<path>]... [--include-loose-files] [--encoding=<name|codepage>]
               [--define=<NAME[=value]>]... [--emit-text] [-h|--help]
```

## Known gaps

- No type-library / OCX reader: members of COM objects and of the VB/VBA runtime (`MsgBox`, `Collection.Add`,
  `Command1.Caption`) are not resolved; only designer control classes and their handled events become external symbols.
- No implicit variables: without `Option Explicit`, undeclared variables get no occurrences.
- Named arguments (`Foo Bar:=1`) and bang access (`rs!Field`) are not resolved; `VB_GlobalNameSpace` classes and
  `DefType` statements are ignored; default members (`obj(1).x` where `obj` is a class) are not followed.
- `Property Get`/`Let`/`Set` share one symbol (VB has no overloading); its signature lists every accessor.
- `.frx`/`.ctx` binary resources and `.res` files are not read; VBA UserForm controls (stored in `.frx`) are not known.
- `.dsr` designers (DataEnvironment, DataReport) only get their root object.

## Research notes

- ANTLR [grammars-v4 `vb6`](https://github.com/antlr/grammars-v4/tree/master/vb6) (MIT, by U. Wolffgang) and its
  `vba` grammars: used as the checklist for the lexer (keywords, literals, line continuation, date literals) and the
  module/statement grammar. Not used as code.
- [proleap-vb6-parser](https://github.com/uwol/proleap-vb6-parser) (MIT): the layout of its semantic graph (program →
  module → procedure/variable scopes, `Me`, `With`) informed the resolver's scope chain. Not used as code.
- Rubberduck (VBA add-in with a full parser/resolver) is GPL-3.0, so it was deliberately not used; the event-handler and
  `Implements` naming rules here follow the VB6 language reference instead.
- Corpora (MIT): [opensoldat/polyworks](https://github.com/opensoldat/polyworks) (robustness, 24k lines, 20 files) and
  [wqweto/VbAsyncSocket](https://github.com/wqweto/VbAsyncSocket) (`WithEvents`, events, cross-directory classes);
  ad-hoc checks on VBA-tools/VBA-Web, Gagniuc's VB6 apps and the full VbAsyncSocket repository. See
  `tests/ScipSharp.Indexer.Vb6.Tests/testdata/SOURCES.md`.
