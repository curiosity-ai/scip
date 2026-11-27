// Ported from abaplint packages/core/src/abap/2_statements/expressions/string_template_formatting.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class StringTemplateFormatting : Expression
{

    public override IStatementRunnable GetRunnable()
    {

        // https://help.sap.com/doc/abapdocu_latest_index_htm/latest/en-us/abapcompute_string_format_options.html
        var alphaOptions = AltPrio("OUT", "RAW", "IN", Expr<Source>());

        var alignOptions = AltPrio("LEFT", "RIGHT", "CENTER", Expr<Source>(), Expr<Dynamic>());

        var dateTimeOptions = AltPrio("RAW", "ISO", "USER", "ENVIRONMENT", Expr<Source>(), Expr<Dynamic>());

        var timeStampOptions = AltPrio("SPACE", "ISO", "USER", "ENVIRONMENT", Expr<Source>(), Expr<Dynamic>());

        var numberOptions = AltPrio("RAW", "USER", "ENVIRONMENT", Expr<Source>(), Expr<Dynamic>());

        var signOptions = AltPrio("LEFT", "LEFTPLUS", "LEFTSPACE", "RIGHT", "RIGHTPLUS", "RIGHTSPACE", Expr<Source>());

        var caseOptions = AltPrio("RAW", "UPPER", "LOWER", Expr<Source>(), Expr<Dynamic>());

        var zeroXSDOptions = AltPrio("YES", "NO", Expr<Source>());

        var styleOptions = AltPrio("SIMPLE",
                                     "SIGN_AS_POSTFIX",
                                     "SCALE_PRESERVING",
                                     "SCIENTIFIC",
                                     "SCIENTIFIC_WITH_LEADING_ZERO",
                                     "SCALE_PRESERVING_SCIENTIFIC",
                                     "ENGINEERING",
                                     Expr<Source>());

        var width = Seq("WIDTH =", Expr<Source>());
        var align = Seq("ALIGN =", alignOptions);
        var timezone = Seq("TIMEZONE =", Expr<Source>());
        var timestamp = Seq("TIMESTAMP =", timeStampOptions);
        var pad = Seq("PAD =", Expr<Source>());
        var number = Seq("NUMBER =", numberOptions);
        var sign = Seq("SIGN =", signOptions);
        var decimals = Seq("DECIMALS =", Expr<Source>());
        var alpha = Ver(Release.v740sp02, Seq("ALPHA =", alphaOptions), also: AlsoIn.OpenABAP);
        var xsd = Ver(Release.v740sp02, Seq("XSD =", zeroXSDOptions));
        var country = Seq("COUNTRY =", Expr<Source>());

        var formatting = AltPrio(Seq("TIME =", dateTimeOptions),
                                   Seq("DATE =", dateTimeOptions),
                                   Seq("CASE =", caseOptions),
                                   Seq("ZERO =", zeroXSDOptions),
                                   xsd,
                                   Seq("STYLE =", styleOptions),
                                   Seq("CURRENCY =", Expr<Source>()),
                                   Per(sign, number, decimals, width, pad, alpha, align, country, Seq("EXPONENT =", Expr<Source>())),
                                   Per(timezone, timestamp));

        return formatting;
    }
}
