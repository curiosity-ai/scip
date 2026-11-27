// Ported from abaplint packages/core/src/abap/2_statements/expressions/parameter_exception.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ParameterException : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var name = AltPrio("OTHERS", Seq(Expr<ParameterName>(), Tok<Tokens.Dash>(), Expr<ComponentName>()), Expr<ParameterName>());
        return Seq(name,
                   OptPrio(Seq("=",
                               AltPrio(Expr<Integer>(), Expr<SimpleFieldChain>()),
                               OptPrio(Seq("MESSAGE", Expr<Target>())))));
    }
}
