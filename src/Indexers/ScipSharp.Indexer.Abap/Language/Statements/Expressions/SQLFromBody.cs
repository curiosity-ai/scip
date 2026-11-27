// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_from_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLFromBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var joins = StarPrio(Seq(OptPrio(Tok<Tokens.WParenRightW>()), Expr<SQLJoin>()));

        var leaf = Seq(Expr<SQLFromSource>(), joins);

        var nested = VerNotLang(LanguageVersion.KeyUser,
                                  Seq(Tok<Tokens.WParenLeftW>(),
                                      Expr<SQLFromBody>(),
                                      OptPrio(Tok<Tokens.WParenRightW>()),
                                      joins));

        return AltPrio(nested, leaf);
    }
}
