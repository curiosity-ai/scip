// Ported from abaplint packages/core/src/abap/2_statements/expressions/corresponding_body_base.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CorrespondingBodyBase : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Seq("BASE", Tok<Tokens.WParenLeftW>(), Expr<Source>(), Tok<Tokens.WParenRightW>());
    }
}
