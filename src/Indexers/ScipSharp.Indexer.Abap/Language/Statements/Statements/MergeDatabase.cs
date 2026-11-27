// Ported from abaplint packages/core/src/abap/2_statements/statements/merge_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MergeDatabase : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var targetSpec = Seq(Expr<DatabaseTable>(), OptPrio(Seq("AS", Expr<SQLAsName>())));

        var subSelect = Seq(Tok<Tokens.WParenLeftW>(), "SELECT", SelectCore.BuildSelectCore(false, false), Tok<Tokens.WParenRightW>(),
                              OptPrio(Seq("AS", Expr<SQLAsName>())));
        var itabSource = Seq(Tok<Tokens.WAt>(), Expr<SQLAsName>(), "AS", Expr<SQLAsName>(),
                               OptPrio(Seq("DECLARE CLIENT", Expr<SQLFieldName>())));
        var tableSource = Seq(Expr<DatabaseTable>(), OptPrio(Seq("AS", Expr<SQLAsName>())));
        var usingClause = Seq("USING", AltPrio(subSelect, itabSource, tableSource));

        var onClause = Seq("ON", Expr<SQLCond>());

        var setEntry = Seq(Expr<SQLFieldName>(), "=", Expr<SQLField>());
        var setList = Seq(setEntry, StarPrio(Seq(",", setEntry)));
        var matchedAction = AltPrio(
          "DELETE",
          Seq("UPDATE", AltPrio("ALL", Seq("SET", setList))));

        var whenMatched = Seq("WHEN MATCHED",
                                OptPrio(Seq("AND", Expr<SQLCond>())),
                                "THEN",
                                matchedAction);

        var insertCols = Seq(Tok<Tokens.WParenLeftW>(), Expr<SQLFieldName>(), StarPrio(Seq(",", Expr<SQLFieldName>())), Tok<Tokens.WParenRightW>());
        var insertValues = Seq(Tok<Tokens.WParenLeftW>(), Expr<SQLField>(), StarPrio(Seq(",", Expr<SQLField>())), Tok<Tokens.WParenRightW>());
        var notMatchedAction = Seq("INSERT",
                                     AltPrio("ALL", Seq(insertCols, "VALUES", insertValues)));

        var whenNotMatched = Seq("WHEN NOT MATCHED",
                                   OptPrio(Seq("AND", Expr<SQLCond>())),
                                   "THEN",
                                   notMatchedAction);

        var whenClauses = AltPrio(
          Seq(whenMatched, OptPrio(whenNotMatched)),
          whenNotMatched);

        var optionsClause = Seq("OPTIONS",
                                  AltPrio(Expr<SQLPrivilegedAccess>(), Expr<SQLClient>(),
                                          Seq(Expr<SQLClient>(), Expr<SQLPrivilegedAccess>()),
                                          Seq(Expr<SQLPrivilegedAccess>(), Expr<SQLClient>())));
        var trailingOptions = OptPrio(optionsClause);

        var staticForm = Seq("INTO", targetSpec,
                               OptPrio(usingClause),
                               OptPrio(onClause),
                               OptPrio(whenClauses),
                               trailingOptions);

        var ret = Ver(Release.v917, Seq("MERGE", Alt(staticForm, Expr<Dynamic>())));
        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
