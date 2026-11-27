// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_path_join_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLPathJoinType : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var innerLike = AltPrio("INNER", "LEFT OUTER", "RIGHT OUTER");
        var card = AltPrio("EXACT ONE", "MANY", "ONE");
        var joinCard = Seq(card, "TO", card);

        return AltPrio(
          Seq(innerLike, OptPrio(joinCard)),
          joinCard);
    }
}
