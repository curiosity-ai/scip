// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_as_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLAsName : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // todo, below allows too much?
        var field = Regex(@"^[&_!]?\*?\w*(\/\w+\/)?\d*[a-zA-Z_%\$][\w\*%\$\?]*(~\w+)?$");

        return Seq(field, StarPrio(Seq(Tok<Tokens.Dash>(), field)));
    }
}
