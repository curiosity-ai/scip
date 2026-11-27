// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_privileged_access.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLPrivilegedAccess : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var accessLevel = Ver(Release.v917, Seq("LEVEL", Expr<SQLSourceSimple>()));
        return Seq("PRIVILEGED ACCESS", OptPrio(accessLevel));
    }
}
