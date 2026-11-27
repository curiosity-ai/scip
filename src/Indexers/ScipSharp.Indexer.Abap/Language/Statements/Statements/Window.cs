// Ported from abaplint packages/core/src/abap/2_statements/statements/window.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Window : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ending = Seq("ENDING AT", Expr<Source>(), Expr<Source>());

        var ret = Seq("WINDOW STARTING AT",
                        Expr<Source>(),
                        Expr<Source>(),
                        Opt(ending));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
