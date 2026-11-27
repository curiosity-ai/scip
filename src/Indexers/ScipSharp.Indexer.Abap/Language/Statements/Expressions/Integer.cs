// Ported from abaplint packages/core/src/abap/2_statements/expressions/integer.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Integer : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var modifier = OptPrio(AltPrio(Tok<Tokens.WDash>(), Tok<Tokens.WDashW>(), Tok<Tokens.WPlus>()));
        return Seq(modifier, Regex(@"^\d+$"));
    }
}
