// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_indicators.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLIndicators : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var name = Regex(@"^%?\w+$");
        var staticForm = Seq(OptPrio("NOT"),
                               "NULL",
                               Alt(Seq("STRUCTURE", name), Seq("BITFIELD", name)));
        var lparen = AltPrio(Tok<Tokens.WParenLeftW>(), Tok<Tokens.WParenLeft>());
        var rparen = AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.ParenRightW>());
        var dynamicForm = Seq(lparen, name, rparen);
        return Ver(Release.v915, Seq("INDICATORS", AltPrio(staticForm, dynamicForm)));
    }
}
