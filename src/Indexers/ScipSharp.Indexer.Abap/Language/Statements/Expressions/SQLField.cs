// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_field.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLField : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var atParen = Seq(Tok<Tokens.ParenLeftW>(), Expr<SimpleFieldChain2>(), Tok<Tokens.WParenRightW>());
        var abap = Ver(Release.v740sp05, Seq(Tok<Tokens.WAt>(), AltPrio(Expr<SimpleFieldChain2>(), atParen)), also: AlsoIn.OpenABAP);

        var @as = Seq("AS", Expr<SQLAsName>());

        var parenFieldName = Seq(Tok<Tokens.WParenLeftW>(), Expr<SQLFieldName>(), AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.WParenRight>()));

        var fieldNoAgg = AltPrio(Expr<SQLCase>(),
                                   Expr<SQLFunction>(),
                                   Expr<SQLPathForColumn>(),
                                   Expr<SQLTypedLiteral>(),
                                   Expr<SQLFieldName>(),
                                   abap,
                                   Expr<Constant>(),
                                   parenFieldName);

        var field = AltPrio(Expr<SQLAggregation>(), fieldNoAgg);

        var parenField = Seq(Tok<Tokens.WParenLeftW>(), field, Tok<Tokens.WParenRightW>());
        var parenFieldNoAgg = Seq(Tok<Tokens.WParenLeftW>(), fieldNoAgg, Tok<Tokens.WParenRightW>());

        var subNoAgg = PlusPrio(Seq(AltPrio("+", "-", "*", "/", "&&"), AltPrio(parenFieldNoAgg, fieldNoAgg)));
        var arithNoAgg = Ver(Release.v740sp05, subNoAgg);

        var subWithAgg = PlusPrio(Seq(AltPrio("+", "-", "*", "/", "&&"), AltPrio(parenField, field)));
        var arithWithAgg = Ver(Release.v754, subWithAgg);

        var arith = AltPrio(arithWithAgg, arithNoAgg);

        var arithSequence = Seq(OptPrio("-"), field, OptPrio(arith));
        var parenArithSequence = Seq(Tok<Tokens.WParenLeftW>(), OptPrio("-"), arithSequence, Tok<Tokens.WParenRightW>());

        var subExtWithAgg = PlusPrio(Seq(AltPrio("+", "-", "*", "/", "&&"), AltPrio(parenArithSequence, parenField, field)));
        var subExtNoAgg = PlusPrio(Seq(AltPrio("+", "-", "*", "/", "&&"), AltPrio(parenArithSequence, parenFieldNoAgg, fieldNoAgg)));
        var arithExt = AltPrio(Ver(Release.v754, subExtWithAgg), Ver(Release.v740sp05, subExtNoAgg));

        return Seq(AltPrio(
          Seq(parenArithSequence, OptPrio(arithExt)),
          arithSequence), OptPrio(@as));
    }
}
