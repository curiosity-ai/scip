// Ported from abaplint packages/core/src/abap/2_statements/statements/form.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Form : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var parameters = Seq(Opt(Expr<FormTables>()),
                               Opt(Expr<FormUsing>()),
                               Opt(Expr<FormChanging>()),
                               Opt(Expr<FormRaising>()));

        var ret = Seq("FORM", Expr<FormName>(), Alt("IMPLEMENTATION", parameters));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
