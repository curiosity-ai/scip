// Ported from abaplint packages/core/src/abap/2_statements/expressions/macro_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MacroName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var r = @"^(\/\w+\/)?[\w\*%\?$&]+>?$";
        return Seq(Regex(r), StarPrio(Seq(Tok<Tokens.Dash>(), Regex(r))));
    }
}
