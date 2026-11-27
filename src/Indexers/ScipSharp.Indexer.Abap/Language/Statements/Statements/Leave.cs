// Ported from abaplint packages/core/src/abap/2_statements/statements/leave.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Leave : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var retu = Seq(Expr<AndReturn>(), "TO SCREEN", Expr<Source>());

        var transaction = Seq("TO TRANSACTION",
                                Expr<Source>(),
                                Opt("AND SKIP FIRST SCREEN"));

        var ret = Seq("LEAVE",
                        Opt(Alt(Seq("TO CURRENT TRANSACTION", Opt("AND SKIP FIRST SCREEN")),
                                Seq(Opt("TO"), "LIST-PROCESSING", Opt(retu)),
                                "LIST-PROCESSING",
                                "SCREEN",
                                transaction,
                                "PROGRAM",
                                Seq("TO SCREEN", Expr<Source>()))));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
