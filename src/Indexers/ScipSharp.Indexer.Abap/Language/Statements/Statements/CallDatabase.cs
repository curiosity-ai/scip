// Ported from abaplint packages/core/src/abap/2_statements/statements/call_database.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallDatabase : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var exporting = Seq("EXPORTING", Expr<ParameterListS>());
        var importing = Seq("IMPORTING", Expr<ParameterListT>());
        var expl = Seq(Opt(exporting), Opt(importing));

        var tab = Seq("PARAMETER-TABLE", Expr<Source>());

        var ret = Seq("CALL DATABASE PROCEDURE",
                        Expr<Dynamic>(),
                        Opt(Expr<DatabaseConnection>()),
                        Alt(expl, tab));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
