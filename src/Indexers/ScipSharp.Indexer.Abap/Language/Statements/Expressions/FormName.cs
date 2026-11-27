// Ported from abaplint packages/core/src/abap/2_statements/expressions/form_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FormName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // todo, does not handle namespaces properly
        return Seq(Regex(@"^!?[\w%$\*\/\?<>]+$"),
                   StarPrio(Seq(Tok<Tokens.Dash>(), OptPrio(Regex(@"^\w+$")))),
                   OptPrio(Tok<Tokens.DashW>()));
    }
}
