// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_path_cardinality.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLPathCardinality : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var exact = Seq(Tok<Tokens.WParenLeftW>(), Regex(@"^\d+$"), Tok<Tokens.WParenRightW>());
        var many = Seq(Tok<Tokens.WParenLeftW>(), "*", Tok<Tokens.WParenRightW>());
        return AltPrio(exact, many);
    }
}
