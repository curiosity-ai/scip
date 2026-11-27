// Ported from abaplint packages/core/src/abap/2_statements/statements/communication.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Communication : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var length = Seq("LENGTH", Expr<Target>());

        var returncode = Seq("RETURNCODE", Expr<Source>());
        var buffer = Seq("BUFFER", Expr<Target>());
        var init = Seq("INIT ID", Expr<Source>(), "DESTINATION", Expr<Target>());
        var allocate = Seq("ALLOCATE ID", Expr<Source>(), Opt(returncode));
        var send = Seq("SEND ID", Expr<Source>(), Opt(Per(buffer, length)), Opt(returncode));
        var deallocate = Seq("DEALLOCATE ID", Expr<Source>(), Opt(returncode));
        var accept = Seq("ACCEPT ID", Expr<Source>());

        var receive = Seq("RECEIVE ID",
                            Expr<Source>(),
                            "BUFFER",
                            Expr<Source>(),
                            Opt(length),
                            "DATAINFO",
                            Expr<Target>(),
                            "STATUSINFO",
                            Expr<Target>(),
                            "RECEIVED",
                            Expr<Target>());

        var ret = Seq("COMMUNICATION",
                        Alt(init, allocate, send, deallocate, receive, accept));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
