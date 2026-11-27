// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_hierarchy_aggregate.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLHierarchyAggregate : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var startWhere = Seq("START", "WHERE", Expr<SQLCond>());
        var join = Seq("JOIN", Expr<DatabaseTable>(), "ON", Expr<SQLCond>());
        var measureItem = Seq(Expr<SQLAggregation>(), "AS", Expr<SQLAsName>());
        var measures = Seq("MEASURES", measureItem, Star(Seq(",", measureItem)));
        var where = Seq("WHERE", Expr<SQLCond>());

        var withEntry = AltPrio("SUBTOTAL", "BALANCE", Seq("NOT", "MATCHED"), "TOTAL");

        var aggregate = Seq(
          "SOURCE", Expr<DatabaseTable>(),
          OptPrio(startWhere),
          OptPrio(join),
          measures,
          OptPrio(where),
          OptPrio(Seq("WITH", withEntry, Star(Seq("WITH", withEntry)))));

        var name = AltPrio("HIERARCHY_DESCENDANTS_AGGREGATE", "HIERARCHY_ANCESTORS_AGGREGATE");

        return Ver(Release.v750, Seq(
          name,
          Tok<Tokens.ParenLeftW>(),
          aggregate,
          Tok<Tokens.WParenRightW>()));
    }
}
