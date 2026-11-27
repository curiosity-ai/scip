// Ported from abaplint packages/core/src/abap/2_statements/statements/convert.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Convert : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var intoTime = Seq("TIME", Expr<Target>());
        var intoDate = Seq("DATE", Expr<Target>());
        var into = Seq("INTO", Per(intoTime, intoDate));

        var daylightSource = Seq("DAYLIGHT SAVING TIME", Expr<Source>());
        var daylightTarget = Seq("DAYLIGHT SAVING TIME", Expr<Target>());
        var zone = Seq("TIME ZONE", Expr<Source>());

        var time = Seq("TIME STAMP",
                         Expr<Source>(),
                         Per(zone, into, daylightTarget));

        var dat = Seq("DATE", Expr<Source>());
        var tim = Seq("TIME", Expr<Source>());

        var stamp = Seq("INTO TIME STAMP", Expr<Target>());
        var intoutc = Ver(Release.v754, Seq("INTO UTCLONG", Expr<Target>()), also: AlsoIn.OpenABAP);
        var invert = Seq("INTO INVERTED-DATE", Expr<Target>());

        var date = Seq(Per(dat, tim),
                         Per(daylightSource, stamp, zone, invert, intoutc));

        var inv = Seq("INVERTED-DATE", Expr<Source>(), "INTO DATE", Expr<Target>());

        var utclong = Ver(Release.v754, Seq("UTCLONG", Expr<Source>(), Per(zone, into, daylightTarget)), also: AlsoIn.OpenABAP);

        return Seq("CONVERT", Alt(time, date, inv, utclong));
    }
}
