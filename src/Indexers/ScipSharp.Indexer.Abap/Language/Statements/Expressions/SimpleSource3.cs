// Ported from abaplint packages/core/src/abap/2_statements/expressions/simple_source3.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SimpleSource3 : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return AltPrio(Expr<Constant>(), Expr<TextElement>(), Expr<FieldChain>());
    }
}
