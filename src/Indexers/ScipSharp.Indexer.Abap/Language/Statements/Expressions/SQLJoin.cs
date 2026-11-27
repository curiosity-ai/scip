// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_join.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLJoin : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var joinType = Seq(OptPrio(AltPrio("INNER", "LEFT OUTER", "LEFT", "RIGHT OUTER", "RIGHT")), "JOIN");

        var joinTag = OptPrio(Ver(Release.v795, Regex(@"^#[A-Z][A-Z0-9_]*$", "i")));

        var join = Seq(joinType, joinTag, new SQLJoinSource(), "ON", Expr<SQLCond>());

        var crossJoin = Ver(Release.v750, Seq("CROSS JOIN", joinTag, new SQLJoinSource()), also: AlsoIn.OpenABAP);

        return AltPrio(crossJoin, join);
    }
}
