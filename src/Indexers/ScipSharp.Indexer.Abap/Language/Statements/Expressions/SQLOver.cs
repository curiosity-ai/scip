// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_over.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLOver : Expression
{

    private static readonly IStatementRunnable lparen = Tok<Tokens.ParenLeftW>();
    private static readonly IStatementRunnable rparen = AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.WParenRight>());

    public override IStatementRunnable GetRunnable()
    {
        var partitionBy = Seq("PARTITION", "BY", Plus(Seq(Expr<SQLFunctionInput>(), Opt(","))));

        var unboundedPreceding = Seq("UNBOUNDED", "PRECEDING");
        var unboundedFollowing = Seq("UNBOUNDED", "FOLLOWING");
        var currentRow = Seq("CURRENT", "ROW");
        var numBound = Seq(Expr<SQLSource>(), AltPrio("PRECEDING", "FOLLOWING"));
        var bound = AltPrio(unboundedPreceding, unboundedFollowing, currentRow, numBound);
        var windowFrameSpec = Ver(Release.v757, Seq("ROWS", "BETWEEN", bound, "AND", bound));

        return Ver(Release.v757,
                   Seq("OVER", lparen,
                       OptPrio(partitionBy),
                       OptPrio(Expr<SQLOrderBy>()),
                       OptPrio(windowFrameSpec),
                       rparen));
    }
}
