// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_substring_regexpr.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLSubstringRegexpr : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var name = AltPrio("SUBSTRING_REGEXPR", "SUBSTR_REGEXPR");
        return Ver(Release.v758, Seq(
          name,
          Tok<Tokens.ParenLeftW>(),
          Expr<SQLRegexprPattern>(),
          OptPrio(Expr<SQLRegexprFrom>()),
          OptPrio(Expr<SQLRegexprOccurrence>()),
          OptPrio(Expr<SQLRegexprGroup>()),
          Tok<Tokens.WParenRightW>()));
    }
}
