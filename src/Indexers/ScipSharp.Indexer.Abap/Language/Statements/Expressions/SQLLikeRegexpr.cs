// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_like_regexpr.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLLikeRegexpr : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var hostParen = Seq(Tok<Tokens.ParenLeftW>(), Expr<Source>(), Tok<Tokens.WParenRightW>());
        var hostVar = Seq(Tok<Tokens.WAt>(), AltPrio(Expr<SimpleSource3>(), hostParen));
        var flag = Seq("FLAG", AltPrio(hostVar, Expr<SQLFunctionInput>()));
        return Ver(Release.v757, Seq(
          "LIKE_REGEXPR",
          Expr<SQLFunctionInput>(),
          OptPrio(flag)));
    }
}
