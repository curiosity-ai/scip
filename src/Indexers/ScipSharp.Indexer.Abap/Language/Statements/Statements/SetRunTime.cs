// Ported from abaplint packages/core/src/abap/2_statements/statements/set_run_time.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetRunTime : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var clock = Seq("CLOCK RESOLUTION", Alt("LOW", "HIGH"));

        var analyzer = Seq("ANALYZER", Alt("ON", "OFF"));

        var ret = Seq("SET RUN TIME", Alt(clock, analyzer));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
