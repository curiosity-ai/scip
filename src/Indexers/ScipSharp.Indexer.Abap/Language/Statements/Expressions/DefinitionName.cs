// Ported from abaplint packages/core/src/abap/2_statements/expressions/definition_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DefinitionName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var r = Regex(@"^&|&?((\w*\/\w+\/)|(\w*\/\w+\/)?[\w\*$%\?#]+)$");

        return Seq(r, StarPrio(Seq(Tok<Tokens.Dash>(), OptPrio(r))), OptPrio(Tok<Tokens.DashW>()));
    }
}
