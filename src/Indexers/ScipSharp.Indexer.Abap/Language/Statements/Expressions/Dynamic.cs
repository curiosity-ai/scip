// Ported from abaplint packages/core/src/abap/2_statements/expressions/dynamic.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Dynamic : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = Seq(AltPrio(Tok<Tokens.WParenLeft>(), Tok<Tokens.ParenLeft>()),
                        AltPrio(Expr<FieldChain>(), Expr<Constant>()),
                        AltPrio(Tok<Tokens.ParenRightW>(), Tok<Tokens.ParenRight>()));

        return ret;
    }
}
