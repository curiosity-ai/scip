// Ported from abaplint packages/core/src/abap/2_statements/statements/report.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Report : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var more = Seq(Tok<Tokens.ParenLeft>(), Expr<Integer>(), Alt(Tok<Tokens.ParenRightW>(), Tok<Tokens.ParenRight>()));
        var heading = Str("NO STANDARD PAGE HEADING");
        var size = Seq("LINE-SIZE", Expr<Integer>());
        var count = Seq("LINE-COUNT", Expr<Integer>(), Opt(more));
        var message = Seq("MESSAGE-ID", Expr<MessageClass>());
        var database = Seq("USING DATABASE", Expr<Expressions.Field>());

        var ret = Seq("REPORT",
                        Opt(Expr<ReportName>()),
                        Opt(Per(heading, size, count, database, message)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
