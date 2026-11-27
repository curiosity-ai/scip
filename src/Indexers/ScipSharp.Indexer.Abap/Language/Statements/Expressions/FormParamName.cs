// Ported from abaplint packages/core/src/abap/2_statements/expressions/form_param_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FormParamName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var r = Regex(@"^[\w$&\*%\/]+$");

        // dashes in form parameter names allowed, intention is not to support this
        // but avoid the structural errors
        return Seq(r, StarPrio(Seq(Tok<Tokens.Dash>(), r)));
    }
}
