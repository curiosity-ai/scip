// Ported from abaplint packages/core/src/abap/2_statements/expressions/value.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Value : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = Seq("VALUE", Alt(Expr<Constant>(), Expr<SimpleFieldChain>(), "IS INITIAL"));
        return ret;
    }
}
