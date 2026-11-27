// Ported from abaplint packages/core/src/abap/2_statements/statements/constant.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Constant : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var def = Seq(Expr<DefinitionName>(),
                        Opt(Expr<ConstantFieldLength>()),
                        Per(Expr<Expressions.Type>(), Expr<Value>(), Expr<Decimals>(), Expr<Length>()));

        var ret = Seq(Alt("CONSTANT", "CONSTANTS"), def, OptPrio("%_PREDEFINED"));

        return ret;
    }
}
