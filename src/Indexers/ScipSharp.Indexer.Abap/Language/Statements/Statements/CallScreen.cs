// Ported from abaplint packages/core/src/abap/2_statements/statements/call_screen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallScreen : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var starting = Seq("STARTING AT", Expr<Source>(), Expr<Source>());
        var ending = Seq("ENDING AT", Expr<Source>(), Expr<Source>());

        var ret = Seq("CALL SCREEN", Expr<Source>(), OptPrio(Seq(starting, OptPrio(ending))));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
