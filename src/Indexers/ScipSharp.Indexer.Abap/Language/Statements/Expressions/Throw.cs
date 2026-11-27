// Ported from abaplint packages/core/src/abap/2_statements/expressions/throw.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Throw : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var message = Seq("MESSAGE",
                            Expr<MessageSource>(),
                            Opt(Expr<RaiseWith>()));

        return Seq("THROW",
                   Opt("RESUMABLE"),
                   Expr<ClassName>(),
                   AltPrio(Tok<Tokens.ParenLeftW>(), Tok<Tokens.ParenLeft>()),
                   Opt(Alt(Expr<Source>(), Expr<ParameterListS>(), message)),
                   AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.ParenRightW>()));
    }
}
