// Ported from abaplint packages/core/src/abap/2_statements/expressions/_lob_handle_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public static class LobHandleTypes
{

    public static IStatementRunnable LobColumns()
    {
        return AltPrio(
        Seq("FOR", Opt("OTHER"), AltPrio("BLOB", "CLOB"), "COLUMNS"),
        Seq("FOR", "ALL", Opt("OTHER"), AltPrio("BLOB", "CLOB"), "COLUMNS"),
        Seq("FOR", "ALL", "COLUMNS"),
        Seq("FOR", "COLUMNS", Plus(Expr<FieldChain>())));
    }

    public static IStatementRunnable LobKind()
    {
        return AltPrio("LOCATOR", "READER", "WRITER", Seq("LOB", "HANDLE"));
    }

    public static IStatementRunnable LobHandleType()
    {
        return Seq("TYPE", Expr<TypeName>(), Opt("READ-ONLY"),
                 Plus(Seq(LobKind(), LobColumns())));
    }
}
