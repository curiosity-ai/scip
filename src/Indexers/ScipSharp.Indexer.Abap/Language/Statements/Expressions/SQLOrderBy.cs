// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_order_by.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLOrderBy : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ding = Alt("ASCENDING", "DESCENDING");
        var nulls = Ver(Release.v778, Seq("NULLS", Alt("FIRST", "LAST")));
        var item = AltPrio(Ver(Release.v789, Expr<SQLField>()), Expr<SQLFieldName>());
        var ofields = Plus(Seq(item, Opt(ding), Opt(nulls), Opt(",")));
        var order = Seq("ORDER BY", AltPrio("PRIMARY KEY", Expr<Dynamic>(), ofields));
        return order;
    }
}
