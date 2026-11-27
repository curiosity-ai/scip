// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_options.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLOptions : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var usingClients = Alt(Seq("CLIENTS IN", Alt(Expr<SQLSourceSimple>(), "T000")),
                                 "ALL CLIENTS",
                                 Seq("CLIENT", Expr<SQLSourceSimple>()));

        var privilegedAccess = Expr<SQLPrivilegedAccess>();

        var general = Per(privilegedAccess, Expr<SQLBypassingBuffer>(), Expr<DatabaseConnection>());

        var usingClause = Seq("USING", usingClients, OptPrio(general));

        return Ver(Release.v796, Seq("OPTIONS", Alt(usingClause, general)));
    }
}
