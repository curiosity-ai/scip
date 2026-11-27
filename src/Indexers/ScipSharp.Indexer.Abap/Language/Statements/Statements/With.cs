// Ported from abaplint packages/core/src/abap/2_statements/statements/with.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class With : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var exposing = OptPrio(Seq("WITH", AltPrio(new SQLCTEHierarchy(), Expr<SQLCTEAssociations>())));
        var cte = Seq(Expr<WithName>(), "AS", Tok<Tokens.WParenLeftW>(), Expr<SelectCTE>(), Tok<Tokens.WParenRightW>(), exposing);
        return VerNotLang(LanguageVersion.KeyUser, Ver(Release.v751, Seq("WITH", cte, Star(Seq(",", cte)), Expr<Expressions.Select>())));
    }
}
