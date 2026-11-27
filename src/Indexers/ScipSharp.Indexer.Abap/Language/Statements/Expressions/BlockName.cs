// Ported from abaplint packages/core/src/abap/2_statements/expressions/block_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class BlockName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = Seq(OptPrio(Tok<Tokens.WDash>()), Regex(@"^[\w%\$\*]+$"), StarPrio(Seq(Tok<Tokens.Dash>(), Regex(@"^[\w%\$\*]+$"))));

        return ret;
    }
}
