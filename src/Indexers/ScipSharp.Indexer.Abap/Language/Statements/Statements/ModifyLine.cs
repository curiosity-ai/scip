// Ported from abaplint packages/core/src/abap/2_statements/statements/modify_line.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ModifyLine : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var onOff = Alt("ON", "OFF");
        var eq = Seq("=", Expr<Source>());

        var form = Seq(Alt("INVERSE", "INPUT", "COLOR", "HOTSPOT"), Opt(Alt(eq, onOff)));

        var from = Seq("FROM", Expr<Source>());
        var value = Seq("FIELD VALUE", Plus(Seq(Expr<Source>(), OptPrio(from))));
        var format = Seq("FIELD FORMAT", Expr<Source>(), Opt(form));
        var lineValue = Seq("LINE VALUE FROM", Expr<Source>());
        var index = Seq("INDEX", Expr<Source>());
        var page = Seq("OF PAGE", Expr<Source>());
        var ocp = Str("OF CURRENT PAGE");
        var intensified = Seq("INTENSIFIED", onOff);
        var intensifiedOpt = Seq("INTENSIFIED", Opt(onOff));
        var inputOpt = Seq("INPUT", Opt(Alt(eq, onOff)));
        var inverseOpt = Seq("INVERSE", Opt(Alt(eq, onOff)));
        var lineFormat = Seq("LINE FORMAT",
                               Per(inputOpt, inverseOpt, "RESET", intensifiedOpt, Expr<Color>()));

        var options = Per(index, value, format, page, lineFormat, lineValue, ocp, intensified, Expr<Color>());

        var ret = Seq("MODIFY",
                        AltPrio("CURRENT LINE",
                                Seq("LINE", Expr<Source>())),
                        Opt(options));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
