// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_case.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLCase : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var abap = Seq(Tok<Tokens.WAt>(), Expr<SimpleFieldChain2>());

        var field = AltPrio(Expr<SQLAggregation>(),
                              Expr<SQLCase>(),
                              Expr<SQLFunction>(),
                              Expr<SQLPathForColumn>(),
                              Expr<SQLFieldName>(),
                              Expr<Constant>());
        var sub = Seq(AltPrio("+", "-", "*", "/", "&&"), OptPrio(Tok<Tokens.WParenLeftW>()), field, OptPrio(Tok<Tokens.WParenRightW>()));

        var source = AltPrio(Expr<SQLCase>(), Expr<SQLAggregation>(), Expr<SQLFunction>(), Expr<SQLFieldName>(), Expr<SQLSource>(), Expr<Constant>());
        var parenSource = Seq(Tok<Tokens.WParenLeftW>(), source, Tok<Tokens.WParenRightW>());
        var val = AltPrio(Expr<SQLCond>(), Expr<Constant>(), abap);
        var when = Seq("WHEN", val, "THEN", AltPrio(parenSource, Seq(OptPrio("-"), source)), StarPrio(sub));
        var else_ = Seq("ELSE", AltPrio(parenSource, Seq(OptPrio("-"), source)));

        return Ver(Release.v740sp05, Seq("CASE", Opt(AltPrio(Expr<SQLFieldName>(), abap)), PlusPrio(when), OptPrio(else_), "END"), also: AlsoIn.OpenABAP);
    }
}
