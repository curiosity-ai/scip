// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_cds_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLCDSParameters : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var param = Seq(Expr<Field>(), "=", Alt(Seq(Tok<Tokens.WAt>(), Expr<FieldChain>()), Expr<Constant>()));
        return Seq(Alt(Tok<Tokens.ParenLeft>(), Tok<Tokens.ParenLeftW>()), param, StarPrio(Seq(",", param)), Alt(Tok<Tokens.WParenRightW>(), Tok<Tokens.WParenRight>()));
    }
}
