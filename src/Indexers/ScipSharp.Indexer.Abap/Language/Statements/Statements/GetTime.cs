// Ported from abaplint packages/core/src/abap/2_statements/statements/get_time.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetTime : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var options = Seq(Alt("STAMP FIELD", "FIELD"), Expr<Target>());

        return VerNotLang(LanguageVersion.KeyUser, Seq("GET TIME", Opt(options)));
    }
}
