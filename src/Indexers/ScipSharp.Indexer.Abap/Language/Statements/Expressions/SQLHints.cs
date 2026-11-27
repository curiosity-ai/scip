// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_hints.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLHints : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var type = AltPrio("ORACLE", "ADABAS", "AS400", "DB2", "HDB", "MSSQLNT", "SYBASE", "DB6", "INFORMIX");
        var ret = Seq("%_HINTS", Plus(Seq(type, Expr<SQLSource>())));
        return ret;
    }
}
