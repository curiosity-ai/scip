// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_into_table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLIntoTable : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var indicators = OptPrio(Ver(Release.v915, Expr<SQLIndicators>()));
        var into = Seq(AltPrio("INTO", "APPENDING"),
                         OptPrio("CORRESPONDING FIELDS OF"),
                         "TABLE",
                         Expr<SQLTarget>(),
                         indicators);
        return into;
    }
}
