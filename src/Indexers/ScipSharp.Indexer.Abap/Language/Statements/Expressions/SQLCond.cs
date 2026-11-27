// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_cond.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLCond : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var @operator = AltPrio("AND", "OR");

        var paren = Seq(Tok<Tokens.WParenLeftW>(),
                          Expr<SQLCond>(),
                          AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.ParenRightW>()));

        var cnd = Seq(OptPrio("NOT"), AltPrio(Expr<SQLCompare>(), paren));

        var ret = Seq(cnd, StarPrio(Seq(@operator, cnd)));

        return ret;
    }
}
