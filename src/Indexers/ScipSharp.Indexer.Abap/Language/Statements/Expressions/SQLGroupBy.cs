// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_group_by.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLGroupBy : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var fieldName = Seq(StopBefore("ORDER", "BY"), Alt(Expr<SQLFieldName>(), Expr<Dynamic>()));

        var expr = Ver(Release.v740sp02, Expr<SQLField>(), also: AlsoIn.OpenABAP);
        var newGroup = Ver(Release.v740sp02,
                             Seq("GROUP BY", expr, AltPrio(Plus(Seq(",", expr)), OptPrio(Plus(fieldName)))), also: AlsoIn.OpenABAP);

        var old = Seq(Plus(Seq(fieldName, ",")), fieldName);
        var oldGroup = Seq("GROUP BY", AltPrio(old, Plus(fieldName)));

        var groupingSetsGroup = Ver(Release.v772, Seq("GROUP BY", Expr<SQLGroupingSets>()));

        return AltPrio(groupingSetsGroup, newGroup, oldGroup);
    }
}
