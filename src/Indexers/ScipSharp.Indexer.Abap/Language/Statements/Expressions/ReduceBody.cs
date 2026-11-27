// Ported from abaplint packages/core/src/abap/2_statements/expressions/reduce_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReduceBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var init = Seq("INIT", Plus(Expr<InlineFieldDefinition>()));

        return Seq(Opt(Expr<Let>()),
                   init,
                   Plus(Expr<For>()),
                   Expr<ReduceNext>());
    }
}
