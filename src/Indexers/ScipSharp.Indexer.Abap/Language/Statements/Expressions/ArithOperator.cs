// Ported from abaplint packages/core/src/abap/2_statements/expressions/arith_operator.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ArithOperator : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = AltPrio(Tok<Tokens.WPlusW>(),
                            Tok<Tokens.WDashW>(),
                            "*",
                            "**",
                            "/",
                            "BIT-XOR",
                            "BIT-AND",
                            "BIT-OR",
                            "DIV",
                            "MOD");

        return ret;
    }
}
