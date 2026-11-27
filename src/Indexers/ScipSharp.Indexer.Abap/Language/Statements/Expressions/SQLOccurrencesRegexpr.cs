// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_occurrences_regexpr.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLOccurrencesRegexpr : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Ver(Release.v757, Seq(
          "OCCURRENCES_REGEXPR",
          Tok<Tokens.ParenLeftW>(),
          Expr<SQLRegexprPattern>(),
          OptPrio(Expr<SQLRegexprFrom>()),
          Tok<Tokens.WParenRightW>()));
    }
}
