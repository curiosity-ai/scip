// Ported from abaplint packages/core/src/abap/2_statements/statements/update_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class UpdateDatabase : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var parameters = Seq(Expr<SQLFieldAndValue>(), Star(Seq(Opt(","), Expr<SQLFieldAndValue>())));

        var set = Seq("SET",
                        Alt(parameters, Expr<Dynamic>()),
                        Opt(Seq("WHERE", Expr<SQLCond>())));

        var setStructure = Seq("INDICATORS SET STRUCTURE", Expr<ComponentName>());
        var updateIndicators = AltPrio(Ver(Release.v915, Expr<SQLIndicators>()), Ver(Release.v755, setStructure));

        var fromTable = Seq("FROM",
                              Opt("TABLE"),
                              Expr<SQLSource>(),
                              OptPrio(updateIndicators),
                              OptPrio(Expr<SQLMappingFromEntity>()));

        var ret = Seq("UPDATE",
                        Expr<DatabaseTable>(),
                        Opt(Expr<SQLClient>()),
                        Opt(Expr<DatabaseConnection>()),
                        Opt(Alt(fromTable, set)),
                        OptPrio(Expr<SQLDmlOptions>()));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
