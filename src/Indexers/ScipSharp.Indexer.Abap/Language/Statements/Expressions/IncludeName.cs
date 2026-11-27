// Ported from abaplint packages/core/src/abap/2_statements/expressions/include_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class IncludeName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Seq(Regex(@"^<?(\/\w+\/)?[\w%#]+(~\w+)?>?$"), StarPrio(Seq(Tok<Tokens.Dash>(), OptPrio(Regex(@"^\w+$")))));
    }
}
