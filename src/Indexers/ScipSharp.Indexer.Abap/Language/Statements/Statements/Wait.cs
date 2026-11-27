// Ported from abaplint packages/core/src/abap/2_statements/statements/wait.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Wait : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var up = Seq("UP TO", Expr<Source>(), "SECONDS");

        string channels = "MESSAGING CHANNELS";
        var push = Ver(Release.v750, "PUSH CHANNELS", also: AlsoIn.OpenABAP);
        string tasks = "ASYNCHRONOUS TASKS";

        var type = Seq("FOR", Per(channels, push, tasks));

        var until = Seq(Opt(type), "UNTIL", Expr<Cond>(), Opt(up));

        var ret = Seq("WAIT", Alt(until, up));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
