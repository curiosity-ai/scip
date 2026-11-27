// Ported from abaplint packages/core/src/abap/2_statements/statements/write.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Write : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var mask = Seq("USING",
                         AltPrio("NO EDIT MASK",
                                 Seq("EDIT MASK", Expr<Source>())));

        var onOff = Alt(AltPrio("ON", "OFF"), Seq("=", Expr<FieldSub>()));

        var dateFormat = AltPrio("DD/MM/YY",
                                   "MM/DD/YY",
                                   "DD/MM/YYYY",
                                   "MM/DD/YYYY",
                                   "DDMMYY",
                                   "MMDDYY",
                                   "YYMMDD");

        var @as = Seq("AS", AltPrio("LINE", "ICON", "CHECKBOX", "SYMBOL"));

        var to = Seq("TO", Expr<Target>());
        var options = Per(mask,
                            to,
                            Seq("EXPONENT", Expr<Source>()),
                            "NO-GROUPING",
                            "NO-ZERO",
                            "CENTERED",
                            Seq("INPUT", Opt(onOff)),
                            "NO-GAP",
                            "LEFT-JUSTIFIED",
                            @as,
                            Seq("FRAMES", onOff),
                            Seq("HOTSPOT", Opt(onOff)),
                            "RIGHT-JUSTIFIED",
                            Seq("TIME ZONE", Expr<Source>()),
                            Seq("UNDER", Expr<Source>()),
                            Seq("STYLE", Expr<Source>()),
                            Seq("ROUND", Expr<Source>()),
                            Seq("QUICKINFO", Expr<Source>()),
                            "ENVIRONMENT TIME FORMAT",
                            dateFormat,
                            Seq("UNIT", Expr<Source>()),
                            Seq("INTENSIFIED", Opt(onOff)),
                            Seq("INDEX", Expr<Source>()),
                            Seq("DECIMALS", Expr<Source>()),
                            Seq("INVERSE", Opt(onOff)),
                            Expr<Color>(),
                            Seq("CURRENCY", Expr<Source>()),
                            "RESET",
                            "NO-SIGN");

        // Need to refactor all this sometime,
        var ret = Seq("WRITE", Alt(Seq("AT /", Opt(Expr<Source>()), Opt(options)),
                                     Seq(Opt(Expr<WriteOffsetLength>()),
                                         Alt(Expr<Source>(), Expr<Dynamic>(), "/"),
                                         Opt(options))));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
