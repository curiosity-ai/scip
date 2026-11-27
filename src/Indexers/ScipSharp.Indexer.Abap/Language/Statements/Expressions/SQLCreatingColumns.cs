// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_creating_columns.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLCreatingColumns : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var guard = Seq(StopBefore("LOCATOR", "FOR"), StopBefore("READER", "FOR"));
        return Seq("COLUMNS", Expr<SQLFieldName>(),
                   StarPrio(Seq(guard, OptPrio(","), Expr<SQLFieldName>())));
    }
}
