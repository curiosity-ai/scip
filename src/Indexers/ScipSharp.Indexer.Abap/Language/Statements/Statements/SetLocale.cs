// Ported from abaplint packages/core/src/abap/2_statements/statements/set_locale.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetLocale : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var country = Seq("COUNTRY", Expr<Source>());

        var modifier = Seq("MODIFIER", Expr<Source>());

        var ret = Seq("SET LOCALE LANGUAGE",
                        Expr<Source>(),
                        Opt(country),
                        Opt(modifier));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
