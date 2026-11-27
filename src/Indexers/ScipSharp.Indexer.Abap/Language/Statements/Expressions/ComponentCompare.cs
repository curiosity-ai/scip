// Ported from abaplint packages/core/src/abap/2_statements/expressions/component_compare.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ComponentCompare : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var val = Alt(Expr<FieldSub>(), Expr<Constant>());

        var list = Seq(Tok<Tokens.WParenLeft>(),
                         val,
                         Plus(Seq(",", val)),
                         Tok<Tokens.ParenRightW>());

        var inn = Seq(Opt("NOT"), "IN", AltPrio(Expr<Source>(), list));

        var sopt = Seq("IS",
                         Opt("NOT"),
                         AltPrio("SUPPLIED",
                                 "BOUND",
                                 Ver(Release.v750, Seq("INSTANCE OF", Expr<Source>())),
                                 "REQUESTED",
                                 "ASSIGNED",
                                 "INITIAL"));

        var between = Seq(Opt("NOT"), "BETWEEN", Expr<Source>(), "AND", Expr<Source>());

        var rett = Seq(Expr<ComponentChainSimple>(), AltPrio(Seq(Expr<CompareOperator>(), Expr<Source>()), inn, between, sopt));

        var ret = Seq(Opt("NOT"), rett);

        return ret;
    }
}
