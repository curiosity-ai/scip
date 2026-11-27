// Ported from abaplint packages/core/src/abap/2_statements/statements/call_selection_screen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallSelectionScreen : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ending = Seq("ENDING AT", Expr<Source>(), Expr<Source>());
        var starting = Seq("STARTING AT", Expr<Source>(), Expr<Source>());
        var @using = Seq("USING SELECTION-SET", Expr<Source>());

        var at = Seq(starting, Opt(ending));

        var ret = Seq("CALL SELECTION-SCREEN", Expr<Source>(), Opt(at), Opt(@using));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
