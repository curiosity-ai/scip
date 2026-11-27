// Ported from abaplint packages/core/src/abap/2_statements/expressions/select_cte.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SelectCTE : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var where = Seq("WHERE", Expr<SQLCond>());
        string bypass = "BYPASSING BUFFER";
        var privileged = Ver(Release.v758, Expr<SQLPrivilegedAccess>());
        var offset = Ver(Release.v751, Seq("OFFSET", Expr<SQLSource>()));
        var conn = OptPrio(VerNotLang(LanguageVersion.KeyUser, Expr<DatabaseConnection>()));

        var groupHaving = Seq(OptPrio(Expr<SQLGroupBy>()), OptPrio(Expr<SQLHaving>()));
        var tail = Seq(
          groupHaving,
          OptPrio(Seq(Expr<SQLOrderBy>(), OptPrio(Expr<SQLUpTo>()), OptPrio(offset))),
          OptPrio(Expr<SQLHints>()),
          OptPrio(privileged),
          OptPrio(Expr<SQLOptions>()),
          conn);

        var sqlStyle = Seq(
          Expr<SQLFrom>(),
          OptPrio(Expr<SQLClient>()),
          OptPrio(bypass),
          Ver(Release.v750, Expr<SQLFieldsLoop>(), also: AlsoIn.OpenABAP),
          OptPrio(Expr<SQLForAllEntries>()),
          OptPrio(where),
          tail);

        var abapStyle = Seq(
          OptPrio("DISTINCT"),
          Expr<SQLFieldListLoopGreedy>(),
          Expr<SQLFrom>(),
          OptPrio(Expr<SQLClient>()),
          OptPrio(bypass),
          OptPrio(Expr<SQLForAllEntries>()),
          OptPrio(where),
          tail);

        var union = Seq("UNION", OptPrio(AltPrio("DISTINCT", "ALL")));
        var intersectExcept = AltPrio(Seq("INTERSECT", OptPrio("DISTINCT")),
                                        Seq("EXCEPT", OptPrio("DISTINCT")));
        var setOp = AltPrio(Ver(Release.v750, union, also: AlsoIn.OpenABAP),
                              Ver(Release.v756, intersectExcept));

        var operandSql = Seq(Expr<SQLFrom>(), OptPrio(Expr<SQLClient>()), OptPrio(bypass), Ver(Release.v750, Expr<SQLFieldsLoop>(), also: AlsoIn.OpenABAP),
                               OptPrio(Expr<SQLForAllEntries>()), OptPrio(where), groupHaving);
        var operandAbap = Seq(OptPrio("DISTINCT"), Expr<SQLFieldListLoopGreedy>(),
                                Expr<SQLFrom>(), OptPrio(Expr<SQLClient>()), OptPrio(bypass), OptPrio(Expr<SQLForAllEntries>()),
                                OptPrio(where), groupHaving);
        var operandCore = AltPrio(operandSql, operandAbap);
        var unionOperand = AltPrio(Expr<SQLSetOpGroup>(), Seq("SELECT", operandCore));
        var unionTail = StarPrio(Seq(setOp, unionOperand));

        var selectBody = Seq("SELECT", AltPrio(sqlStyle, abapStyle));

        return Seq(selectBody, unionTail);
    }
}
