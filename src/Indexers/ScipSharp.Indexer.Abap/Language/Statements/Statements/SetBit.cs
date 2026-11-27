// Ported from abaplint packages/core/src/abap/2_statements/statements/set_bit.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SetBit : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("SET BIT",
                        Expr<Source>(),
                        "OF",
                        Expr<Target>(),
                        Opt(Seq("TO", Expr<Source>())));

        return ret;
    }
}
