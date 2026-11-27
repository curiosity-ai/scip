// Ported from abaplint packages/core/src/abap/2_statements/statements/get_locale.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetLocale : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var country = Seq("COUNTRY", Expr<Target>());

        var modifier = Seq("MODIFIER", Expr<Target>());

        var ret = Seq("GET LOCALE LANGUAGE",
                        Expr<Target>(),
                        country,
                        modifier);

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
