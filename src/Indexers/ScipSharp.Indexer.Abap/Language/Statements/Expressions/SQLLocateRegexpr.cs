// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_locate_regexpr.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLLocateRegexpr : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var startOrAfter = OptPrio(AltPrio("START", "AFTER"));
        return Ver(Release.v758, Seq(
          "LOCATE_REGEXPR",
          Tok<Tokens.ParenLeftW>(),
          startOrAfter,
          Expr<SQLRegexprPattern>(),
          OptPrio(Expr<SQLRegexprFrom>()),
          OptPrio(Expr<SQLRegexprOccurrence>()),
          OptPrio(Expr<SQLRegexprGroup>()),
          Tok<Tokens.WParenRightW>()));
    }
}
