// Ported from abaplint packages/core/src/abap/2_statements/expressions/exception_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

/** non class based exception name */
public class ExceptionName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Seq(Expr<Field>(), StarPrio(Seq(Tok<Tokens.Dash>(), Expr<Field>())));
    }
}
