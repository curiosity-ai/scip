// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_hierarchy_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLHierarchySource : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var orderDir = OptPrio(AltPrio("ASCENDING", "DESCENDING"));
        var orderFieldName = Regex(@"^(?!(?:DEPTH|MULTIPLE|ORPHAN|CACHE|CYCLE|LOAD|GENERATE|RETAIN|DEPTH)$)(\/\w+\/)?(\*?\w+~(\/\w+\/)?(\w+|\*)|\w+)$", "i");
        var orderField = Seq(orderFieldName, orderDir, OptPrio(","));
        var siblingsOrderBy = Seq("SIBLINGS", "ORDER", "BY",
                                    AltPrio("PRIMARY KEY", Expr<Dynamic>(), Seq(orderField, StarPrio(orderField))));

        var cache = Seq("CACHE", AltPrio("FORCE", "OFF", "ON"));
        var retainNulls = Seq("RETAIN", "NULLS", Expr<SQLSource>());
        var cycle = Seq("CYCLE", AltPrio("BREAKUP", "ERROR"));
        var multipleParents = Seq("MULTIPLE", "PARENTS",
                                    AltPrio(Seq("NOT", "ALLOWED"), Seq("LEAVES", "ONLY"), "ALLOWED"));
        var orphan = Seq("ORPHAN", AltPrio("IGNORE", "ERROR", "ROOT"));
        var depth = Seq("DEPTH", Expr<SQLSource>());
        var load = Seq("LOAD", AltPrio("INCREMENTAL", "BULK", Expr<SQLSource>()));
        var generateSpantree = Seq("GENERATE", "SPANTREE");
        var startWhere = Seq("START", "WHERE", Expr<SQLCond>());
        var period = Seq("PERIOD", "FROM", Expr<SQLFieldName>(), "TO", Expr<SQLFieldName>());
        var valid = Seq("VALID", "FROM", Expr<SQLSource>(), "TO", Expr<SQLSource>());

        var association = Seq("CHILD", "TO", "PARENT", "ASSOCIATION", Expr<SQLFieldName>());

        var sourceTable = Seq(AltPrio(Expr<WithName>(), Expr<DatabaseTable>()), OptPrio(Seq("AS", Expr<SQLAsName>())));

        var levelColumn = Expr<SQLFieldName>();
        var levels = Seq("LEVELS", Tok<Tokens.WParenLeftW>(), levelColumn, Star(Seq(",", levelColumn)), Tok<Tokens.WParenRightW>());
        var leveledGenerator = Seq(
          levels,
          OptPrio(siblingsOrderBy),
          OptPrio(cache),
          OptPrio(retainNulls));

        var generator = Seq(
          association,
          OptPrio(Seq(period, valid)),
          startWhere,
          OptPrio(siblingsOrderBy),
          OptPrio(depth),
          OptPrio(multipleParents),
          OptPrio(orphan),
          OptPrio(cache),
          OptPrio(cycle),
          OptPrio(load),
          OptPrio(generateSpantree));

        return Ver(Release.v750, Seq(
          "HIERARCHY",
          Tok<Tokens.ParenLeftW>(),
          "SOURCE", sourceTable,
          AltPrio(leveledGenerator, generator),
          Tok<Tokens.WParenRightW>()));
    }
}
