// Ported from abaplint packages/core/src/abap/2_statements/expressions/cond.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Cond : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var @operator = AltPrio("AND", "OR", Ver(Release.v702, "EQUIV"));
        var cnd = AltPrio(Expr<Compare>(), Expr<CondSub>());
        var ret = Seq(cnd, StarPrio(Seq(@operator, cnd)));
        return ret;
    }
}
