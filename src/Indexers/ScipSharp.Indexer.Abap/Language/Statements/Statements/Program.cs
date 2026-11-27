// Ported from abaplint packages/core/src/abap/2_statements/statements/program.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Program : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var message = Seq("MESSAGE-ID", Expr<MessageClass>());
        var size = Seq("LINE-SIZE", Expr<Source>());
        var heading = Str("NO STANDARD PAGE HEADING");
        var line = Seq("LINE-COUNT", Expr<Source>(), Opt(Expr<FieldLength>()));
        var options = Per(message, size, heading, line);

        var ret = Seq("PROGRAM", Opt(Expr<ReportName>()), Opt(options));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
