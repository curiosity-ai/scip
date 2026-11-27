// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_param_optional.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodParamOptional : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // not optPrio, a parameter named "optional" can follow one with a DEFAULT value:
        // "a TYPE i DEFAULT 5 optional TYPE i"
        return Seq(Expr<MethodParam>(), Opt("OPTIONAL"));
    }
}
