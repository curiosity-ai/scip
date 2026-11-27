// Ported from abaplint packages/core/src/abap/2_statements/expressions/compare.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Compare : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var val = AltPrio(Expr<FieldSub>(), Expr<Constant>());

        var list = Seq(Tok<Tokens.WParenLeft>(),
                         val,
                         Plus(Seq(",", val)),
                         Tok<Tokens.ParenRightW>());

        var inn = Seq(OptPrio("NOT"), "IN", AltPrio(Expr<Source>(), list));

        var sopt = Seq("IS",
                         OptPrio("NOT"),
                         AltPrio("SUPPLIED",
                                 "BOUND",
                                 Ver(Release.v750, Seq("INSTANCE OF", Expr<ClassName>()), also: AlsoIn.OpenABAP),
                                 "REQUESTED",
                                 "INITIAL"));

        var between = Seq(OptPrio("NOT"), "BETWEEN", Expr<Source>(), "AND", Expr<Source>());

        // https://help.sap.com/doc/abapdocu_latest_index_htm/latest/en-us/abennews-740_sp08-expressions.html
        // but also seems to work in v740sp05, blah
        var predicate = Ver(Release.v740sp08, Expr<MethodCallChain>(), also: AlsoIn.OpenABAP);

        var rett = Seq(Expr<Source>(), AltPrio(Seq(Expr<CompareOperator>(), Expr<Source>()), inn, between, sopt));

        var fsassign = Seq(Expr<SourceFieldSymbolChain>(), "IS", OptPrio("NOT"), "ASSIGNED");

        var ret = Seq(Opt("NOT"), AltPrio(rett, predicate, fsassign));

        return ret;
    }
}
