// Ported from abaplint packages/core/src/stuff.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)

namespace ScipSharp.Indexer.Abap;

using Stmt = ScipSharp.Indexer.Abap.Statements.Statements;

public static class Stuff
{
    public static readonly List<System.Type> SELECTION_EVENTS =
    [
        typeof(Stmt.StartOfSelection),
        typeof(Stmt.Get),
        typeof(Stmt.AtSelectionScreen),
        typeof(Stmt.AtLineSelection),
        typeof(Stmt.AtPF),
        typeof(Stmt.AtUserCommand),
        typeof(Stmt.EndOfSelection),
        typeof(Stmt.Initialization),
        typeof(Stmt.TopOfPage),
        typeof(Stmt.LoadOfProgram),
        typeof(Stmt.EndOfPage),
    ];

    public static readonly List<System.Type> DECLARATION_STUFF =
    [
        typeof(Stmt.Type),
        typeof(Stmt.TypeBegin),
        typeof(Stmt.TypeEnum),
        typeof(Stmt.TypeEnumBegin),
        typeof(Stmt.Data),
        typeof(Stmt.DataBegin),
        typeof(Stmt.Constant),
        typeof(Stmt.ConstantBegin),
        typeof(Stmt.Static),
        typeof(Stmt.StaticBegin),
        typeof(Stmt.FieldSymbol),
        typeof(Stmt.Ranges),
        typeof(Stmt.Tables),
        typeof(Stmt.Include), // this is not super correct, but anyhow
        typeof(Stmt.Parameter),
        typeof(Stmt.SelectionScreen),
        typeof(Stmt.Define),
    ];
}
