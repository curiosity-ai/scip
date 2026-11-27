// Ported from abaplint packages/core/src/abap/2_statements/statements/call_subscreen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallSubscreen : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var including = Seq("INCLUDING", Expr<Source>(), Expr<Source>());

        var ret = Seq("CALL SUBSCREEN", Expr<Source>(), OptPrio(including));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
