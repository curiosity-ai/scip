// Ported from abaplint packages/core/src/abap/2_statements/expressions/_select_core.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public static class SelectCore
{

    public static IStatementRunnable BuildSelectSingleCore(bool allowInto = false)
    {
        var where = Seq("WHERE", Expr<SQLCond>());
        var sqlFields = Ver(Release.v750, Expr<SQLFields>(), also: AlsoIn.OpenABAP);
        var privileged = Ver(Release.v758, Expr<SQLPrivilegedAccess>());
        var fieldList = OptPrio(Expr<SQLFieldList>());
        var extResult = OptPrio(Ver(Release.v766, Expr<SQLExtendedResult>()));
        var creating = OptPrio(Ver(Release.v750, Expr<SQLCreating>()));

        var client = OptPrio(Expr<SQLClient>());
        var byp = OptPrio(Expr<SQLBypassingBuffer>());
        var conn = OptPrio(VerNotLang(LanguageVersion.KeyUser, Expr<DatabaseConnection>()));
        var whereClause = OptPrio(where);
        var groupHaving = Seq(OptPrio(Expr<SQLGroupBy>()), OptPrio(Expr<SQLHaving>()));
        var trailingOpts = Seq(conn, OptPrio(Expr<SQLHints>()), OptPrio(privileged), OptPrio(Expr<SQLOptions>()));

        var intoSingle = Seq(AltPrio(Expr<SQLIntoStructure>(), Expr<SQLIntoList>()), extResult, creating);

        if (!allowInto)
        {
            var singleBody = Seq(Expr<SQLFrom>(), client, byp, whereClause, groupHaving, trailingOpts);
            return Seq(fieldList, singleBody);
        }

        var singleAfterFrom = Seq(
        Expr<SQLFrom>(), client, byp, conn,
        AltPrio(
          Seq(intoSingle, byp, whereClause, groupHaving, trailingOpts),
          Seq(OptPrio(sqlFields), whereClause, groupHaving, trailingOpts, OptPrio(intoSingle), conn)));
        var singleIntoBeforeFrom = Seq(intoSingle, Expr<SQLFrom>(), client, byp, conn, whereClause, groupHaving, trailingOpts);

        return Seq(fieldList, byp, AltPrio(singleIntoBeforeFrom, singleAfterFrom));
    }

    public static IStatementRunnable BuildSelectCore(bool allowInto = false, bool allowOrderBy = true)
    {
        var where = Seq("WHERE", Expr<SQLCond>());
        var offset = Ver(Release.v751, Expr<SQLOffset>());
        var sqlFields = Ver(Release.v750, Expr<SQLFields>(), also: AlsoIn.OpenABAP);
        var privileged = Ver(Release.v758, Expr<SQLPrivilegedAccess>());
        var fieldList = OptPrio(Expr<SQLFieldList>());
        var extResult = OptPrio(Ver(Release.v766, Expr<SQLExtendedResult>()));
        var creating = OptPrio(Ver(Release.v750, Expr<SQLCreating>()));

        var client = OptPrio(Expr<SQLClient>());
        var byp = OptPrio(Expr<SQLBypassingBuffer>());
        var conn = OptPrio(VerNotLang(LanguageVersion.KeyUser, Expr<DatabaseConnection>()));
        var fae = OptPrio(Expr<SQLForAllEntries>());
        var whereClause = OptPrio(where);
        var groupHaving = Seq(OptPrio(Expr<SQLGroupBy>()), OptPrio(Expr<SQLHaving>()));
        IStatementRunnable[] orderUpOff = allowOrderBy
            ? [OptPrio(Seq(Expr<SQLOrderBy>(), OptPrio(Expr<SQLUpTo>()), OptPrio(offset)))]
            : [];
        var trailingOpts = Seq(conn, OptPrio(Expr<SQLHints>()), OptPrio(privileged), OptPrio(Expr<SQLOptions>()));

        var intoSingle = Seq(AltPrio(Expr<SQLIntoStructure>(), Expr<SQLIntoList>()), extResult, creating);
        var intoForPackSize = Seq(Expr<SQLIntoTable>(), extResult, creating);

        if (!allowInto)
        {
            var afterFromNoInto = Seq(
            Expr<SQLFrom>(), client, byp,
            SeqOf([OptPrio(sqlFields), fae, whereClause, groupHaving, .. orderUpOff, trailingOpts]));
            return AltPrio(
            Seq("SINGLE", BuildSelectSingleCore(false)),
            Seq(OptPrio("DISTINCT"), fieldList, byp, afterFromNoInto));
        }

        var fromPackSize = Seq(OptPrio(Expr<SQLPackageSize>()), OptPrio(Expr<SQLUpTo>()), byp, conn, byp, OptPrio(Expr<SQLUpTo>()));

        var trailingInto = Seq(
        OptPrio(AltPrio(
          Seq(intoForPackSize, OptPrio(Expr<SQLPackageSize>()), byp, OptPrio(Expr<SQLUpTo>()), byp, OptPrio(offset), OptPrio(Expr<SQLOrderBy>())),
          Seq(intoSingle, byp, OptPrio(Expr<SQLUpTo>()), byp, OptPrio(offset)))),
        OptPrio(Expr<SQLOptions>()));

        var afterFromWithInto = Seq(
        OptPrio(Expr<SQLFrom>()), client, byp, fromPackSize,
        AltPrio(
          SeqOf([intoForPackSize, OptPrio(Expr<SQLPackageSize>()), byp, conn, OptPrio(Expr<SQLUpTo>()), byp, OptPrio(offset), fae, whereClause, groupHaving, .. orderUpOff, trailingOpts]),
          SeqOf([intoSingle, byp, OptPrio(Expr<SQLUpTo>()), byp, fae, OptPrio(Expr<SQLUpTo>()), byp, OptPrio(offset), whereClause, groupHaving, .. orderUpOff, trailingOpts]),
          SeqOf([OptPrio(sqlFields), fae, whereClause, groupHaving, .. orderUpOff, trailingOpts, trailingInto])));

        var selectTableIntoThenFrom = Seq(intoForPackSize, OptPrio(Expr<SQLPackageSize>()));
        var selectOtherIntoThenFrom = intoSingle;

        var nonSingleBody = Seq(OptPrio("DISTINCT"), fieldList, OptPrio(Expr<SQLUpTo>()), byp,
                                OptPrio(AltPrio(selectTableIntoThenFrom, selectOtherIntoThenFrom)),
                                OptPrio(Expr<SQLUpTo>()), byp, conn, afterFromWithInto);

        return AltPrio(
        Seq("SINGLE", BuildSelectSingleCore(true)),
        nonSingleBody);
    }
}
