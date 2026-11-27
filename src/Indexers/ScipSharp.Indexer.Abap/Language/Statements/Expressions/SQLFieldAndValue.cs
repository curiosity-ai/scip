// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_field_and_value.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLFieldAndValue : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var op = AltPrio(Tok<Tokens.WPlusW>(), Tok<Tokens.WDashW>());
        var opt1 = Seq(AltPrio(Expr<Integer>(), Expr<SQLFieldName>()), op, Expr<SQLSource>());
        var param = Seq(Expr<SQLFieldName>(), "=", AltPrio(opt1, Expr<SQLSource>()));
        return param;
    }
}
