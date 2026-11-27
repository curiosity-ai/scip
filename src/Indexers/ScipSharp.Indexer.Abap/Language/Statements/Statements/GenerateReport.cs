// Ported from abaplint packages/core/src/abap/2_statements/statements/generate_report.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GenerateReport : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var without = Str("WITHOUT SELECTION-SCREEN");
        var message = Seq("MESSAGE", Expr<Target>());
        var include = Seq("INCLUDE", Expr<Target>());
        var line = Seq("LINE", Expr<Target>());
        var word = Seq("WORD", Expr<Target>());
        var offset = Seq("OFFSET", Expr<Target>());
        var headers = Str("WITH PRECOMPILED HEADERS");
        var test = Str("WITH TEST CODE");
        var messageid = Seq("MESSAGE-ID", Expr<Target>());
        var trace = Seq("TRACE-FILE", Expr<Target>());
        var shortdumpid = Seq("SHORTDUMP-ID", Expr<Target>());
        var directory = Seq("DIRECTORY ENTRY", Expr<Target>());

        var options = Per(without, message, include, trace, line, word, offset, headers, test, messageid, shortdumpid, directory);

        var ret = Seq("GENERATE REPORT",
                        Expr<Source>(),
                        Opt(options));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
