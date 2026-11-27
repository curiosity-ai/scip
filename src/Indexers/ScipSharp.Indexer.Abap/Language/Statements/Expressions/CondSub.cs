// Ported from abaplint packages/core/src/abap/2_statements/expressions/cond_sub.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CondSub : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // rule ParserMissingSpace makes sure the whitespace is correct
        var another = Seq(OptPrio("NOT"),
                            AltPrio(Tok<Tokens.WParenLeftW>(), Tok<Tokens.WParenLeft>()),
                            Expr<Cond>(),
                            AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.ParenRightW>()));

        return another;
    }
}
