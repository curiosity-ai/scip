// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_path_for_column.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLPathForColumn : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var tablePrefix = Regex(@"^(\/\w+\/)?\w+~$");

        var withPrefix = Seq(tablePrefix, Plus(new SQLPathSegment(true)), Tok<Tokens.Dash>(), Expr<Field>());
        var standalone = Seq(Plus(new SQLPathSegment()), Tok<Tokens.Dash>(), Expr<Field>());

        return Ver(Release.v740sp05, AltPrio(withPrefix, standalone));
    }
}
