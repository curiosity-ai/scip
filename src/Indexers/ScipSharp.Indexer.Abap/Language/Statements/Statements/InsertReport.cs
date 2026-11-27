// Ported from abaplint packages/core/src/abap/2_statements/statements/insert_report.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InsertReport : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var options = Per(Seq("STATE", Expr<Source>()),
                            Seq("EXTENSION TYPE", Expr<Source>()),
                            Seq("DIRECTORY ENTRY", Expr<Source>()),
                            Seq("UNICODE ENABLING", Expr<Source>()),
                            Seq("PROGRAM TYPE", Expr<Source>()),
                            Ver(Release.v750, Seq("VERSION", Expr<Source>())),
                            Seq("FIXED-POINT ARITHMETIC", Expr<Source>()),
                            "KEEPING DIRECTORY ENTRY");

        var ret = Seq("INSERT REPORT",
                        Expr<Source>(),
                        "FROM",
                        Expr<Source>(),
                        Opt(options));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
