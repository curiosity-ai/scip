// Ported from abaplint packages/core/src/abap/2_statements/statements/loop_at_screen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class LoopAtScreen : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var l = Seq("LOOP AT SCREEN", Opt(Seq("INTO", Expr<Target>())));
        return VerNotLang(LanguageVersion.Cloud, l);
    }
}
