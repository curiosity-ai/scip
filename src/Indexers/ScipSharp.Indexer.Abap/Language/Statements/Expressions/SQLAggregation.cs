// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_aggregation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLAggregation : Expression
{

    private static readonly IStatementRunnable lparen = AltPrio(Tok<Tokens.ParenLeft>(), Tok<Tokens.ParenLeftW>());
    private static readonly IStatementRunnable rparen = AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.WParenRight>(), Tok<Tokens.ParenRightW>());

    public override IStatementRunnable GetRunnable()
    {
        var arg = AltPrio(Ver(Release.v740sp08, Expr<SQLFunctionInput>()), Expr<SQLFieldName>());
        var avgRparen = AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.WParenRight>(), Tok<Tokens.ParenRightW>());
        var lenDecimals = Seq(Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), ",", Expr<SQLFunctionInput>(), Tok<Tokens.WParenRightW>());
        var avgCastType = AltPrio(
          Seq("DEC", lenDecimals),
          Seq("CURR", lenDecimals),
          Seq("QUAN", lenDecimals),
          "D16N",
          "D34N",
          "FLTP");

        var count = Seq("COUNT", lparen, OptPrio("DISTINCT"), AltPrio("*", arg), rparen, OptPrio(Expr<SQLOver>()));
        var max = Seq("MAX", lparen, OptPrio("DISTINCT"), arg, rparen, OptPrio(Expr<SQLOver>()));
        var min = Seq("MIN", lparen, OptPrio("DISTINCT"), arg, rparen, OptPrio(Expr<SQLOver>()));
        var sum = Seq("SUM", lparen, OptPrio("DISTINCT"), arg, rparen, OptPrio(Expr<SQLOver>()));
        var avg = Seq("AVG", Tok<Tokens.ParenLeftW>(), OptPrio("DISTINCT"), arg,
                        OptPrio(Ver(Release.v751, Seq("AS", avgCastType))), avgRparen, OptPrio(Expr<SQLOver>()));
        var rank = Ver(Release.v757, Seq(AltPrio("ROW_NUMBER", "RANK", "DENSE_RANK"), lparen, rparen, Expr<SQLOver>()));
        var leadLag = Ver(Release.v757, Seq(AltPrio("LEAD", "LAG"),
                                              Tok<Tokens.ParenLeftW>(),
                                              Expr<SQLFunctionInput>(),
                                              OptPrio(Seq(",", Expr<SQLFunctionInput>(), OptPrio(Seq(",", Expr<SQLFunctionInput>())))),
                                              rparen,
                                              Expr<SQLOver>()));
        var firstLastValue = Ver(Release.v757, Seq(AltPrio("FIRST_VALUE", "LAST_VALUE"),
                                                     Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), rparen,
                                                     Expr<SQLOver>()));
        var stringAgg = Ver(Release.v757, Seq("STRING_AGG",
                                                Tok<Tokens.ParenLeftW>(),
                                                Expr<SQLFunctionInput>(),
                                                OptPrio(Seq(",", Expr<SQLFunctionInput>())),
                                                rparen,
                                                OptPrio(Expr<SQLOver>())));
        var ntile = Ver(Release.v757, Seq("NTILE", Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), rparen, Expr<SQLOver>()));
        var corr = Ver(Release.v757, Seq(AltPrio("CORR_SPEARMAN", "CORR"),
                                           Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), ",", Expr<SQLFunctionInput>(), rparen,
                                           OptPrio(Expr<SQLOver>())));
        var stat = Ver(Release.v757, Seq(AltPrio("PRODUCT", "MEDIAN", "VAR", "STDDEV"),
                                           Tok<Tokens.ParenLeftW>(), Expr<SQLFunctionInput>(), rparen,
                                           OptPrio(Expr<SQLOver>())));

        return AltPrio(
          rank,
          leadLag,
          firstLastValue,
          stringAgg,
          ntile,
          corr,
          stat,
          count,
          max,
          min,
          sum,
          avg);
    }
}
