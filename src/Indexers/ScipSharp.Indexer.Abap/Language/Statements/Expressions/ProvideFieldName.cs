// Ported from abaplint packages/core/src/abap/2_statements/expressions/provide_field_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ProvideFieldName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Seq(Regex(@"^(?!(?:FROM|BETWEEN|WHERE)$)(\/\w+\/)?(\w+~(\w+|\*)|\w+)$", "i"), OptPrio(Seq(Tok<Tokens.Dash>(), Regex(@"^\w+$", "i"))));
    }
}
