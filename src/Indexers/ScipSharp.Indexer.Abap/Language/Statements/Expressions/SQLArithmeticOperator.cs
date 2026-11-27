// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_arithmetic_operator.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLArithmeticOperator : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var @operator = AltPrio(Tok<Tokens.WPlusW>(), Tok<Tokens.WDashW>(), "*", "/");
        return @operator;
    }
}
