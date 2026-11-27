// Ported from abaplint packages/core/src/abap/2_statements/expressions/loop_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class LoopSource : Expression
{

    public override IStatementRunnable GetRunnable()
    {
        return Alt(Expr<SimpleSource2>(), Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP));
    }
}
