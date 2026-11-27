// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_up_to.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLUpTo : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var up = Seq("UP TO", Expr<SQLSource>(), "ROWS");
        return up;
    }
}
