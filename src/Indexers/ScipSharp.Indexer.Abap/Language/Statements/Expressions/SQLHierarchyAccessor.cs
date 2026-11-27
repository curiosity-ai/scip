// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_hierarchy_accessor.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLHierarchyAccessor : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var distanceTo = Seq("TO", Expr<SQLSource>());
        var distanceFrom = Seq("FROM", Expr<SQLSource>(), OptPrio(distanceTo));
        var distance = Seq("DISTANCE", AltPrio(distanceFrom, distanceTo));

        var accessor = Seq(
          "SOURCE", Expr<DatabaseTable>(),
          "START", "WHERE", Expr<SQLCond>(),
          OptPrio(distance));

        var name = AltPrio("HIERARCHY_DESCENDANTS", "HIERARCHY_ANCESTORS", "HIERARCHY_SIBLINGS");

        return Ver(Release.v750, Seq(
          name,
          Tok<Tokens.ParenLeftW>(),
          accessor,
          Tok<Tokens.WParenRightW>()));
    }
}
