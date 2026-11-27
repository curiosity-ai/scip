// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_arithmetics.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLArithmetics : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var field = Alt(Expr<SQLFieldName>(), Expr<SQLFunction>());
        return Seq(field, StarPrio(Seq(Expr<SQLArithmeticOperator>(), field)));
    }
}
