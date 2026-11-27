// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_compare.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLCompare : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var subSelect = Ver(Release.v750, Expr<SQLSetOpGroup>(), also: AlsoIn.OpenABAP);
        var simpleSubSelect = Seq("(", "SELECT", SelectCore.BuildSelectCore(default, false), ")");
        var simpleScalarSubSelect = Ver(Release.v740sp08, simpleSubSelect, also: AlsoIn.OpenABAP);

        var between = Seq("BETWEEN", Expr<SQLSource>(), "AND", Expr<SQLSource>());

        var like = Seq("LIKE", Expr<SQLSource>(), OptPrio(Seq("ESCAPE", Expr<SQLSource>())));

        var nul = Seq("IS", OptPrio("NOT"), AltPrio("NULL", Ver(Release.v753, "INITIAL")));

        var sub = Seq(OptPrio(AltPrio("ALL", "ANY", "SOME")), AltPrio(subSelect, simpleScalarSubSelect));

        var source = new SQLSource();

        var arith = Ver(Release.v750, PlusPrio(Seq(AltPrio("+", "-", "*", "/"), Expr<SQLFieldName>())), also: AlsoIn.OpenABAP);

        var paren = Seq(Tok<Tokens.ParenLeftW>(), Expr<Source>(), Tok<Tokens.WParenRightW>());
        var at = Ver(Release.v740sp05, Seq(Tok<Tokens.WAt>(), AltPrio(Expr<SimpleSource3>(), paren)), also: AlsoIn.OpenABAP);

        var lhs = AltPrio(Expr<SQLCase>(), Expr<SQLAggregation>(), Expr<SQLFunction>(), Expr<ConstantString>(),
                            Seq(AltPrio(Expr<SQLPathForColumn>(), Expr<SQLAliasField>(), Expr<SQLFieldName>()), OptPrio(arith)), at);
        var rhs = AltPrio(Seq(Expr<SQLCompareOperator>(), AltPrio(sub, Expr<SQLCase>(), Expr<SQLAggregation>(), Expr<SQLFunction>(), Seq(source, OptPrio(arith)))),
                            Seq(OptPrio("NOT"), AltPrio(Expr<SQLIn>(), like, between)),
                            Expr<SQLLikeRegexpr>(),
                            nul);
        var rett = Seq(lhs, rhs);

        var exists = Seq("EXISTS", AltPrio(subSelect, simpleSubSelect));

        return AltPrio(exists, Expr<Dynamic>(), rett);
    }
}
