// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_dml_options.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLDmlOptions : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var usingClient = Seq("USING CLIENT", Expr<SQLSourceSimple>());
        var general = Per(Expr<SQLPrivilegedAccess>(), Expr<DatabaseConnection>());
        var body = Alt(Seq(usingClient, OptPrio(general)), general);
        return Ver(Release.v914, Seq("OPTIONS", body));
    }
}
